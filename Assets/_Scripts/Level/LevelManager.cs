using System;
using UnityEngine;

public enum LevelResult
{
    Win,
    Lose
}

[DisallowMultipleComponent]
public sealed class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField, Min(0.01f)] private float loseDelayAtDestination = 3f;

    public event Action<LevelResult> LevelEnded;
    public bool HasEnded { get; private set; }
    public LevelResult Result { get; private set; }

    private float destinationTimer;
    private bool winCheckArmed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one LevelManager is allowed in a scene.", this);
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

    private void Update()
    {
        if (HasEnded)
            return;

        DragonController dragon = DragonController.Instance;
        if (dragon == null)
            return;

        if (dragon.BodySegmentCount > 0)
            winCheckArmed = true;

        if (winCheckArmed && dragon.BodySegmentCount == 0 && !dragon.IsRepacking)
        {
            EndLevel(LevelResult.Win);
            return;
        }

        bool canCountLoseTime = dragon.IsAtFinalDestination
            && !dragon.IsRepacking
            && !dragon.IsAnyBodySegmentBeingConsumed;

        destinationTimer = canCountLoseTime
            ? destinationTimer + Time.deltaTime
            : 0f;

        if (destinationTimer >= loseDelayAtDestination)
            EndLevel(LevelResult.Lose);
    }

    private void EndLevel(LevelResult result)
    {
        if (HasEnded)
            return;

        HasEnded = true;
        Result = result;

        if (result == LevelResult.Win)
            DragonController.Instance?.StopWaypointMovement();

        LevelEnded?.Invoke(result);
    }
}
