using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PianoManager))]
public class PianoManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        PianoManager manager = (PianoManager)target;

        if (GUILayout.Button("Auto Fill Key Visuals (Find In Scene)"))
        {
            AutoFillKeyVisuals(manager);
        }
    }

    private void AutoFillKeyVisuals(PianoManager manager)
    {
        if (manager == null)
            return;

        // 씬 전체에서 PianoKeyVisual 찾기
        PianoKeyVisual[] found = Object.FindObjectsByType<PianoKeyVisual>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (found == null || found.Length == 0)
        {
            Debug.LogWarning("씬 전체에서 PianoKeyVisual을 하나도 찾지 못했습니다.");
            return;
        }

        List<PianoKeyVisual> visuals = new List<PianoKeyVisual>(found);

        // 왼쪽 -> 오른쪽 순으로 정렬
        visuals.Sort((a, b) =>
        {
            float ax = GetWorldCenterX(a.transform);
            float bx = GetWorldCenterX(b.transform);
            return ax.CompareTo(bx);
        });

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty keyVisualsProp = so.FindProperty("keyVisuals");
        SerializedProperty layoutConfigProp = so.FindProperty("layoutConfig");

        so.Update();
        keyVisualsProp.arraySize = visuals.Count;

        for (int i = 0; i < visuals.Count; i++)
        {
            keyVisualsProp.GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);

        int layoutCount = -1;
        if (layoutConfigProp != null && layoutConfigProp.objectReferenceValue != null)
        {
            PianoLayoutConfig config = layoutConfigProp.objectReferenceValue as PianoLayoutConfig;
            if (config != null && config.keys != null)
                layoutCount = config.keys.Count;
        }

        if (layoutCount >= 0 && layoutCount != visuals.Count)
        {
            Debug.LogWarning(
                $"Auto Fill 완료. 개수 불일치: layoutConfig.keys = {layoutCount}, found visuals = {visuals.Count}"
            );
        }
        else
        {
            Debug.Log($"Auto Fill 완료: {visuals.Count}개 연결됨");
        }

        for (int i = 0; i < visuals.Count; i++)
        {
            Debug.Log($"keyVisuals[{i}] = {visuals[i].name}");
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