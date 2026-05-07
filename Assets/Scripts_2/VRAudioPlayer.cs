using UnityEngine;
using System;
using System.Collections;
using System.Linq;

public class VRAudioPlayer : MonoBehaviour
{
    [Header("Audio Clips")]
    public bool loadFromResources = true;
    public string resourcesFolder = "Piano Audio";

    [Tooltip("0=A0, 1=A#0, 2=B0, 3=C1 ...")]
    public AudioClip[] clips = new AudioClip[88];

    [Header("Playback")]
    [Range(0f, 1f)]
    public float minVolume = 0.05f;

    [Range(0f, 1f)]
    public float maxVolume = 1.0f;

    [Tooltip("1보다 작으면 약한 입력도 조금 더 크게 들림")]
    public float velocityCurve = 1.8f;

    [Header("Release")]
    [Tooltip("건반을 뗐을 때 바로 Stop하지 않고 이 시간 동안 볼륨을 줄임")]
    public float releaseFadeTime = 0.12f;

    private AudioSource[] keySources = new AudioSource[88];
    private Coroutine[] fadeCoroutines = new Coroutine[88];

    private void Awake()
    {
        if (loadFromResources)
        {
            LoadClipsFromResources();
        }

        CreateKeyAudioSources();
    }

    private void LoadClipsFromResources()
    {
        AudioClip[] loaded = Resources.LoadAll<AudioClip>(resourcesFolder);

        if (loaded == null || loaded.Length == 0)
        {
            Debug.LogError($"[VRAudioPlayer] Resources/{resourcesFolder} 에서 AudioClip을 찾지 못함");
            return;
        }

        loaded = loaded
            .OrderBy(c => c.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        clips = new AudioClip[88];

        int count = Mathf.Min(88, loaded.Length);

        for (int i = 0; i < count; i++)
        {
            clips[i] = loaded[i];
        }

        Debug.Log($"[VRAudioPlayer] AudioClip 로드 완료: {count}/88");
    }

    private void CreateKeyAudioSources()
    {
        for (int i = 0; i < 88; i++)
        {
            GameObject sourceObj = new GameObject($"KeyAudioSource_{i:00}");
            sourceObj.transform.SetParent(transform);

            AudioSource source = sourceObj.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;

            keySources[i] = source;
        }

        Debug.Log("[VRAudioPlayer] 건반별 AudioSource 88개 생성 완료");
    }

    public void Play(int keyIdx, float velocity)
    {
        if (keyIdx < 0 || keyIdx >= 88)
        {
            Debug.LogWarning($"[VRAudioPlayer] 잘못된 keyIdx: {keyIdx}");
            return;
        }

        if (clips == null || clips.Length < 88)
        {
            Debug.LogWarning("[VRAudioPlayer] clips 배열이 준비되지 않음");
            return;
        }

        AudioClip clip = clips[keyIdx];

        if (clip == null)
        {
            Debug.LogWarning($"[VRAudioPlayer] clip 없음 keyIdx={keyIdx}");
            return;
        }

        AudioSource source = keySources[keyIdx];

        if (source == null)
        {
            Debug.LogWarning($"[VRAudioPlayer] AudioSource 없음 keyIdx={keyIdx}");
            return;
        }

        // 이전 release fade가 진행 중이면 중단
        if (fadeCoroutines[keyIdx] != null)
        {
            StopCoroutine(fadeCoroutines[keyIdx]);
            fadeCoroutines[keyIdx] = null;
        }

        float v = Mathf.Clamp01(velocity);
        v = Mathf.Pow(v, velocityCurve);

        float volume = Mathf.Lerp(minVolume, maxVolume, v);

        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.time = 0f;
        source.Play();

        Debug.Log($"[VRAudioPlayer] Play key={keyIdx} clip={clip.name} volume={volume:F2}");
    }

    public void Stop(int keyIdx)
    {
        if (keyIdx < 0 || keyIdx >= 88)
        {
            Debug.LogWarning($"[VRAudioPlayer] 잘못된 keyIdx Stop: {keyIdx}");
            return;
        }

        AudioSource source = keySources[keyIdx];

        if (source == null) return;
        if (!source.isPlaying) return;

        if (fadeCoroutines[keyIdx] != null)
        {
            StopCoroutine(fadeCoroutines[keyIdx]);
        }

        fadeCoroutines[keyIdx] = StartCoroutine(FadeOutAndStop(keyIdx, source));
    }

    private IEnumerator FadeOutAndStop(int keyIdx, AudioSource source)
    {
        float startVolume = source.volume;
        float elapsed = 0f;

        if (releaseFadeTime <= 0f)
        {
            source.Stop();
            source.volume = startVolume;
            fadeCoroutines[keyIdx] = null;
            yield break;
        }

        while (elapsed < releaseFadeTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / releaseFadeTime);
            source.volume = Mathf.Lerp(startVolume, 0f, t);

            yield return null;
        }

        source.Stop();
        source.volume = startVolume;

        fadeCoroutines[keyIdx] = null;

        Debug.Log($"[VRAudioPlayer] FadeStop key={keyIdx}");
    }
}