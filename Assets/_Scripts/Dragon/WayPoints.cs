using UnityEngine;
using System.Collections.Generic;

/// <summary>Container singleton cho đường đi của rồng. Các DragonWaypoint phải là con của object này.</summary>
public sealed class WayPoints : MonoBehaviour
{
    public static WayPoints Instance { get; private set; }

    [SerializeField] private DragonWaypoint[] waypoints;
    [SerializeField] private bool loop;
    public DragonWaypoint[] Points => waypoints;
    public bool Loop => loop;

    /// <summary>Sample chính xác spline Catmull-Rom đang dùng để hiển thị và di chuyển rồng.</summary>
    public List<Vector3> GetSplineSamples(int samplesPerSegment)
    {
        List<Vector3> samples = new();
        if (waypoints == null || waypoints.Length < 2)
            return samples;

        foreach (DragonWaypoint waypoint in waypoints)
            if (waypoint == null)
                return samples;

        samplesPerSegment = Mathf.Max(1, samplesPerSegment);
        int segmentCount = loop ? waypoints.Length : waypoints.Length - 1;
        for (int segment = 0; segment < segmentCount; segment++)
        {
            int startSample = loop ? 0 : (segment == 0 ? 0 : 1);
            int endSample = loop ? samplesPerSegment - 1 : samplesPerSegment;
            for (int sample = startSample; sample <= endSample; sample++)
                samples.Add(GetSplinePoint(segment, sample / (float)samplesPerSegment));
        }

        return samples;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one WayPoints object is allowed in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Collect Child Waypoints")]
    public void CollectChildWaypoints()
    {
        waypoints = GetComponentsInChildren<DragonWaypoint>(true);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2)
            return;

        foreach (DragonWaypoint waypoint in waypoints)
            if (waypoint == null)
                return;

        Gizmos.color = Color.magenta;
        List<Vector3> samples = GetSplineSamples(24);
        for (int i = 1; i < samples.Count; i++)
            Gizmos.DrawLine(samples[i - 1], samples[i]);
        if (loop && samples.Count > 1)
            Gizmos.DrawLine(samples[^1], samples[0]);
    }

    // Catmull-Rom curve: it passes exactly through every waypoint and smooths its corners.
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

    private Vector3 GetWaypointPosition(int index)
    {
        if (loop)
        {
            index = (index % waypoints.Length + waypoints.Length) % waypoints.Length;
            return waypoints[index].transform.position;
        }

        index = Mathf.Clamp(index, 0, waypoints.Length - 1);
        return waypoints[index].transform.position;
    }
}
