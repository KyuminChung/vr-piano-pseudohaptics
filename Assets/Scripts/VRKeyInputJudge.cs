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

        // rawImpactSpeed: 손가락에서 직접 계산한 접촉 직전 하강 속도
        // effectiveImpactSpeed: 누른 위치를 반영해 보정한 최종 속도
        public float rawImpactSpeed;
        public float effectiveImpactSpeed;
        public float contactPosition01;
        public float positionImpactFactor;

        public KeyCandidate(
            int fingerId,
            int keyIdx,
            float centerScore,
            float rawImpactSpeed,
            float effectiveImpactSpeed,
            float contactPosition01,
            float positionImpactFactor)
        {
            this.fingerId = fingerId;
            this.keyIdx = keyIdx;
            this.centerScore = centerScore;
            this.rawImpactSpeed = rawImpactSpeed;
            this.effectiveImpactSpeed = effectiveImpactSpeed;
            this.contactPosition01 = contactPosition01;
            this.positionImpactFactor = positionImpactFactor;
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
            TryPressWithImpact(
                finger,
                candidate.keyIdx,
                candidate.rawImpactSpeed,
                candidate.effectiveImpactSpeed,
                candidate.contactPosition01,
                candidate.positionImpactFactor
            );
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
                TryPressWithImpact(
                finger,
                candidate.keyIdx,
                candidate.rawImpactSpeed,
                candidate.effectiveImpactSpeed,
                candidate.contactPosition01,
                candidate.positionImpactFactor
            );
            }
        }
    }

    // ─────────────────────────────────────────────
    // Candidate search
    // ─────────────────────────────────────────────

    private bool TryFindBestCandidateGlobal(out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f, 0f, 1f, 1f);

        bool foundBlack = TryFindBestCandidateByKeyTypeGlobal(true, out bestCandidate);

        if (foundBlack)
        {
            return true;
        }

        return TryFindBestCandidateByKeyTypeGlobal(false, out bestCandidate);
    }

    private bool TryFindBestCandidateByKeyTypeGlobal(bool blackOnly, out KeyCandidate bestCandidate)
    {
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f, 0f, 1f, 1f);
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
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f, 0f, 1f, 1f);

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
        bestCandidate = new KeyCandidate(-1, -1, float.MinValue, 0f, 0f, 1f, 1f);
        bool found = false;

        if (fingerId < 0 || fingerId >= 10) return false;

        VRFingerData finger = handTracker.FingerData[fingerId];

        if (!finger.isTracked) return false;
        if (!IsFingerArmed(finger)) return false;

        // 후보 탐색에서는 최소 속도 조건을 보지 않는다.
        // 손가락이 실제 표면에 닿았다면, 느린 접촉이어도 건반은 살짝 눌려야 한다.
        // 단, 소리 발생 여부는 TryPressWithImpact 안에서 위치 보정된 impactSpeed로 따로 판단한다.
        float rawImpactSpeed = GetPreContactImpactSpeed(fingerId);

        for (int k = 0; k < 88; k++)
        {
            VRKeyData data = _keyData[k];

            if (data == null) continue;
            if (data.isBlackKey != blackOnly) continue;

            float centerScore;
            float contactPosition01;
            float positionImpactFactor;

            if (!TryScoreCandidateSimple(
                finger,
                data,
                out centerScore,
                out contactPosition01,
                out positionImpactFactor))
            {
                continue;
            }

            float effectiveImpactSpeed = GetEffectiveImpactSpeed(
                rawImpactSpeed,
                contactPosition01
            );

            KeyCandidate candidate = new KeyCandidate(
                finger.fingerId,
                k,
                centerScore,
                rawImpactSpeed,
                effectiveImpactSpeed,
                contactPosition01,
                positionImpactFactor
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
        out float centerScore,
        out float contactPosition01,
        out float positionImpactFactor)
    {
        centerScore = 0f;
        contactPosition01 = 1f;
        positionImpactFactor = 1f;

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
        contactPosition01 = GetContactPosition01(data, lz);
        positionImpactFactor = GetPositionImpactFactor(contactPosition01);

        return true;
    }

    private float GetContactPosition01(VRKeyData data, float fingerLocalZ)
    {
        if (data == null) return 1f;
        if (data.keyLength <= 0.0001f) return 1f;

        // VRKeyData.GetSurfaceY와 같은 방향을 사용한다.
        // distFromHinge = 0이면 힌지 근처, 1이면 건반 앞쪽 끝.
        float distFromHinge = data.hingeLocalZ - fingerLocalZ;
        return Mathf.Clamp01(distFromHinge / data.keyLength);
    }

    private float GetPositionImpactFactor(float contactPosition01)
    {
        if (!VRPianoConst.UsePositionImpactWeighting)
        {
            return 1f;
        }

        float p = Mathf.Clamp01(contactPosition01);
        float curvedP = Mathf.Pow(p, Mathf.Max(0.0001f, VRPianoConst.PositionImpactPower));

        return Mathf.Lerp(
            VRPianoConst.RearImpactMultiplier,
            VRPianoConst.FrontImpactMultiplier,
            curvedP
        );
    }

    private float GetEffectiveImpactSpeed(float rawImpactSpeed, float contactPosition01)
    {
        return rawImpactSpeed * GetPositionImpactFactor(contactPosition01);
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
        float rawImpactSpeed,
        float effectiveImpactSpeed,
        float contactPosition01,
        float positionImpactFactor)
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

        bool shouldSound = effectiveImpactSpeed >= VRPianoConst.MinImpactSpeedToPress;

        float impact01 = Mathf.Clamp01(
            effectiveImpactSpeed / VRPianoConst.ImpactSpeedForMax
        );

        float initialPress = shouldSound
            ? Mathf.Lerp(
                VRPianoConst.InitialPressMin,
                VRPianoConst.InitialPressMax,
                impact01
            )
            : VRPianoConst.TouchPressMin;

        float fillSpeed = shouldSound
            ? Mathf.Lerp(
                VRPianoConst.FillSpeedMin,
                VRPianoConst.FillSpeedMax,
                impact01
            )
            : 0f;

        state.pressState = VRKeyPressState.Pressed;
        state.AddFinger(finger.fingerId);

        state.occlusionHoldStartByFinger[finger.fingerId] = 0f;
        state.rawImpactSpeed = Mathf.Max(state.rawImpactSpeed, rawImpactSpeed);
        state.effectiveImpactSpeed = Mathf.Max(state.effectiveImpactSpeed, effectiveImpactSpeed);
        state.impactSpeed = Mathf.Max(state.impactSpeed, effectiveImpactSpeed);
        state.contactPosition01 = contactPosition01;
        state.positionImpactFactor = positionImpactFactor;
        state.contactStartTime = Time.time;

        // 닿으면 무조건 최소 눌림을 보장한다.
        // 소리가 안 나는 느린 접촉이어도 TouchPressMin만큼은 내려간다.
        state.pressAmount = Mathf.Max(state.pressAmount, initialPress);
        state.pressFillSpeed = Mathf.Max(state.pressFillSpeed, fillSpeed);
        state.targetAngle = GetMaxAngle(data) * state.pressAmount;

        finger.boundKeyIndex = keyIdx;
        DisarmFinger(finger);

        // 오디오는 접촉 순간 속도가 충분할 때만 1회 발생.
        // 이미 누른 건반에 다른 손가락이 추가로 닿아도, hasSounded가 true면 중복 재생하지 않는다.
        bool playedSound = false;

        if (shouldSound && !state.hasSounded)
        {
            state.hasSounded = true;
            pianoManager.OnPress(keyIdx, impact01);
            playedSound = true;
        }

        if (logPress)
        {
            Debug.Log(
                $"[TOUCH-FINGER] key={keyIdx} finger={finger.fingerId} black={data.isBlackKey} " +
                $"wasIdle={wasIdle} shouldSound={shouldSound} playedSound={playedSound} " +
                $"rawImpact={rawImpactSpeed:F3} effectiveImpact={effectiveImpactSpeed:F3} " +
                $"pos01={contactPosition01:F2} posFactor={positionImpactFactor:F2} impact01={impact01:F2} " +
                $"pressAmount={state.pressAmount:F2} fillSpeed={state.pressFillSpeed:F2} " +
                $"fingerCount={state.pressedFingerCount}"
            );
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

            float contactPressAmount = CalculateContactPressAmount(state, data);

            if (state.hasSounded)
            {
                // 빠르게 닿아서 실제 note on이 발생한 경우에는
                // 기존처럼 pseudo-haptic 하강이 끝까지 채워지도록 유지한다.
                state.pressAmount = Mathf.MoveTowards(
                    state.pressAmount,
                    1f,
                    state.pressFillSpeed * Time.deltaTime
                );

                // 그래도 실제 접촉 기반 최소 눌림보다 작아지지 않게 보장.
                state.pressAmount = Mathf.Max(state.pressAmount, contactPressAmount);
            }
            else
            {
                // 느리게 닿은 경우에는 소리는 내지 않고,
                // 실제 접촉 깊이에 따른 최소 눌림만 보여준다.
                state.pressAmount = contactPressAmount;
            }

            state.targetAngle = GetMaxAngle(data) * state.pressAmount;
        }
    }


    private float CalculateContactPressAmount(VRKeyState state, VRKeyData data)
    {
        if (state == null || data == null) return 0f;

        state.EnsureArrays();

        float amount = 0f;

        for (int f = 0; f < 10; f++)
        {
            if (!state.pressingFingers[f])
            {
                continue;
            }

            if (handTracker == null || handTracker.FingerData == null)
            {
                continue;
            }

            VRFingerData finger = handTracker.FingerData[f];

            if (finger == null || !finger.isTracked)
            {
                // Occlusion hold 중에는 기존 눌림량을 유지한다.
                amount = Mathf.Max(amount, state.pressAmount);
                continue;
            }

            float depth = data.restTopLocalY + VRPianoConst.ContactTolerance - finger.localPos.y;
            float depth01 = Mathf.Clamp01(depth / VRPianoConst.PressDepthForMaxAngle);

            if (depth >= 0f)
            {
                amount = Mathf.Max(
                    amount,
                    Mathf.Max(VRPianoConst.TouchPressMin, depth01)
                );
            }
        }

        return Mathf.Clamp01(amount);
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

        bool shouldStopAudio = state.hasSounded;

        state.pressState = VRKeyPressState.Released;
        state.occupiedByFinger = -1;
        state.targetAngle = 0f;
        state.pressAmount = 0f;
        state.pressFillSpeed = 0f;
        state.impactSpeed = 0f;
        state.rawImpactSpeed = 0f;
        state.effectiveImpactSpeed = 0f;
        state.contactPosition01 = 1f;
        state.positionImpactFactor = 1f;
        state.contactStartTime = 0f;
        state.hasSounded = false;
        state.pressedFingerCount = 0;

        state.EnsureArrays();

        for (int i = 0; i < 10; i++)
        {
            state.pressingFingers[i] = false;
            state.occlusionHoldStartByFinger[i] = 0f;
        }

        if (shouldStopAudio)
        {
            pianoManager?.OnRelease(keyIdx);
        }

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