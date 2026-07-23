using UnityEngine;
using System.Collections;


#if UNITY_EDITOR
using UnityEditor;
#endif

public enum AudioClipId
{
    CarGo,
    CarHit,
    GameOver,
    GameStart,
    GameWin,
    Pull,
    Unlock,
    Successed,
    CatMeow,
    BubblePop
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Music")]
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.25f;

    [Header("Clips")]
    [SerializeField] private AudioClip carGoClip;
    [SerializeField] private AudioClip carHitClip;
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] private AudioClip gameStartClip;
    [SerializeField] private AudioClip gameWinClip;
    [SerializeField] private AudioClip pullClip;
    [SerializeField] private AudioClip unlockClip;
    [SerializeField] private AudioClip successedClip;
    [SerializeField] private AudioClip catMeowClip;
    [SerializeField] private AudioClip BubblePopClip;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;

        if (musicSource == null || musicSource == sfxSource)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
    }

    private void Start()
    {
        LevelManager.Instance.LevelEnded += OnLevelEnded;
        PlayMusic();
    }

    private void OnLevelEnded(LevelResult result)
    {
        Play(result == LevelResult.Win ? AudioClipId.GameWin : AudioClipId.GameOver);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Play(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayPull()
    {
        Play(AudioClipId.Pull);
    }

    public void PlayMusic()
    {
        if (music == null || musicSource == null)
            return;

        musicSource.clip = music;
        musicSource.volume = musicVolume;

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    public void Play(AudioClipId clipId, float volumeScale = 1f)
    {
        Play(GetClip(clipId), volumeScale);
    }

    private AudioClip GetClip(AudioClipId clipId)
    {
        return clipId switch
        {
            AudioClipId.CarGo => carGoClip,
            AudioClipId.CarHit => carHitClip,
            AudioClipId.GameOver => gameOverClip,
            AudioClipId.GameStart => gameStartClip,
            AudioClipId.GameWin => gameWinClip,
            AudioClipId.Pull => pullClip,
            AudioClipId.Unlock => unlockClip,
            AudioClipId.Successed => successedClip,
            AudioClipId.CatMeow => catMeowClip,
            AudioClipId.BubblePop => BubblePopClip,
            _ => null
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        music = LoadClipIfMissing(music,
            "Assets/Sounds/Music/Conspiracy Theory - Rod Kim.mp3");

        carGoClip = LoadClipIfMissing(carGoClip, "Assets/Sounds/car_go.wav");
        carHitClip = LoadClipIfMissing(carHitClip, "Assets/Sounds/car_hit.mp3");
        gameOverClip = LoadClipIfMissing(gameOverClip, "Assets/Sounds/game_over.mp3");
        gameStartClip = LoadClipIfMissing(gameStartClip, "Assets/Sounds/game_start.mp3");
        gameWinClip = LoadClipIfMissing(gameWinClip, "Assets/Sounds/game_win.mp3");
        pullClip = LoadClipIfMissing(pullClip, "Assets/Sounds/pull.mp3");
        unlockClip = LoadClipIfMissing(unlockClip, "Assets/Sounds/unlock.mp3");
        successedClip = LoadClipIfMissing(successedClip, "Assets/Sounds/successed.mp3");
        catMeowClip = LoadClipIfMissing(catMeowClip, "Assets/Sounds/cat_meow.mp3");
        catMeowClip = LoadClipIfMissing(catMeowClip, "Assets/Sounds/bubble_pop.mp3"); 
    }

    private static AudioClip LoadClipIfMissing(AudioClip currentClip, string assetPath)
    {
        return currentClip != null
            ? currentClip
            : AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
    }
#endif
}
