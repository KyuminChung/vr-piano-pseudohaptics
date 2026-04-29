using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PianoKeyFullFixWindow : EditorWindow
{
    private Transform editRoot;          // ex) PianoA
    private Transform whiteKeysRoot;     // ex) WhiteKeys
    private Transform blackKeysRoot;     // ex) BlackKeys
    private PianoManager pianoManager;

    private string pivotName = "KeyVisualPivot";
    private bool autoUnpackPrefabRoot = true;

    private float whiteMaxPressAngle = 12f;
    private float whiteMaxPressOffsetY = 0.01f;
    private float blackMaxPressAngle = 10f;
    private float blackMaxPressOffsetY = 0.008f;
    private float pressSpeed = 100f;
    private float releaseSpeed = 100f;
    private bool rotateAroundLocalX = true;
    private bool invertRotation = false;

    [MenuItem("Tools/Piano/Fix Key Visual Hierarchy + Fill Manager")]
    public static void ShowWindow()
    {
        GetWindow<PianoKeyFullFixWindow>("Piano Key Fix");
    }

    private void OnGUI()
    {
        GUILayout.Label("References", EditorStyles.boldLabel);

        editRoot = (Transform)EditorGUILayout.ObjectField("Edit Root", editRoot, typeof(Transform), true);
        whiteKeysRoot = (Transform)EditorGUILayout.ObjectField("White Keys Root", whiteKeysRoot, typeof(Transform), true);
        blackKeysRoot = (Transform)EditorGUILayout.ObjectField("Black Keys Root", blackKeysRoot, typeof(Transform), true);
        pianoManager = (PianoManager)EditorGUILayout.ObjectField("Piano Manager", pianoManager, typeof(PianoManager), true);

        GUILayout.Space(8);
        GUILayout.Label("Options", EditorStyles.boldLabel);

        autoUnpackPrefabRoot = EditorGUILayout.Toggle("Auto Unpack Prefab Root", autoUnpackPrefabRoot);
        pivotName = EditorGUILayout.TextField("Pivot Name", pivotName);

        GUILayout.Space(8);
        GUILayout.Label("Visual Defaults", EditorStyles.boldLabel);

        whiteMaxPressAngle = EditorGUILayout.FloatField("White Max Press Angle", whiteMaxPressAngle);
        whiteMaxPressOffsetY = EditorGUILayout.FloatField("White Max Press OffsetY", whiteMaxPressOffsetY);
        blackMaxPressAngle = EditorGUILayout.FloatField("Black Max Press Angle", blackMaxPressAngle);
        blackMaxPressOffsetY = EditorGUILayout.FloatField("Black Max Press OffsetY", blackMaxPressOffsetY);
        pressSpeed = EditorGUILayout.FloatField("Press Speed", pressSpeed);
        releaseSpeed = EditorGUILayout.FloatField("Release Speed", releaseSpeed);
        rotateAroundLocalX = EditorGUILayout.Toggle("Rotate Around Local X", rotateAroundLocalX);
        invertRotation = EditorGUILayout.Toggle("Invert Rotation", invertRotation);

        GUILayout.Space(12);

        if (GUILayout.Button("Do All Fixes"))
        {
            DoAllFixes();
        }
    }

    private void DoAllFixes()
    {
        if (editRoot == null || whiteKeysRoot == null || blackKeysRoot == null || pianoManager == null)
        {
            Debug.LogWarning("Edit Root, White Keys Root, Black Keys Root, Piano Manager를 모두 넣어주세요.");
            return;
        }

        TryUnpackRoot(editRoot);

        ProcessKeyGroup(whiteKeysRoot, isBlackKey: false);
        ProcessKeyGroup(blackKeysRoot, isBlackKey: true);

        FillManagerKeyVisuals();

        AssetDatabase.SaveAssets();
        Debug.Log("전체 정리 완료");
    }

    private void TryUnpackRoot(Transform root)
    {
        if (!autoUnpackPrefabRoot || root == null)
            return;

        GameObject go = root.gameObject;

        if (PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.IsOutermostPrefabInstanceRoot(go))
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            Debug.Log($"Unpack Completely 완료: {go.name}");
        }
    }

    private void ProcessKeyGroup(Transform groupRoot, bool isBlackKey)
    {
        if (groupRoot == null)
            return;

        List<Transform> keyRoots = new List<Transform>();
        for (int i = 0; i < groupRoot.childCount; i++)
        {
            keyRoots.Add(groupRoot.GetChild(i));
        }

        foreach (Transform keyRoot in keyRoots)
        {
            ProcessSingleKey(keyRoot, isBlackKey);
        }
    }

    private void ProcessSingleKey(Transform keyRoot, bool isBlackKey)
    {
        if (keyRoot == null)
            return;

        // 1) 루트 직계 자식 중 pivot 찾기 / 없으면 생성
        Transform mainPivot = keyRoot.Find(pivotName);
        if (mainPivot == null)
        {
            GameObject pivotGo = new GameObject(pivotName);
            Undo.RegisterCreatedObjectUndo(pivotGo, $"Create {pivotName}");
            mainPivot = pivotGo.transform;
            mainPivot.SetParent(keyRoot, false);
            mainPivot.localPosition = Vector3.zero;
            mainPivot.localRotation = Quaternion.identity;
            mainPivot.localScale = Vector3.one;
        }

        // 2) 루트 직계 자식 중 pivot 제외 전부 pivot 아래로 이동
        List<Transform> directChildren = new List<Transform>();
        for (int i = 0; i < keyRoot.childCount; i++)
        {
            Transform child = keyRoot.GetChild(i);
            if (child != mainPivot)
                directChildren.Add(child);
        }

        foreach (Transform child in directChildren)
        {
            Undo.SetTransformParent(child, mainPivot, $"Move {child.name} under {pivotName}");
        }

        // 3) 중복 pivot 정리 (mainPivot 외 모든 pivot 삭제)
        List<Transform> allPivots = keyRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == pivotName)
            .ToList();

        foreach (Transform p in allPivots)
        {
            if (p == mainPivot)
                continue;

            // 자식들을 상위로 올린 뒤 pivot 삭제
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < p.childCount; i++)
                children.Add(p.GetChild(i));

            foreach (Transform c in children)
            {
                Undo.SetTransformParent(c, p.parent, $"Lift {c.name} out of duplicate pivot");
            }

            Undo.DestroyObjectImmediate(p.gameObject);
        }

        // 4) 루트가 아닌 곳의 PianoKeyVisual 제거
        PianoKeyVisual[] visuals = keyRoot.GetComponentsInChildren<PianoKeyVisual>(true);
        foreach (var v in visuals)
        {
            if (v != null && v.transform != keyRoot)
            {
                Undo.DestroyObjectImmediate(v);
            }
        }

        // 5) 루트에 PianoKeyVisual 보장
        PianoKeyVisual rootVisual = keyRoot.GetComponent<PianoKeyVisual>();
        if (rootVisual == null)
        {
            rootVisual = Undo.AddComponent<PianoKeyVisual>(keyRoot.gameObject);
        }

        // 6) 루트 피벗 연결 + 기본값 세팅
        SerializedObject so = new SerializedObject(rootVisual);
        SetObjectRef(so, "visualPivot", mainPivot);
        SetBool(so, "rotateAroundLocalX", rotateAroundLocalX);
        SetBool(so, "invertRotation", invertRotation);
        SetFloat(so, "maxPressAngle", isBlackKey ? blackMaxPressAngle : whiteMaxPressAngle);
        SetFloat(so, "maxPressOffsetY", isBlackKey ? blackMaxPressOffsetY : whiteMaxPressOffsetY);
        SetFloat(so, "pressSpeed", pressSpeed);
        SetFloat(so, "releaseSpeed", releaseSpeed);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 7) base state 갱신
        var captureMethod = typeof(PianoKeyVisual).GetMethod("CaptureBaseState");
        if (captureMethod != null)
            captureMethod.Invoke(rootVisual, null);

        EditorUtility.SetDirty(keyRoot.gameObject);
    }

    private void FillManagerKeyVisuals()
    {
        List<PianoKeyVisual> list = new List<PianoKeyVisual>();

        CollectRootVisuals(whiteKeysRoot, list);
        CollectRootVisuals(blackKeysRoot, list);

        // 좌 -> 우 정렬
        list.Sort((a, b) =>
        {
            float ax = GetSortX(a.transform);
            float bx = GetSortX(b.transform);
            return ax.CompareTo(bx);
        });

        SerializedObject so = new SerializedObject(pianoManager);
        SerializedProperty arr = so.FindProperty("keyVisuals");

        so.Update();
        arr.arraySize = list.Count;

        for (int i = 0; i < list.Count; i++)
        {
            arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pianoManager);

        Debug.Log($"PianoManager.keyVisuals 채움 완료: {list.Count}개");
    }

    private void CollectRootVisuals(Transform groupRoot, List<PianoKeyVisual> list)
    {
        if (groupRoot == null)
            return;

        for (int i = 0; i < groupRoot.childCount; i++)
        {
            Transform keyRoot = groupRoot.GetChild(i);
            PianoKeyVisual v = keyRoot.GetComponent<PianoKeyVisual>();
            if (v != null)
                list.Add(v);
            else
                Debug.LogWarning($"{keyRoot.name} 루트에 PianoKeyVisual이 없습니다.");
        }
    }

    private float GetSortX(Transform tr)
    {
        Renderer[] renderers = tr.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return editRoot.InverseTransformPoint(tr.position).x;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return editRoot.InverseTransformPoint(b.center).x;
    }

    private void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
    }

    private void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    private void SetObjectRef(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
    }
}