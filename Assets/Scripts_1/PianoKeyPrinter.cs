using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class PianoKeyPrinter : MonoBehaviour
{
    [System.Serializable]
    private class KeyPrintData
    {
        public int keyIndex;
        public string keyName;
        public bool isBlackKey;

        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
        public float topY;

        public float centerX;
    }

    [Header("References")]
    [SerializeField] private Transform pianoFrame;
    [SerializeField] private Transform whiteKeysRoot;
    [SerializeField] private Transform blackKeysRoot;

    [Header("Options")]
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool printNow = false;

    private void OnValidate()
    {
        if (!printNow)
            return;

        printNow = false;
        PrintAllKeys();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void PrintAllKeys()
    {
        if (pianoFrame == null)
        {
            Debug.LogWarning("pianoFrame이 비어 있습니다.");
            return;
        }

        List<KeyPrintData> allKeys = new List<KeyPrintData>();

        CollectKeysFromRoot(whiteKeysRoot, false, allKeys);
        CollectKeysFromRoot(blackKeysRoot, true, allKeys);

        if (allKeys.Count == 0)
        {
            Debug.LogWarning("건반 데이터를 찾지 못했습니다.");
            return;
        }

        // 왼쪽 -> 오른쪽 순으로 정렬
        allKeys.Sort((a, b) => a.centerX.CompareTo(b.centerX));

        // keyIndex 다시 부여
        for (int i = 0; i < allKeys.Count; i++)
        {
            allKeys[i].keyIndex = i;
        }

        Debug.Log("========== Piano Key Data Print Start ==========");

        for (int i = 0; i < allKeys.Count; i++)
        {
            KeyPrintData k = allKeys[i];

            Debug.Log(
                $"new PianoKeyData {{ " +
                $"keyIndex = {k.keyIndex}, " +
                $"keyName = \"{k.keyName}\", " +
                $"isBlackKey = {k.isBlackKey.ToString().ToLower()}, " +
                $"minX = {k.minX:F6}f, " +
                $"maxX = {k.maxX:F6}f, " +
                $"minZ = {k.minZ:F6}f, " +
                $"maxZ = {k.maxZ:F6}f, " +
                $"topY = {k.topY:F6}f " +
                $"}}"
            );
        }

        Debug.Log($"========== Piano Key Data Print End / Total: {allKeys.Count} ==========");
    }

    private void CollectKeysFromRoot(Transform root, bool isBlackKey, List<KeyPrintData> result)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform keyTransform = root.GetChild(i);
            if (keyTransform == null)
                continue;

            Renderer[] renderers = keyTransform.GetComponentsInChildren<Renderer>(includeInactive);
            if (renderers == null || renderers.Length == 0)
                continue;

            Bounds worldBounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++)
            {
                worldBounds.Encapsulate(renderers[r].bounds);
            }

            Vector3[] corners = GetWorldBoundsCorners(worldBounds);

            Vector3 firstLocal = pianoFrame.InverseTransformPoint(corners[0]);

            float minX = firstLocal.x;
            float maxX = firstLocal.x;
            float minY = firstLocal.y;
            float maxY = firstLocal.y;
            float minZ = firstLocal.z;
            float maxZ = firstLocal.z;

            for (int c = 1; c < corners.Length; c++)
            {
                Vector3 local = pianoFrame.InverseTransformPoint(corners[c]);

                if (local.x < minX) minX = local.x;
                if (local.x > maxX) maxX = local.x;

                if (local.y < minY) minY = local.y;
                if (local.y > maxY) maxY = local.y;

                if (local.z < minZ) minZ = local.z;
                if (local.z > maxZ) maxZ = local.z;
            }

            KeyPrintData data = new KeyPrintData
            {
                keyName = keyTransform.name,
                isBlackKey = isBlackKey,
                minX = minX,
                maxX = maxX,
                minZ = minZ,
                maxZ = maxZ,
                topY = maxY,
                centerX = (minX + maxX) * 0.5f
            };

            result.Add(data);
        }
    }

    private Vector3[] GetWorldBoundsCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z),
        };
    }
}