using UnityEngine;

/// <summary>Metadata và hiệu ứng biến mất cho một đoạn thân rồng.</summary>
public sealed class DragonBodySegment : MonoBehaviour
{
    public SpoolColor Color { get; private set; }
    public bool IsBeingConsumed { get; private set; }

    private Transform visual;
    private Vector3 initialVisualScale;
    private Vector3 initialVisualPosition;
    private float pullLocalZOffset;

    public void Initialize(SpoolColor color, Transform visualTransform, float localZOffset)
    {
        Color = color;
        visual = visualTransform;
        pullLocalZOffset = localZOffset;
        if (visual == null)
            return;

        initialVisualScale = visual.localScale;
        initialVisualPosition = visual.localPosition;
    }

    public bool TryBeginConsume()
    {
        if (IsBeingConsumed)
            return false;

        IsBeingConsumed = true;
        return true;
    }

    public void CancelConsume() => IsBeingConsumed = false;

    /// <summary>Thu mesh từ đầu (+local forward) về phía sau cho tới khi biến mất.</summary>
    public void SetDisappearProgress(float progress)
    {
        if (visual == null)
            return;

        progress = Mathf.Clamp01(progress);
        Vector3 scale = initialVisualScale;
        scale.z *= 1f - progress;
        visual.localScale = scale;
        visual.localPosition = initialVisualPosition
            + Vector3.forward * (pullLocalZOffset * progress);
    }
}
