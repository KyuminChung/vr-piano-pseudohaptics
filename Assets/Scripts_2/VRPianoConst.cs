using UnityEngine;

public static class VRPianoConst
{
    // ── Zone 후보 판정 ─────────────────────────────
    public const float ProximityHeight = 0.08f;

    // 지금 ZoneMargin은 0으로 두는 게 안정적
    public const float ZoneMarginX = 0f;
    public const float ZoneMarginZ = 0f;

    // ── 접촉 / 유지 / 해제 ─────────────────────────
    public const float ContactTolerance = 0.003f; // 접촉 시작
    public const float HoldTolerance = 0.008f;    // 접촉 유지
    public const float ReleaseMargin = 0.015f;    // 손 뗌

    // ── 접촉 전 속도 기반 pseudo-haptic ────────────
    public const float PreContactVelocityWindow = 0.12f; // 최근 120ms
    public const float ImpactSpeedForMax = 1.5f;         // m/s 기준 정규화

    public const float InitialPressMin = 0.15f;
    public const float InitialPressMax = 0.75f;

    public const float FillSpeedMin = 0.8f;
    public const float FillSpeedMax = 5.0f;

    // ── 후보 억제 점수 가중치 ─────────────────────
    public const float CandidateDepthWeight = 0.40f;
    public const float CandidateVelocityWeight = 0.35f;
    public const float CandidateCenterWeight = 0.20f;
    public const float CandidateBlackBonus = 0.05f;

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

    // 기존 depth 모델에서 쓰던 값이 남아 있으면 컴파일 호환용으로 유지
    public const float PressDepthForMaxAngle = 0.035f;
}