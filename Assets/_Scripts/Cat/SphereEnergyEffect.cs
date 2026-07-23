using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SphereEnergyEffect : MonoBehaviour
{
    [SerializeField] private GameObject sphere;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float holdDuration = 3f;
    [SerializeField, Min(0.01f)] private float scaleDuration = 1f;

    [Header("Scale")]
    [SerializeField, Min(1f)] private float targetScaleMultiplier = 2f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Vector3 initialScale;
    private Coroutine activeRoutine;

    private void Awake()
    {
        if (sphere == null)
        {
            Debug.LogWarning("SphereEnergyEffect needs a Sphere reference.", this);
            return;
        }

        initialScale = sphere.transform.localScale;
        sphere.SetActive(false);
    }

    private void Start()
    {
        if (DragonController.Instance == null)
        {
            Debug.LogWarning("SphereEnergyEffect could not find DragonController.", this);
            return;
        }

        DragonController.Instance.DestinationReached += HandleDragonDestinationReached;
    }

    private void OnDestroy()
    {
        if (DragonController.Instance != null)
            DragonController.Instance.DestinationReached -= HandleDragonDestinationReached;
    }

    private void HandleDragonDestinationReached()
    {
        ActiveSphere();
    }

    /// <summary>
    /// Activates the sphere, holds for three seconds, scales to 200% over one
    /// second, then deactivates it. Calling again restarts the sequence.
    /// </summary>
    public void ActiveSphere()
    {
        if (sphere == null)
            return;

        sphere.SetActive(true);
        sphere.transform.localScale = initialScale;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        yield return new WaitForSeconds(holdDuration);

        Vector3 targetScale = initialScale * targetScaleMultiplier;
        float elapsed = 0f;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / scaleDuration);
            float curvedProgress = scaleCurve.Evaluate(progress);
            sphere.transform.localScale = Vector3.LerpUnclamped(
                initialScale, targetScale, curvedProgress);
            yield return null;
        }
        AudioManager.Instance.Play(AudioClipId.BubblePop);
        sphere.transform.localScale = targetScale;
        sphere.SetActive(false);
        sphere.transform.localScale = initialScale;
        activeRoutine = null;
    }
}
