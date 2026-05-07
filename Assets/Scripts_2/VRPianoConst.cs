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

    // 너무 작은 노이즈 속도로 press 되는 것 방지
    // 가만히 닿아도 눌리면 0.15~0.18로 올리기
    // 특정 손가락이 잘 안 눌리면 0.07~0.09로 낮추기
    public const float MinImpactSpeedToPress = 0.07f;

    // ── 접촉 전 속도 기반 pseudo-haptic ────────────
    public const float PreContactVelocityWindow = 0.15f;

    // 이 속도 이상이면 impact01 = 1
    public const float ImpactSpeedForMax = 0.65f;

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