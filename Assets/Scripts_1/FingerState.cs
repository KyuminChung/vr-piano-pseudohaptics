using UnityEngine;

[System.Serializable]
public class FingerState
{
    [Header("World")]
    public Vector3 worldPos;
    public Vector3 prevWorldPos;
    public Vector3 velocityWorld;
    public bool isTracked;

    [Header("Piano Local")]
    public Vector3 localPos;

    [Header("Runtime")]
    public int currentKeyIndex = -1;
}