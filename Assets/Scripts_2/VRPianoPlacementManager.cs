using UnityEngine;
using TMPro;

public class VRPianoPlacementManager : MonoBehaviour
{
    [Header("References")]
    public VRHandTracker   handTracker;
    public GameObject      pianoPrefab;
    public Transform       cameraTransform;

    [Header("Runtime Systems")]
    public VRKeyInputJudge keyInputJudge;
    public VRPianoManager  pianoManager;
    public VRHingeAnimator hingeAnimator;

    [Header("UI")]
    public GameObject      guideUI;
    public TextMeshProUGUI countdownText;
    public RectTransform   guideCanvas;

    [Header("Tuning")]
    public float yTolerance   = 0.02f;
    public float holdTime     = 3f;
    public float pianoYOffset = 0f;

    [Header("Placement Offset")]
    [Tooltip("피아노 회전 보정. 방향 반대면 180")]
    public float rotationOffset = 180f;
    [Tooltip("앞뒤 보정 (m). 양수=카메라 방향")]
    public float zOffset        = 0.3f;

    public GameObject SpawnedPiano { get; private set; }
    public bool       IsPlaced     { get; private set; }

    private enum Phase { Guide, Detecting, Placed }
    private Phase _phase     = Phase.Guide;
    private float _holdTimer = 0f;

    private void Start()
    {
        if (guideUI != null)       guideUI.SetActive(true);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        PositionGuideCanvas();
    }

    private void Update()
    {
        if (_phase == Phase.Placed) return;

        PositionGuideCanvas();

        bool conditionMet = handTracker.AreAllFingersTracked() &&
                            handTracker.GetFingertipYVariance() < yTolerance;

        if (conditionMet)
        {
            _phase      = Phase.Detecting;
            _holdTimer += Time.deltaTime;
            ShowCountdown(_holdTimer);

            if (_holdTimer >= holdTime)
                PlacePiano();
        }
        else
        {
            _phase     = Phase.Guide;
            _holdTimer = 0f;
            HideCountdown();
        }
    }

    private void PositionGuideCanvas()
    {
        if (guideCanvas == null || cameraTransform == null) return;
        guideCanvas.position = cameraTransform.position +
                               cameraTransform.forward * 1.5f +
                               cameraTransform.up * -0.15f;
        guideCanvas.rotation = cameraTransform.rotation;
    }

    private void ShowCountdown(float elapsed)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = Mathf.Max(Mathf.CeilToInt(holdTime - elapsed), 1).ToString();
        countdownText.rectTransform.anchoredPosition = new Vector2(0f, -80f);
    }

    private void HideCountdown()
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void PlacePiano()
    {
        _phase   = Phase.Placed;
        IsPlaced = true;

        Vector3 center  = handTracker.GetFingertipCenter();
        float   fingerY = handTracker.GetFingertipAverageY();

        Vector3 facing = cameraTransform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
        facing.Normalize();

        Quaternion spawnRot = Quaternion.LookRotation(facing) *
                              Quaternion.Euler(0f, rotationOffset, 0f);

        Vector3 basePos = center - facing * zOffset;

        // Y=0으로 임시 spawn
        SpawnedPiano = Instantiate(pianoPrefab,
            new Vector3(basePos.x, 0f, basePos.z), spawnRot);

        // LayoutInitializer 가져오기 (Awake에서 이미 1회 실행됨)
        var initializer = SpawnedPiano.GetComponentInChildren<VRLayoutInitializer>();
        if (initializer == null)
        {
            Debug.LogError("[Placement] VRLayoutInitializer 없음");
            return;
        }

        // Y 보정: 건반 표면이 손가락 높이에 오도록
        float correctedY = fingerY - initializer.RestTopY + pianoYOffset;
        SpawnedPiano.transform.position = new Vector3(basePos.x, correctedY, basePos.z);

        // HandTracker pianoParent 세팅
        handTracker.pianoParent = SpawnedPiano.transform;

        // 각 시스템 Initialize
        if (pianoManager == null)
            pianoManager = FindFirstObjectByType<VRPianoManager>();
        if (pianoManager != null)
            pianoManager.Initialize(initializer);
        else
            Debug.LogError("[Placement] VRPianoManager 없음");

        if (keyInputJudge == null)
            keyInputJudge = FindFirstObjectByType<VRKeyInputJudge>();
        if (keyInputJudge != null)
            keyInputJudge.Initialize(handTracker, initializer, pianoManager);
        else
            Debug.LogError("[Placement] VRKeyInputJudge 없음");

        if (hingeAnimator == null)
            hingeAnimator = FindFirstObjectByType<VRHingeAnimator>();
        if (hingeAnimator != null)
            hingeAnimator.Initialize(initializer, keyInputJudge);
        else
            Debug.LogError("[Placement] VRHingeAnimator 없음");

        if (guideUI != null)       guideUI.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        Debug.Log($"[Placement] 완료 pos={SpawnedPiano.transform.position} " +
                  $"fingerY={fingerY:F3} RestTopY={initializer.RestTopY:F3} correctedY={correctedY:F3}");
    }
}