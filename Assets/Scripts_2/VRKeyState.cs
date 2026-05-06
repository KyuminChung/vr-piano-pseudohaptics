using UnityEngine;

public enum VRKeyPressState
{
    Idle,
    Pressed,
    Released
}

[System.Serializable]
public class VRKeyState
{
    public VRKeyPressState pressState = VRKeyPressState.Idle;

    public int occupiedByFinger = -1;

    // HingeAnimator가 읽는 값
    public float currentAngle = 0f;
    public float targetAngle = 0f;

    // pseudo-haptic press model
    public float pressAmount = 0f;      // 0~1
    public float pressFillSpeed = 0f;   // max까지 들어가는 속도
    public float impactSpeed = 0f;      // 접촉 직전 속도
    public float contactStartTime = 0f;

    public float occlusionHoldStart = 0f;

    public void Reset()
    {
        pressState = VRKeyPressState.Idle;
        occupiedByFinger = -1;

        targetAngle = 0f;

        pressAmount = 0f;
        pressFillSpeed = 0f;
        impactSpeed = 0f;
        contactStartTime = 0f;

        occlusionHoldStart = 0f;

        // currentAngle은 애니메이터가 자연스럽게 0으로 복귀시키도록 유지
    }

    public bool IsOcclusionTimedOut()
    {
        if (occlusionHoldStart <= 0f) return false;
        return Time.time - occlusionHoldStart > VRPianoConst.OcclusionHoldTime;
    }
}