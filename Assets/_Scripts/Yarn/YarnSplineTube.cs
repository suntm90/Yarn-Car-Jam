using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[DefaultExecutionOrder(0)]
public sealed class YarnSplineTube : MonoBehaviour
{
    [Header("Bezier control points")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform startControl;
    [SerializeField] private Transform endControl;
    [SerializeField] private Transform endPoint;

    [Header("Tube")]
    [Min(2)]
    [SerializeField] private int lengthSegments = 20;

    [Range(3, 16)]
    [SerializeField] private int radialSegments = 6;

    [Min(0.001f)]
    [SerializeField] private float radius = 0.035f;

    [Min(0.01f)]
    [SerializeField] private float uvRepeatPerUnit = 8f;

    [Header("Visible range")]
    [Range(0f, 1f)]
    [SerializeField] private float visibleStart;

    [Range(0f, 1f)]
    [SerializeField] private float visibleEnd = 1f;

    [Header("Update")]
    [SerializeField] private bool updateEveryFrame = true;

    private Mesh mesh;

    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] uv;
    private int[] triangles;

    public float VisibleStart
    {
        get => visibleStart;
        set => visibleStart = Mathf.Clamp01(value);
    }

    public float VisibleEnd
    {
        get => visibleEnd;
        set => visibleEnd = Mathf.Clamp01(value);
    }

    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0.001f, value);
    }

    public void ConfigurePoints(Vector3 start, Vector3 end)
    {
        if (startPoint == null || startControl == null || endControl == null || endPoint == null)
            return;

        Vector3 delta = end - start;
        startPoint.position = start;
        endPoint.position = end;
        // Collinear control points at 1/3 and 2/3 produce a straight Bezier segment.
        startControl.position = start + delta / 3f;
        endControl.position = start + delta * (2f / 3f);
        RebuildMesh();
    }

    private void Awake()
    {
        CreateMesh();
        RebuildMesh();
    }

    private void OnEnable()
    {
        if (mesh == null)
            CreateMesh();

        RebuildMesh();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
            RebuildMesh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        lengthSegments = Mathf.Max(2, lengthSegments);
        radialSegments = Mathf.Clamp(radialSegments, 3, 16);
        radius = Mathf.Max(0.001f, radius);
        uvRepeatPerUnit = Mathf.Max(0.01f, uvRepeatPerUnit);

        if (!Application.isPlaying)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (meshFilter.sharedMesh == null)
                CreateMesh();

            RebuildMesh();
        }
    }
#endif

    private void CreateMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        mesh = new Mesh
        {
            name = "Procedural Yarn Tube"
        };

        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;
    }

    public void RebuildMesh()
    {
        if (mesh == null)
            return;

        if (startPoint == null ||
            startControl == null ||
            endControl == null ||
            endPoint == null)
        {
            mesh.Clear();
            return;
        }

        float fromT = Mathf.Min(visibleStart, visibleEnd);
        float toT = Mathf.Max(visibleStart, visibleEnd);

        int ringCount = lengthSegments + 1;
        int vertexCount = ringCount * radialSegments;
        int indexCount = lengthSegments * radialSegments * 6;

        EnsureBuffers(vertexCount, indexCount);

        Vector3 previousPosition = Vector3.zero;
        Vector3 previousSide = Vector3.zero;
        float accumulatedLength = 0f;

        for (int ring = 0; ring < ringCount; ring++)
        {
            float ringRatio = ring / (float)lengthSegments;
            float t = Mathf.Lerp(fromT, toT, ringRatio);

            Vector3 worldPosition = EvaluateBezier(t);
            Vector3 worldTangent = EvaluateSafeTangent(t);

            if (!IsFinite(worldPosition))
            {
                Debug.LogError(
                    $"Invalid yarn position at ring {ring}, t = {t}",
                    this
                );

                worldPosition = endPoint.position;
            }

            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector3 localTangent =
                transform.InverseTransformDirection(worldTangent).normalized;

            if (ring > 0)
                accumulatedLength += Vector3.Distance(
                    previousPosition,
                    localPosition
                );

            Vector3 side;

            if (ring == 0)
            {
                side = CreateInitialSide(localTangent);
            }
            else
            {
                // Project the previous side onto the plane perpendicular
                // to the new tangent. This reduces sudden tube twisting.
                Vector3 projectedSide = Vector3.ProjectOnPlane(
    previousSide,
    localTangent
);

                if (IsFinite(projectedSide) &&
                    projectedSide.sqrMagnitude > 0.000001f)
                {
                    side = projectedSide.normalized;
                }
                else
                {
                    side = CreateInitialSide(localTangent);
                }

                if (side.sqrMagnitude < 0.0001f)
                    side = CreateInitialSide(localTangent);
            }

            Vector3 up = Vector3.Cross(localTangent, side).normalized;

            for (int sideIndex = 0;
                 sideIndex < radialSegments;
                 sideIndex++)
            {
                float sideRatio = sideIndex / (float)radialSegments;
                float angle = sideRatio * Mathf.PI * 2f;

                Vector3 radialDirection =
                    side * Mathf.Cos(angle) +
                    up * Mathf.Sin(angle);

                int vertexIndex = ring * radialSegments + sideIndex;

                vertices[vertexIndex] =
                    localPosition + radialDirection * radius;

                normals[vertexIndex] = radialDirection.normalized;

                uv[vertexIndex] = new Vector2(
                    sideRatio,
                    accumulatedLength * uvRepeatPerUnit
                );
            }

            previousPosition = localPosition;
            previousSide = side;
        }

        int triangleIndex = 0;

        for (int ring = 0; ring < lengthSegments; ring++)
        {
            int currentRing = ring * radialSegments;
            int nextRing = (ring + 1) * radialSegments;

            for (int sideIndex = 0;
                 sideIndex < radialSegments;
                 sideIndex++)
            {
                int nextSide = (sideIndex + 1) % radialSegments;

                int a = currentRing + sideIndex;
                int b = currentRing + nextSide;
                int c = nextRing + sideIndex;
                int d = nextRing + nextSide;

                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;

                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = d;
                triangles[triangleIndex++] = c;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;

        // Average normals across shared vertices for smooth lighting on the tube.
        mesh.RecalculateNormals();
        if (toT - fromT > 0.001f)   mesh.RecalculateTangents();

        mesh.RecalculateBounds();
    }

    private Vector3 EvaluateSafeTangent(float t)
    {
        Vector3 tangent = EvaluateBezierTangent(t);

        if (IsFinite(tangent) &&
            tangent.sqrMagnitude > 0.000001f)
        {
            return tangent.normalized;
        }

        const float epsilon = 0.001f;

        float previousT = Mathf.Clamp01(t - epsilon);
        float nextT = Mathf.Clamp01(t + epsilon);

        Vector3 previousPosition = EvaluateBezier(previousT);
        Vector3 nextPosition = EvaluateBezier(nextT);

        tangent = nextPosition - previousPosition;

        if (IsFinite(tangent) &&
            tangent.sqrMagnitude > 0.000001f)
        {
            return tangent.normalized;
        }

        Vector3 fallback =
            endPoint.position - startPoint.position;

        if (IsFinite(fallback) &&
            fallback.sqrMagnitude > 0.000001f)
        {
            return fallback.normalized;
        }

        return Vector3.forward;
    }

    private static bool IsFinite(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }

    private void EnsureBuffers(int vertexCount, int indexCount)
    {
        if (vertices == null || vertices.Length != vertexCount)
        {
            vertices = new Vector3[vertexCount];
            normals = new Vector3[vertexCount];
            uv = new Vector2[vertexCount];
        }

        if (triangles == null || triangles.Length != indexCount)
            triangles = new int[indexCount];
    }

    private Vector3 CreateInitialSide(Vector3 tangent)
    {
        if (!IsFinite(tangent) ||
            tangent.sqrMagnitude < 0.000001f)
        {
            tangent = Vector3.forward;
        }

        tangent.Normalize();

        Vector3 referenceAxis =
            Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f
                ? Vector3.right
                : Vector3.up;

        Vector3 side = Vector3.Cross(
            referenceAxis,
            tangent
        );

        if (!IsFinite(side) ||
            side.sqrMagnitude < 0.000001f)
        {
            side = Vector3.Cross(
                Vector3.forward,
                tangent
            );
        }

        if (!IsFinite(side) ||
            side.sqrMagnitude < 0.000001f)
        {
            return Vector3.right;
        }

        return side.normalized;
    }

    public Vector3 EvaluateBezier(float t)
    {
        Vector3 p0 = startPoint.position;
        Vector3 p1 = startControl.position;
        Vector3 p2 = endControl.position;
        Vector3 p3 = endPoint.position;

        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;

        return
            uu * u * p0 +
            3f * uu * t * p1 +
            3f * u * tt * p2 +
            tt * t * p3;
    }

    public Vector3 EvaluateBezierTangent(float t)
    {
        Vector3 p0 = startPoint.position;
        Vector3 p1 = startControl.position;
        Vector3 p2 = endControl.position;
        Vector3 p3 = endPoint.position;

        float u = 1f - t;

        return
            3f * u * u * (p1 - p0) +
            6f * u * t * (p2 - p1) +
            3f * t * t * (p3 - p2);
    }
}
