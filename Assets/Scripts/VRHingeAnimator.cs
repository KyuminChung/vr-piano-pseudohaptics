using UnityEngine;

public class VRHingeAnimator : MonoBehaviour
{
    [Header("References")]
    public VRLayoutInitializer layoutInitializer;
    public VRKeyInputJudge keyInputJudge;

    [Header("Animation Tuning")]
    public float pressSpeed = 18f;
    public float releaseSpeed = 12f;

    [Header("Direction")]
    [Tooltip("건반이 위로 올라가면 체크")]
    public bool invertDirection = false;

    [Header("Axis")]
    [Tooltip("보통 건반은 X축 기준으로 회전")]
    public Vector3 localRotationAxis = Vector3.right;

    private VRKeyData[] _keyData;
    private bool _initialized = false;

    private Quaternion[] _initialLocalRot;

    private void Start()
    {
        if (layoutInitializer != null && keyInputJudge != null)
        {
            Initialize(layoutInitializer, keyInputJudge);
        }
    }

    public void Initialize(VRLayoutInitializer initializer, VRKeyInputJudge judge)
    {
        layoutInitializer = initializer;
        keyInputJudge = judge;

        if (layoutInitializer == null || keyInputJudge == null)
        {
            Debug.LogError("[VRHingeAnimator] Initialize 실패: 레퍼런스 없음");
            return;
        }

        _keyData = layoutInitializer.KeyData;

        if (_keyData == null)
        {
            Debug.LogError("[VRHingeAnimator] Initialize 실패: KeyData 없음");
            return;
        }

        _initialLocalRot = new Quaternion[_keyData.Length];

        for (int i = 0; i < _keyData.Length; i++)
        {
            if (_keyData[i] == null || _keyData[i].keyTransform == null) continue;

            _initialLocalRot[i] = _keyData[i].keyTransform.localRotation;
        }

        _initialized = true;

        Debug.Log("[VRHingeAnimator] Initialize 완료");
    }

    private void Update()
    {
        if (!_initialized) return;
        if (_keyData == null) return;
        if (keyInputJudge == null) return;

        for (int i = 0; i < _keyData.Length; i++)
        {
            VRKeyData data = _keyData[i];

            if (data == null) continue;
            if (data.keyTransform == null) continue;

            VRKeyState state = keyInputJudge.GetKeyState(i);

            if (state == null) continue;

            AnimateKey(i, data, state);
        }
    }

    private void AnimateKey(int idx, VRKeyData data, VRKeyState state)
    {
        float targetDeg = state.pressState == VRKeyPressState.Pressed
            ? state.targetAngle
            : 0f;

        float speed = state.pressState == VRKeyPressState.Pressed
            ? pressSpeed
            : releaseSpeed;

        state.currentAngle = Mathf.Lerp(
            state.currentAngle,
            targetDeg,
            Time.deltaTime * speed
        );

        ApplyRotationOnly(idx, data, state.currentAngle);
    }

    private void ApplyRotationOnly(int idx, VRKeyData data, float angleDeg)
    {
        Transform key = data.keyTransform;

        float finalAngle = invertDirection ? -angleDeg : angleDeg;

        Quaternion deltaRot = Quaternion.AngleAxis(finalAngle, localRotationAxis.normalized);

        // 위치는 절대 건드리지 않음.
        // 그래서 건반이 공중으로 뜨는 문제 방지.
        key.localRotation = _initialLocalRot[idx] * deltaRot;
    }
}