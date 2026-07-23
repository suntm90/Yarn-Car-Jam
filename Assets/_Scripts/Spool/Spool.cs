using UnityEngine;

public enum SpoolSize
{
    Size4 = 4,
    Size6 = 6,
    Size8 = 8,
    Size10 = 10
}

public enum SpoolColor
{
    Blue,
    Green,
    Pink,
    Violet,
    Yellow,
}

/// <summary>Thông tin và hiển thị của một Spool đã được spawn.</summary>
public sealed class Spool : MonoBehaviour
{
    [SerializeField] private SpoolSize size;
    [SerializeField] private SpoolColor color;
    [Header("Yarn Rolls")]
    [SerializeField] private GameObject yarnRollPrefab;
    [SerializeField] private SpoolYarnRollMarker[] yarnRollMarkers;
    [Tooltip("Các Renderer cần được thay Material khi đổi màu.")]
    [SerializeField] private Renderer[] colorRenderers;

    public SpoolSize Size => size;
    public SpoolColor Color => color;
    public GameObject YarnRollPrefab => yarnRollPrefab;
    public SpoolYarnRollMarker[] YarnRollMarkers => yarnRollMarkers;
    public int RemainingSize { get; private set; }

    private int nextYarnRollIndex;

    public void Configure(SpoolSize newSize, SpoolColor newColor, Material material)
    {
        size = newSize;
        color = newColor;
        RemainingSize = (int)newSize;
        nextYarnRollIndex = 0;

        if (yarnRollMarkers != null)
            foreach (SpoolYarnRollMarker marker in yarnRollMarkers)
                if (marker != null)
                    marker.gameObject.SetActive(false);

        if (material == null)
            return;

        if (colorRenderers == null || colorRenderers.Length == 0)
            colorRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in colorRenderers)
        {
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            targetRenderer.sharedMaterials = materials;
        }
    }

    /// <summary>Tiêu hao một đơn vị size. Trả về true khi Spool đã cạn.</summary>
    public bool ConsumeOneSize()
    {
        RemainingSize = Mathf.Max(0, RemainingSize - 1);
        return RemainingSize == 0;
    }

    public bool TryActivateNextYarnRoll(out SpoolYarnRollMarker marker)
    {
        marker = null;
        if (yarnRollMarkers == null)
            return false;

        while (nextYarnRollIndex < yarnRollMarkers.Length)
        {
            marker = yarnRollMarkers[nextYarnRollIndex++];
            if (marker == null)
                continue;

            marker.gameObject.SetActive(true);
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    public void EditorSetYarnRollMarkers(SpoolYarnRollMarker[] markers)
    {
        yarnRollMarkers = markers;
    }
#endif
}
