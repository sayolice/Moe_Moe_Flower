using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전역 사운드(BGM 및 SFX)를 총괄하는 싱글턴.
///
/// [핵심 기능]
/// 1. 외부 오디오 에셋(.wav/.mp3)이 아직 프로젝트에 없더라도, 절차적 톤 생성기(AudioClip.Create)를
///    통해 부드러운 팝/차임/멜로디 효과음을 자체 합성하여 재생합니다.
/// 2. 외부 오디오 클립이 인스펙터에 등록되면 커스텀 클립이 최우선으로 재생됩니다.
/// 3. 빠른 연타(클릭) 시 음정이 반음씩 살짝 올라가는 '피치 사다리(Pitch Ladder)' 연출을 적용하여,
///    단조로운 클릭음이 아닌 경쾌하고 리듬감 있는 타격감을 제공합니다.
/// 4. SFX 오디오 소스 풀링(6채널)으로 빠른 터치 시 소리가 끊기거나 버벅이지 않습니다.
/// 5. BGM / SFX 볼륨 및 음소거 설정이 PlayerPrefs에 영구 저장됩니다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SoundManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SoundManager");
                    instance = go.AddComponent<SoundManager>();
                }
            }
            return instance;
        }
    }

    [Header("커스텀 오디오 클립 (비워두면 내장 절차적 사운드 자동 생성)")]
    public AudioClip customTapClip;
    public AudioClip customBloomClip;
    public AudioClip customLevelUpClip;
    public AudioClip customButtonClickClip;
    public AudioClip customBgmClip;

    [Header("볼륨 설정 (0.0 ~ 1.0)")]
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    public bool isBgmMuted = false;
    public bool isSfxMuted = false;

    // 내부 오디오 컴포넌트
    private AudioSource bgmSource;
    private AudioSource[] sfxSources;
    private int nextSfxIndex = 0;
    private const int SfxChannelCount = 6;

    // 절차적 합성 사운드 캐시
    private AudioClip generatedTapClip;
    private AudioClip generatedGoldClip;
    private AudioClip generatedBloomClip;
    private AudioClip generatedLevelUpClip;
    private AudioClip generatedButtonClickClip;
    private AudioClip generatedBgmClip;

    // 연타 피치 모듈레이션
    private float lastTapTime = -1f;
    private int consecutiveTapCount = 0;
    private const float TapComboWindow = 0.45f;
    private const int MaxPitchSteps = 8;

    private const string PrefsKeyBgmVolume = "Sound_BgmVolume";
    private const string PrefsKeySfxVolume = "Sound_SfxVolume";
    private const string PrefsKeyBgmMute   = "Sound_BgmMuted";
    private const string PrefsKeySfxMute   = "Sound_SfxMuted";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        InitializeAudioSources();
        GenerateProceduralClips();
    }

    private void Start()
    {
        // FlowerManager 이벤트 구독
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
            FlowerManager.Instance.OnBondLevelUp += HandleBondLevelUp;
        }

        // BGM 자동 시작 (커스텀 BGM이 없으면 내장 힐링 BGM 재생)
        AudioClip bgmToPlay = customBgmClip != null ? customBgmClip : generatedBgmClip;
        if (bgmToPlay != null)
        {
            PlayBGM(bgmToPlay);
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
            FlowerManager.Instance.OnBondLevelUp -= HandleBondLevelUp;
        }
    }

    private void InitializeAudioSources()
    {
        // BGM 소스
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = isBgmMuted ? 0f : bgmVolume;

        // SFX 풀링 소스
        sfxSources = new AudioSource[SfxChannelCount];
        for (int i = 0; i < SfxChannelCount; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.loop = false;
            src.playOnAwake = false;
            src.volume = isSfxMuted ? 0f : sfxVolume;
            sfxSources[i] = src;
        }
    }

    // ===================================================================
    // 공개 SFX 재생 API
    // ===================================================================

    /// <summary>
    /// 꽃 터치 효과음. 연속 터치 시 피치가 퐁-퐁-퐁 경쾌하게 올라갑니다.
    /// </summary>
    public void PlayTapSound(bool isBloomed = false)
    {
        if (isSfxMuted || sfxVolume <= 0f) return;

        float now = Time.unscaledTime;
        if (now - lastTapTime < TapComboWindow)
            consecutiveTapCount = Mathf.Min(consecutiveTapCount + 1, MaxPitchSteps);
        else
            consecutiveTapCount = 0;
        lastTapTime = now;

        // 피치 계단: 1.0부터 반음 단위(2^(n/12))로 살짝 상승
        float pitch = Mathf.Pow(1.05946f, consecutiveTapCount);
        if (isBloomed) pitch *= 1.15f; // 개화 꽃은 조금 더 밝은 톤

        AudioClip clip = customTapClip != null ? customTapClip : generatedTapClip;
        PlaySFXWithPitch(clip, pitch, 0.85f);
    }

    /// <summary>
    /// 골드 획득 시 재생되는 짤랑하는 동전 소리
    /// </summary>
    public void PlayGoldSound()
    {
        if (isSfxMuted || sfxVolume <= 0f) return;
        PlaySFXWithPitch(generatedGoldClip, UnityEngine.Random.Range(0.96f, 1.04f), 0.7f);
    }

    /// <summary>
    /// 개화 성공 시 축하 아르페지오/팡파르
    /// </summary>
    public void PlayBloomSound()
    {
        AudioClip clip = customBloomClip != null ? customBloomClip : generatedBloomClip;
        PlaySFXWithPitch(clip, 1.0f, 1.0f);
    }

    /// <summary>
    /// 레벨업/유대업 효과음
    /// </summary>
    public void PlayLevelUpSound()
    {
        AudioClip clip = customLevelUpClip != null ? customLevelUpClip : generatedLevelUpClip;
        PlaySFXWithPitch(clip, 1.0f, 0.95f);
    }

    /// <summary>
    /// 일반 버튼 클릭음
    /// </summary>
    public void PlayButtonClick()
    {
        AudioClip clip = customButtonClickClip != null ? customButtonClickClip : generatedButtonClickClip;
        PlaySFXWithPitch(clip, 1.0f, 0.6f);
    }

    private void HandleFlowerBloomed(string flowerId) => PlayBloomSound();
    private void HandleBondLevelUp(string flowerId, int level) => PlayLevelUpSound();

    private void PlaySFXWithPitch(AudioClip clip, float pitch, float volumeScale = 1.0f)
    {
        if (clip == null || isSfxMuted || sfxVolume <= 0f || sfxSources == null) return;

        AudioSource src = sfxSources[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % SfxChannelCount;

        src.pitch = pitch;
        src.volume = sfxVolume * volumeScale;
        src.PlayOneShot(clip);
    }

    // ===================================================================
    // BGM 제어 (스택 지원: 메모리얼/이벤트 등으로 BGM 전환 및 복귀 지원)
    // ===================================================================

    private struct BgmState
    {
        public AudioClip clip;
        public float time;
    }
    private readonly Stack<BgmState> bgmStack = new Stack<BgmState>();

    public void PlayBGM(AudioClip clip)
    {
        if (bgmSource == null || clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.volume = isBgmMuted ? 0f : bgmVolume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    public void PauseBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
            bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        if (bgmSource != null && bgmSource.clip != null)
            bgmSource.UnPause();
    }

    /// <summary>
    /// 현재 BGM 상태를 스택에 보관하고 새 BGM을 재생하거나 끕니다.
    /// newClip이 null이면 배경음을 끕니다(메모리얼 진입 등).
    /// </summary>
    public void PushBGM(AudioClip newClip = null)
    {
        if (bgmSource != null && bgmSource.clip != null)
        {
            bgmStack.Push(new BgmState
            {
                clip = bgmSource.clip,
                time = bgmSource.isPlaying ? bgmSource.time : 0f
            });
        }

        if (newClip != null)
        {
            PlayBGM(newClip);
        }
        else
        {
            if (bgmSource != null) bgmSource.Stop();
        }
    }

    /// <summary>
    /// 보관해 둔 이전 BGM으로 복원하여 재생을 재개합니다(메모리얼 퇴장 등).
    /// </summary>
    public void PopBGM()
    {
        if (bgmStack.Count > 0)
        {
            BgmState prev = bgmStack.Pop();
            if (prev.clip != null && bgmSource != null)
            {
                bgmSource.clip = prev.clip;
                bgmSource.volume = isBgmMuted ? 0f : bgmVolume;
                bgmSource.time = Mathf.Clamp(prev.time, 0f, Mathf.Max(0f, prev.clip.length - 0.1f));
                bgmSource.Play();
            }
        }
        else
        {
            // 스택이 비어있으면 기본 합성 BGM 재생
            if (generatedBgmClip != null && (bgmSource == null || !bgmSource.isPlaying))
                PlayBGM(generatedBgmClip);
        }
    }

    // ===================================================================
    // 설정 및 볼륨 관리 (SettingsPanel 연동)
    // ===================================================================

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null) bgmSource.volume = isBgmMuted ? 0f : bgmVolume;
        PlayerPrefs.SetFloat(PrefsKeyBgmVolume, bgmVolume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSources != null)
        {
            foreach (var src in sfxSources)
                if (src != null) src.volume = isSfxMuted ? 0f : sfxVolume;
        }
        PlayerPrefs.SetFloat(PrefsKeySfxVolume, sfxVolume);
    }

    public void SetBGMMuted(bool muted)
    {
        isBgmMuted = muted;
        if (bgmSource != null) bgmSource.volume = isBgmMuted ? 0f : bgmVolume;
        PlayerPrefs.SetInt(PrefsKeyBgmMute, isBgmMuted ? 1 : 0);
    }

    public void SetSFXMuted(bool muted)
    {
        isSfxMuted = muted;
        if (sfxSources != null)
        {
            foreach (var src in sfxSources)
                if (src != null) src.volume = isSfxMuted ? 0f : sfxVolume;
        }
        PlayerPrefs.SetInt(PrefsKeySfxMute, isSfxMuted ? 1 : 0);
    }

    private void LoadSettings()
    {
        bgmVolume = PlayerPrefs.GetFloat(PrefsKeyBgmVolume, 0.5f);
        sfxVolume = PlayerPrefs.GetFloat(PrefsKeySfxVolume, 0.7f);
        isBgmMuted = PlayerPrefs.GetInt(PrefsKeyBgmMute, 0) == 1;
        isSfxMuted = PlayerPrefs.GetInt(PrefsKeySfxMute, 0) == 1;
    }

    // ===================================================================
    // 절차적 사운드 생성 (Procedural Audio Synthesis)
    // 에셋 파일이 없어도 수학 공식으로 귀엽고 부드러운 톤을 실시간 합성합니다.
    // ===================================================================

    private void GenerateProceduralClips()
    {
        generatedTapClip = CreateToneClip("Proc_Tap", 0.07f, t =>
        {
            // 550Hz 기본음 + 부드러운 지수 감쇠 (비눗방울 톡 터지는 듯한 소리)
            float freq = Mathf.Lerp(620f, 480f, t);
            float env = Mathf.Exp(-t * 35f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
        });

        generatedGoldClip = CreateToneClip("Proc_Gold", 0.10f, t =>
        {
            // 1500Hz 벨 하모닉스
            float env = Mathf.Exp(-t * 28f);
            float wave = 0.7f * Mathf.Sin(2f * Mathf.PI * 1480f * t) +
                         0.3f * Mathf.Sin(2f * Mathf.PI * 2960f * t);
            return wave * env;
        });

        generatedButtonClickClip = CreateToneClip("Proc_Click", 0.04f, t =>
        {
            // 깔끔한 UI 클릭 펄스
            float env = Mathf.Exp(-t * 80f);
            return Mathf.Sin(2f * Mathf.PI * 800f * t) * env;
        });

        generatedLevelUpClip = CreateToneClip("Proc_LevelUp", 0.35f, t =>
        {
            // 상승 3화음 (C5 -> E5 -> G5)
            float freq = t < 0.1f ? 523.25f : (t < 0.2f ? 659.25f : 783.99f);
            float env = Mathf.Exp(-(t % 0.1f) * 12f) * (1f - t / 0.35f);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
        });

        generatedBloomClip = CreateToneClip("Proc_Bloom", 0.65f, t =>
        {
            // 찬란한 5음 상승 아르페지오 (C5 -> E5 -> G5 -> B5 -> C6)
            float[] notes = { 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f };
            int idx = Mathf.Min((int)(t / 0.11f), notes.Length - 1);
            float noteTime = t - (idx * 0.11f);
            float env = Mathf.Exp(-noteTime * 10f);
            float shimmer = 0.2f * Mathf.Sin(2f * Mathf.PI * notes[idx] * 2f * t);
            return (Mathf.Sin(2f * Mathf.PI * notes[idx] * t) + shimmer) * env;
        });

        // 잔잔하고 따뜻한 정원 앰비언트 BGM 루프 합성
        generatedBgmClip = GenerateBgmClip();
    }

    private AudioClip GenerateBgmClip()
    {
        const int sampleRate = 22050;
        const float duration = 12.0f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // 4개 마디 코드 구성음 (주파수 Hz)
        float[][] chords = {
            new float[] { 261.63f, 329.63f, 392.00f, 493.88f }, // Cmaj7 (C4, E4, G4, B4)
            new float[] { 246.94f, 293.66f, 392.00f, 587.33f }, // G/B (B3, D4, G4, D5)
            new float[] { 220.00f, 261.63f, 329.63f, 392.00f }, // Am7 (A3, C4, E4, G4)
            new float[] { 174.61f, 220.00f, 261.63f, 329.63f }  // Fmaj7 (F3, A3, C4, E4)
        };

        // 아르페지오 멜로디 시퀀스
        float[] melodyNotes = {
            523.25f, 659.25f, 783.99f, 659.25f, // C - E - G - E
            587.33f, 493.88f, 392.00f, 493.88f, // D - B - G - B
            440.00f, 523.25f, 659.25f, 523.25f, // A - C - E - C
            349.23f, 440.00f, 523.25f, 440.00f  // F - A - C - A
        };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;

            // 1. 코드 패드 (부드러운 잔향)
            int chordIdx = Mathf.Clamp((int)(t / 3.0f), 0, 3);
            float chordTime = t % 3.0f;
            float chordEnv = Mathf.Clamp01(Mathf.Sin(chordTime / 3.0f * Mathf.PI)) * 0.15f;

            float chordSample = 0f;
            float[] currentChord = chords[chordIdx];
            for (int c = 0; c < currentChord.Length; c++)
            {
                chordSample += Mathf.Sin(2f * Mathf.PI * currentChord[c] * t);
            }
            chordSample *= (chordEnv / currentChord.Length);

            // 2. 맑은 아르페지오 멜로디 (0.75초 간격)
            int melodyIdx = Mathf.Clamp((int)(t / 0.75f), 0, melodyNotes.Length - 1);
            float noteTime = t % 0.75f;
            float noteEnv = Mathf.Exp(-noteTime * 4.5f) * 0.18f;
            float melodySample = (Mathf.Sin(2f * Mathf.PI * melodyNotes[melodyIdx] * t)
                               + 0.3f * Mathf.Sin(4f * Mathf.PI * melodyNotes[melodyIdx] * t)) * noteEnv;

            // 전체 믹싱 & 클램핑
            samples[i] = Mathf.Clamp(chordSample + melodySample, -0.85f, 0.85f);
        }

        AudioClip clip = AudioClip.Create("Proc_Garden_BGM", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateToneClip(string clipName, float duration, Func<float, float> waveFunc)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            samples[i] = Mathf.Clamp(waveFunc(t), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
