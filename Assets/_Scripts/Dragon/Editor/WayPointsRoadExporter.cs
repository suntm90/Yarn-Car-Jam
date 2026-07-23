using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WayPoints))]
public sealed class WayPointsRoadExporter : Editor
{
    [Serializable]
    private sealed class RoadExportData
    {
        public string coordinateSystem = "Unity: X right, Y up, Z forward. Blender importer maps this to X right, Z up, Y forward.";
        public bool loop;
        public int samplesPerSegment;
        public Vector3[] splinePoints;
    }

    private int samplesPerSegment = 24;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Blender Road Export", EditorStyles.boldLabel);
        samplesPerSegment = EditorGUILayout.IntSlider("Samples Per Segment", samplesPerSegment, 1, 200);

        if (GUILayout.Button("Export Road JSON"))
            Export((WayPoints)target, samplesPerSegment);
    }

    private static void Export(WayPoints wayPoints, int samplesPerSegment)
    {
        string path = EditorUtility.SaveFilePanel("Export WayPoints Road", Application.dataPath,
            "waypoints_road", "json");
        if (string.IsNullOrEmpty(path))
            return;

        RoadExportData data = new()
        {
            loop = wayPoints.Loop,
            samplesPerSegment = samplesPerSegment,
            splinePoints = wayPoints.GetSplineSamples(samplesPerSegment).ToArray()
        };

        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        Debug.Log($"Exported {data.splinePoints.Length} road spline points to: {path}", wayPoints);
    }
}
