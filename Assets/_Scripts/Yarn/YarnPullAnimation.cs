using System.Collections;
using System;
using UnityEngine;

public sealed class YarnPullAnimation : MonoBehaviour
{
    private static readonly int FlowOffsetId =
        Shader.PropertyToID("_FlowOffset");

    private static readonly int YarnColorId =
        Shader.PropertyToID("_BaseColor");
    [SerializeField] private YarnControlWobble controlWobble;

    [Header("References")]
    [SerializeField] private YarnSplineTube yarn;
    [SerializeField] private Renderer yarnRenderer;
    [SerializeField] private Transform spoolVisual;

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float pullDuration = 0.55f;

    [Min(0f)]
    [SerializeField] private float settleDuration = 0.18f;

    [Header("Motion")]
    [SerializeField]
    private AnimationCurve pullCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.25f, 0.08f),
            new Keyframe(1f, 1f, 0f, 0f)
        );

    [Min(0f)]
    [SerializeField] private float flowSpeed = 4f;

    [Header("Spool punch")]
    [Range(0f, 0.5f)]
    [SerializeField] private float punchAmount = 0.12f;

    [Min(1f)]
    [SerializeField] private float punchFrequency = 2f;

    private MaterialPropertyBlock propertyBlock;
    private Coroutine animationCoroutine;
    private Vector3 spoolOriginalScale;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (spoolVisual != null)
            spoolOriginalScale = spoolVisual.localScale;
    }

    [ContextMenu("Play Pull Animation")]
    public void Play()
    {
        if (!isActiveAndEnabled || yarn == null)
            return;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(PullRoutine(pullDuration, true, null, null));
    }

    public void PlayTimed(float duration, Action<float> onProgress, Action onComplete)
    {
        if (!isActiveAndEnabled || yarn == null)
            return;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(PullRoutine(Mathf.Max(0.01f, duration), false,
            onProgress, onComplete));
    }

    public void ConfigurePoints(Vector3 start, Vector3 end)
    {
        yarn?.ConfigurePoints(start, end);
        // YarnControlWobble caches world-space bases, so refresh them after endpoints move.
        controlWobble?.DistributeControlPointsEvenly();
    }

    public void ResetYarn()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        controlWobble?.ResetControlPoints();

        yarn.VisibleStart = 0f;
        yarn.VisibleEnd = 1f;
        yarn.RebuildMesh();

        SetFlowOffset(0f);

        if (spoolVisual != null)
            spoolVisual.localScale = spoolOriginalScale;
    }

    public void SetColor(Color color)
    {
        if (yarnRenderer == null)
            return;

        yarnRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(YarnColorId, color);
        yarnRenderer.SetPropertyBlock(propertyBlock);
    }

    private IEnumerator PullRoutine(float duration, bool playSettle, Action<float> onProgress,
        Action onComplete)
    {
        AudioManager.Instance?.PlayPull();

        yarn.VisibleStart = 0f;
        yarn.VisibleEnd = 1f;

        if (playSettle)
            controlWobble?.StartWobble();
        else
            controlWobble?.SetWobbleStrength(0f);

        float elapsed = 0f;

        onProgress?.Invoke(0f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(elapsed / duration);

            float progress =
                pullCurve.Evaluate(normalizedTime);

            //yarn.VisibleStart = Mathf.Min(progress, 0.995f);

            float attack = Mathf.Clamp01(
                normalizedTime / 0.08f
            );

            float decay = Mathf.Pow(
                1f - normalizedTime,
                0.8f
            );

            controlWobble?.SetWobbleStrength(
                attack * decay
            );

            SetFlowOffset(progress * flowSpeed);
            onProgress?.Invoke(normalizedTime);

            yield return null;
        }

        yarn.VisibleStart = 0.995f;

        controlWobble?.StopWobble();

        if (playSettle && spoolVisual != null && settleDuration > 0f)
            yield return PunchSpoolRoutine();

        animationCoroutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PunchSpoolRoutine()
    {
        float elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / settleDuration);

            float damping = 1f - t;
            float wave = Mathf.Sin(
                t * Mathf.PI * punchFrequency
            );

            float punch = wave * damping * punchAmount;

            // Compress in the pull direction and expand sideways.
            Vector3 scaleMultiplier = new Vector3(
                1f + punch,
                1f - punch * 0.65f,
                1f + punch
            );

            spoolVisual.localScale = Vector3.Scale(
                spoolOriginalScale,
                scaleMultiplier
            );

            yield return null;
        }

        spoolVisual.localScale = spoolOriginalScale;
    }

    private void SetFlowOffset(float value)
    {
        if (yarnRenderer == null)
            return;

        yarnRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FlowOffsetId, value);
        yarnRenderer.SetPropertyBlock(propertyBlock);
    }
}
