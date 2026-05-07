using UnityEngine;

[System.Serializable]
public class PianoKeyData
{
    [Header("Identity")]
    public int keyIndex;
    public string keyName;
    public bool isBlackKey;

    [Header("Piano Local Area")]
    public float minX;
    public float maxX;
    public float minZ;
    public float maxZ;
    public float topY = 0f;

    [Header("Audio")]
    public AudioClip noteClip;
}