using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class VRHandTracker : MonoBehaviour
{
    public VRFingerData[] FingerData { get; private set; }

    public Transform pianoParent { get; set; }

    private XRHandSubsystem _subsystem;

    private static readonly XRHandJointID[] TipJoints = new[]
    {
        XRHandJointID.ThumbTip,
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip,
    };

    private void Awake()
    {
        FingerData = new VRFingerData[10];
        for (int i = 0; i < 10; i++)
            FingerData[i] = new VRFingerData { fingerId = i };
    }

    private void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            _subsystem = subsystems[0];

        if (_subsystem != null)
            _subsystem.updatedHands += OnUpdatedHands;
        else
            Debug.LogWarning("[VRHandTracker] XRHandSubsystem을 찾지 못했습니다.");
    }

    private void OnDisable()
    {
        if (_subsystem != null)
            _subsystem.updatedHands -= OnUpdatedHands;
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            return;

        UpdateHand(subsystem.leftHand,  isLeft: true);
        UpdateHand(subsystem.rightHand, isLeft: false);
    }

    private void UpdateHand(XRHand hand, bool isLeft)
    {
        int baseId = isLeft ? 0 : 5;

        for (int i = 0; i < TipJoints.Length; i++)
        {
            int fingerId = baseId + i;
            var jointId  = TipJoints[i];

            Pose pose    = default;
            bool tracked = hand.isTracked &&
                           hand.GetJoint(jointId).TryGetPose(out pose);

            Vector3 worldPos = tracked ? pose.position : FingerData[fingerId].worldPos;

            FingerData[fingerId].UpdateWorldPos(worldPos, tracked);

            if (pianoParent != null)
                FingerData[fingerId].localPos =
                    pianoParent.InverseTransformPoint(worldPos);
        }
    }

    public bool AreAllFingersTracked()
    {
        foreach (var f in FingerData)
            if (!f.isTracked) return false;
        return true;
    }

    public float GetFingertipYVariance()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (var f in FingerData)
        {
            if (!f.isTracked) return float.MaxValue;
            if (f.worldPos.y < min) min = f.worldPos.y;
            if (f.worldPos.y > max) max = f.worldPos.y;
        }

        return max - min;
    }

    public Vector3 GetFingertipCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (var f in FingerData)
            sum += f.worldPos;
        return sum / 10f;
    }

    public float GetFingertipAverageY()
    {
        float sum = 0f;
        foreach (var f in FingerData)
            sum += f.worldPos.y;
        return sum / 10f;
    }
}