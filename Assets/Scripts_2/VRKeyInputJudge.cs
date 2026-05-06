using UnityEngine;
using System.Collections.Generic;

public class VRKeyInputJudge : MonoBehaviour
{
    [Header("References")]
    public VRHandTracker handTracker;
    public VRLayoutInitializer layoutInitializer;
    public VRPianoManager pianoManager;

    [Header("Input Mode")]
    public bool singleKeyMode = true;

    [Header("Debug")]
    public bool logPress = true;
    public bool logRelease = true;
    public bool logCandidate = false;

    private VRKeyData[] _keyData;
    private VRKeyState[] _keyState;
    private bool _initialized = false;

    private List<VelocitySample>[] _velocityHistory;

    private struct VelocitySample
    {
        public float time;
        public float downwardSpeed;

        public VelocitySample(float time, float downwardSpeed)
        {
            this.time = time;
            this.downwardSpeed = downwardSpeed;
        }
    }

    private struct KeyCandidate
    {
        public int fingerId;
        public int keyIdx;
        public float score;
        public float impactSpeed;

        public KeyCandidate(int fingerId, int keyIdx, float score, float impactSpeed)
        {
            this.fingerId = fingerId;
            this.keyIdx = keyIdx;
            this.score = score;
            this.impactSpeed = impactSpeed;
        }
    }

    private void Start()
    {
        if (handTracker != null && layoutInitializer != null && pianoManager != null)
        {
            Initialize(handTracker, layoutInitializer, pianoManager);
        }
    }

    public void Initialize(
        VRHandTracker tracker,
        VRLayoutInitializer initializer,
        VRPianoManager manager)
    {
        handTracker = tracker;
        layoutInitializer = initializer;
        pianoManager = manager;

        if (handTracker == null || layoutInitializer == null || pianoManager == null)
        {
            Debug.LogError("[VRKeyInputJudge] Initialize 실패: 레퍼런스 없음");
            return;
        }

        _keyData = layoutInitializer.KeyData;

        if (_keyData == null)
        {
            Debug.LogError("[VRKeyInputJudge] Initialize 실패: KeyData 없음");
            return;
        }

        _keyState = new VRKeyState[88];

        for (int i = 0; i < 88; i++)
        {
            _keyState[i] = new VRKeyState();
        }

        _velocityHistory = new List<VelocitySample>[10];

        for (int i = 0; i < 10; i++)
        {
            _velocityHistory[i] = new List<VelocitySample>();
        }

        _initialized = true;

        Debug.Log("[VRKeyInputJudge] Initialize 완료");
    }

    private void Update()
    {
        if (!_initialized) return;
        if (_keyData == null || _keyState == null) return;
        if (handTracker == null) return;

        UpdatePreContactVelocityHistory();

        if (singleKeyMode)
        {
            JudgeSingleKeyMode();
        }
        else
        {
            JudgeMultiFingerMode();
        }

        ResetReleasedStates();
    }

    private void UpdatePreContactVelocityHistory()
    {
        float now = Time.time;
        float window = VRPianoConst.PreContactVelocityWindow;

        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (!finger.isTracked)
            {
                continue;
            }

            // Unity Y축 기준: 아래로 내려가면 velocityY가 음수라고 가정
            float downwardSpeed = Mathf.Max(0f, -finger.velocityY);

            _velocityHistory[f].Add(new VelocitySample(now, downwardSpeed));

            for (int i = _velocityHistory[f].Count - 1; i >= 0; i--)
            {
                if (now - _velocityHistory[f][i].time > window)
                {
                    _velocityHistory[f].RemoveAt(i);
                }
            }
        }
    }

    private float GetPreContactImpactSpeed(int fingerId)
    {
        if (fingerId < 0 || fingerId >= 10) return 0f;
        if (_velocityHistory == null || _velocityHistory[fingerId] == null) return 0f;

        float maxSpeed = 0f;

        foreach (VelocitySample sample in _velocityHistory[fingerId])
        {
            if (sample.downwardSpeed > maxSpeed)
            {
                maxSpeed = sample.downwardSpeed;
            }
        }

        return maxSpeed;
    }

    private void JudgeSingleKeyMode()
    {
        int pressedKey = GetCurrentlyPressedKey();

        if (pressedKey >= 0)
        {
            ContinuePressedKey(pressedKey);
            return;
        }

        KeyCandidate candidate;

        if (TryFindBestCandidate(out candidate))
        {
            VRFingerData finger = handTracker.FingerData[candidate.fingerId];

            if (logCandidate)
            {
                Debug.Log(
                    $"[Candidate] key={candidate.keyIdx} finger={candidate.fingerId} " +
                    $"score={candidate.score:F3} impact={candidate.impactSpeed:F3}"
                );
            }

            TryPressWithImpact(finger, candidate.keyIdx, candidate.impactSpeed);
        }
    }

    private void JudgeMultiFingerMode()
    {
        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (!finger.isTracked)
            {
                HandleOcclusionTimeout(finger);
                continue;
            }

            if (finger.boundKeyIndex >= 0 && finger.boundKeyIndex < 88)
            {
                ContinuePressedKey(finger.boundKeyIndex);
                continue;
            }

            KeyCandidate candidate;

            if (TryFindBestCandidateForFinger(f, out candidate))
            {
                TryPressWithImpact(finger, candidate.keyIdx, candidate.impactSpeed);
            }
        }
    }

    private int GetCurrentlyPressedKey()
    {
        for (int k = 0; k < 88; k++)
        {
            if (_keyState[k].pressState == VRKeyPressState.Pressed)
            {
                return k;
            }
        }

        return -1;
    }

    private void ContinuePressedKey(int keyIdx)
    {
        if (keyIdx < 0 || keyIdx >= 88) return;

        VRKeyState state = _keyState[keyIdx];
        VRKeyData data = _keyData[keyIdx];

        if (state == null || data == null) return;

        int fingerId = state.occupiedByFinger;

        if (fingerId < 0 || fingerId >= 10)
        {
            ForceRelease(keyIdx, state, null, "InvalidFinger");
            return;
        }

        VRFingerData finger = handTracker.FingerData[fingerId];

        if (!finger.isTracked)
        {
            HandleOcclusionTimeout(finger);
            return;
        }

        JudgePressedKey(finger, data, state, keyIdx);
    }

    private void JudgePressedKey(
        VRFingerData finger,
        VRKeyData data,
        VRKeyState state,
        int keyIdx)
    {
        float lx = finger.localPos.x;
        float lz = finger.localPos.z;
        float ly = finger.localPos.y;

        bool stillOverZone = data.IsFingerOver(lx, lz);

        if (!stillOverZone)
        {
            ForceRelease(keyIdx, state, finger, "ZoneExit");
            return;
        }

        // 손가락이 표면 근처에 계속 있으면 pseudo-haptic으로 max까지 진행
        bool stillHolding = ly <= data.restTopLocalY + VRPianoConst.HoldTolerance;

        if (stillHolding)
        {
            state.pressAmount = Mathf.MoveTowards(
                state.pressAmount,
                1f,
                state.pressFillSpeed * Time.deltaTime
            );

            state.targetAngle = GetMaxAngle(data) * state.pressAmount;
            return;
        }

        // 확실히 위로 올라오면 Release
        bool shouldRelease = ly >= data.restTopLocalY + VRPianoConst.ReleaseMargin;

        if (shouldRelease)
        {
            ForceRelease(keyIdx, state, finger, "Release");
        }
    }

    private bool TryFindBestCandidate(out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);

        // 1순위: 검은 건반 후보 전체
        bool foundBlack = TryFindBestCandidateByKeyType(true, out bestCandidate);

        if (foundBlack)
        {
            return true;
        }

        // 2순위: 흰 건반 후보 전체
        return TryFindBestCandidateByKeyType(false, out bestCandidate);
    }

    private bool TryFindBestCandidateForFinger(int fingerId, out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);

        bool foundBlack = TryFindBestCandidateByKeyTypeForFinger(fingerId, true, out bestCandidate);

        if (foundBlack)
        {
            return true;
        }

        return TryFindBestCandidateByKeyTypeForFinger(fingerId, false, out bestCandidate);
    }

    private bool TryFindBestCandidateByKeyType(bool blackOnly, out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);
        bool found = false;

        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (!finger.isTracked) continue;

            KeyCandidate candidate;

            if (!TryFindBestCandidateByKeyTypeForFinger(f, blackOnly, out candidate))
                continue;

            if (!found || candidate.score > bestCandidate.score)
            {
                bestCandidate = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool TryFindBestCandidateByKeyTypeForFinger(
        int fingerId,
        bool blackOnly,
        out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);
        bool found = false;

        VRFingerData finger = handTracker.FingerData[fingerId];

        float impactSpeed = GetPreContactImpactSpeed(fingerId);

        for (int k = 0; k < 88; k++)
        {
            VRKeyData data = _keyData[k];
            VRKeyState state = _keyState[k];

            if (data == null || state == null) continue;
            if (data.isBlackKey != blackOnly) continue;

            if (state.occupiedByFinger != -1 &&
                state.occupiedByFinger != finger.fingerId)
            {
                continue;
            }

            float score;

            if (!TryScoreCandidate(finger, data, impactSpeed, out score))
            {
                continue;
            }

            KeyCandidate candidate = new KeyCandidate(
                finger.fingerId,
                k,
                score,
                impactSpeed
            );

            if (!found || candidate.score > bestCandidate.score)
            {
                bestCandidate = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool TryScoreCandidate(
        VRFingerData finger,
        VRKeyData data,
        float impactSpeed,
        out float score)
    {
        score = 0f;

        float lx = finger.localPos.x;
        float lz = finger.localPos.z;
        float ly = finger.localPos.y;

        // X/Z Zone 안에 있어야 후보
        if (!data.IsFingerOver(lx, lz))
        {
            return false;
        }

        // 실제 Press 시작은 표면 근처에 닿았을 때만
        if (ly > data.restTopLocalY + VRPianoConst.ContactTolerance)
        {
            return false;
        }

        float depthScore = Mathf.Clamp01(
            1f - Mathf.Abs(ly - data.restTopLocalY) / VRPianoConst.ProximityHeight
        );

        float velocityScore = Mathf.Clamp01(
            impactSpeed / VRPianoConst.ImpactSpeedForMax
        );

        float centerScore = GetZoneCenterScore(data, lx, lz);

        float blackBonus = data.isBlackKey
            ? VRPianoConst.CandidateBlackBonus
            : 0f;

        score =
            depthScore * VRPianoConst.CandidateDepthWeight +
            velocityScore * VRPianoConst.CandidateVelocityWeight +
            centerScore * VRPianoConst.CandidateCenterWeight +
            blackBonus;

        return true;
    }

    private float GetZoneCenterScore(VRKeyData data, float lx, float lz)
    {
        float bestScore = 0f;

        bestScore = Mathf.Max(
            bestScore,
            GetRectCenterScore(
                lx, lz,
                data.zoneAMinX,
                data.zoneAMaxX,
                data.zoneAMinZ,
                data.zoneAMaxZ
            )
        );

        if (data.hasZoneB)
        {
            bestScore = Mathf.Max(
                bestScore,
                GetRectCenterScore(
                    lx, lz,
                    data.zoneBMinX,
                    data.zoneBMaxX,
                    data.zoneBMinZ,
                    data.zoneBMaxZ
                )
            );
        }

        return bestScore;
    }

    private float GetRectCenterScore(
        float lx,
        float lz,
        float minX,
        float maxX,
        float minZ,
        float maxZ)
    {
        float cx = (minX + maxX) * 0.5f;
        float cz = (minZ + maxZ) * 0.5f;

        float halfX = Mathf.Max((maxX - minX) * 0.5f, 0.0001f);
        float halfZ = Mathf.Max((maxZ - minZ) * 0.5f, 0.0001f);

        float nx = Mathf.Abs(lx - cx) / halfX;
        float nz = Mathf.Abs(lz - cz) / halfZ;

        float normalizedDistance = Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz) * 0.707f);

        return 1f - normalizedDistance;
    }

    private void TryPressWithImpact(
        VRFingerData finger,
        int keyIdx,
        float impactSpeed)
    {
        if (keyIdx < 0 || keyIdx >= 88) return;

        if (singleKeyMode && GetCurrentlyPressedKey() >= 0)
        {
            return;
        }

        VRKeyData data = _keyData[keyIdx];
        VRKeyState state = _keyState[keyIdx];

        if (data == null || state == null) return;
        if (state.occupiedByFinger != -1) return;

        float ly = finger.localPos.y;

        if (ly > data.restTopLocalY + VRPianoConst.ContactTolerance)
        {
            return;
        }

        float impact01 = Mathf.Clamp01(
            impactSpeed / VRPianoConst.ImpactSpeedForMax
        );

        state.pressState = VRKeyPressState.Pressed;
        state.occupiedByFinger = finger.fingerId;
        state.occlusionHoldStart = 0f;

        state.impactSpeed = impactSpeed;
        state.contactStartTime = Time.time;

        state.pressAmount = Mathf.Lerp(
            VRPianoConst.InitialPressMin,
            VRPianoConst.InitialPressMax,
            impact01
        );

        state.pressFillSpeed = Mathf.Lerp(
            VRPianoConst.FillSpeedMin,
            VRPianoConst.FillSpeedMax,
            impact01
        );

        state.targetAngle = GetMaxAngle(data) * state.pressAmount;

        finger.boundKeyIndex = keyIdx;

        float noteVelocity = impact01;

        if (logPress)
        {
            Debug.Log(
                $"[PRESS] key={keyIdx} finger={finger.fingerId} black={data.isBlackKey} " +
                $"impact={impactSpeed:F3} impact01={impact01:F2} " +
                $"pressAmount={state.pressAmount:F2} fillSpeed={state.pressFillSpeed:F2} " +
                $"targetAngle={state.targetAngle:F2}"
            );
        }

        pianoManager.OnPress(keyIdx, noteVelocity);
    }

    private float GetMaxAngle(VRKeyData data)
    {
        return Mathf.Atan(data.TanThetaMax) * Mathf.Rad2Deg;
    }

    private void ForceRelease(
        int keyIdx,
        VRKeyState state,
        VRFingerData finger,
        string reason)
    {
        if (state == null) return;

        state.pressState = VRKeyPressState.Released;
        state.occupiedByFinger = -1;
        state.occlusionHoldStart = 0f;

        state.targetAngle = 0f;
        state.pressAmount = 0f;
        state.pressFillSpeed = 0f;
        state.impactSpeed = 0f;
        state.contactStartTime = 0f;

        if (finger != null)
        {
            finger.boundKeyIndex = -1;
        }

        pianoManager?.OnRelease(keyIdx);

        if (logRelease)
        {
            Debug.Log($"[Release] key={keyIdx} reason={reason}");
        }
    }

    private void HandleOcclusionTimeout(VRFingerData finger)
    {
        if (finger.boundKeyIndex < 0 || finger.boundKeyIndex >= 88) return;

        VRKeyState state = _keyState[finger.boundKeyIndex];

        if (state == null) return;
        if (state.pressState != VRKeyPressState.Pressed) return;

        if (state.occlusionHoldStart <= 0f)
        {
            state.occlusionHoldStart = Time.time;
        }

        if (state.IsOcclusionTimedOut())
        {
            ForceRelease(finger.boundKeyIndex, state, finger, "OcclusionTimeout");
        }
    }

    private void ResetReleasedStates()
    {
        for (int k = 0; k < 88; k++)
        {
            if (_keyState[k].pressState == VRKeyPressState.Released)
            {
                _keyState[k].Reset();
            }
        }
    }

    public VRKeyState GetKeyState(int keyIdx)
    {
        if (_keyState == null) return null;
        if (keyIdx < 0 || keyIdx >= _keyState.Length) return null;

        return _keyState[keyIdx];
    }

    public VRKeyState[] GetAllKeyStates()
    {
        return _keyState;
    }
}