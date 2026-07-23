using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Xe chạm màn hình: kiểm tra vật cản phía trước rồi chạy ra cạnh và theo chu vi đến Point X.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class CarJamCar : MonoBehaviour, IPointerDownHandler
{
    [Header("Spool data")]
    [SerializeField] private SpoolColor spoolColor;
    [SerializeField] private SpoolSize spoolSize;

    [Header("Movement")]
    [Tooltip("Collider dùng làm kích thước cho Physics.BoxCast. Bỏ trống để tự lấy Collider con đầu tiên.")]
    [SerializeField] private Collider carCollider;
    // Bit 0 is Unity's Default layer. This is intentionally fixed for Car Jam.
    private const int DefaultObstacleLayerMask = 1 << 0;
    [SerializeField] private LayerMask obstacleLayers = DefaultObstacleLayerMask;
    [Header("Blocked car feedback")]
    [SerializeField, Min(0f)] private float blockedCarBounceDistance = 0.5f;
    [SerializeField, Min(0.01f)] private float blockedCarBounceHalfDuration = 0.12f;
    public SpoolColor SpoolColor => spoolColor;
    public SpoolSize SpoolSize => spoolSize;
    public SpoolSlot TargetSlot { get; private set; }
    public bool IsMoving { get; private set; }

    private const float ObstacleCheckDistance = 25f;

    private void Awake()
    {
        if (carCollider == null)
            carCollider = GetComponentInChildren<Collider>();
    }

    private void OnValidate()
    {
        // Migrate existing scene/prefab instances that still contain the old mask.
        obstacleLayers = DefaultObstacleLayerMask;
    }

    public void OnPointerDown(PointerEventData eventData) => TryMoveToPointX();

    public void TryMoveToPointX()
    {
        CarJamArea area = CarJamArea.Instance;
        SpoolManager spoolManager = SpoolManager.Instance;
        if (IsMoving || area == null || spoolManager == null)
            return;

        Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        if (TryGetObstacleAhead(area, direction, out RaycastHit obstacleHit,
                out CarJamCar blockingCar))
        {
            if (blockingCar != null && !blockingCar.IsMoving)
            {
                StartCoroutine(BumpBlockedCar(area.MoveSpeed, direction, obstacleHit.distance,
                    blockingCar));
            }
            else
            {
                Debug.Log($"{name}: có vật cản phía trước.", this);
            }

            return;
        }

        // A free slot has IsOccupied == false: it has no Spool and is not locked.
        TargetSlot = spoolManager.GetFreeSlot();
        if (TargetSlot == null)
        {
            Debug.Log("Không còn Spool Slot trống.", this);
            return;
        }

        // Reserve immediately so another clicked car cannot select the same free slot.
        if (!TargetSlot.TryReserve())
        {
            TargetSlot = null;
            return;
        }

        Vector3 targetPoint = area.ProjectToSurface(TargetSlot.transform.position);
        gameObject.layer = 2;
        AudioManager.Instance?.Play(AudioClipId.CarGo);
        StartCoroutine(MoveToPointX(area, area.GetEdgePoint(transform.position, direction), targetPoint));
    }

    /// <summary>Kiểm tra đúng 25 mét phía trước bằng BoxCast, bỏ qua collider của xe và vùng ngoài CarJamArea.</summary>
    private bool TryGetObstacleAhead(CarJamArea area, Vector3 direction, out RaycastHit closestHit,
        out CarJamCar blockingCar)
    {
        closestHit = default;
        blockingCar = null;

        GetCastBox(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation);
        RaycastHit[] hits = Physics.BoxCastAll(center, halfExtents, direction, orientation,
            ObstacleCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
                continue;

            if (!area.Contains(hit.point) || hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            closestHit = hit;
            blockingCar = hit.collider.GetComponentInParent<CarJamCar>();
        }
        return closestDistance < float.PositiveInfinity;
    }

    private void GetCastBox(out Vector3 center, out Vector3 halfExtents,
        out Quaternion orientation)
    {
        if (carCollider is BoxCollider boxCollider)
        {
            Transform colliderTransform = boxCollider.transform;
            Vector3 scale = colliderTransform.lossyScale;
            center = colliderTransform.TransformPoint(boxCollider.center);
            halfExtents = Vector3.Scale(boxCollider.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            halfExtents = Vector3.Max(halfExtents, Vector3.one * 0.01f);
            orientation = colliderTransform.rotation;
            return;
        }

        // A generic Collider only exposes a world-axis-aligned bounds box, so it
        // must be cast with identity rotation instead of rotating the AABB again.
        Bounds bounds = carCollider != null
            ? carCollider.bounds
            : new Bounds(transform.position, Vector3.one);
        center = bounds.center;
        halfExtents = Vector3.Max(bounds.extents, Vector3.one * 0.01f);
        orientation = Quaternion.identity;
    }

    private IEnumerator BumpBlockedCar(float moveSpeed, Vector3 direction, float hitDistance,
        CarJamCar blockingCar)
    {
        IsMoving = true;
        blockingCar.IsMoving = true;

        Vector3 originalPosition = transform.position;
        Vector3 blockingOriginalPosition = blockingCar.transform.position;
        Vector3 contactPosition = originalPosition + direction * Mathf.Max(0f, hitDistance);

        yield return MoveTransform(transform, contactPosition, Mathf.Max(0.01f, moveSpeed));
        AudioManager.Instance?.Play(AudioClipId.CarHit);

        Vector3 blockingBouncePosition =
            blockingOriginalPosition + direction * blockedCarBounceDistance;
        yield return MoveTransformTimed(blockingCar.transform, blockingOriginalPosition,
            blockingBouncePosition, blockedCarBounceHalfDuration);
        yield return MoveTransformTimed(blockingCar.transform, blockingBouncePosition,
            blockingOriginalPosition, blockedCarBounceHalfDuration);

        yield return MoveTransform(transform, originalPosition, Mathf.Max(0.01f, moveSpeed));

        transform.position = originalPosition;
        blockingCar.transform.position = blockingOriginalPosition;
        blockingCar.IsMoving = false;
        IsMoving = false;
    }

    private static IEnumerator MoveTransform(Transform movingTransform, Vector3 target,
        float moveSpeed)
    {
        while ((movingTransform.position - target).sqrMagnitude > 0.0001f)
        {
            movingTransform.position = Vector3.MoveTowards(movingTransform.position, target,
                moveSpeed * Time.deltaTime);
            yield return null;
        }

        movingTransform.position = target;
    }

    private static IEnumerator MoveTransformTimed(Transform movingTransform, Vector3 from,
        Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            movingTransform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        movingTransform.position = to;
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform);
    }

    private IEnumerator MoveToPointX(CarJamArea area, Vector3 edgePoint, Vector3 targetPoint)
    {
        IsMoving = true;

        // Phase 1: keep the current rotation and move only along the car's forward line.
        yield return MoveStraightToEdge(area.MoveSpeed, edgePoint);

        // Phase 2: now that the car has reached the edge, select the shorter XZ perimeter path.
        Vector3[] clockwiseRoute = area.GetPerimeterRoute(edgePoint, targetPoint, true);
        Vector3[] counterClockwiseRoute = area.GetPerimeterRoute(edgePoint, targetPoint, false);
        Vector3[] selectedRoute = GetRouteLengthXZ(transform.position, clockwiseRoute)
            <= GetRouteLengthXZ(transform.position, counterClockwiseRoute)
            ? clockwiseRoute
            : counterClockwiseRoute;

        foreach (Vector3 point in selectedRoute)
            yield return TurnThenMove(area.MoveSpeed, point, true);

        // Phase 3: leave Point X and move into the actual reserved Slot position.
        if (TargetSlot != null)
            yield return TurnThenMove(area.MoveSpeed, TargetSlot.transform.position, false);

        IsMoving = false;
        area.NotifyCarReachedPointX(spoolColor, spoolSize, TargetSlot);
        Destroy(gameObject);
    }

    private static float GetRouteLengthXZ(Vector3 startPoint, Vector3[] route)
    {
        float length = 0f;
        Vector3 previous = startPoint;
        foreach (Vector3 point in route)
        {
            Vector3 delta = point - previous;
            length += new Vector2(delta.x, delta.z).magnitude;
            previous = point;
        }

        return length;
    }

    private IEnumerator MoveStraightToEdge(float moveSpeed, Vector3 target)
    {
        target.y = transform.position.y;
        while ((transform.position - target).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
    }

    private IEnumerator TurnThenMove(float moveSpeed, Vector3 target, bool keepCurrentHeight)
    {
        if (keepCurrentHeight)
            target.y = transform.position.y;

        Vector3 direction = target - transform.position;
        Vector3 lookDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            while (Quaternion.Angle(transform.rotation, targetRotation) > 0.1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);
                yield return null;
            }
            transform.rotation = targetRotation;
        }

        while ((transform.position - target).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }
}
