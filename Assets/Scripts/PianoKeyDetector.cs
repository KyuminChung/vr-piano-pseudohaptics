using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PianoKeyDetector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform tipPoint;      // RightIndexTipPoint
    [SerializeField] private Transform topSurface;    // Key01 아래 만든 TopSurface
    [SerializeField] private AudioClip noteClip;      // 재생할 음원

    [Header("Active Area (Key Local)")]
    [SerializeField] private float minX = -0.011f;
    [SerializeField] private float maxX =  0.011f;
    [SerializeField] private float minZ = -0.060f;
    [SerializeField] private float maxZ =  0.060f;

    [Header("Finger Margin")]
    [SerializeField] private float marginX = 0.002f;
    [SerializeField] private float marginZ = 0.002f;

    [Header("Depth")]
    [SerializeField] private float maxDepth = 0.02f;      // depth clamp
    [SerializeField] private float pressDepth = 0.003f;   // 3mm
    [SerializeField] private float releaseDepth = 0.001f; // 1mm

    [Header("Velocity")]
    [SerializeField] private float minVelocityForSound = 0.02f;
    [SerializeField] private float maxVelocityForSound = 0.5f;

    [Header("Audio")]
    [SerializeField] private bool useVelocityVolume = true;
    [SerializeField] private float minVolume = 0.2f;
    [SerializeField] private float maxVolume = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    private AudioSource audioSource;
    private bool isPressed = false;

    private Vector3 prevPLocal;
    private bool hasPrevPLocal = false;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (tipPoint == null || topSurface == null)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        // 1. 손끝 월드 좌표
        Vector3 p_World = tipPoint.position;

        // 2. 건반 기준 로컬 좌표로 변환
        Vector3 p_Local = topSurface.InverseTransformPoint(p_World);

        // 3. 활성 영역 검사 (x = 폭, z = 길이)
        bool inside =
            p_Local.x >= (minX - marginX) && p_Local.x <= (maxX + marginX) &&
            p_Local.z >= (minZ - marginZ) && p_Local.z <= (maxZ + marginZ);

        // 4. depth 계산
        // topSurface 기준 y=0을 건반 윗면으로 가정
        float depth = Mathf.Clamp(-p_Local.y, 0f, maxDepth);

        // 5. 속도 계산 (로컬 좌표 기준)
        Vector3 velocityLocal = Vector3.zero;

        if (hasPrevPLocal)
        {
            velocityLocal = (p_Local - prevPLocal) / dt;
        }

        // 6. 눌림 방향 속도만 추출
        // 건반을 누르는 방향은 로컬 -Y
        float pressVelocity = Mathf.Max(0f, -velocityLocal.y);

        if (showDebugLog)
        {
            Debug.Log(
                $"[{gameObject.name}] " +
                $"p_Local=({p_Local.x:F4}, {p_Local.y:F4}, {p_Local.z:F4}) " +
                $"inside={inside} depth={depth:F4} " +
                $"velLocal=({velocityLocal.x:F3}, {velocityLocal.y:F3}, {velocityLocal.z:F3}) " +
                $"pressVelocity={pressVelocity:F3}"
            );
        }

        // 7. press / release 판정
        if (!isPressed && inside && depth >= pressDepth)
        {
            isPressed = true;
            PlayNote(pressVelocity);
            Debug.Log($"[{gameObject.name}] PRESS depth={depth:F4}, pressVelocity={pressVelocity:F3}");
        }
        else if (isPressed && (!inside || depth <= releaseDepth))
        {
            isPressed = false;
            Debug.Log($"[{gameObject.name}] RELEASE");
        }

        // 8. 현재 프레임 좌표 저장
        prevPLocal = p_Local;
        hasPrevPLocal = true;
    }

    private void PlayNote(float pressVelocity)
    {
        if (noteClip == null)
        {
            Debug.LogWarning($"[{gameObject.name}] noteClip이 비어 있습니다.");
            return;
        }

        float volumeScale = maxVolume;

        if (useVelocityVolume)
        {
            float t = Mathf.InverseLerp(minVelocityForSound, maxVelocityForSound, pressVelocity);
            volumeScale = Mathf.Lerp(minVolume, maxVolume, t);
        }

        audioSource.PlayOneShot(noteClip, volumeScale);
    }
}