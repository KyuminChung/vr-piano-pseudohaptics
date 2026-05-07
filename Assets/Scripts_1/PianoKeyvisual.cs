using UnityEngine;

public class PianoKeyVisual : MonoBehaviour
{
    [SerializeField] private Transform visualPivot;

    [SerializeField] private float maxPressAngle = 12f;
    [SerializeField] private float maxPressOffsetY = 0.01f;
    [SerializeField] private float pressSpeed = 100f;
    [SerializeField] private float releaseSpeed = 100f;

    private Quaternion baseLocalRotation;
    private Vector3 baseLocalPosition;

    private float currentPress01 = 0f;
    private float targetPress01 = 0f;

    private void Awake()
    {
        if (visualPivot == null)
        {
            Debug.LogWarning($"{name}: visualPivot is NULL");
            return;
        }

        baseLocalRotation = visualPivot.localRotation;
        baseLocalPosition = visualPivot.localPosition;
    }

    private void Update()
    {
        if (visualPivot == null)
            return;

        float speed = targetPress01 > currentPress01 ? pressSpeed : releaseSpeed;
        currentPress01 = Mathf.MoveTowards(currentPress01, targetPress01, speed * Time.deltaTime);

        float angle = Mathf.Lerp(0f, maxPressAngle, currentPress01);
        float offsetY = Mathf.Lerp(0f, maxPressOffsetY, currentPress01);

        visualPivot.localRotation = baseLocalRotation * Quaternion.Euler(angle, 0f, 0f);
        visualPivot.localPosition = baseLocalPosition + Vector3.down * offsetY;
    }

    public void SetPressed(bool pressed)
    {
        Debug.Log($"{name} SetPressed({pressed})");
        targetPress01 = pressed ? 1f : 0f;
    }
}