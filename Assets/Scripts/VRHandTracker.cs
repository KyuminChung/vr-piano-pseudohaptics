using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class VRHandTracker : MonoBehaviour
{
    public VRFingerData[] FingerData { get; private set; }  // 손가락 10개의 데이터를 저장하는 배열
    
    public Transform pianoParent { get; set; }  // 손끝 world position을 피아노 기준 local position으로 바꾸기 위한 기준 Transform

    private XRHandSubsystem _subsystem; // Unity XR Hands의 손 추적 서브시스템, 왼손/오른손의 joint 정보를 가져옴

    private static readonly XRHandJointID[] TipJoints = new[]   // 추적할 손끝 관절 목록
    {
        XRHandJointID.ThumbTip,
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip,
    };

    private void Awake()
    {
        FingerData = new VRFingerData[10];      // VRFingerData 배열을 만들고, 손가락 10개 데이터를 초기화
        for (int i = 0; i < 10; i++)
            FingerData[i] = new VRFingerData { fingerId = i };
    }

    private void OnEnable()     // XR Hands의 XRHandSubsystem을 찾고, 손 데이터 갱신 이벤트에 등록
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            _subsystem = subsystems[0];

        if (_subsystem != null)
            _subsystem.updatedHands += OnUpdatedHands;  // 손 추적 데이터가 갱신될 때마다 OnUpdatedHands()가 자동으로 호출
        else
            Debug.LogWarning("[VRHandTracker] XRHandSubsystem을 찾지 못했습니다.");
    }

    private void OnDisable()    // 오브젝트가 비활성화될 때 updatedHands 이벤트 등록을 해제
    {
        if (_subsystem != null)
            _subsystem.updatedHands -= OnUpdatedHands;
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem,  // XR Hands 데이터가 갱신될 때 호출되는 콜백
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)   
            return;

        UpdateHand(subsystem.leftHand,  isLeft: true);  // 왼손 데이터 갱신
        UpdateHand(subsystem.rightHand, isLeft: false); // 오른손 데이터 갱신
    }

    private void UpdateHand(XRHand hand, bool isLeft)   // 한 손의 손끝 5개 위치를 읽어서 FingerData에 저장
    {
        int baseId = isLeft ? 0 : 5;    // 왼손/오른손 여부에 따라 fingerId 시작값 결정

        for (int i = 0; i < TipJoints.Length; i++)  // ThumbTip ~ LittleTip 순서로 joint 확인
        {
            int fingerId = baseId + i;
            var jointId  = TipJoints[i];    // 현재 손가락에 해당하는 XR Hands 관절 ID를 가져옴

            Pose pose    = default;     // 손끝 관절의 위치와 회전 정보를 담을 Pose 변수를 기본값으로 생성
            bool tracked = hand.isTracked &&
                           hand.GetJoint(jointId).TryGetPose(out pose);     // 현재 손가락 손끝이 실제로 추적되고 있는지 확인

            Vector3 worldPos = tracked ? pose.position : FingerData[fingerId].worldPos; // tracked가 false이면 이전 프레임의 worldPos 유지

            FingerData[fingerId].UpdateWorldPos(worldPos, tracked); // 현재 손가락 데이터에 월드 위치와 추적 상태를 저장

            if (pianoParent != null)
                FingerData[fingerId].localPos =
                    pianoParent.InverseTransformPoint(worldPos);    // 손끝의 월드 좌표를 피아노 기준 로컬 좌표로 변환
        }
    }

    public bool AreAllFingersTracked()  // 손끝 10개가 모두 추적 중인지 확인(피아노 배치 조건)
    {
        foreach (var f in FingerData)
            if (!f.isTracked) return false;
        return true;
    }

    public float GetFingertipYVariance()    // 손끝 10개의 Y 높이 차이를 계산(피아노 배치 조건)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (var f in FingerData)
        {
            if (!f.isTracked) return float.MaxValue;
            if (f.worldPos.y < min) min = f.worldPos.y;
            if (f.worldPos.y > max) max = f.worldPos.y;
        }

        return max - min;
    }

    public Vector3 GetFingertipCenter() // 추적 중인 손끝들의 중심 위치를 계산(피아노 배치 위치)
    {
        Vector3 sum = Vector3.zero;
        foreach (var f in FingerData)
            sum += f.worldPos;
        return sum / 10f;
    }

    public float GetFingertipAverageY() // 추적 중인 손끝들의 평균 Y 높이(피아노 배치 위치)
    {
        float sum = 0f;
        foreach (var f in FingerData)
            sum += f.worldPos.y;
        return sum / 10f;
    }
}