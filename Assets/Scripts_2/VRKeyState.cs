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

    // 호환용. 이제는 대표 손가락 정도로만 사용.
    public int occupiedByFinger = -1;

    // HingeAnimator가 읽는 값
    public float currentAngle = 0f;
    public float targetAngle = 0f;

    // Pseudo-haptic press model
    public float pressAmount = 0f;
    public float pressFillSpeed = 0f;
    public float impactSpeed = 0f;
    public float contactStartTime = 0f;

    // 한 건반을 여러 손가락이 동시에 누를 수 있음
    public bool[] pressingFingers = new bool[10];
    public int pressedFingerCount = 0;

    // 손가락별 occlusion hold
    public float[] occlusionHoldStartByFinger = new float[10];

    public void Reset()
    {
        pressState = VRKeyPressState.Idle;
        occupiedByFinger = -1;

        targetAngle = 0f;

        pressAmount = 0f;
        pressFillSpeed = 0f;
        impactSpeed = 0f;
        contactStartTime = 0f;

        pressedFingerCount = 0;

        EnsureArrays();

        for (int i = 0; i < 10; i++)
        {
            pressingFingers[i] = false;
            occlusionHoldStartByFinger[i] = 0f;
        }

        // currentAngle은 애니메이터가 자연스럽게 0으로 복귀시키도록 유지
    }

    public void EnsureArrays()
    {
        if (pressingFingers == null || pressingFingers.Length != 10)
        {
            pressingFingers = new bool[10];
        }

        if (occlusionHoldStartByFinger == null || occlusionHoldStartByFinger.Length != 10)
        {
            occlusionHoldStartByFinger = new float[10];
        }
    }

    public bool IsFingerPressing(int fingerId)
    {
        EnsureArrays();

        if (fingerId < 0 || fingerId >= 10) return false;

        return pressingFingers[fingerId];
    }

    public bool AddFinger(int fingerId)
    {
        EnsureArrays();

        if (fingerId < 0 || fingerId >= 10) return false;

        if (pressingFingers[fingerId])
        {
            return false;
        }

        pressingFingers[fingerId] = true;
        pressedFingerCount++;

        if (occupiedByFinger < 0)
        {
            occupiedByFinger = fingerId;
        }

        return true;
    }

    public bool RemoveFinger(int fingerId)
    {
        EnsureArrays();

        if (fingerId < 0 || fingerId >= 10) return false;

        if (!pressingFingers[fingerId])
        {
            return false;
        }

        pressingFingers[fingerId] = false;
        pressedFingerCount = Mathf.Max(0, pressedFingerCount - 1);
        occlusionHoldStartByFinger[fingerId] = 0f;

        if (occupiedByFinger == fingerId)
        {
            occupiedByFinger = FindFirstPressingFinger();
        }

        return true;
    }

    public int FindFirstPressingFinger()
    {
        EnsureArrays();

        for (int i = 0; i < 10; i++)
        {
            if (pressingFingers[i])
            {
                return i;
            }
        }

        return -1;
    }

    public void ClearOcclusion(int fingerId)
    {
        EnsureArrays();

        if (fingerId < 0 || fingerId >= 10) return;

        occlusionHoldStartByFinger[fingerId] = 0f;
    }

    public void StartOcclusionIfNeeded(int fingerId)
    {
        EnsureArrays();

        if (fingerId < 0 || fingerId >= 10) return;

        if (occlusionHoldStartByFinger[fingerId] <= 0f)
        {
            occlusionHoldStartByFinger[fingerId] = Time.time;
        }
    }

    public bool IsOcclusionTimedOut(int fingerId)
    {
        EnsureArrays();

        if (fingerId < 0 || fingerId >= 10) return false;

        float start = occlusionHoldStartByFinger[fingerId];

        if (start <= 0f) return false;

        return Time.time - start > VRPianoConst.OcclusionHoldTime;
    }
}