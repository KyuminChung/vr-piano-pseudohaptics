using UnityEngine;
using System.Collections.Generic;

public class VRLayoutInitializer : MonoBehaviour
{
    [Header("건반 오브젝트 배열")]
    public GameObject[] whiteKeyObjects;
    public GameObject[] blackKeyObjects;

    [Header("힌지 Z 오버라이드")]
    public float whiteHingeZOverride = float.NaN;
    public float blackHingeZOverride = float.NaN;

    [Header("Zone Tuning")]
    [Tooltip("검은 건반과 흰 건반 Zone B 사이 여유 간격")]
    public float blackClearanceX = 0.002f;

    [Header("Debug Gizmos")]
    public bool drawZones = true;

    [Tooltip("흰 건반 Zone A 색")]
    public Color whiteZoneAColor = Color.green;

    [Tooltip("흰 건반 Zone B 색")]
    public Color whiteZoneBColor = Color.blue;

    [Tooltip("검은 건반 Zone A 색")]
    public Color blackZoneAColor = Color.red;

    public float gizmoYOffset = 0.001f;

    public VRKeyData[] KeyData { get; private set; }
    public float RestTopY { get; private set; }

    public static int MidiToIndex(int midi) => midi - 21;

    private static readonly bool[] IsBlack = BuildIsBlackTable();

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        KeyData = new VRKeyData[88];

        Dictionary<GameObject, Bounds> blackBoundsMap = ComputeBlackBounds();

        int whiteIdx = 0;
        int blackIdx = 0;

        for (int midi = 21; midi <= 108; midi++)
        {
            int arrayIdx = MidiToIndex(midi);
            bool isBlack = IsBlack[arrayIdx];

            VRKeyData data = new VRKeyData
            {
                keyIndex = midi,
                isBlackKey = isBlack
            };

            if (isBlack)
            {
                if (blackIdx < blackKeyObjects.Length)
                {
                    BuildBlackKeyData(data, blackKeyObjects[blackIdx]);
                }

                blackIdx++;
            }
            else
            {
                if (whiteIdx < whiteKeyObjects.Length)
                {
                    BuildWhiteKeyData(data, whiteKeyObjects[whiteIdx], blackBoundsMap);
                }

                whiteIdx++;
            }

            KeyData[arrayIdx] = data;
        }

        CachePianoConstants();

        Debug.Log($"[VRLayoutInitializer] 완료 | white={whiteIdx}, black={blackIdx}, RestTopY={RestTopY:F4}");
    }

    private void BuildBlackKeyData(VRKeyData data, GameObject blackGo)
    {
        if (blackGo == null) return;

        Bounds b = GetLocalBounds(blackGo);

        data.keyTransform = blackGo.transform;
        data.isBlackKey = true;

        data.maxDrop = VRPianoConst.BlackMaxDrop;
        data.restTopLocalY = b.max.y;
        data.keyLength = b.max.z - b.min.z;

        data.hingeLocalZ =
            float.IsNaN(blackHingeZOverride)
                ? b.max.z
                : blackHingeZOverride;

        // 검은 건반은 자기 전체가 Zone A
        data.zoneAMinX = b.min.x;
        data.zoneAMaxX = b.max.x;
        data.zoneAMinZ = b.min.z;
        data.zoneAMaxZ = b.max.z;

        data.hasZoneB = false;
    }

    private void BuildWhiteKeyData(
        VRKeyData data,
        GameObject whiteGo,
        Dictionary<GameObject, Bounds> blackBoundsMap)
    {
        if (whiteGo == null) return;

        Bounds white = GetLocalBounds(whiteGo);

        data.keyTransform = whiteGo.transform;
        data.isBlackKey = false;

        data.maxDrop = VRPianoConst.WhiteMaxDrop;
        data.restTopLocalY = white.max.y;
        data.keyLength = white.max.z - white.min.z;

        data.hingeLocalZ =
            float.IsNaN(whiteHingeZOverride)
                ? white.max.z
                : whiteHingeZOverride;

        List<Bounds> overlappingBlacks = FindOverlappingBlackKeys(white, blackBoundsMap);

        if (overlappingBlacks.Count == 0)
        {
            // 검은 건반이 안 겹치는 흰 건반은 전체 Zone A
            data.zoneAMinX = white.min.x;
            data.zoneAMaxX = white.max.x;
            data.zoneAMinZ = white.min.z;
            data.zoneAMaxZ = white.max.z;

            data.hasZoneB = false;
            return;
        }

        // 핵심:
        // 검은 건반의 앞쪽 끝을 기준으로 Zone A/B를 나눈다.
        // 네 모델에서는 앞쪽/바깥쪽이 +Z 방향이므로 black.max.z 사용.
        float zoneABoundaryZ = GetBlackFrontEdgeZ(overlappingBlacks);

        zoneABoundaryZ = Mathf.Clamp(
            zoneABoundaryZ,
            white.min.z,
            white.max.z
        );

        // Zone A:
        // 검은 건반과 Z 방향으로 겹치지 않는 앞쪽/바깥쪽 전체 폭
        data.zoneAMinX = white.min.x;
        data.zoneAMaxX = white.max.x;
        data.zoneAMinZ = zoneABoundaryZ;
        data.zoneAMaxZ = white.max.z;

        // Zone B:
        // 검은 건반이 있는 뒤쪽/안쪽 영역 중,
        // 검은 건반과 X 방향으로 겹치지 않는 부분
        bool hasZoneB = TryBuildWhiteZoneB(
            white,
            overlappingBlacks,
            white.min.z,
            zoneABoundaryZ,
            out float bMinX,
            out float bMaxX,
            out float bMinZ,
            out float bMaxZ
        );

        if (hasZoneB)
        {
            data.hasZoneB = true;
            data.zoneBMinX = bMinX;
            data.zoneBMaxX = bMaxX;
            data.zoneBMinZ = bMinZ;
            data.zoneBMaxZ = bMaxZ;
        }
        else
        {
            data.hasZoneB = false;
        }
    }

    private float GetBlackFrontEdgeZ(List<Bounds> overlappingBlacks)
    {
        // 앞쪽/바깥쪽이 +Z 방향이므로 가장 큰 max.z를 사용
        float frontZ = float.MinValue;

        foreach (Bounds black in overlappingBlacks)
        {
            if (black.max.z > frontZ)
            {
                frontZ = black.max.z;
            }
        }

        return frontZ;
    }

    private bool TryBuildWhiteZoneB(
        Bounds white,
        List<Bounds> overlappingBlacks,
        float zoneBMinZ,
        float zoneBMaxZ,
        out float outMinX,
        out float outMaxX,
        out float outMinZ,
        out float outMaxZ)
    {
        outMinX = 0f;
        outMaxX = 0f;
        outMinZ = zoneBMinZ;
        outMaxZ = zoneBMaxZ;

        if (zoneBMaxZ <= zoneBMinZ)
        {
            return false;
        }

        List<Vector2> blockedXIntervals = new List<Vector2>();

        foreach (Bounds black in overlappingBlacks)
        {
            float minX = Mathf.Clamp(
                black.min.x - blackClearanceX,
                white.min.x,
                white.max.x
            );

            float maxX = Mathf.Clamp(
                black.max.x + blackClearanceX,
                white.min.x,
                white.max.x
            );

            if (maxX > minX)
            {
                blockedXIntervals.Add(new Vector2(minX, maxX));
            }
        }

        List<Vector2> freeIntervals = GetFreeXIntervals(
            white.min.x,
            white.max.x,
            blockedXIntervals
        );

        if (freeIntervals.Count == 0)
        {
            return false;
        }

        // VRKeyData는 ZoneB 하나만 저장하므로 가장 넓은 빈 X 구간 하나 선택
        Vector2 best = freeIntervals[0];
        float bestWidth = best.y - best.x;

        for (int i = 1; i < freeIntervals.Count; i++)
        {
            float width = freeIntervals[i].y - freeIntervals[i].x;

            if (width > bestWidth)
            {
                best = freeIntervals[i];
                bestWidth = width;
            }
        }

        if (bestWidth <= 0.001f)
        {
            return false;
        }

        outMinX = best.x;
        outMaxX = best.y;
        outMinZ = zoneBMinZ;
        outMaxZ = zoneBMaxZ;

        return true;
    }

    private List<Vector2> GetFreeXIntervals(
        float minX,
        float maxX,
        List<Vector2> blocked)
    {
        List<Vector2> free = new List<Vector2>();

        if (blocked.Count == 0)
        {
            free.Add(new Vector2(minX, maxX));
            return free;
        }

        blocked.Sort((a, b) => a.x.CompareTo(b.x));

        List<Vector2> merged = MergeIntervals(blocked);

        float cursor = minX;

        foreach (Vector2 block in merged)
        {
            if (block.x > cursor)
            {
                free.Add(new Vector2(cursor, block.x));
            }

            cursor = Mathf.Max(cursor, block.y);
        }

        if (cursor < maxX)
        {
            free.Add(new Vector2(cursor, maxX));
        }

        return free;
    }

    private List<Vector2> MergeIntervals(List<Vector2> intervals)
    {
        List<Vector2> merged = new List<Vector2>();

        if (intervals.Count == 0)
        {
            return merged;
        }

        Vector2 current = intervals[0];

        for (int i = 1; i < intervals.Count; i++)
        {
            Vector2 next = intervals[i];

            if (next.x <= current.y)
            {
                current.y = Mathf.Max(current.y, next.y);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);

        return merged;
    }

    private List<Bounds> FindOverlappingBlackKeys(
        Bounds white,
        Dictionary<GameObject, Bounds> blackBoundsMap)
    {
        List<Bounds> result = new List<Bounds>();

        foreach (KeyValuePair<GameObject, Bounds> kv in blackBoundsMap)
        {
            Bounds black = kv.Value;

            bool overlapsX =
                black.min.x < white.max.x &&
                black.max.x > white.min.x;

            bool overlapsZ =
                black.min.z < white.max.z &&
                black.max.z > white.min.z;

            if (overlapsX && overlapsZ)
            {
                result.Add(black);
            }
        }

        result.Sort((a, b) => a.center.x.CompareTo(b.center.x));

        return result;
    }

    private Dictionary<GameObject, Bounds> ComputeBlackBounds()
    {
        Dictionary<GameObject, Bounds> dict = new Dictionary<GameObject, Bounds>();

        foreach (GameObject go in blackKeyObjects)
        {
            if (go == null) continue;

            dict[go] = GetLocalBounds(go);
        }

        return dict;
    }

    private void CachePianoConstants()
    {
        if (whiteKeyObjects != null && whiteKeyObjects.Length > 0 && whiteKeyObjects[0] != null)
        {
            Bounds b = GetLocalBounds(whiteKeyObjects[0]);

            VRPianoConst.WhiteRestTopLocalY = b.max.y;
            VRPianoConst.WhiteHingeLocalZ = b.max.z;
            VRPianoConst.WhiteKeyLength = b.max.z - b.min.z;

            Renderer r = whiteKeyObjects[0].GetComponentInChildren<Renderer>();

            if (r != null)
            {
                RestTopY = r.bounds.max.y;
            }
        }

        if (blackKeyObjects != null && blackKeyObjects.Length > 0 && blackKeyObjects[0] != null)
        {
            Bounds b = GetLocalBounds(blackKeyObjects[0]);

            VRPianoConst.BlackRestTopLocalY = b.max.y;
            VRPianoConst.BlackHingeLocalZ = b.max.z;
            VRPianoConst.BlackKeyLength = b.max.z - b.min.z;
        }
    }

    private Bounds GetLocalBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds();
        }

        bool hasBounds = false;
        Bounds result = new Bounds();

        foreach (Renderer r in renderers)
        {
            Bounds worldBounds = r.bounds;

            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            Vector3[] corners =
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

            foreach (Vector3 corner in corners)
            {
                Vector3 local = transform.InverseTransformPoint(corner);

                if (!hasBounds)
                {
                    result = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(local);
                }
            }
        }

        return result;
    }

    private void OnDrawGizmos()
    {
        if (!drawZones || KeyData == null) return;

        foreach (VRKeyData data in KeyData)
        {
            if (data == null) continue;

            if (data.isBlackKey)
            {
                // 검은 건반 Zone A
                DrawRectGizmo(
                    data.zoneAMinX,
                    data.zoneAMaxX,
                    data.zoneAMinZ,
                    data.zoneAMaxZ,
                    data.restTopLocalY + gizmoYOffset,
                    blackZoneAColor
                );
            }
            else
            {
                // 흰 건반 Zone A
                DrawRectGizmo(
                    data.zoneAMinX,
                    data.zoneAMaxX,
                    data.zoneAMinZ,
                    data.zoneAMaxZ,
                    data.restTopLocalY + gizmoYOffset,
                    whiteZoneAColor
                );

                // 흰 건반 Zone B
                if (data.hasZoneB)
                {
                    DrawRectGizmo(
                        data.zoneBMinX,
                        data.zoneBMaxX,
                        data.zoneBMinZ,
                        data.zoneBMaxZ,
                        data.restTopLocalY + gizmoYOffset,
                        whiteZoneBColor
                    );
                }
            }
        }
    }

    private void DrawRectGizmo(
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        float y,
        Color color)
    {
        Gizmos.color = color;

        Vector3 p1 = transform.TransformPoint(new Vector3(minX, y, minZ));
        Vector3 p2 = transform.TransformPoint(new Vector3(maxX, y, minZ));
        Vector3 p3 = transform.TransformPoint(new Vector3(maxX, y, maxZ));
        Vector3 p4 = transform.TransformPoint(new Vector3(minX, y, maxZ));

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }

    private static bool[] BuildIsBlackTable()
    {
        bool[] pattern =
        {
            false, true, false, false, true, false, true,
            false, false, true, false, true
        };

        bool[] table = new bool[88];

        for (int i = 0; i < 88; i++)
        {
            table[i] = pattern[i % 12];
        }

        return table;
    }
}