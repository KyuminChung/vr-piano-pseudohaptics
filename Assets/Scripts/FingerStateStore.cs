using UnityEngine;

public class FingerStateStore : MonoBehaviour
{
    private const int FingerCount = 10;

    [Header("Scene Reference")]
    [SerializeField] private Transform pianoFrame;

    [Header("Runtime States")]
    [SerializeField] private FingerState[] fingers = new FingerState[FingerCount];

    public Transform PianoFrame => pianoFrame;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnValidate()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (fingers == null || fingers.Length != FingerCount)
            fingers = new FingerState[FingerCount];

        for (int i = 0; i < fingers.Length; i++)
        {
            if (fingers[i] == null)
                fingers[i] = new FingerState();
        }
    }

    public FingerState GetFinger(FingerId fingerId)
    {
        return fingers[(int)fingerId];
    }

    public FingerState[] GetAllFingers()
    {
        return fingers;
    }

    public void SetPianoFrame(Transform frame)
    {
        pianoFrame = frame;
    }

    public void UpdateFinger(FingerId fingerId, Vector3 newWorldPos, bool tracked, float deltaTime)
    {
        FingerState finger = fingers[(int)fingerId];

        if (!tracked)
        {
            finger.isTracked = false;
            finger.velocityWorld = Vector3.zero;
            finger.currentKeyIndex = -1;
            return;
        }

        if (deltaTime <= 0f)
            deltaTime = Time.deltaTime;

        // World 갱신
        finger.prevWorldPos = finger.worldPos;
        finger.worldPos = newWorldPos;
        finger.velocityWorld = (finger.worldPos - finger.prevWorldPos) / deltaTime;
        finger.isTracked = true;

        // Piano Local 갱신
        if (pianoFrame != null)
        {
            finger.localPos = pianoFrame.InverseTransformPoint(finger.worldPos);         
        }
        else
        {
            finger.localPos = finger.worldPos;
        }
    }

    public void ClearAllTracking()
    {
        for (int i = 0; i < fingers.Length; i++)
        {
            fingers[i].isTracked = false;
            fingers[i].velocityWorld = Vector3.zero;           
            fingers[i].currentKeyIndex = -1;
        }
    }
}