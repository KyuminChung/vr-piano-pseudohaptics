using UnityEngine;

[CreateAssetMenu(fileName = "PianoSettings", menuName = "Piano/Piano Settings")]
public class PianoSettings : ScriptableObject
{
    [Header("Finger Margin")]
    public float marginX = 0.002f;
    public float marginZ = 0.002f;

    [Header("Depth")]
    public float pressDepth = 0.005f;
    public float releaseDepth = 0.0015f;
    public float maxDepth = 0.02f;

    [Header("Debounce")]
    public int releaseFramesRequired = 2;   // 2~3 추천
    public float repressCooldown = 0.08f;   // 80ms
    public float holdMarginX = 0.004f;      // hold 중엔 범위 조금 넓게
    public float holdMarginZ = 0.004f;

    [Header("Velocity -> Volume")]
    public float minVelocityForSound = 0.02f;
    public float maxVelocityForSound = 0.50f;
    public float minVolume = 0.2f;
    public float maxVolume = 1.0f;

    [Header("Debug")]
    public bool showDebugLog = false;
}