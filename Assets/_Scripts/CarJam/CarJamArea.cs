using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class CarReachedPointXEvent : UnityEvent<SpoolColor, SpoolSize>
{
}

/// <summary>Vùng hình chữ nhật mà xe được phép di chuyển bên trong.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class CarJamArea : MonoBehaviour
{
    public static CarJamArea Instance { get; private set; }

    [SerializeField] private BoxCollider areaCollider;
    [SerializeField, Min(0.01f)] private float moveSpeed = 30f;
    [Header("Events")]
    [SerializeField] private CarReachedPointXEvent onCarReachedPointX;

    public CarReachedPointXEvent OnCarReachedPointX => onCarReachedPointX;
    public event Action<SpoolColor, SpoolSize, SpoolSlot> CarReachedPointX;
    public float MoveSpeed => moveSpeed;

    private void Reset() => areaCollider = GetComponent<BoxCollider>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one CarJamArea is allowed in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        if (areaCollider == null)
            areaCollider = GetComponent<BoxCollider>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Được CarJamCar gọi khi xe đã đến Point X.</summary>
    public void NotifyCarReachedPointX(SpoolColor color, SpoolSize size, SpoolSlot targetSlot)
    {
        onCarReachedPointX?.Invoke(color, size);
        CarReachedPointX?.Invoke(color, size, targetSlot);
    }

    /// <summary>Trả về quãng đường từ điểm trong vùng tới cạnh theo hướng worldDirection.</summary>
    public float DistanceToEdge(Vector3 worldPosition, Vector3 worldDirection)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition) - areaCollider.center;
        Vector3 normalizedWorldDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
        // Local displacement made by moving exactly one world unit. Do not normalize it:
        // its magnitude contains the conversion needed for non-uniform Transform scale.
        Vector3 localDirection = transform.InverseTransformPoint(worldPosition + normalizedWorldDirection)
            - transform.InverseTransformPoint(worldPosition);
        Vector3 halfSize = areaCollider.size * 0.5f;

        float distanceX = Mathf.Abs(localDirection.x) > 0.0001f
            ? (localDirection.x > 0f ? halfSize.x - localPosition.x : -halfSize.x - localPosition.x) / localDirection.x
            : float.PositiveInfinity;
        float distanceZ = Mathf.Abs(localDirection.z) > 0.0001f
            ? (localDirection.z > 0f ? halfSize.z - localPosition.z : -halfSize.z - localPosition.z) / localDirection.z
            : float.PositiveInfinity;

        return Mathf.Max(0f, Mathf.Min(distanceX, distanceZ));
    }

    public bool Contains(Vector3 worldPosition, float tolerance = 0.01f)
    {
        Vector3 point = transform.InverseTransformPoint(worldPosition) - areaCollider.center;
        Vector3 half = areaCollider.size * 0.5f + new Vector3(tolerance, 0f, tolerance);
        return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.z) <= half.z;
    }

    /// <summary>Điểm trên cạnh mà xe sẽ chạm khi đi thẳng theo hướng worldDirection.</summary>
    public Vector3 GetEdgePoint(Vector3 worldPosition, Vector3 worldDirection)
    {
        Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
        Vector3 edgePoint = worldPosition + direction * DistanceToEdge(worldPosition, direction);
        edgePoint.y = worldPosition.y;
        return edgePoint;
    }

    /// <summary>Chiếu một world point lên bề mặt gần nhất của Box.</summary>
    public Vector3 ProjectToSurface(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint) - areaCollider.center;
        Vector3 half = areaCollider.size * 0.5f;
        localPoint.x = Mathf.Clamp(localPoint.x, -half.x, half.x);
        localPoint.z = Mathf.Clamp(localPoint.z, -half.z, half.z);

        float left = Mathf.Abs(localPoint.x + half.x);
        float top = Mathf.Abs(localPoint.z - half.z);
        float right = Mathf.Abs(localPoint.x - half.x);
        float bottom = Mathf.Abs(localPoint.z + half.z);
        float min = Mathf.Min(left, top, right, bottom);

        if (min == left) localPoint.x = -half.x;
        else if (min == top) localPoint.z = half.z;
        else if (min == right) localPoint.x = half.x;
        else localPoint.z = -half.z;

        return transform.TransformPoint(areaCollider.center + localPoint);
    }

    /// <summary>Đường theo chu vi từ cạnh xuất phát tới đích trên bề mặt Box.</summary>
    public Vector3[] GetPerimeterRoute(Vector3 fromEdgePoint, Vector3 targetPoint, bool clockwise)
    {

        Vector3 center = areaCollider.center;
        Vector3 half = areaCollider.size * 0.5f;
        Vector3[] localCorners =
        {
            center + new Vector3(-half.x, 0f, -half.z),
            center + new Vector3(-half.x, 0f, half.z),
            center + new Vector3(half.x, 0f, half.z),
            center + new Vector3(half.x, 0f, -half.z)
        };

        Vector3[] corners = new Vector3[4];
        for (int i = 0; i < corners.Length; i++)
            corners[i] = transform.TransformPoint(localCorners[i]);

        int startEdge = FindEdgeIndex(fromEdgePoint);
        int endEdge = FindEdgeIndex(targetPoint);
        int step = clockwise ? 1 : -1;
        int cornerCount = clockwise
            ? (endEdge - startEdge + 4) % 4
            : (startEdge - endEdge + 4) % 4;

        Vector3[] route = new Vector3[cornerCount + 1];
        for (int i = 0; i < cornerCount; i++)
        {
            int cornerIndex = (startEdge + (step > 0 ? 1 : 0) + step * i + 4) % 4;
            route[i] = corners[cornerIndex];
        }
        route[^1] = targetPoint;
        return route;
    }

    // Edge 0: trái, 1: trên (+Z), 2: phải, 3: dưới (-Z), theo thứ tự local.
    private int FindEdgeIndex(Vector3 worldPoint)
    {
        Vector3 point = transform.InverseTransformPoint(worldPoint) - areaCollider.center;
        Vector3 half = areaCollider.size * 0.5f;
        float left = Mathf.Abs(point.x + half.x);
        float top = Mathf.Abs(point.z - half.z);
        float right = Mathf.Abs(point.x - half.x);
        float bottom = Mathf.Abs(point.z + half.z);

        float min = Mathf.Min(left, top, right, bottom);
        if (min == left) return 0;
        if (min == top) return 1;
        if (min == right) return 2;
        return 3;
    }
}
