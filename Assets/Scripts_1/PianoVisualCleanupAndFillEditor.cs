using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PianoVisualCleanupAndFillEditor : EditorWindow
{
    private Transform whiteKeysRoot;
    private Transform blackKeysRoot;
    private PianoManager pianoManager;

    [MenuItem("Tools/Piano/Cleanup Visuals And Fill Manager")]
    public static void ShowWindow()
    {
        GetWindow<PianoVisualCleanupAndFillEditor>("Piano Visual Fix");
    }

    private void OnGUI()
    {
        GUILayout.Label("References", EditorStyles.boldLabel);

        pianoManager = (PianoManager)EditorGUILayout.ObjectField(
            "Piano Manager", pianoManager, typeof(PianoManager), true);

        whiteKeysRoot = (Transform)EditorGUILayout.ObjectField(
            "White Keys Root", whiteKeysRoot, typeof(Transform), true);

        blackKeysRoot = (Transform)EditorGUILayout.ObjectField(
            "Black Keys Root", blackKeysRoot, typeof(Transform), true);

        GUILayout.Space(10);

        if (GUILayout.Button("1. Remove PianoKeyVisual From _Visual Objects"))
        {
            RemoveVisualComponentsFromVisualMeshes();
        }

        if (GUILayout.Button("2. Fill PianoManager.keyVisuals From Key Roots"))
        {
            FillManagerKeyVisuals();
        }

        if (GUILayout.Button("Do All"))
        {
            RemoveVisualComponentsFromVisualMeshes();
            FillManagerKeyVisuals();
        }
    }

    private void RemoveVisualComponentsFromVisualMeshes()
    {
        PianoKeyVisual[] all = Object.FindObjectsByType<PianoKeyVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int removedCount = 0;

        foreach (var visual in all)
        {
            if (visual == null)
                continue;

            string objName = visual.gameObject.name;

            // *_Visual 에 붙은 건 제거
            if (objName.EndsWith("_Visual"))
            {
                Undo.DestroyObjectImmediate(visual);
                removedCount++;
            }
        }

        Debug.Log($"삭제 완료: _Visual 오브젝트에서 PianoKeyVisual {removedCount}개 제거");
    }

    private void FillManagerKeyVisuals()
    {
        if (pianoManager == null)
        {
            Debug.LogWarning("PianoManager를 넣어주세요.");
            return;
        }

        if (whiteKeysRoot == null && blackKeysRoot == null)
        {
            Debug.LogWarning("WhiteKeysRoot / BlackKeysRoot 중 하나 이상 넣어주세요.");
            return;
        }

        List<PianoKeyVisual> visuals = new List<PianoKeyVisual>();

        CollectRootKeyVisuals(whiteKeysRoot, visuals);
        CollectRootKeyVisuals(blackKeysRoot, visuals);

        // 좌 -> 우 정렬
        visuals.Sort((a, b) =>
        {
            float ax = GetWorldCenterX(a.transform);
            float bx = GetWorldCenterX(b.transform);
            return ax.CompareTo(bx);
        });

        SerializedObject so = new SerializedObject(pianoManager);
        SerializedProperty keyVisualsProp = so.FindProperty("keyVisuals");

        so.Update();
        keyVisualsProp.arraySize = visuals.Count;

        for (int i = 0; i < visuals.Count; i++)
        {
            keyVisualsProp.GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pianoManager);

        Debug.Log($"PianoManager.keyVisuals 채움 완료: {visuals.Count}개");
    }

    private void CollectRootKeyVisuals(Transform root, List<PianoKeyVisual> result)
    {
        if (root == null)
            return;

        // 직계 자식만 key root로 간주
        for (int i = 0; i < root.childCount; i++)
        {
            Transform keyRoot = root.GetChild(i);
            PianoKeyVisual visual = keyRoot.GetComponent<PianoKeyVisual>();

            if (visual != null)
            {
                result.Add(visual);
            }
            else
            {
                Debug.LogWarning($"{keyRoot.name} 에 PianoKeyVisual이 없습니다.");
            }
        }
    }

    private float GetWorldCenterX(Transform tr)
    {
        Renderer[] renderers = tr.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
            return tr.position.x;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center.x;
    }
}