using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// BGM・SE の一元管理。クロスフェード、3D空間音響に対応。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource1;
    [SerializeField] private AudioSource bgmSource2;
    [SerializeField] private float bgmFadeDuration = 1.5f;

    [Header("SE Pool")]
    [SerializeField] private int sePoolSize = 16;
    [SerializeField] private AudioSource seSourcePrefab;

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume    = 0.6f;
    [Range(0f, 1f)] public float seVolume     = 1f;

    [Header("Audio Clips")]
    public AudioClip bgmExplore;
    public AudioClip bgmBoss;
    public AudioClip bgmVictory;
    public AudioClip bgmGameOver;

    public AudioClip seLightAttack;
    public AudioClip seHeavyAttack;
    public AudioClip seHit;
    public AudioClip seDodge;
    public AudioClip seSkill;
    public AudioClip sePlayerDamage;
    public AudioClip sePlayerDeath;
    public AudioClip seDragonRoar;
    public AudioClip seDragonFireBreath;
    public AudioClip seItemPickup;
    public AudioClip seChestOpen;
    public AudioClip seUIClick;
    public AudioClip seLevelUp;

    private AudioSource _activeBgmSource;
    private List<AudioSource> _sePool = new List<AudioSource>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource1 == null) bgmSource1 = CreateBgmSource("BGM_1");
        if (bgmSource2 == null) bgmSource2 = CreateBgmSource("BGM_2");

        bgmSource2.volume = 0f;
        _activeBgmSource  = bgmSource1;

        InitSEPool();
    }

    private AudioSource CreateBgmSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.loop         = true;
        src.playOnAwake  = false;
        src.spatialBlend = 0f;
        return src;
    }

    private void InitSEPool()
    {
        for (int i = 0; i < sePoolSize; i++)
        {
            var go  = new GameObject($"SE_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f;
            src.volume       = seVolume * masterVolume;
            _sePool.Add(src);
        }
    }

    // ============================================================
    // BGM
    // ============================================================
    public void PlayBGM(AudioClip clip, bool forceRestart = false)
    {
        if (clip == null) return;
        if (!forceRestart && _activeBgmSource.clip == clip && _activeBgmSource.isPlaying) return;
        StartCoroutine(CrossFadeBGM(clip));
    }

    private IEnumerator CrossFadeBGM(AudioClip newClip)
    {
        var fadeIn  = (_activeBgmSource == bgmSource1) ? bgmSource2 : bgmSource1;
        var fadeOut = _activeBgmSource;

        fadeIn.clip   = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        float timer = 0f;
        float targetVol = bgmVolume * masterVolume;

        while (timer < bgmFadeDuration)
        {
            timer         += Time.unscaledDeltaTime;
            float t        = timer / bgmFadeDuration;
            fadeIn.volume  = Mathf.Lerp(0f, targetVol, t);
            fadeOut.volume = Mathf.Lerp(targetVol, 0f, t);
            yield return null;
        }

        fadeOut.Stop();
        fadeOut.clip   = null;
        fadeOut.volume = 0f;
        _activeBgmSource = fadeIn;
    }

    public void StopBGM() => StartCoroutine(FadeOutBGM());

    private IEnumerator FadeOutBGM()
    {
        float start = _activeBgmSource.volume;
        float timer = 0f;
        while (timer < bgmFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            _activeBgmSource.volume = Mathf.Lerp(start, 0f, timer / bgmFadeDuration);
            yield return null;
        }
        _activeBgmSource.Stop();
    }

    // ============================================================
    // SE
    // ============================================================
    public void PlaySE(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        var src = GetFreeSESource();
        if (src == null) return;
        src.spatialBlend = 0f;
        src.volume       = seVolume * masterVolume * volumeScale;
        src.clip         = clip;
        src.Play();
    }

    public void PlaySE3D(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;
        var src = GetFreeSESource();
        if (src == null) return;
        src.transform.position = position;
        src.spatialBlend       = 1f;
        src.minDistance        = 3f;
        src.maxDistance        = 30f;
        src.volume             = seVolume * masterVolume * volumeScale;
        src.clip               = clip;
        src.Play();
    }

    private AudioSource GetFreeSESource()
    {
        foreach (var src in _sePool)
            if (!src.isPlaying) return src;
        return _sePool[0]; // 最も古いものを再利用
    }

    // --- 便利メソッド ---
    public void PlayLightAttack()   => PlaySE(seLightAttack);
    public void PlayHeavyAttack()   => PlaySE(seHeavyAttack);
    public void PlayHit()           => PlaySE(seHit, 0.8f);
    public void PlayDodge()         => PlaySE(seDodge, 0.7f);
    public void PlaySkill()         => PlaySE(seSkill);
    public void PlayPlayerDamage()  => PlaySE(sePlayerDamage);
    public void PlayPlayerDeath()   => PlaySE(sePlayerDeath);
    public void PlayDragonRoar()    => PlaySE(seDragonRoar, 1.2f);
    public void PlayFireBreath(Vector3 pos) => PlaySE3D(seDragonFireBreath, pos);
    public void PlayItemPickup()    => PlaySE(seItemPickup, 0.6f);
    public void PlayChestOpen()     => PlaySE(seChestOpen);
    public void PlayUIClick()       => PlaySE(seUIClick, 0.5f);
    public void PlayLevelUp()       => PlaySE(seLevelUp);

    // ============================================================
    // Volume Control
    // ============================================================
    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        ApplyVolumes();
    }

    public void SetBGMVolume(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        ApplyVolumes();
    }

    public void SetSEVolume(float v)
    {
        seVolume = Mathf.Clamp01(v);
    }

    private void ApplyVolumes()
    {
        _activeBgmSource.volume = bgmVolume * masterVolume;
    }
}
