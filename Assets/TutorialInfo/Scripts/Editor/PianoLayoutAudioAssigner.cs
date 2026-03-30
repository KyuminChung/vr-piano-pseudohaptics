using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PianoLayoutAudioAssigner
{
    private const string AudioFolderPath = "Assets/Piano Audio";

    [MenuItem("Tools/Piano/Assign Audio Clips To Selected Layout")]
    public static void AssignAudioClipsToSelectedLayout()
    {
        PianoLayoutConfig layout = Selection.activeObject as PianoLayoutConfig;

        if (layout == null)
        {
            Debug.LogWarning("PianoLayoutConfig 에셋을 먼저 선택해.");
            return;
        }

        if (layout.keys == null || layout.keys.Count == 0)
        {
            Debug.LogWarning("선택한 PianoLayoutConfig의 keys가 비어 있어.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolderPath });

        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning($"오디오 클립을 찾지 못했어. 폴더 경로 확인: {AudioFolderPath}");
            return;
        }

        List<AudioClip> clips = new List<AudioClip>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip != null)
                clips.Add(clip);
        }

        // 파일명이 01_A0, 02_As0 ... 형태면 이름순 정렬로 순서 맞음
        clips = clips.OrderBy(c => c.name, StringComparer.Ordinal).ToList();

        if (clips.Count < layout.keys.Count)
        {
            Debug.LogWarning(
                $"오디오 개수가 부족해. clips={clips.Count}, keys={layout.keys.Count}"
            );
            return;
        }

        for (int i = 0; i < layout.keys.Count; i++)
        {
            layout.keys[i].noteClip = clips[i];
            Debug.Log($"[{i}] {layout.keys[i].keyName} <= {clips[i].name}");
        }

        EditorUtility.SetDirty(layout);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"오디오 자동 할당 완료: {layout.name}");
    }
}