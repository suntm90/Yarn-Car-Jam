using UnityEngine;

public sealed class CatWayPoints : MonoBehaviour
{
    [SerializeField] private CatWaypoint[] points;
    [SerializeField] private bool loop;

    public CatWaypoint[] Points => points;
    public bool Loop => loop;

    public Vector3 GetSplinePoint(int segmentIndex, float t)
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

    public Vector3 GetSplineTangent(int segmentIndex, float t)
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

    public float GetSegmentLength(int segmentIndex)
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

    [ContextMenu("Collect Child Cat Waypoints")]
    public void CollectChildWaypoints()
    {
        points = GetComponentsInChildren<CatWaypoint>(true);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnValidate()
    {
        if (points == null || points.Length == 0)
            points = GetComponentsInChildren<CatWaypoint>(true);
    }

    private void OnDrawGizmos()
    {
        if (points == null || points.Length < 2)
            return;

        foreach (CatWaypoint point in points)
        {
            if (point == null)
                return;
        }

        Gizmos.color = Color.cyan;
        const int samplesPerSegment = 24;
        int segmentCount = loop ? points.Length : points.Length - 1;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            Vector3 previous = GetSplinePoint(segment, 0f);
            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                Vector3 current = GetSplinePoint(segment, sample / (float)samplesPerSegment);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }

    private Vector3 GetWaypointPosition(int index)
    {
        if (loop)
        {
            index = (index % points.Length + points.Length) % points.Length;
            return points[index].transform.position;
        }

        index = Mathf.Clamp(index, 0, points.Length - 1);
        return points[index].transform.position;
    }
}
