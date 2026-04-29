using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class PianoFrameAutoPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform whiteKeysRoot;
    [SerializeField] private Transform pianoFrame;

    [Header("Options")]
    [SerializeField] private float yOffset = 0f;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool placeNow = false;

    private void OnValidate()
    {
        if (!placeNow)
            return;

        placeNow = false;
        PlacePianoFrame();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        if (pianoFrame != null)
            EditorUtility.SetDirty(pianoFrame);
#endif
    }

    private void PlacePianoFrame()
    {
        if (whiteKeysRoot == null)
        {
            Debug.LogWarning("whiteKeysRoot가 비어 있습니다.");
            return;
        }

        if (pianoFrame == null)
        {
            Debug.LogWarning("pianoFrame이 비어 있습니다.");
            return;
        }

        Renderer[] renderers = whiteKeysRoot.GetComponentsInChildren<Renderer>(includeInactive);

        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("WhiteKeys 아래에서 Renderer를 찾지 못했습니다.");
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        // 흰건반 전체의 윗면 중앙 (월드 좌표)
        Vector3 worldTopCenter = new Vector3(
            combinedBounds.center.x,
            combinedBounds.max.y + yOffset,
            combinedBounds.center.z
        );

        pianoFrame.position = worldTopCenter;

        // 피아노 전체와 같은 회전 사용
        pianoFrame.rotation = transform.rotation;
        pianoFrame.localScale = Vector3.one;

        Debug.Log(
            $"PianoFrame 자동 배치 완료\n" +
            $"World Pos: {pianoFrame.position}\n" +
            $"Bounds Center: {combinedBounds.center}\n" +
            $"Bounds MaxY: {combinedBounds.max.y}"
        );
    }
}