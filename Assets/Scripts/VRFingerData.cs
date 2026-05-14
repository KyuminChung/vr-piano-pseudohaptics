using UnityEngine;

public class VRFingerData
{
    // 0~4: 왼손 엄지~소지, 5~9: 오른손 엄지~소지
    public int fingerId;

    public Vector3 worldPos;
    public Vector3 localPos;

    // 수직 속도 (m/s). 음수=하강, 양수=상승
    public float velocityY;

    public bool isTracked;

    // 현재 바인딩된 건반 인덱스. -1=없음
    public int boundKeyIndex = -1;

    private float _prevWorldY;
    private bool  _hasPrev;

    public void UpdateWorldPos(Vector3 newWorldPos, bool tracked)
    {
        isTracked = tracked;
        worldPos  = newWorldPos;

        if (_hasPrev && tracked)
            velocityY = (newWorldPos.y - _prevWorldY) / Time.deltaTime;
        else
            velocityY = 0f;

        _prevWorldY = newWorldPos.y;
        _hasPrev    = tracked;
    }

    public void Reset()
    {
        worldPos      = Vector3.zero;
        localPos      = Vector3.zero;
        velocityY     = 0f;
        isTracked     = false;
        boundKeyIndex = -1;
        _hasPrev      = false;
    }
}