using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a dragon from root-level CarJamCar objects and makes every body piece follow the head.
/// </summary>
public sealed class DragonController : MonoBehaviour
{
    public static DragonController Instance { get; private set; }

    /// <summary>Sent once whenever the dragon reaches the final waypoint.</summary>
    public event Action DestinationReached;

    /// <summary>Sent when the dragon reaches a non-final waypoint.</summary>
    public event Action<int> WaypointReached;

    public GameObject fireVfx;

    [Header("Fixed parts")]
    private Transform head;
    [SerializeField] private Transform tailPrefab;

    [Header("Body parts")]
    [SerializeField] private Transform defaultBodyPrefab;
    [SerializeField, Min(0.01f)] private float segmentSpacing = 0.5f;
    [SerializeField] private bool buildOnStart = true;
    [Tooltip("Total local Z offset applied to a BodyPart while Yarn pull progress goes from 0 to 1.")]
    [SerializeField] private float bodyPartPullLocalZOffset = -0.5f;

    [Header("Body sway")]
    [SerializeField, Min(0f)] private float bodyRollAngle = 8f;
    [SerializeField, Min(0f)] private float bodyRollFrequency = 1.25f;
    [SerializeField, Min(0f)] private float bodyRollWavelength = 3f;

    [Header("Waypoints")]
    [SerializeField, Min(0.01f)] private float defaultMoveSpeed = 3f;
    [SerializeField] private bool snapHeadToFirstWaypointOnStart = true;

    [Header("Color sequence chances (%)")]
    [SerializeField, Range(0f, 100f)] private float secondSameColorChance = 80f;
    [SerializeField, Range(0f, 100f)] private float thirdSameColorChance = 60f;
    [SerializeField, Range(0f, 100f)] private float fourthSameColorChance = 35f;
    [SerializeField, Range(0f, 100f)] private float fifthSameColorChance = 15f;
    [SerializeField, Range(0f, 100f)] private float sixthSameColorChance = 5f;

    private readonly List<Transform> followers = new();
    private readonly List<Transform> spawnedParts = new();
    private readonly List<Vector3> headTrail = new();
    private int currentSegmentIndex;
    private float currentSegmentT;
    private bool isFollowingWaypoints;
    private bool isRepacking;
    private bool isAtFinalDestination;

    public int BodySegmentCount { get; private set; }
    public Transform Head => head;
    public Transform Tail => followers.Count > 0 ? followers[^1] : null;
    public bool IsRepacking => isRepacking;
    public bool IsAtFinalDestination => isAtFinalDestination;
    public bool IsAnyBodySegmentBeingConsumed
    {
        get
        {
            foreach (Transform follower in followers)
            {
                if (follower != null &&
                    follower.TryGetComponent(out DragonBodySegment segment) &&
                    segment.IsBeingConsumed)
                    return true;
            }

            return false;
        }
    }
    private DragonWaypoint[] Waypoints => WayPoints.Instance != null && WayPoints.Instance.Points != null
        ? WayPoints.Instance.Points
        : Array.Empty<DragonWaypoint>();
    private bool LoopWaypoints => WayPoints.Instance != null && WayPoints.Instance.Loop;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one DragonController is allowed in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        head = transform;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (snapHeadToFirstWaypointOnStart && HasPath())
            SetHeadPose(0, 0f);

        if (buildOnStart)
            RebuildDragon();

        InitializeHeadTrail();
        isFollowingWaypoints = HasPath();
        isAtFinalDestination = false;
    }

    public void FireStart()
    {
        fireVfx.SetActive(true);
    }

    public void FireEnd()
    {
        fireVfx.SetActive(false);
    }

    private void Update()
    {
        if (isFollowingWaypoints && !isRepacking)
            MoveHeadAlongWaypoints();

        if (!isRepacking)
            RecordHeadTrail();
    }

    private void LateUpdate()
    {
        if (head == null || headTrail.Count == 0 || isRepacking)
            return;

        for (int i = 0; i < followers.Count; i++)
        {
            Transform follower = followers[i];
            if (follower == null)
                continue;

            SetFollowerOnTrail(follower, (i + 1) * segmentSpacing);
        }
    }

    /// <summary>Khởi động lại việc đi theo đường từ Waypoint đầu tiên.</summary>
    public void RestartWaypointPath()
    {
        currentSegmentIndex = 0;
        currentSegmentT = 0f;
        isAtFinalDestination = false;
        if (HasPath())
        {
            SetHeadPose(0, 0f);
            InitializeHeadTrail();
            isFollowingWaypoints = true;
        }
    }

    public void StopWaypointMovement()
    {
        isFollowingWaypoints = false;
    }

    [ContextMenu("Rebuild Dragon")]
    public void RebuildDragon()
    {
        ClearGeneratedParts();
        BodySegmentCount = 0;

        if (head == null || tailPrefab == null)
        {
            Debug.LogWarning("Dragon needs both Head and Tail Prefab references.", this);
            return;
        }

        int[] segmentsByColor = CountRootCarSegments();
        Vector3 nextPosition = head.position - segmentSpacing * head.forward;

        int remainingSegments = 0;
        foreach (int count in segmentsByColor)
            remainingSegments += count;

        int previousColor = -1;
        int sameColorRunLength = 0;
        bool warnedImpossibleSequence = false;

        while (remainingSegments > 0)
        {
            int colorIndex = ChooseNextColor(segmentsByColor, previousColor, sameColorRunLength,
                ref warnedImpossibleSequence);
            SpoolColor color = (SpoolColor)colorIndex;

            if (defaultBodyPrefab == null)
            {
                Debug.LogWarning("No Default Body Prefab assigned.", this);
                break;
            }

            GameObject segmentRootObject = new($"Dragon Body {color}");
            Transform segmentRoot = segmentRootObject.transform;
            segmentRoot.SetPositionAndRotation(nextPosition, head.rotation);
            Transform segmentVisual = Instantiate(defaultBodyPrefab, nextPosition, head.rotation);
            segmentVisual.SetParent(segmentRoot, true);
            DragonBodySegment bodySegment = segmentRootObject.AddComponent<DragonBodySegment>();
            bodySegment.Initialize(color, segmentVisual, bodyPartPullLocalZOffset);
            ApplyColorMaterial(segmentVisual, color);
            spawnedParts.Add(segmentRoot);
            followers.Add(segmentRoot);
            BodySegmentCount++;
            nextPosition -= head.forward * segmentSpacing;

            segmentsByColor[colorIndex]--;
            remainingSegments--;
            sameColorRunLength = colorIndex == previousColor ? sameColorRunLength + 1 : 1;
            previousColor = colorIndex;
        }
        
        Transform tail = Instantiate(tailPrefab, nextPosition, head.rotation);
        spawnedParts.Add(tail);
        followers.Add(tail);
        InitializeHeadTrail();
    }

    public DragonBodySegment FindAndReserveClosestBodySegment(SpoolColor color, Vector3 position,
        float maxDistance)
    {
        DragonBodySegment closest = null;
        float closestSqrDistance = maxDistance * maxDistance;

        foreach (Transform follower in followers)
        {
            if (follower == null || !follower.TryGetComponent(out DragonBodySegment segment)
                || segment.Color != color || segment.IsBeingConsumed)
                continue;

            float sqrDistance = (follower.position - position).sqrMagnitude;
            if (sqrDistance <= closestSqrDistance)
            {
                closest = segment;
                closestSqrDistance = sqrDistance;
            }
        }

        return closest != null && closest.TryBeginConsume() ? closest : null;
    }

    /// <summary>Xóa segment và lùi tuyến tính đầu + phần thân phía trước để khép khoảng trống.</summary>
    public IEnumerator RemoveBodySegmentAndCloseGap(DragonBodySegment segment, float duration)
    {
        while (isRepacking)
            yield return null;

        if (segment == null)
            yield break;

        int removedIndex = followers.IndexOf(segment.transform);
        if (removedIndex < 0)
            yield break;

        isRepacking = true;
        bool resumeWaypointMovement = isFollowingWaypoints;
        bool resumeFromFinalDestination = isAtFinalDestination;
        isFollowingWaypoints = false;
        isAtFinalDestination = false;

        Transform[] frontParts = new Transform[removedIndex + 1];
        Vector3[] startPositions = new Vector3[frontParts.Length];
        Vector3[] targetPositions = new Vector3[frontParts.Length];

        frontParts[0] = head;
        for (int i = 1; i < frontParts.Length; i++)
            frontParts[i] = followers[i - 1];

        for (int i = 0; i < frontParts.Length; i++)
        {
            startPositions[i] = frontParts[i].position;
            Transform target = i < removedIndex ? followers[i] : segment.transform;
            targetPositions[i] = target.position;
        }

        followers.RemoveAt(removedIndex);
        spawnedParts.Remove(segment.transform);
        BodySegmentCount = Mathf.Max(0, BodySegmentCount - 1);

        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < frontParts.Length; i++)
                frontParts[i].position = Vector3.Lerp(startPositions[i], targetPositions[i], progress);
            yield return null;
        }

        Destroy(segment.gameObject);
        SnapWaypointProgressToHead();
        ShiftHeadTrailOrigin(segmentSpacing);
        isRepacking = false;
        isFollowingWaypoints = resumeWaypointMovement || resumeFromFinalDestination;
    }

    private void SnapWaypointProgressToHead()
    {
        if (!HasPath() || head == null)
            return;

        int segmentCount = LoopWaypoints ? Waypoints.Length : Waypoints.Length - 1;
        const int samplesPerSegment = 200;
        float closestSqrDistance = float.PositiveInfinity;
        int closestSegment = 0;
        float closestT = 0f;

        for (int segment = 0; segment < segmentCount; segment++)
        {
            for (int sample = 0; sample <= samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                float sqrDistance = (GetSplinePoint(segment, t) - head.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closestSegment = segment;
                    closestT = t;
                }
            }
        }

        currentSegmentIndex = closestSegment;
        currentSegmentT = closestT;
    }

    private int ChooseNextColor(int[] remainingByColor, int previousColor, int runLength,
        ref bool warnedImpossibleSequence)
    {
        bool hasPreviousColor = previousColor >= 0 && remainingByColor[previousColor] > 0;
        bool hasOtherColor = HasAvailableColor(remainingByColor, previousColor, true);

        if (hasPreviousColor && runLength < 6)
        {
            float chance = GetRepeatChance(runLength);
            if (!hasOtherColor || UnityEngine.Random.value * 100f < chance)
                return previousColor;
        }

        if (hasOtherColor)
            return PickRandomAvailableColor(remainingByColor, previousColor, true);

        if (hasPreviousColor)
        {
            if (runLength >= 6 && !warnedImpossibleSequence)
            {
                Debug.LogWarning("Cannot avoid seven identical body colors: no other color has remaining segments.", this);
                warnedImpossibleSequence = true;
            }
            return previousColor;
        }

        return PickRandomAvailableColor(remainingByColor, -1, false);
    }

    private float GetRepeatChance(int currentRunLength)
    {
        return currentRunLength switch
        {
            1 => secondSameColorChance,
            2 => thirdSameColorChance,
            3 => fourthSameColorChance,
            4 => fifthSameColorChance,
            5 => sixthSameColorChance,
            _ => 0f
        };
    }

    private static bool HasAvailableColor(int[] remainingByColor, int excludedColor, bool excludeColor)
    {
        for (int i = 0; i < remainingByColor.Length; i++)
            if (remainingByColor[i] > 0 && (!excludeColor || i != excludedColor))
                return true;

        return false;
    }

    private static int PickRandomAvailableColor(int[] remainingByColor, int excludedColor, bool excludeColor)
    {
        int totalWeight = 0;
        for (int i = 0; i < remainingByColor.Length; i++)
            if (!excludeColor || i != excludedColor)
                totalWeight += remainingByColor[i];

        int selectedWeight = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < remainingByColor.Length; i++)
        {
            if (excludeColor && i == excludedColor)
                continue;

            selectedWeight -= remainingByColor[i];
            if (selectedWeight < 0)
                return i;
        }

        return -1;
    }

    private int[] CountRootCarSegments()
    {
        int[] segmentsByColor = new int[Enum.GetValues(typeof(SpoolColor)).Length];
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject rootObject in rootObjects)
        {
            // GetComponent only checks this root GameObject; children are intentionally excluded.
            CarJamCar car = rootObject.GetComponent<CarJamCar>();
            if (car != null)
                segmentsByColor[(int)car.SpoolColor] += (int)car.SpoolSize;
        }

        return segmentsByColor;
    }

    private bool HasPath()
    {
        if (head == null || Waypoints.Length < 2)
            return false;

        foreach (DragonWaypoint waypoint in Waypoints)
            if (waypoint == null)
                return false;

        return true;
    }

    private void MoveHeadAlongWaypoints()
    {
        float distanceToMove = Waypoints[currentSegmentIndex].GetMoveSpeed(defaultMoveSpeed) * Time.deltaTime;
        while (distanceToMove > 0f && isFollowingWaypoints)
        {
            float segmentLength = GetSegmentLength(currentSegmentIndex);
            if (segmentLength < 0.0001f)
            {
                AdvanceSegment();
                continue;
            }

            float distanceRemaining = (1f - currentSegmentT) * segmentLength;
            if (distanceToMove < distanceRemaining)
            {
                currentSegmentT += distanceToMove / segmentLength;
                distanceToMove = 0f;
            }
            else
            {
                currentSegmentT = 1f;
                distanceToMove -= distanceRemaining;
            }

            SetHeadPose(currentSegmentIndex, currentSegmentT);
            if (currentSegmentT >= 1f)
                AdvanceSegment();
        }
    }

    private void AdvanceSegment()
    {
        int lastSegmentIndex = LoopWaypoints ? Waypoints.Length - 1 : Waypoints.Length - 2;
        if (currentSegmentIndex >= lastSegmentIndex)
        {
            if (!LoopWaypoints)
            {
                isFollowingWaypoints = false;
                isAtFinalDestination = true;
                DestinationReached?.Invoke();
                return;
            }

            currentSegmentIndex = 0;
            WaypointReached?.Invoke(0);
        }
        else
        {
            currentSegmentIndex++;
            WaypointReached?.Invoke(currentSegmentIndex);
        }

        currentSegmentT = 0f;
    }

    private void SetHeadPose(int segmentIndex, float t)
    {
        head.position = GetSplinePoint(segmentIndex, t);
        Vector3 tangent = GetSplineTangent(segmentIndex, t);
        if (tangent.sqrMagnitude > 0.0001f)
            head.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
    }

    private float GetSegmentLength(int segmentIndex)
    {
        const int samples = 20;
        float length = 0f;
        Vector3 previous = GetSplinePoint(segmentIndex, 0f);
        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = GetSplinePoint(segmentIndex, i / (float)samples);
            length += Vector3.Distance(previous, point);
            previous = point;
        }
        return length;
    }

    // Catmull-Rom passes through every waypoint while smoothing its turns.
    private Vector3 GetSplinePoint(int segmentIndex, float t)
    {
        Vector3 p0 = GetWaypointPosition(segmentIndex - 1);
        Vector3 p1 = GetWaypointPosition(segmentIndex);
        Vector3 p2 = GetWaypointPosition(segmentIndex + 1);
        Vector3 p3 = GetWaypointPosition(segmentIndex + 2);
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private Vector3 GetSplineTangent(int segmentIndex, float t)
    {
        Vector3 p0 = GetWaypointPosition(segmentIndex - 1);
        Vector3 p1 = GetWaypointPosition(segmentIndex);
        Vector3 p2 = GetWaypointPosition(segmentIndex + 1);
        Vector3 p3 = GetWaypointPosition(segmentIndex + 2);
        float t2 = t * t;

        return 0.5f * ((-p0 + p2)
            + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t
            + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
    }

    private Vector3 GetWaypointPosition(int index)
    {
        if (LoopWaypoints)
        {
            index = (index % Waypoints.Length + Waypoints.Length) % Waypoints.Length;
            return Waypoints[index].transform.position;
        }

        index = Mathf.Clamp(index, 0, Waypoints.Length - 1);
        return Waypoints[index].transform.position;
    }


    private void ApplyColorMaterial(Transform segment, SpoolColor color)
    {
        Material material = SpoolManager.Instance != null
            ? SpoolManager.Instance.GetMaterial(color)
            : null;

        if (material == null)
        {
            Debug.LogWarning($"No Spool material configured for {color}.", this);
            return;
        }

        foreach (Renderer targetRenderer in segment.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            targetRenderer.sharedMaterials = materials;
        }
    }

    private void InitializeHeadTrail()
    {
        headTrail.Clear();
        if (head == null)
            return;

        // Seed a straight trail so the body has valid spacing before the head starts moving.
        int seedCount = followers.Count + 6;
        for (int i = 0; i <= seedCount; i++)
            headTrail.Add(head.position - head.forward * (segmentSpacing * i));
    }

    private void ShiftHeadTrailOrigin(float distanceToRemove)
    {
        if (head == null || headTrail.Count < 2)
            return;

        List<Vector3> shiftedTrail = new() { head.position };
        float traveledLength = 0f;
        bool foundNewOrigin = false;

        for (int i = 0; i < headTrail.Count - 1; i++)
        {
            Vector3 newerPoint = headTrail[i];
            Vector3 olderPoint = headTrail[i + 1];
            float sectionLength = Vector3.Distance(newerPoint, olderPoint);
            if (sectionLength < 0.0001f)
                continue;

            if (!foundNewOrigin && traveledLength + sectionLength >= distanceToRemove)
            {
                float t = (distanceToRemove - traveledLength) / sectionLength;
                Vector3 originOnOldTrail = Vector3.Lerp(newerPoint, olderPoint, t);
                if ((shiftedTrail[^1] - originOnOldTrail).sqrMagnitude > 0.000001f)
                    shiftedTrail.Add(originOnOldTrail);
                shiftedTrail.Add(olderPoint);
                foundNewOrigin = true;
            }
            else if (foundNewOrigin)
            {
                shiftedTrail.Add(olderPoint);
            }

            traveledLength += sectionLength;
        }

        if (!foundNewOrigin)
            InitializeHeadTrail();
        else
        {
            headTrail.Clear();
            headTrail.AddRange(shiftedTrail);
        }
    }

    private void RecordHeadTrail()
    {
        if (head == null)
            return;

        if (headTrail.Count == 0 || (headTrail[0] - head.position).sqrMagnitude > 0.000001f)
            headTrail.Insert(0, head.position);

        float requiredLength = (followers.Count + 4) * segmentSpacing;
        float traveledLength = 0f;
        int lastRequiredIndex = 0;
        for (int i = 0; i < headTrail.Count - 1; i++)
        {
            traveledLength += Vector3.Distance(headTrail[i], headTrail[i + 1]);
            lastRequiredIndex = i + 1;
            if (traveledLength >= requiredLength)
                break;
        }

        if (lastRequiredIndex < headTrail.Count - 1 && traveledLength >= requiredLength)
            headTrail.RemoveRange(lastRequiredIndex + 1, headTrail.Count - lastRequiredIndex - 1);
    }

    private void SetFollowerOnTrail(Transform follower, float distanceBehindHead)
    {
        float traveledLength = 0f;
        for (int i = 0; i < headTrail.Count - 1; i++)
        {
            Vector3 newerPoint = headTrail[i];
            Vector3 olderPoint = headTrail[i + 1];
            float segmentLength = Vector3.Distance(newerPoint, olderPoint);
            if (segmentLength < 0.0001f)
                continue;

            if (traveledLength + segmentLength >= distanceBehindHead)
            {
                float t = (distanceBehindHead - traveledLength) / segmentLength;
                follower.position = Vector3.Lerp(newerPoint, olderPoint, t);

                Vector3 currentForward = newerPoint - olderPoint;
                Vector3 headSideForward = i > 0
                    ? headTrail[i - 1] - newerPoint
                    : currentForward;
                Vector3 tailSideForward = i + 2 < headTrail.Count
                    ? olderPoint - headTrail[i + 2]
                    : currentForward;

                Vector3 tangentAtNewer = headSideForward.normalized + currentForward.normalized;
                Vector3 tangentAtOlder = currentForward.normalized + tailSideForward.normalized;
                Vector3 forward = Vector3.Slerp(tangentAtNewer, tangentAtOlder, t);
                if (forward.sqrMagnitude > 0.0001f)
                {
                    Quaternion followRotation = Quaternion.LookRotation(forward, Vector3.up);
                    float wavePhase = Time.time * bodyRollFrequency * Mathf.PI * 2f
                        - distanceBehindHead / Mathf.Max(0.01f, bodyRollWavelength) * Mathf.PI * 2f;
                    float roll = Mathf.Sin(wavePhase) * bodyRollAngle;
                    follower.rotation = followRotation * Quaternion.AngleAxis(roll, Vector3.forward);
                }
                return;
            }

            traveledLength += segmentLength;
        }

        follower.position = headTrail[^1];
    }

    private void ClearGeneratedParts()
    {
        foreach (Transform part in spawnedParts)
        {
            if (part != null)
                Destroy(part.gameObject);
        }

        spawnedParts.Clear();
        followers.Clear();
    }
}
