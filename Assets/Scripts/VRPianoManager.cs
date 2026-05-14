using UnityEngine;

public class VRPianoManager : MonoBehaviour
{
    [Header("References")]
    public VRLayoutInitializer layoutInitializer;
    public VRHingeAnimator hingeAnimator;
    public VRAudioPlayer audioPlayer;

    private VRKeyData[] _keyData;
    private VRKeyState[] _keyState;

    public VRKeyData[] KeyData => _keyData;
    public VRKeyState[] KeyState => _keyState;

    private bool _initialized = false;

    private void Start()
    {
        if (layoutInitializer != null)
        {
            Initialize(layoutInitializer);
        }
    }

    public void Initialize(VRLayoutInitializer initializer)
    {
        layoutInitializer = initializer;

        if (layoutInitializer == null)
        {
            Debug.LogError("[VRPianoManager] Initialize 실패: layoutInitializer 없음");
            return;
        }

        _keyData = layoutInitializer.KeyData;

        if (_keyData == null)
        {
            Debug.LogError("[VRPianoManager] Initialize 실패: KeyData 없음");
            return;
        }

        _keyState = new VRKeyState[88];

        for (int i = 0; i < 88; i++)
        {
            _keyState[i] = new VRKeyState();
        }

        _initialized = true;

        Debug.Log("[VRPianoManager] Initialize 완료");
    }

    public void OnPress(int keyIdx, float velocity)
    {
        if (keyIdx < 0 || keyIdx >= 88) return;

        Debug.Log($"[VRPianoManager] Press key={keyIdx} vel={velocity:F2}");

        if (audioPlayer != null)
        {
            audioPlayer.Play(keyIdx, velocity);
        }
    }

    public void OnRelease(int keyIdx)
    {
        if (keyIdx < 0 || keyIdx >= 88) return;

        Debug.Log($"[VRPianoManager] Release key={keyIdx}");

        if (audioPlayer != null)
        {
            audioPlayer.Stop(keyIdx);
        }
    }

    public bool IsInitialized()
    {
        return _initialized;
    }
}