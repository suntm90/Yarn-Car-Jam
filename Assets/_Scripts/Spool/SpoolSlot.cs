using UnityEngine;

/// <summary>Điểm đặt một Spool trong màn chơi.</summary>
public sealed class SpoolSlot : MonoBehaviour
{
    private const float UnlockedDragonSegmentDistanceBonus = 1.2f;

    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool isLocked;

    public Spool CurrentSpool { get; private set; }
    public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
    public bool IsLocked => isLocked;
    public bool IsReserved { get; private set; }
    public bool IsOccupied => CurrentSpool != null || isLocked || IsReserved;
    public bool HasBeenUnlocked { get; private set; }

    public void SetSpool(Spool spool)
    {
        Clear();
        CurrentSpool = spool;
        IsReserved = false;
    }

    /// <summary>Đánh dấu slot đã được một xe chọn làm đích.</summary>
    public bool TryReserve()
    {
        if (IsOccupied)
            return false;

        IsReserved = true;
        return true;
    }

    public bool TryUnlock()
    {
        if (!isLocked)
            return false;

        isLocked = false;
        HasBeenUnlocked = true;
        return true;
    }

    public float GetDragonSegmentDistance(float defaultDistance)
    {
        return defaultDistance + (HasBeenUnlocked ? UnlockedDragonSegmentDistanceBonus : 0f);
    }

    public void ReleaseReservation() => IsReserved = false;

    public void Clear()
    {
        if (CurrentSpool != null)
            Destroy(CurrentSpool.gameObject);

        CurrentSpool = null;
        IsReserved = false;
    }
}
