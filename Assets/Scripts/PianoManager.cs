using UnityEngine;

public class PianoManager : MonoBehaviour
{
    [System.Serializable]
    private class KeyRuntime
    {
        public bool isPressed;
        public int ownerFingerIndex = -1;

        public int releaseCandidateFrames = 0;
        public float lastPressTime = -999f;
    }

    [Header("References")]
    [SerializeField] private FingerStateStore fingerStore;
    [SerializeField] private PianoSettings settings;
    [SerializeField] private PianoLayoutConfig layoutConfig;
    [SerializeField] private AudioSource pianoAudioSource;

    private KeyRuntime[] runtimes;

    private void Awake()
    {
        InitializeRuntimes();
        
        if(pianoAudioSource != null)
        {
            pianoAudioSource.playOnAwake = false;
            pianoAudioSource.loop = false;
            pianoAudioSource.spatialBlend = 0f;
            pianoAudioSource.volume = 1f;
        }
    }

    private void InitializeRuntimes()
    {
        if (layoutConfig == null || layoutConfig.keys == null)
            return;

        runtimes = new KeyRuntime[layoutConfig.keys.Count];

        for (int i = 0; i < runtimes.Length; i++)
        {
            runtimes[i] = new KeyRuntime
            {
                isPressed = false,
                ownerFingerIndex = -1  
            };
        }
    }

    private void Update()
    {
        if (fingerStore == null || settings == null || layoutConfig == null || layoutConfig.keys == null)
            return;

        if (runtimes == null || runtimes.Length != layoutConfig.keys.Count)
            InitializeRuntimes();

        HandleRelease();
        HandlePress();
    }

    private void HandlePress()
    {
        FingerState[] fingers = fingerStore.GetAllFingers();

        for (int fingerIndex = 0; fingerIndex < fingers.Length; fingerIndex++)
        {
            FingerState finger = fingers[fingerIndex];
            if (!finger.isTracked)
                continue;

            int bestKeyIndex = -1;
            float bestDepth = -1f;

            for (int keyIndex = 0; keyIndex < layoutConfig.keys.Count; keyIndex++)
            {
                PianoKeyData key = layoutConfig.keys[keyIndex];

                bool inside =
                    finger.localPos.x >= (key.minX - settings.marginX) &&
                    finger.localPos.x <= (key.maxX + settings.marginX) &&
                    finger.localPos.z >= (key.minZ - settings.marginZ) &&
                    finger.localPos.z <= (key.maxZ + settings.marginZ);

                if (!inside)
                    continue;

                float depth = Mathf.Clamp(key.topY - finger.localPos.y, 0f, settings.maxDepth);

                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    bestKeyIndex = keyIndex;
                }
            }

            if (bestKeyIndex < 0)
                continue;

            if (bestDepth < settings.pressDepth)
                continue;

            KeyRuntime runtime = runtimes[bestKeyIndex];

            // 이미 다른 손가락이 점유 중이면 무시
            if (runtime.isPressed && runtime.ownerFingerIndex != fingerIndex)
                continue;

            // 같은 손가락이 이미 누르고 있는 건반이면 유지
            if (runtime.isPressed && runtime.ownerFingerIndex == fingerIndex)
            {
                runtime.releaseCandidateFrames = 0;
                continue;
            }

            // release 직후 즉시 재트리거 방지
            if (Time.time - runtime.lastPressTime < settings.repressCooldown)
                continue;
            // 새로 press
            runtime.isPressed = true;
            runtime.ownerFingerIndex = fingerIndex;
            runtime.releaseCandidateFrames = 0;
            runtime.lastPressTime = Time.time;
            finger.currentKeyIndex = bestKeyIndex;

            PlayKey(bestKeyIndex);
            float pressVelocity = Mathf.Max(0f, -finger.velocityWorld.y);
            //float t = Mathf.InverseLerp(settings.minVelocityForSound, settings.maxVelocityForSound, pressVelocity);
            //float volume = Mathf.Lerp(settings.minVolume, settings.maxVolume, t);

            // AudioClip clip = layoutConfig.keys[bestKeyIndex].noteClip;
            // if (runtime.audioSource != null && clip != null)
            // {
            //     runtime.audioSource.PlayOneShot(clip, volume);
            // }

            if (settings.showDebugLog)
            {
                Debug.Log($"PRESS key={layoutConfig.keys[bestKeyIndex].keyName}, finger={(FingerId)fingerIndex}, depth={bestDepth:F4}, vel={pressVelocity:F3}");
            }
        }
    }

    private void HandleRelease()
    {
        FingerState[] fingers = fingerStore.GetAllFingers();

        for (int keyIndex = 0; keyIndex < runtimes.Length; keyIndex++)
        {
            KeyRuntime runtime = runtimes[keyIndex];
            if (!runtime.isPressed)
                continue;

            int ownerIndex = runtime.ownerFingerIndex;
            if (ownerIndex < 0 || ownerIndex >= fingers.Length)
            {
                ReleaseKey(keyIndex);
                continue;
            }

            FingerState ownerFinger = fingers[ownerIndex];
            PianoKeyData key = layoutConfig.keys[keyIndex];

            if (!ownerFinger.isTracked)
            {
                ownerFinger.currentKeyIndex = -1;
                ReleaseKey(keyIndex);
                continue;
            }

            // bool inside =
            //     ownerFinger.localPos.x >= (key.minX - settings.marginX) &&
            //     ownerFinger.localPos.x <= (key.maxX + settings.marginX) &&
            //     ownerFinger.localPos.z >= (key.minZ - settings.marginZ) &&
            //     ownerFinger.localPos.z <= (key.maxZ + settings.marginZ);
            bool insideHold =
                ownerFinger.localPos.x >= (key.minX - settings.holdMarginX) &&
                ownerFinger.localPos.x <= (key.maxX + settings.holdMarginX) &&
                ownerFinger.localPos.z >= (key.minZ - settings.holdMarginZ) &&
                ownerFinger.localPos.z <= (key.maxZ + settings.holdMarginZ);

            float depth = Mathf.Clamp(key.topY - ownerFinger.localPos.y, 0f, settings.maxDepth);
            bool shouldRelease = !insideHold || depth <= settings.releaseDepth;
            //if (!inside || depth <= settings.releaseDepth)
            // {
            //     ownerFinger.currentKeyIndex = -1;

            //     if (settings.showDebugLog)
            //     {
            //         Debug.Log($"RELEASE key={key.keyName}, finger={(FingerId)ownerIndex}");
            //     }

            //     ReleaseKey(keyIndex);
            // }
            if (shouldRelease)
                runtime.releaseCandidateFrames++;
            else
                runtime.releaseCandidateFrames = 0;

            // 1~2프레임 튐은 무시
            if (runtime.releaseCandidateFrames < settings.releaseFramesRequired)
                continue;

            ownerFinger.currentKeyIndex = -1;

            if (settings.showDebugLog)
            {
                Debug.Log($"RELEASE key={key.keyName}, finger={(FingerId)ownerIndex}");
            }

            ReleaseKey(keyIndex);
        }
    }

    private void PlayKey(int keyIndex)
    {
        if (pianoAudioSource == null)
        {
            Debug.LogWarning("pianoAudioSource가 비어있습니다.");
            return;
        }
        AudioClip clip = layoutConfig.keys[keyIndex].noteClip;
        if (clip == null)
        {
            return;
        }
        pianoAudioSource.PlayOneShot(clip, 1f);
    }
    private void ReleaseKey(int keyIndex)
    {
        runtimes[keyIndex].isPressed = false;
        runtimes[keyIndex].ownerFingerIndex = -1;
        runtimes[keyIndex].releaseCandidateFrames = 0;
    }
}