using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpoolSlot))]
public sealed class UnlockSlot : MonoBehaviour, IPointerDownHandler
{
    [Header("References")]
    [SerializeField] private SpoolSlot spoolSlot;
    [SerializeField] private GameObject lockedObject;
    [SerializeField] private GameObject freeObject;

    [Header("VFX")]
    [SerializeField] private GameObject unlockVfxPrefab;
    [SerializeField] private Transform vfxSpawnPoint;
    [SerializeField, Min(0f)] private float vfxLifetime = 2f;

    private void Awake()
    {
        if (spoolSlot == null)
            spoolSlot = GetComponent<SpoolSlot>();

        RefreshVisuals();
    }

    private void OnValidate()
    {
        if (spoolSlot == null)
            spoolSlot = GetComponent<SpoolSlot>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TryUnlock();
    }

    public bool TryUnlock()
    {
        if (spoolSlot == null || !spoolSlot.TryUnlock())
            return false;

        RefreshVisuals();
        PlayUnlockVfx();
        AudioManager.Instance?.Play(AudioClipId.Unlock);
        return true;
    }

    private void RefreshVisuals()
    {
        bool isLocked = spoolSlot != null && spoolSlot.IsLocked;

        if (lockedObject != null)
            lockedObject.SetActive(isLocked);

        if (freeObject != null)
            freeObject.SetActive(!isLocked);
    }

    private void PlayUnlockVfx()
    {
        if (unlockVfxPrefab == null)
            return;

        Transform spawnPoint = vfxSpawnPoint != null ? vfxSpawnPoint : transform;
        GameObject vfx = Instantiate(unlockVfxPrefab, spawnPoint.position,
            spawnPoint.rotation);

        if (vfxLifetime > 0f)
            Destroy(vfx, vfxLifetime);
    }
}
