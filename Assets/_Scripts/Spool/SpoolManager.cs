using System;
using System.Collections;
using UnityEngine;

public sealed class SpoolManager : MonoBehaviour
{
    public static SpoolManager Instance { get; private set; }
    [Serializable]
    private struct SpoolPrefabEntry
    {
        public SpoolSize size;
        public Spool prefab;
    }

    [Serializable]
    private struct SpoolMaterialEntry
    {
        public SpoolColor color;
        public Material material;
    }

    [SerializeField] private SpoolSlot[] slots;
    [SerializeField] private SpoolPrefabEntry[] spoolPrefabs;
    [SerializeField] private SpoolMaterialEntry[] colorMaterials;

    [Header("Spool Spawn")]
    [SerializeField] private GameObject spoolSpawnVfxPrefab;
    [SerializeField, Min(0.01f)] private float spoolSpawnDuration = 0.35f;
    [SerializeField, Min(0f)] private float spoolSpawnVfxLifetime = 2f;
    [SerializeField, Range(0f, 0.5f)] private float yarnPullScaleAmount = 0.12f;

    [Header("Spool Depleted")]
    [SerializeField] private GameObject spoolDepletedVfxPrefab;
    [SerializeField, Min(0f)] private float spoolDepletedVfxLifetime = 2f;

    [Header("Dragon Yarn")]
    [SerializeField] private YarnPullAnimation yarnSystemPrefab;
    [SerializeField, Min(0f)] private float dragonSegmentDistance = 5f;
    [SerializeField] private Vector3 yarnStartOffset;
    [SerializeField, Min(0.01f)] private float yarnAnimationDuration = 0.5f;
    [SerializeField, Range(0.6f,0.95f)] private float yarnMoveWhenPulled = -0.78f;

    public SpoolSlot[] Slots => slots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one SpoolManager is allowed in a scene.", this);
            enabled = false;
            return;
        }

        Instance = this;
        CollectChildSlots();
    }

    private void OnValidate()
    {
        CollectChildSlots();
    }

    [ContextMenu("Collect Child Spool Slots")]
    private void CollectChildSlots()
    {
        SpoolSlot[] childSlots = GetComponentsInChildren<SpoolSlot>(true);
        if (childSlots.Length > 0)
            slots = childSlots;
    }

    private void Start()
    {
        if (CarJamArea.Instance != null)
            CarJamArea.Instance.CarReachedPointX += HandleCarReachedPointX;
        else
            Debug.LogWarning("SpoolManager could not find CarJamArea to subscribe to.", this);
    }

    private void OnDestroy()
    {
        if (CarJamArea.Instance != null)
            CarJamArea.Instance.CarReachedPointX -= HandleCarReachedPointX;

        if (Instance == this)
            Instance = null;
    }

    /// <summary>Trả về ngẫu nhiên một slot chưa có Spool và không bị khóa.</summary>
    public SpoolSlot GetFreeSlot()
    {
        int freeCount = 0;
        foreach (SpoolSlot slot in slots)
            if (slot != null && !slot.IsOccupied)
                freeCount++;

        if (freeCount == 0)
            return null;

        int selectedIndex = UnityEngine.Random.Range(0, freeCount);
        foreach (SpoolSlot slot in slots)
        {
            if (slot == null || slot.IsOccupied)
                continue;

            if (selectedIndex-- == 0)
                return slot;
        }

        return null;
    }

    public bool HasFreeSlot() => GetFreeSlot() != null;

    /// <summary>Spawn một Spool tại slot, tự chọn prefab đúng size và material đúng màu.</summary>
    public Spool SpawnSpool(int slotIndex, SpoolSize size, SpoolColor color)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
        {
            Debug.LogWarning($"Spool slot index {slotIndex} is invalid.", this);
            return null;
        }

        return SpawnSpool(slots[slotIndex], size, color);
    }

    /// <summary>Spawn Spool vào đúng slot xe đã chọn làm đích.</summary>
    public Spool SpawnSpool(SpoolSlot slot, SpoolSize size, SpoolColor color)
    {
        // A reserved slot belongs to the car that just reached it, so it is valid to spawn there.
        if (slot == null || slot.IsLocked || slot.CurrentSpool != null)
        {
            Debug.LogWarning("Cannot spawn Spool: target slot is occupied or locked.", this);
            return null;
        }

        Spool prefab = FindPrefab(size);
        Material material = FindMaterial(color);
        if (prefab == null || material == null)
        {
            Debug.LogWarning($"Cannot spawn Spool: missing prefab for {size} or material for {color}.", this);
            return null;
        }

        Transform point = slot.SpawnPoint;
        // Do not parent to the slot: parent scale must not alter the prefab scale.
        Spool spool = Instantiate(prefab, point.position, Quaternion.identity);
        spool.Configure(size, color, material);
        slot.SetSpool(spool);
        StartCoroutine(AnimateSpoolSpawnThenProcess(spool, slot));
        return spool;
    }

    private IEnumerator AnimateSpoolSpawnThenProcess(Spool spool, SpoolSlot slot)
    {
        if (spool == null)
            yield break;

        Transform spoolTransform = spool.transform;
        Vector3 prefabScale = spoolTransform.localScale;
        spoolTransform.localScale = prefabScale * 0.8f;

        if (spoolSpawnVfxPrefab != null)
        {
            GameObject vfx = Instantiate(spoolSpawnVfxPrefab, spoolTransform.position, spoolTransform.rotation);
            if (spoolSpawnVfxLifetime > 0f)
                Destroy(vfx, spoolSpawnVfxLifetime);
        }

        float elapsed = 0f;
        while (elapsed < spoolSpawnDuration && spool != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / spoolSpawnDuration);
            spoolTransform.localScale = Vector3.LerpUnclamped(prefabScale * 0.8f, prefabScale, progress);
            yield return null;
        }

        if (spool == null)
            yield break;

        spoolTransform.localScale = prefabScale;
        yield return ProcessDragonSegments(spool, slot);
    }

    private IEnumerator ProcessDragonSegments(Spool spool, SpoolSlot slot)
    {
        DragonController dragon = DragonController.Instance;
        if (spool == null || dragon == null || yarnSystemPrefab == null)
            yield break;

        while (spool != null && spool.RemainingSize > 0)
        {
            while (dragon.IsRepacking)
                yield return null;

            float segmentDistance = slot != null
                ? slot.GetDragonSegmentDistance(dragonSegmentDistance)
                : dragonSegmentDistance;
            DragonBodySegment segment = dragon.FindAndReserveClosestBodySegment(
                spool.Color, spool.transform.position, segmentDistance);
            if (segment == null)
            {
                // Keep checking: a matching body segment may move into range later.
                yield return null;
                continue;
            }

            if (!spool.TryActivateNextYarnRoll(out SpoolYarnRollMarker yarnRoll))
            {
                segment.CancelConsume();
                Debug.LogWarning("Spool has no inactive Yarn Roll marker available.", spool);
                yield break;
            }

            Transform yarnRollTransform = yarnRoll.transform;
            Vector3 yarnRollTargetScale = yarnRollTransform.localScale;
            yarnRollTransform.localScale = yarnRollTargetScale * 0.6f;

            YarnPullAnimation yarn = Instantiate(yarnSystemPrefab);

            SetYarnColor(yarn, spool.Color);
            bool animationComplete = false;
            bool removalStarted = false;
            bool removalComplete = false;
            Vector3 spoolBaseScale = spool.transform.localScale;

            yarn.PlayTimed(yarnAnimationDuration, progress =>
            {
                if (segment != null && spool != null)
                {
                    float startForwardOffset = Mathf.Lerp(0.15f, yarnMoveWhenPulled, progress);
                    Vector3 start = segment.transform.TransformPoint(yarnStartOffset)
                        + segment.transform.forward * startForwardOffset;
                    Vector3 end = yarnRollTransform.position + new Vector3(0f, 0.7f, 0f);
                    yarn.ConfigurePoints(start, end);
                    segment.SetDisappearProgress(progress);
                    yarnRollTransform.localScale = Vector3.LerpUnclamped(
                        yarnRollTargetScale * 0.6f, yarnRollTargetScale, progress);
                    float scaleMultiplier = 1f + Mathf.Sin(progress * Mathf.PI) * yarnPullScaleAmount;
                    spool.transform.localScale = spoolBaseScale * scaleMultiplier;
                }

                if (!removalStarted && progress >= 0.5f)
                {
                    removalStarted = true;
                    StartCoroutine(RemoveSegmentAndSignal(dragon, segment,
                        yarnAnimationDuration * 0.5f, () => removalComplete = true));
                }
            }, () =>
            {
                if (spool != null)
                    spool.transform.localScale = spoolBaseScale;
                if (yarnRoll != null)
                    yarnRollTransform.localScale = yarnRollTargetScale;
                animationComplete = true;
            });

            while (!animationComplete || !removalComplete)
                yield return null;

            Destroy(yarn.gameObject);

            if (spool != null && spool.ConsumeOneSize())
            {
                PlaySpoolDepletedFeedback(spool.transform);

                // Spools are instantiated and destroyed directly; they do not use an object pool.
                if (slot != null && slot.CurrentSpool == spool)
                    slot.Clear();
                else
                    Destroy(spool.gameObject);
                yield break;
            }

            yield return null;
        }
    }

    private void PlaySpoolDepletedFeedback(Transform spoolTransform)
    {
        if (spoolTransform == null)
            return;

        if (spoolDepletedVfxPrefab != null)
        {
            GameObject vfx = Instantiate(spoolDepletedVfxPrefab, spoolTransform.position,
                spoolTransform.rotation);
            if (spoolDepletedVfxLifetime > 0f)
                Destroy(vfx, spoolDepletedVfxLifetime);
        }

        AudioManager.Instance?.Play(AudioClipId.Successed);
    }

    private static IEnumerator RemoveSegmentAndSignal(DragonController dragon,
        DragonBodySegment segment, float duration, Action onComplete)
    {
        // Body positions are resolved from the head trail in LateUpdate. Waiting here prevents
        // capturing the head from the current frame with body positions from the previous frame.
        yield return new WaitForEndOfFrame();
        yield return dragon.RemoveBodySegmentAndCloseGap(segment, duration);
        onComplete?.Invoke();
    }

    private void SetYarnColor(YarnPullAnimation yarn, SpoolColor color)
    {
        Material material = FindMaterial(color);
        if (material == null)
            return;

        Color yarnColor = material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : material.color;
        yarn.SetColor(yarnColor);
    }

    private void HandleCarReachedPointX(SpoolColor color, SpoolSize size, SpoolSlot targetSlot)
    {
        SpawnSpool(targetSlot, size, color);
    }

    public void ClearSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slots.Length && slots[slotIndex] != null)
            slots[slotIndex].Clear();
    }

    private Spool FindPrefab(SpoolSize size)
    {
        foreach (SpoolPrefabEntry entry in spoolPrefabs)
            if (entry.size == size)
                return entry.prefab;
        return null;
    }

    private Material FindMaterial(SpoolColor color)
    {
        foreach (SpoolMaterialEntry entry in colorMaterials)
            if (entry.color == color)
                return entry.material;
        return null;
    }

    /// <summary>Material của SpoolColor, dùng chung cho các hệ thống hiển thị khác như Dragon.</summary>
    public Material GetMaterial(SpoolColor color) => FindMaterial(color);
}
