using UnityEngine;

public class VRKeyData
{
    // ── Identity ───────────────────────────────────────────
    public int       keyIndex;
    public bool      isBlackKey;
    public Transform keyTransform;

    // ── Contact Zone — 피아노 부모 로컬 기준 ───────────────
    public float zoneAMinX, zoneAMaxX;
    public float zoneAMinZ, zoneAMaxZ;

    public bool  hasZoneB;
    public float zoneBMinX, zoneBMaxX;
    public float zoneBMinZ, zoneBMaxZ;

    // ── 힌지 모델 ─────────────────────────────────────────
    public float hingeLocalZ;
    public float restTopLocalY;
    public float keyLength;
    public float maxDrop;

    public float TanThetaMax
    {
        get
        {
            if (keyLength <= 0.0001f) return 0f;
            return maxDrop / keyLength;
        }
    }

    // ── Audio ──────────────────────────────────────────────
    public AudioClip noteClip;

    // ── 메서드 ─────────────────────────────────────────────
    public bool IsFingerOver(float localX, float localZ)
    {
        bool inA = localX >= zoneAMinX - VRPianoConst.ZoneMarginX &&
                   localX <= zoneAMaxX + VRPianoConst.ZoneMarginX &&
                   localZ >= zoneAMinZ - VRPianoConst.ZoneMarginZ &&
                   localZ <= zoneAMaxZ + VRPianoConst.ZoneMarginZ;
        if (inA) return true;
        if (!hasZoneB) return false;

        return localX >= zoneBMinX - VRPianoConst.ZoneMarginX &&
               localX <= zoneBMaxX + VRPianoConst.ZoneMarginX &&
               localZ >= zoneBMinZ - VRPianoConst.ZoneMarginZ &&
               localZ <= zoneBMaxZ + VRPianoConst.ZoneMarginZ;
    }

    /// <summary>
    /// 현재 회전각(degree)과 손가락 Z로 건반 표면 Y 계산
    /// dist = hingeLocalZ - fingerLocalZ
    /// surfaceY = restTopLocalY - dist * tan(currentAngleDeg)
    /// </summary>
    public float GetSurfaceY(float fingerLocalZ, float currentAngleDeg)
    {
        float dist     = hingeLocalZ - fingerLocalZ;
        float tanTheta = Mathf.Tan(currentAngleDeg * Mathf.Deg2Rad);
        return restTopLocalY - dist * tanTheta;
    }

    public string KeyName
    {
        get
        {
            string[] names = { "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#" };
            int idx    = keyIndex - 21;
            int octave = (idx + 9) / 12 + 1;
            return $"{names[idx % 12]}{octave}";
        }
    }
}