using UnityEngine;
using System.Collections.Generic;

public class VRKeyInputJudge : MonoBehaviour
{
    [Header("References")]
    public VRHandTracker handTracker;
    public VRLayoutInitializer layoutInitializer;
    public VRPianoManager pianoManager;

    [Header("Input Mode")]
    [Tooltip("켜면 전체에서 건반 하나만 눌림. 끄면 손가락별 다중 입력.")]
    public bool singleKeyMode = false;

    [Header("Arming")]
    [Tooltip("켜면 손가락을 한 번 표면에서 들어올려야 Press 가능")]
    public bool requireLiftBeforePress = true;

    [Header("Debug")]
    public bool logPress = true;
    public bool logRelease = true;
    public bool logCandidate = false;
    public bool logArming = false;

    private VRKeyData[] _keyData;
    private VRKeyState[] _keyState;
    private bool _initialized = false;

    private List<VelocitySample>[] _velocityHistory;
    private bool[] _fingerArmed;

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
        public float centerScore;
        public float impactSpeed;

        public KeyCandidate(int fingerId, int keyIdx, float centerScore, float impactSpeed)
        {
            this.fingerId = fingerId;
            this.keyIdx = keyIdx;
            this.centerScore = centerScore;
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

        _fingerArmed = new bool[10];

        for (int i = 0; i < 10; i++)
        {
            _fingerArmed[i] = !requireLiftBeforePress;
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
        UpdateFingerArming();

        if (singleKeyMode)
        {
            JudgeSingleKeyMode();
        }
        else
        {
            JudgePerFingerMultiKeyMode();
        }

        UpdatePressedKeyAmounts();
        ResetReleasedStates();
    }

    // ─────────────────────────────────────────────
    // Pre-contact velocity
    // ─────────────────────────────────────────────

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

    // ─────────────────────────────────────────────
    // Finger arming
    // ─────────────────────────────────────────────

    private void UpdateFingerArming()
    {
        if (!requireLiftBeforePress) return;
        if (_fingerArmed == null) return;

        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (!finger.isTracked)
            {
                continue;
            }

            // 이미 누르고 있는 손가락은 arming 갱신 안 함
            if (finger.boundKeyIndex >= 0)
            {
                continue;
            }

            if (_fingerArmed[f])
            {
                continue;
            }

            if (IsFingerLiftedEnoughToArm(finger))
            {
                _fingerArmed[f] = true;

                if (logArming)
                {
                    Debug.Log($"[FingerArmed] finger={f}");
                }
            }
        }
    }

    private bool IsFingerLiftedEnoughToArm(VRFingerData finger)
    {
        float lx = finger.localPos.x;
        float lz = finger.localPos.z;
        float ly = finger.localPos.y;

        bool overAnyKey = false;
        float highestSurfaceY = float.MinValue;

        for (int k = 0; k < 88; k++)
        {
            VRKeyData data = _keyData[k];

            if (data == null) continue;

            if (data.IsFingerOver(lx, lz))
            {
                overAnyKey = true;

                if (data.restTopLocalY > highestSurfaceY)
                {
                    highestSurfaceY = data.restTopLocalY;
                }
            }
        }

        // 건반 영역 밖으로 나갔으면 다시 press 준비 가능
        if (!overAnyKey)
        {
            return true;
        }

        // 건반 위에 있다면 표면보다 충분히 위로 올라가야 armed
        return ly >= highestSurfaceY + VRPianoConst.PressArmHeight;
    }

    private bool IsFingerArmed(VRFingerData finger)
    {
        if (!requireLiftBeforePress) return true;

        int fid = finger.fingerId;

        if (fid < 0 || fid >= 10) return false;
        if (_fingerArmed == null) return false;

        return _fingerArmed[fid];
    }

    private void DisarmFinger(VRFingerData finger)
    {
        if (finger == null) return;
        if (_fingerArmed == null) return;

        int fid = finger.fingerId;

        if (fid < 0 || fid >= _fingerArmed.Length) return;

        _fingerArmed[fid] = false;
    }

    // ─────────────────────────────────────────────
    // Single key mode
    // ─────────────────────────────────────────────

    private void JudgeSingleKeyMode()
    {
        int pressedKey = GetCurrentlyPressedKey();

        if (pressedKey >= 0)
        {
            ContinuePressedKey(pressedKey);
            return;
        }

        KeyCandidate candidate;

        if (TryFindBestCandidateGlobal(out candidate))
        {
            VRFingerData finger = handTracker.FingerData[candidate.fingerId];
            TryPressWithImpact(finger, candidate.keyIdx, candidate.impactSpeed);
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

    // ─────────────────────────────────────────────
    // Multi key mode
    // 손가락별 독립 입력 + 같은 건반 다중 손가락 허용
    // ─────────────────────────────────────────────

    private void JudgePerFingerMultiKeyMode()
    {
        // 1. 이미 누르고 있는 손가락은 자기 건반만 유지/해제 판정
        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (finger.boundKeyIndex >= 0 && finger.boundKeyIndex < 88)
            {
                if (!finger.isTracked)
                {
                    HandleOcclusionTimeout(finger);
                    continue;
                }

                ContinueBoundFinger(finger);
            }
        }

        // 2. 아직 건반을 점유하지 않은 손가락은 새 후보 탐색
        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (!finger.isTracked)
            {
                continue;
            }

            if (finger.boundKeyIndex >= 0)
            {
                continue;
            }

            if (!IsFingerArmed(finger))
            {
                continue;
            }

            KeyCandidate candidate;

            if (TryFindBestCandidateForFinger(f, out candidate))
            {
                TryPressWithImpact(finger, candidate.keyIdx, candidate.impactSpeed);
            }
        }
    }

    // ─────────────────────────────────────────────
    // Candidate search
    // ─────────────────────────────────────────────

    private bool TryFindBestCandidateGlobal(out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);

        bool foundBlack = TryFindBestCandidateByKeyTypeGlobal(true, out bestCandidate);

        if (foundBlack)
        {
            return true;
        }

        return TryFindBestCandidateByKeyTypeGlobal(false, out bestCandidate);
    }

    private bool TryFindBestCandidateByKeyTypeGlobal(bool blackOnly, out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);
        bool found = false;

        for (int f = 0; f < 10; f++)
        {
            VRFingerData finger = handTracker.FingerData[f];

            if (!finger.isTracked) continue;
            if (finger.boundKeyIndex >= 0) continue;
            if (!IsFingerArmed(finger)) continue;

            KeyCandidate candidate;

            if (!TryFindBestCandidateByKeyTypeForFinger(f, blackOnly, out candidate))
            {
                continue;
            }

            if (!found || candidate.centerScore > bestCandidate.centerScore)
            {
                bestCandidate = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool TryFindBestCandidateForFinger(int fingerId, out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);

        // 손가락 하나 기준에서도 검은 건반 우선
        bool foundBlack = TryFindBestCandidateByKeyTypeForFinger(fingerId, true, out bestCandidate);

        if (foundBlack)
        {
            return true;
        }

        return TryFindBestCandidateByKeyTypeForFinger(fingerId, false, out bestCandidate);
    }

    private bool TryFindBestCandidateByKeyTypeForFinger(
        int fingerId,
        bool blackOnly,
        out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f);
        bool found = false;

        if (fingerId < 0 || fingerId >= 10) return false;

        VRFingerData finger = handTracker.FingerData[fingerId];

        if (!finger.isTracked) return false;
        if (!IsFingerArmed(finger)) return false;

        float impactSpeed = GetPreContactImpactSpeed(fingerId);

        if (impactSpeed < VRPianoConst.MinImpactSpeedToPress)
        {
            return false;
        }

        for (int k = 0; k < 88; k++)
        {
            VRKeyData data = _keyData[k];

            if (data == null) continue;
            if (data.isBlackKey != blackOnly) continue;

            float centerScore;

            if (!TryScoreCandidateSimple(finger, data, out centerScore))
            {
                continue;
            }

            KeyCandidate candidate = new KeyCandidate(
                finger.fingerId,
                k,
                centerScore,
                impactSpeed
            );

            if (!found || candidate.centerScore > bestCandidate.centerScore)
            {
                bestCandidate = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool TryScoreCandidateSimple(
        VRFingerData finger,
        VRKeyData data,
        out float centerScore)
    {
        centerScore = 0f;

        float lx = finger.localPos.x;
        float lz = finger.localPos.z;
        float ly = finger.localPos.y;

        if (!data.IsFingerOver(lx, lz))
        {
            return false;
        }

        if (ly > data.restTopLocalY + VRPianoConst.ContactTolerance)
        {
            return false;
        }

        centerScore = GetZoneCenterScore(data, lx, lz);

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

        float normalizedDistance =
            Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz) * 0.707f);

        return 1f - normalizedDistance;
    }

    // ─────────────────────────────────────────────
    // Press / Hold / Release
    // ─────────────────────────────────────────────

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

        if (!IsFingerArmed(finger))
        {
            return;
        }

        if (impactSpeed < VRPianoConst.MinImpactSpeedToPress)
        {
            return;
        }

        VRKeyData data = _keyData[keyIdx];
        VRKeyState state = _keyState[keyIdx];

        if (data == null || state == null) return;
        if (finger.boundKeyIndex >= 0) return;

        float ly = finger.localPos.y;

        if (ly > data.restTopLocalY + VRPianoConst.ContactTolerance)
        {
            return;
        }

        bool wasIdle = state.pressedFingerCount == 0 ||
                       state.pressState != VRKeyPressState.Pressed;

        float impact01 = Mathf.Clamp01(
            impactSpeed / VRPianoConst.ImpactSpeedForMax
        );

        float initialPress = Mathf.Lerp(
            VRPianoConst.InitialPressMin,
            VRPianoConst.InitialPressMax,
            impact01
        );

        float fillSpeed = Mathf.Lerp(
            VRPianoConst.FillSpeedMin,
            VRPianoConst.FillSpeedMax,
            impact01
        );

        state.pressState = VRKeyPressState.Pressed;
        state.AddFinger(finger.fingerId);

        state.occlusionHoldStartByFinger[finger.fingerId] = 0f;
        state.impactSpeed = Mathf.Max(state.impactSpeed, impactSpeed);
        state.contactStartTime = Time.time;

        // 이미 눌린 건반에 다른 손가락이 추가로 닿으면
        // 소리는 다시 안 나지만, 더 강한 입력이면 눌림 속도/깊이는 보강
        state.pressAmount = Mathf.Max(state.pressAmount, initialPress);
        state.pressFillSpeed = Mathf.Max(state.pressFillSpeed, fillSpeed);
        state.targetAngle = GetMaxAngle(data) * state.pressAmount;

        finger.boundKeyIndex = keyIdx;
        DisarmFinger(finger);

        if (logPress)
        {
            Debug.Log(
                $"[PRESS-FINGER] key={keyIdx} finger={finger.fingerId} black={data.isBlackKey} " +
                $"wasIdle={wasIdle} impact={impactSpeed:F3} impact01={impact01:F2} " +
                $"pressAmount={state.pressAmount:F2} fillSpeed={state.pressFillSpeed:F2} " +
                $"fingerCount={state.pressedFingerCount}"
            );
        }

        // 오디오는 Idle → Pressed 순간에만 Play
        if (wasIdle)
        {
            pianoManager.OnPress(keyIdx, impact01);
        }
    }

    private void ContinuePressedKey(int keyIdx)
    {
        if (keyIdx < 0 || keyIdx >= 88) return;

        VRKeyState state = _keyState[keyIdx];

        if (state == null) return;

        int fingerId = state.FindFirstPressingFinger();

        if (fingerId < 0)
        {
            ForceReleaseKey(keyIdx, state, "NoFinger");
            return;
        }

        VRFingerData finger = handTracker.FingerData[fingerId];

        if (!finger.isTracked)
        {
            HandleOcclusionTimeout(finger);
            return;
        }

        ContinueBoundFinger(finger);
    }

    private void ContinueBoundFinger(VRFingerData finger)
    {
        int keyIdx = finger.boundKeyIndex;

        if (keyIdx < 0 || keyIdx >= 88) return;

        VRKeyData data = _keyData[keyIdx];
        VRKeyState state = _keyState[keyIdx];

        if (data == null || state == null) return;

        if (!state.IsFingerPressing(finger.fingerId))
        {
            finger.boundKeyIndex = -1;
            DisarmFinger(finger);
            return;
        }

        JudgePressedFinger(finger, data, state, keyIdx);
    }

    private void JudgePressedFinger(
        VRFingerData finger,
        VRKeyData data,
        VRKeyState state,
        int keyIdx)
    {
        int fid = finger.fingerId;

        state.ClearOcclusion(fid);

        float lx = finger.localPos.x;
        float lz = finger.localPos.z;
        float ly = finger.localPos.y;

        bool stillOverZone = data.IsFingerOver(lx, lz);

        if (!stillOverZone)
        {
            ReleaseFingerFromKey(keyIdx, state, finger, "ZoneExit");
            return;
        }

        bool stillHolding = ly <= data.restTopLocalY + VRPianoConst.HoldTolerance;

        if (stillHolding)
        {
            return;
        }

        bool shouldRelease = ly >= data.restTopLocalY + VRPianoConst.ReleaseMargin;

        if (shouldRelease)
        {
            ReleaseFingerFromKey(keyIdx, state, finger, "Release");
        }
    }

    private void UpdatePressedKeyAmounts()
    {
        for (int k = 0; k < 88; k++)
        {
            VRKeyState state = _keyState[k];
            VRKeyData data = _keyData[k];

            if (state == null || data == null) continue;

            if (state.pressState != VRKeyPressState.Pressed)
            {
                continue;
            }

            if (state.pressedFingerCount <= 0)
            {
                ForceReleaseKey(k, state, "NoFinger");
                continue;
            }

            state.pressAmount = Mathf.MoveTowards(
                state.pressAmount,
                1f,
                state.pressFillSpeed * Time.deltaTime
            );

            state.targetAngle = GetMaxAngle(data) * state.pressAmount;
        }
    }

    private void ReleaseFingerFromKey(
        int keyIdx,
        VRKeyState state,
        VRFingerData finger,
        string reason)
    {
        if (state == null || finger == null) return;

        int fid = finger.fingerId;

        state.RemoveFinger(fid);
        finger.boundKeyIndex = -1;
        DisarmFinger(finger);

        if (logRelease)
        {
            Debug.Log(
                $"[ReleaseFinger] key={keyIdx} finger={fid} reason={reason} " +
                $"remaining={state.pressedFingerCount}"
            );
        }

        // 아직 다른 손가락이 같은 건반을 누르고 있으면 건반 유지
        if (state.pressedFingerCount > 0)
        {
            return;
        }

        ForceReleaseKey(keyIdx, state, "LastFingerReleased");
    }

    private void ForceReleaseKey(
        int keyIdx,
        VRKeyState state,
        string reason)
    {
        if (state == null) return;

        state.pressState = VRKeyPressState.Released;
        state.occupiedByFinger = -1;
        state.targetAngle = 0f;
        state.pressAmount = 0f;
        state.pressFillSpeed = 0f;
        state.impactSpeed = 0f;
        state.contactStartTime = 0f;
        state.pressedFingerCount = 0;

        state.EnsureArrays();

        for (int i = 0; i < 10; i++)
        {
            state.pressingFingers[i] = false;
            state.occlusionHoldStartByFinger[i] = 0f;
        }

        pianoManager?.OnRelease(keyIdx);

        if (logRelease)
        {
            Debug.Log($"[ReleaseKey] key={keyIdx} reason={reason}");
        }
    }

    private void HandleOcclusionTimeout(VRFingerData finger)
    {
        if (finger.boundKeyIndex < 0 || finger.boundKeyIndex >= 88) return;

        int keyIdx = finger.boundKeyIndex;
        VRKeyState state = _keyState[keyIdx];

        if (state == null) return;
        if (state.pressState != VRKeyPressState.Pressed) return;

        int fid = finger.fingerId;

        if (!state.IsFingerPressing(fid)) return;

        state.StartOcclusionIfNeeded(fid);

        if (state.IsOcclusionTimedOut(fid))
        {
            ReleaseFingerFromKey(keyIdx, state, finger, "OcclusionTimeout");
        }
    }

    private float GetMaxAngle(VRKeyData data)
    {
        return Mathf.Atan(data.TanThetaMax) * Mathf.Rad2Deg;
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