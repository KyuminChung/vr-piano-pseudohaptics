using UnityEngine;

[CreateAssetMenu(fileName = "PianoSettings", menuName = "Piano/Piano Settings")]
public class PianoSettings : ScriptableObject
{
    [Header("Finger Margin")]
    public float marginX = 0.002f;
    public float marginZ = 0.002f;

    [Header("Depth")]
    public float pressDepth = 0.003f;
    public float releaseDepth = 0.001f;
    public float maxDepth = 0.02f;

    [Header("Velocity -> Volume")]
    public float minVelocityForSound = 0.02f;
    public float maxVelocityForSound = 0.50f;
    public float minVolume = 0.2f;
    public float maxVolume = 1.0f;

    [Header("Debug")]
    public bool showDebugLog = false;
}