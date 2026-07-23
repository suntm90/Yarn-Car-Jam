using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(Spool))]
public sealed class SpoolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        Spool spool = (Spool)target;
        using (new EditorGUI.DisabledScope(spool.YarnRollPrefab == null))
        {
            if (GUILayout.Button("Build Yarn Rolls"))
                BuildYarnRolls(spool);
        }
    }

    private static void BuildYarnRolls(Spool spool)
    {
        RemoveGeneratedRolls(spool);

        int rollCount = (int)spool.Size;
        float x0 = -(rollCount / 2f) + 0.5f;
        List<SpoolYarnRollMarker> markers = new(rollCount);

        for (int i = 0; i < rollCount; i++)
        {
            GameObject roll = (GameObject)PrefabUtility.InstantiatePrefab(
                spool.YarnRollPrefab, spool.transform);
            Undo.RegisterCreatedObjectUndo(roll, "Build Spool Yarn Rolls");
            roll.name = $"Yarn Roll {i}";
            roll.transform.localPosition = new Vector3(x0 + i, 2f, 0f);
            roll.transform.localRotation = Quaternion.identity;
            SpoolYarnRollMarker marker = Undo.AddComponent<SpoolYarnRollMarker>(roll);
            markers.Add(marker);
            roll.SetActive(false);
        }

        spool.EditorSetYarnRollMarkers(markers.ToArray());
        EditorUtility.SetDirty(spool);
        PrefabUtility.RecordPrefabInstancePropertyModifications(spool);
    }

    private static void RemoveGeneratedRolls(Spool spool)
    {
        SpoolYarnRollMarker[] generatedRolls =
            spool.GetComponentsInChildren<SpoolYarnRollMarker>(true);

        foreach (SpoolYarnRollMarker marker in generatedRolls)
            if (marker.transform.parent == spool.transform)
                Undo.DestroyObjectImmediate(marker.gameObject);
    }
}
