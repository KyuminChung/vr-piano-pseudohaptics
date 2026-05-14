using UnityEngine;

public class VRFingerData
{
    // 0 ~ 4: 왼손 엄지 ~ 소지, 5 ~ 9: 오른손 엄지 ~ 소지
    public int fingerId;

    public Vector3 worldPos;    // XR Hands에서 가져온 실제 손끝 위치
    public Vector3 localPos;    // 피아노 기준 로컬 좌표 -> 건반 zone 판정

    // 수직 속도 (m/s). 음수=하강, 양수=상승
    public float velocityY;

    // 현재 손가락이 XR Hands에서 정상적으로 추적되고 있는지 여부
    public bool isTracked;

    // 현재 이 손가락이 누르고 있는 건반 번호. -1 = 없음
    public int boundKeyIndex = -1;

    private float _prevWorldY;  // 이전 프레임의 손끝 Y 위치
    private bool  _hasPrev; // 이전 위치값이 존재하는지 확인하는 변수

    public void UpdateWorldPos(Vector3 newWorldPos, bool tracked)   // 새로운 손끝 위치와 추적 상태를 받아서 worldPos, isTracked, velocityY를 갱신
    {
        isTracked = tracked;
        worldPos  = newWorldPos;

        if (_hasPrev && tracked)
            velocityY = (newWorldPos.y - _prevWorldY) / Time.deltaTime;     // Y축 속도를 계산
            // 손가락이 아래로 내려가면 newWorldPos.y가 이전보다 작아지므로 velocityY는 음수가 됩
        else    // 이전 위치가 없거나 추적 중이 아니면 속도를 0으로 처리
            velocityY = 0f;

        _prevWorldY = newWorldPos.y;    // 현재 Y 위치를 다음 프레임에서 사용할 이전 위치로 저장
        _hasPrev    = tracked;      // 현재 추적에 성공했다면 다음 프레임에서 속도 계산이 가능하도록 표시,
        // 추적에 실패했다면 다음 프레임에는 이전 위치를 사용하지 않음
    }

    public void Reset() // 손가락 데이터를 초기 상태로 되돌리는 메소드
    {
        worldPos      = Vector3.zero;
        localPos      = Vector3.zero;
        velocityY     = 0f;
        isTracked     = false;
        boundKeyIndex = -1;
        _hasPrev      = false;
    }
}