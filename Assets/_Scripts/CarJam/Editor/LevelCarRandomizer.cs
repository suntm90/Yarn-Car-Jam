#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public static class LevelCarRandomizer
{
    private const string ScenePath = "Assets/Prefabs/Level.unity";
    private const string CarPrefabFolder = "Assets/Prefabs/Car";
    private const float Margin = 0.7f;
    private const float MinGap = 0.5f;
    private const float MaxGap = 0.6f;
    private const int MaxDirectionCountDifference = 4;
    private const float RestrictedRegionRatio = 0.4f;
    private const int PlacementAttempts = 5000;

    private sealed class CarTemplate
    {
        public GameObject prefab;
        public float width;
        public float length;
    }

    private struct Placement
    {
        public Vector2 center;
        public Vector2 size;
        public float gap;
    }

    [MenuItem("Tools/Yarn Car Jam/Arrange Level Cars")]
    public static void ArrangeLevelCars()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool sceneWasAlreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (!sceneWasAlreadyLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        CarJamArea jamArea = FindInScene<CarJamArea>(scene).FirstOrDefault();
        if (jamArea == null || !jamArea.TryGetComponent(out BoxCollider areaCollider))
            throw new InvalidOperationException("Level needs a CarJamArea with a BoxCollider.");

        List<CarTemplate> templates = LoadCarTemplates();
        CarJamCar[] sceneCars = FindInScene<CarJamCar>(scene).ToArray();
        DeleteSceneCars(sceneCars);
        Physics.SyncTransforms();

        Bounds areaBounds = areaCollider.bounds;
        float groundY = areaBounds.min.y;
        int randomSeed = Guid.NewGuid().GetHashCode();
        Random.InitState(randomSeed);

        List<Placement> placements = new();
        HashSet<CarJamCar> placedCars = new();
        int[] directionCounts = new int[4];
        int duplicateCount = 0;
        bool placedAnyInRound;
        do
        {
            placedAnyInRound = false;
            foreach (CarTemplate template in templates)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(template.prefab, scene) as GameObject;
                if (instance == null)
                    continue;

                CarJamCar car = instance.GetComponent<CarJamCar>();
                if (car != null && TryPlace(car, template.width, template.length,
                        areaBounds, groundY, placements, placedCars, directionCounts))
                {
                    duplicateCount++;
                    placedAnyInRound = true;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }
        while (placedAnyInRound);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException($"Could not save {ScenePath}.");

        if (!sceneWasAlreadyLoaded)
            EditorSceneManager.CloseScene(scene, true);

        Debug.Log($"Deleted {sceneCars.Length} old cars and arranged {duplicateCount} cars "
            + $"from {templates.Count} prefabs. Seed={randomSeed}, "
            + $"margin={Margin}, gap={MinGap}-{MaxGap}.");
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (T component in root.GetComponentsInChildren<T>(true))
            yield return component;
    }

    private static void DeleteSceneCars(IEnumerable<CarJamCar> cars)
    {
        HashSet<GameObject> roots = new();
        foreach (CarJamCar car in cars)
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(car.gameObject);
            roots.Add(root != null ? root : car.gameObject);
        }

        foreach (GameObject root in roots)
            UnityEngine.Object.DestroyImmediate(root);
    }

    private static List<CarTemplate> LoadCarTemplates()
    {
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { CarPrefabFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        List<CarTemplate> templates = new(paths.Length);
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CarJamCar car = prefab != null ? prefab.GetComponent<CarJamCar>() : null;
            if (car == null || car.GetComponent<BoxCollider>() == null)
                continue;

            GetCarFootprint(car, out float width, out float length);
            templates.Add(new CarTemplate { prefab = prefab, width = width, length = length });
        }

        if (templates.Count == 0)
            throw new InvalidOperationException(
                $"No prefabs with CarJamCar and BoxCollider were found in {CarPrefabFolder}.");

        return templates;
    }

    private static void GetCarFootprint(CarJamCar car, out float width, out float length)
    {
        BoxCollider collider = car.GetComponent<BoxCollider>();
        if (collider == null)
            throw new InvalidOperationException($"Car '{car.name}' has no BoxCollider.");

        Vector3 scale = car.transform.lossyScale;
        width = collider.size.x * Mathf.Abs(scale.x);
        length = collider.size.z * Mathf.Abs(scale.z);
    }

    private static bool TryPlace(CarJamCar car, float baseWidth, float baseLength,
        Bounds areaBounds, float groundY, List<Placement> placements,
        HashSet<CarJamCar> placedCars, int[] directionCounts)
    {
        Transform carTransform = car.transform;
        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            int directionIndex = Random.Range(0, 4);
            if (!WouldKeepDirectionsBalanced(directionIndex, directionCounts))
                continue;

            float yaw = directionIndex * 90f;
            bool sideways = (directionIndex & 1) == 1;
            float width = sideways ? baseLength : baseWidth;
            float length = sideways ? baseWidth : baseLength;
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            float minX = areaBounds.min.x + Margin + halfWidth;
            float maxX = areaBounds.max.x - Margin - halfWidth;
            float minZ = areaBounds.min.z + Margin + halfLength;
            float maxZ = areaBounds.max.z - Margin - halfLength;
            if (minX > maxX || minZ > maxZ)
                return false;

            Vector2 center = new(Random.Range(minX, maxX), Random.Range(minZ, maxZ));
            if (!IsDirectionAllowedForRegion(center, directionIndex, areaBounds))
                continue;

            float gap = Random.Range(MinGap, MaxGap);
            Vector2 size = new(width, length);
            if (OverlapsAny(center, size, gap, placements))
                continue;

            carTransform.SetPositionAndRotation(
                new Vector3(center.x, groundY, center.y),
                Quaternion.Euler(0f, yaw, 0f));

            Physics.SyncTransforms();
            if (HasOpposingCarInForwardBox(car, areaBounds, placedCars))
                continue;

            placements.Add(new Placement { center = center, size = size, gap = gap });
            placedCars.Add(car);
            directionCounts[directionIndex]++;
            return true;
        }

        return false;
    }

    private static bool IsDirectionAllowedForRegion(Vector2 center, int directionIndex,
        Bounds areaBounds)
    {
        float normalizedX = Mathf.InverseLerp(areaBounds.min.x, areaBounds.max.x, center.x);
        float normalizedZ = Mathf.InverseLerp(areaBounds.min.z, areaBounds.max.z, center.y);

        // Yaw 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.
        if (normalizedX <= RestrictedRegionRatio && directionIndex == 1)
            return false;
        if (normalizedX >= 1f - RestrictedRegionRatio && directionIndex == 3)
            return false;
        if (normalizedZ <= RestrictedRegionRatio && directionIndex == 0)
            return false;
        if (normalizedZ >= 1f - RestrictedRegionRatio && directionIndex == 2)
            return false;

        return true;
    }

    private static bool WouldKeepDirectionsBalanced(int candidateDirection, int[] counts)
    {
        int minCount = int.MaxValue;
        int maxCount = int.MinValue;
        for (int direction = 0; direction < counts.Length; direction++)
        {
            int count = counts[direction] + (direction == candidateDirection ? 1 : 0);
            minCount = Mathf.Min(minCount, count);
            maxCount = Mathf.Max(maxCount, count);
        }

        return maxCount - minCount <= MaxDirectionCountDifference;
    }

    private static bool HasOpposingCarInForwardBox(CarJamCar car, Bounds areaBounds,
        HashSet<CarJamCar> placedCars)
    {
        BoxCollider collider = car.GetComponent<BoxCollider>();
        if (collider == null)
            return true;

        Transform carTransform = car.transform;
        Vector3 forward = Vector3.ProjectOnPlane(carTransform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
            return true;

        Vector3 scale = carTransform.lossyScale;
        Vector3 halfExtents = Vector3.Scale(collider.size * 0.5f,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        halfExtents *= 0.98f;

        Vector3 origin = collider.transform.TransformPoint(collider.center);
        float castDistance = new Vector2(areaBounds.size.x, areaBounds.size.z).magnitude;
        RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, forward,
            carTransform.rotation, castDistance, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            CarJamCar other = hit.collider.GetComponentInParent<CarJamCar>();
            if (other == null || other == car || !placedCars.Contains(other))
                continue;

            Vector3 otherForward = Vector3.ProjectOnPlane(
                other.transform.forward, Vector3.up).normalized;
            if (Vector3.Dot(forward, otherForward) > -0.95f)
                continue;

            Vector3 toOther = Vector3.ProjectOnPlane(
                other.transform.position - carTransform.position, Vector3.up);
            if (toOther.sqrMagnitude < 0.0001f)
                return true;

            toOther.Normalize();
            bool carFacesOther = Vector3.Dot(forward, toOther) > 0.5f;
            bool otherFacesCar = Vector3.Dot(otherForward, -toOther) > 0.5f;
            if (carFacesOther && otherFacesCar)
                return true;
        }

        return false;
    }

    private static bool OverlapsAny(Vector2 center, Vector2 size, float gap,
        List<Placement> placements)
    {
        foreach (Placement other in placements)
        {
            float requiredGap = Mathf.Max(gap, other.gap);
            float allowedX = (size.x + other.size.x) * 0.5f + requiredGap;
            float allowedZ = (size.y + other.size.y) * 0.5f + requiredGap;
            if (Mathf.Abs(center.x - other.center.x) < allowedX
                && Mathf.Abs(center.y - other.center.y) < allowedZ)
                return true;
        }

        return false;
    }
}
#endif
