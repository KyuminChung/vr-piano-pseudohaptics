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

    private enum Phase { 
        Guide,      // 손 위치를 맞추도록 안내 중 
        Detecting,  // 조건이 만족되어 카운트다운 진행 중
        Placed      // 피아노 배치 완료
        }
    
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

    private void PositionGuideCanvas()  // 안내 UI를 항상 사용자 시야 앞에 배치
    {
        if (guideCanvas == null || cameraTransform == null) return;
        // 카메라 위치 +카메라 정면 방향 1.5m, -카메라 위 방향 0.15m
        guideCanvas.position = cameraTransform.position +
                               cameraTransform.forward * 1.5f +
                               cameraTransform.up * -0.15f;
        guideCanvas.rotation = cameraTransform.rotation;
    }

    private void ShowCountdown(float elapsed)   // 손 위치 조건이 만족되었을 때 카운트다운을 표시
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        // 남은 시간 = holdTime - elapsed, 소수점은 올림 처리, 최소 1 표시
        countdownText.text = Mathf.Max(Mathf.CeilToInt(holdTime - elapsed), 1).ToString();
        countdownText.rectTransform.anchoredPosition = new Vector2(0f, -80f);
    }

    private void HideCountdown()    // 손 위치 조건이 꺼졌을 때 카운트다운 텍스트 숨김
    {
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void PlacePiano()   // 피아노를 실제로 생성하고, 위치 보정 후 전체 시스템을 초기화
    {
        _phase   = Phase.Placed;    
        IsPlaced = true;    // 피아노가 배치 완료 상태가 되어 이후 Update()에서 더 이상 배치 로직을 실행하지 않음

        // 피아노의 X/Z 위치는 손끝 중심을 기준으로 하고, Y 위치는 손끝 평균 높이를 기준으로 맞춤
        Vector3 center  = handTracker.GetFingertipCenter();     // 손끝 10개의 중심 위치
        float   fingerY = handTracker.GetFingertipAverageY();   // 손끝 10개의 평균 Y 높이
        // 사용자가 바라보는 방향을 기준으로 피아노 방향을 정함
        Vector3 facing = cameraTransform.forward;
        facing.y = 0f;  // 수평 방향만 사용
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
        facing.Normalize();
        // 카메라가 바라보는 수평 방향을 기준으로 피아노를 회전시킴 (피아노 모델 방향이 반대로 되어 있으면 rotationOffset = 180f)
        Quaternion spawnRot = Quaternion.LookRotation(facing) *
                              Quaternion.Euler(0f, rotationOffset, 0f);
        // 피아노 기본 위치 계산: 손끝 중심에서 카메라 방향 기준 zOffset만큼 이동시킨 위치
        Vector3 basePos = center - facing * zOffset;

        // Y=0으로 임시 spawn, 생성 후 VRLayoutInitializer.RestTopY를 읽어서, 실제 건반 표면 높이에 맞춰 다시 Y를 보정
        SpawnedPiano = Instantiate(pianoPrefab,
            new Vector3(basePos.x, 0f, basePos.z), spawnRot);

        // LayoutInitializer 가져오기
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
        handTracker.pianoParent = SpawnedPiano.transform;   // 손가락 위치를 피아노 기준 local 좌표로 변환하기 위해 피아노 Transform을 전달

        // 각 시스템 Initialize
        // 피아노 매니저에 VRLayoutInitializer를 전달해서 88건반 데이터를 연결
        if (pianoManager == null)
            pianoManager = FindFirstObjectByType<VRPianoManager>();
        if (pianoManager != null)
            pianoManager.Initialize(initializer);
        else
            Debug.LogError("[Placement] VRPianoManager 없음");
        // 입력 판정 시스템에 필요한 세 가지를 연결
        if (keyInputJudge == null)
            keyInputJudge = FindFirstObjectByType<VRKeyInputJudge>();
        if (keyInputJudge != null)
            keyInputJudge.Initialize(handTracker, initializer, pianoManager);
        else
            Debug.LogError("[Placement] VRKeyInputJudge 없음");
        // 건반 애니메이터에 건반 데이터와 입력 판정기를 연결
        if (hingeAnimator == null)
            hingeAnimator = FindFirstObjectByType<VRHingeAnimator>();
        if (hingeAnimator != null)
            hingeAnimator.Initialize(initializer, keyInputJudge);
        else
            Debug.LogError("[Placement] VRHingeAnimator 없음");
        // 피아노 배치가 끝났으므로 안내 UI와 카운트다운 텍스트를 숨깁
        if (guideUI != null)       guideUI.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        Debug.Log($"[Placement] 완료 pos={SpawnedPiano.transform.position} " +
                  $"fingerY={fingerY:F3} RestTopY={initializer.RestTopY:F3} correctedY={correctedY:F3}");
    }
}