using UnityEngine;
using TMPro;

public class VRPianoPlacementManager : MonoBehaviour
{
    [Header("References")]
    public VRHandTracker   handTracker;     // 손끝 10개 위치와 추적 상태를 제공하는 코드
    public GameObject      pianoPrefab;     // 배치할 피아노 프리팹  
    public Transform       cameraTransform; // 사용자의 시선 방향과 위치를 얻기 위한 카메라 Transform

    [Header("Runtime Systems")]
    public VRKeyInputJudge keyInputJudge;   // 손가락 위치와 속도로 건반 입력을 판정하는 시스템
    public VRPianoManager  pianoManager;    // 입력 이벤트와 오디오 재생을 연결하는 관리자
    public VRHingeAnimator hingeAnimator;   // 건반 회전 애니메이션을 담당하는 시스템

    [Header("UI")]
    public GameObject      guideUI;         // 사용자에게 손 위치를 맞추라고 안내하는 UI
    public TextMeshProUGUI countdownText;   // 손 위치가 안정되었을 때 표시되는 카운트다운 텍스트
    public RectTransform   guideCanvas;     // 안내 UI를 카메라 앞에 배치하기 위한 Canvas

    [Header("Tuning")]
    public float yTolerance   = 0.02f;      // 손끝 10개의 높이 차이가 이 값보다 작아야 배치 조건 만족
    public float holdTime     = 3f;         // 배치 조건을 유지해야 하는 시간
    public float pianoYOffset = 0f;         // 피아노 배치 후 Y축 추가 보정값

    [Header("Placement Offset")]
    [Tooltip("피아노 회전 보정. 방향 반대면 180")]
    public float rotationOffset = 180f;     // 피아노 방향 보정값.
    [Tooltip("앞뒤 보정 (m). 양수=카메라 방향")]
    public float zOffset        = 0.3f;     // 손 위치 기준으로 피아노를 앞뒤로 얼마나 이동할지 결정, 카메라가 바라보는 방향 기준으로 적용

    public GameObject SpawnedPiano { get; private set; }    // 실제로 생성된 피아노 오브젝트
    public bool       IsPlaced     { get; private set; }    // 피아노 배치 완료 여부

    private enum Phase { Guide, Detecting, Placed }
    //Guide: 손 위치를 맞추도록 안내 중 
    //Detecting: 조건이 만족되어 카운트다운 진행 중
    //Placed: 피아노 배치 완료
    private Phase _phase     = Phase.Guide;
    private float _holdTimer = 0f;          // 손 위치 조건이 유지된 시간

    private void Start()
    {
        if (guideUI != null)       guideUI.SetActive(true);     // 시작 시 guideUI를 킴
        if (countdownText != null) countdownText.gameObject.SetActive(false);   // countdownText는 숨김
        PositionGuideCanvas();  // guideCanvas를 카메라 앞에 배치
    }

    private void Update()
    {
        if (_phase == Phase.Placed) return;     // 이미 배치 완료면 return

        PositionGuideCanvas();  // 안내 UI를 카메라 앞에 유지

        bool conditionMet = handTracker.AreAllFingersTracked() &&   // 손끝 10개 추적 확인
                            handTracker.GetFingertipYVariance() < yTolerance;   // 손끝 높이 차이 확인

        if (conditionMet)   // 조건 만족
        {
            _phase      = Phase.Detecting;  // Detecting 상태
            _holdTimer += Time.deltaTime;   // holdTimer 증가
            ShowCountdown(_holdTimer);      // countdown 표시

            if (_holdTimer >= holdTime)     // holdTime 이상 유지되면 PlacePiano()
                PlacePiano();
        }
        else                // 조건 불만족
        {
            _phase     = Phase.Guide;       // Guide 상태
            _holdTimer = 0f;                // holdTimer 초기화
            HideCountdown();                // countdown 숨김
        }
    }

    private void PositionGuideCanvas()  //안내 UI를 항상 사용자 시야 앞에 배치
    {
        if (guideCanvas == null || cameraTransform == null) return;
        // 카메라 위치 +카메라 정면 방향 1.5m, -카메라 위 방향 0.15m
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