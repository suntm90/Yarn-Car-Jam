using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CatWaypointMover : MonoBehaviour
{
    private const float IdleYaw = 180f;
    private static readonly int IdleState = Animator.StringToHash("Cat_Idle");
    private static readonly int RunState = Animator.StringToHash("Cat_Run");

    [SerializeField] private CatWayPoints wayPoints;
    [SerializeField] private Animator animator;
    [SerializeField, Min(0)] private int triggerWaypointIndex = 7;
    [SerializeField, Min(0.01f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 540f;
    [SerializeField] private bool snapToFirstPointOnStart = true;
    [SerializeField, Min(0f)] private float animationFadeDuration = 0.12f;

    private int currentPointIndex;
    private int queuedMoves;
    private Coroutine moveRoutine;
    private readonly HashSet<int> triggeredWaypointIndices = new();

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void LateUpdate()
    {
        if (moveRoutine == null)
            SetTargetYaw(IdleYaw, true);
    }

    private void Start()
    {
        CatWaypoint[] points = wayPoints != null ? wayPoints.Points : null;
        if (points == null || points.Length == 0 || points[0] == null)
        {
            Debug.LogWarning("CatWaypointMover needs at least one CatWaypoint.", this);
            enabled = false;
            return;
        }

        if (snapToFirstPointOnStart)
        {
            transform.position = points[0].transform.position;
            SetTargetYaw(points[0].transform.eulerAngles.y, true);
        }

        PlayIdle();

        if (DragonController.Instance == null)
        {
            Debug.LogWarning("CatWaypointMover could not find DragonController.", this);
            enabled = false;
            return;
        }

        DragonController.Instance.WaypointReached += HandleDragonWaypointReached;
        DragonController.Instance.DestinationReached += HandleDragonDestinationReached;
    }

    private void OnDestroy()
    {
        if (DragonController.Instance == null)
            return;

        DragonController.Instance.WaypointReached -= HandleDragonWaypointReached;
        DragonController.Instance.DestinationReached -= HandleDragonDestinationReached;
    }

    private void HandleDragonWaypointReached(int waypointIndex)
    {
        if (waypointIndex < triggerWaypointIndex
            || !triggeredWaypointIndices.Add(waypointIndex)
            || !HasNextPoint())
            return;

        queuedMoves++;
        if (moveRoutine == null)
            moveRoutine = StartCoroutine(ProcessQueuedMoves());
    }

    private void HandleDragonDestinationReached()
    {
        AudioManager.Instance?.Play(AudioClipId.CatMeow);
    }

    private IEnumerator ProcessQueuedMoves()
    {
        while (queuedMoves > 0 && HasNextPoint())
        {
            queuedMoves--;
            int nextPointIndex = GetNextPointIndex();
            CatWaypoint targetPoint = wayPoints.Points[nextPointIndex];

            AudioManager.Instance?.Play(AudioClipId.CatMeow);
            PlayAnimation(RunState);

            int segmentIndex = currentPointIndex;
            float segmentLength = wayPoints.GetSegmentLength(segmentIndex);
            float progress = 0f;

            while (targetPoint != null && progress < 1f)
            {
                if (segmentLength < 0.0001f)
                {
                    progress = 1f;
                }
                else
                {
                    progress = Mathf.Min(1f, progress + moveSpeed * Time.deltaTime / segmentLength);
                    transform.position = wayPoints.GetSplinePoint(segmentIndex, progress);

                    Vector3 tangent = wayPoints.GetSplineTangent(segmentIndex, progress);
                    tangent.y = 0f;
                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        float targetYaw = Quaternion.LookRotation(tangent, Vector3.up).eulerAngles.y;
                        SetTargetYaw(targetYaw, false);
                    }
                }

                yield return null;
            }

            if (targetPoint != null)
            {
                transform.position = targetPoint.transform.position;
                currentPointIndex = nextPointIndex;
            }

            PlayIdle();
            yield return null;
        }

        queuedMoves = 0;
        moveRoutine = null;
    }

    private bool HasNextPoint()
    {
        if (wayPoints == null || wayPoints.Points == null || wayPoints.Points.Length < 2)
            return false;

        return wayPoints.Loop || currentPointIndex < wayPoints.Points.Length - 1;
    }

    private int GetNextPointIndex()
    {
        int nextIndex = currentPointIndex + 1;
        return wayPoints.Loop ? nextIndex % wayPoints.Points.Length : nextIndex;
    }

    private void PlayAnimation(int stateHash)
    {
        if (animator != null)
            animator.CrossFade(stateHash, animationFadeDuration);
    }

    private void PlayIdle()
    {
        SetTargetYaw(IdleYaw, true);
        PlayAnimation(IdleState);
    }

    private void SetTargetYaw(float targetYaw, bool immediate)
    {
        Vector3 eulerAngles = transform.eulerAngles;
        eulerAngles.y = immediate
            ? targetYaw
            : Mathf.MoveTowardsAngle(eulerAngles.y, targetYaw, rotationSpeed * Time.deltaTime);
        transform.eulerAngles = eulerAngles;
    }
}
