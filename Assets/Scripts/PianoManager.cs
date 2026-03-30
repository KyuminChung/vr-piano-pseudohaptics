using UnityEngine;

public class PianoManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FingerStateStore fingerStore;
    [SerializeField] private PianoSettings settings;
    [SerializeField] private PianoLayoutConfig layoutConfig;
    [SerializeField] private AudioSource pianoAudioSource;   // 하나만 사용

    private void Update()
    {
        if (fingerStore == null || settings == null || layoutConfig == null || layoutConfig.keys == null)
            return;

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

            if (bestKeyIndex < 0 || bestDepth < settings.pressDepth)
                continue;

            AudioClip clip = layoutConfig.keys[bestKeyIndex].noteClip;

            if (pianoAudioSource != null && clip != null)
            {
                pianoAudioSource.PlayOneShot(clip, 1f);
                Debug.Log($"PLAY {layoutConfig.keys[bestKeyIndex].keyName}");
            }
        }
    }
}