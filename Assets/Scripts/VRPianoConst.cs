using UnityEngine;

public static class VRPianoConst
{
    // ── Zone 후보 판정 ─────────────────────────────
    public const float ProximityHeight = 0.1f;

    // Zone 여유값. 지금은 의도치 않은 오입력 방지를 위해 0 유지.
    public const float ZoneMarginX = 0f;
    public const float ZoneMarginZ = 0f;

    // ── 접촉 / 유지 / 해제 ─────────────────────────
    public const float ContactTolerance = 0.003f;
    public const float HoldTolerance = 0.008f;
    public const float ReleaseMargin = 0.015f;

    // 배치 직후 손이 이미 책상 위에 있을 때 바로 눌리지 않게 하는 arming 조건
    public const float PressArmHeight = 0.025f;

    // 닿았을 때 건반이 최소한 이 정도는 내려가 보이게 함.
    // 오디오는 이 값과 무관하며, 시각적 pseudo-haptic 반응 전용.
    public const float TouchPressMin = 0.08f;

    // 너무 작은 노이즈 속도로 소리 나는 것 방지.
    // 이제 이 값은 "건반 눌림" 조건이 아니라 "소리 발생" 조건으로만 사용함.
    // 닿았는데 소리가 너무 안 나면 0.05~0.06으로 낮추기.
    // 가볍게 스쳐도 소리가 나면 0.10~0.15로 올리기.
    public const float MinImpactSpeedToPress = 0.07f;

    // ── 접촉 전 속도 기반 pseudo-haptic ────────────
    public const float PreContactVelocityWindow = 0.15f;

    // 이 속도 이상이면 impact01 = 1
    public const float ImpactSpeedForMax = 0.65f;

    // ── 위치 기반 레버 보정 ───────────────────────
    // 실제 피아노처럼 힌지에 가까운 뒤쪽은 무겁게, 앞쪽은 가볍게 반응하도록
    // 접촉 위치에 따라 impactSpeed를 보정한다.
    public const bool UsePositionImpactWeighting = true;

    // 힌지 근처에서의 속도/힘 반영 비율. 낮을수록 뒤쪽이 더 무거워짐.
    public const float RearImpactMultiplier = 0.65f;

    // 건반 앞쪽에서의 속도/힘 반영 비율. 높을수록 앞쪽이 더 잘 눌림.
    public const float FrontImpactMultiplier = 1.25f;

    // 위치 곡선. 1은 선형, 2 이상이면 앞쪽으로 갈수록 더 급격히 가벼워짐.
    public const float PositionImpactPower = 1.0f;

    // 느린 입력은 거의 안 내려가고, 빠른 입력은 크게 내려가게
    public const float InitialPressMin = 0.05f;
    public const float InitialPressMax = 0.90f;

    // 느린 입력은 천천히, 빠른 입력은 확 내려가게
    public const float FillSpeedMin = 0.35f;
    public const float FillSpeedMax = 12.0f;

    // ── Occlusion ─────────────────────────────────
    public const float OcclusionHoldTime = 0.15f;

    // ── 건반 기본값 ───────────────────────────────
    public static float WhiteRestTopLocalY;
    public static float BlackRestTopLocalY;

    public static float WhiteHingeLocalZ;
    public static float BlackHingeLocalZ;

    public static float WhiteKeyLength;
    public static float BlackKeyLength;

    public const float WhiteMaxDrop = 0.018f;
    public const float BlackMaxDrop = 0.012f;

    // 기존 코드 호환용
    public const float PressDepthForMaxAngle = 0.035f;
}