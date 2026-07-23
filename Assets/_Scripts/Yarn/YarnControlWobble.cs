using UnityEngine;
[DefaultExecutionOrder(-100)]
public sealed class YarnControlWobble : MonoBehaviour
{
    [Header("Control Points")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform startControl;
    [SerializeField] private Transform endControl;
    [SerializeField] private Transform endPoint;

    [Header("Wave")]
    [Min(0f)]
    [SerializeField] private float amplitude = 0.25f;

    [Min(0f)]
    [SerializeField] private float frequency = 2.5f;

    [Range(0f, 180f)]
    [SerializeField] private float controlPhaseDifference = 60f;

    [Header("Smoothing")]
    [Min(0f)]
    [SerializeField] private float smoothSpeed = 15f;

    [Header("Optional Vertical Motion")]
    [Min(0f)]
    [SerializeField] private float verticalAmplitude = 0.03f;

    [Min(0f)]
    [SerializeField] private float verticalFrequencyMultiplier = 1.4f;


    private Vector3 startControlBasePosition;
    private Vector3 endControlBasePosition;

    private float currentStrength;
    private float targetStrength;

    private float animationTime;
    private bool initialized;

    public float Strength
    {
        get => targetStrength;
        set => targetStrength = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (startControl == null || endControl == null)
            return;

        startControlBasePosition = startControl.position;
        endControlBasePosition = endControl.position;

        animationTime = 0f;
        currentStrength = 0f;
        targetStrength = 0f;

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized ||
            startPoint == null ||
            startControl == null ||
            endControl == null ||
            endPoint == null)
        {
            return;
        }

        animationTime += Time.deltaTime;

        currentStrength = Mathf.Lerp(
            currentStrength,
            targetStrength,
            1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
        );

        UpdateControlPoints();
    }

    private void UpdateControlPoints()
    {
        Vector3 yarnDirection =
            endPoint.position - startPoint.position;

        if (yarnDirection.sqrMagnitude < 0.000001f)
            return;

        yarnDirection.Normalize();

        Vector3 sideDirection = Vector3.Cross(
            Vector3.up,
            yarnDirection
        );

        if (sideDirection.sqrMagnitude < 0.000001f)
        {
            sideDirection = Vector3.Cross(
                Vector3.forward,
                yarnDirection
            );
        }

        sideDirection.Normalize();

        Vector3 verticalDirection = Vector3.Cross(
            yarnDirection,
            sideDirection
        ).normalized;

        float angularFrequency =
            frequency * Mathf.PI * 2f;

        float phase1 =
            animationTime * angularFrequency;

        float phaseOffset =
            controlPhaseDifference * Mathf.Deg2Rad;

        float phase2 =
            phase1 + phaseOffset;

        float startSideWave = Mathf.Sin(phase1);
        float endSideWave = Mathf.Sin(phase2);

        float startVerticalWave = Mathf.Sin(
            phase1 * verticalFrequencyMultiplier + 1.1f
        );

        float endVerticalWave = Mathf.Sin(
            phase2 * verticalFrequencyMultiplier + 1.1f
        );

        Vector3 startOffset =
            sideDirection *
            startSideWave *
            amplitude;

        Vector3 endOffset =
            sideDirection *
            endSideWave *
            amplitude;

        startOffset +=
            verticalDirection *
            startVerticalWave *
            verticalAmplitude;

        endOffset +=
            verticalDirection *
            endVerticalWave *
            verticalAmplitude;

        startControl.position =
            startControlBasePosition +
            startOffset * currentStrength;

        endControl.position =
            endControlBasePosition +
            endOffset * currentStrength;
    }

    public void StartWobble()
    {
        targetStrength = 1f;
    }

    public void StopWobble()
    {
        targetStrength = 0f;
    }

    public void SetWobbleStrength(float strength)
    {
        targetStrength = Mathf.Clamp01(strength);
    }

    public void ResetControlPoints()
    {
        targetStrength = 0f;
        currentStrength = 0f;
        animationTime = 0f;

        if (startControl != null)
            startControl.position = startControlBasePosition;

        if (endControl != null)
            endControl.position = endControlBasePosition;
    }

    public void RefreshBasePositions()
    {
        if (startControl != null)
            startControlBasePosition = startControl.position;

        if (endControl != null)
            endControlBasePosition = endControl.position;
    }

    public void DistributeControlPointsEvenly()
    {
        if (startPoint == null ||
            startControl == null ||
            endControl == null ||
            endPoint == null)
        {
            return;
        }

        startControl.position = new Vector3(0, 3.0f, 0) + Vector3.Lerp(
            startPoint.position,
            endPoint.position,
            1f / 3f
        );

        endControl.position = new Vector3(0, 2.0f, 0) + Vector3.Lerp(
            startPoint.position,
            endPoint.position,
            2f / 3f
        );

        RefreshBasePositions();
    }
}