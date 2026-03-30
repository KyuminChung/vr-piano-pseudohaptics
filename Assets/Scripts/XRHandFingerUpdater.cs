using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class XRHandFingerUpdater : MonoBehaviour
{
    [SerializeField] private Transform xrOriginTransform;
    [SerializeField] private FingerStateStore fingerStore;

    private XRHandSubsystem handSubsystem;

    private void Update()
    {
        //Test
        //Debug.Log(fingerStore.GetFinger(FingerId.RightIndex).localPos);
        if (fingerStore == null || xrOriginTransform == null)
            return;

        if (handSubsystem == null)
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            foreach (var subsystem in subsystems)
            {
                if (subsystem != null && subsystem.running)
                {
                    handSubsystem = subsystem;
                    break;
                }
            }

            if (handSubsystem == null)
                return;
        }

        float dt = Time.deltaTime;

        UpdateFinger(handSubsystem.leftHand, XRHandJointID.ThumbTip, FingerId.LeftThumb, dt);
        UpdateFinger(handSubsystem.leftHand, XRHandJointID.IndexTip, FingerId.LeftIndex, dt);
        UpdateFinger(handSubsystem.leftHand, XRHandJointID.MiddleTip, FingerId.LeftMiddle, dt);
        UpdateFinger(handSubsystem.leftHand, XRHandJointID.RingTip, FingerId.LeftRing, dt);
        UpdateFinger(handSubsystem.leftHand, XRHandJointID.LittleTip, FingerId.LeftLittle, dt);

        UpdateFinger(handSubsystem.rightHand, XRHandJointID.ThumbTip, FingerId.RightThumb, dt);
        UpdateFinger(handSubsystem.rightHand, XRHandJointID.IndexTip, FingerId.RightIndex, dt);
        UpdateFinger(handSubsystem.rightHand, XRHandJointID.MiddleTip, FingerId.RightMiddle, dt);
        UpdateFinger(handSubsystem.rightHand, XRHandJointID.RingTip, FingerId.RightRing, dt);
        UpdateFinger(handSubsystem.rightHand, XRHandJointID.LittleTip, FingerId.RightLittle, dt);
    }

    private void UpdateFinger(XRHand hand, XRHandJointID jointId, FingerId fingerId, float dt)
    {
        if (!hand.isTracked)
        {
            fingerStore.UpdateFinger(fingerId, Vector3.zero, false, dt);
            return;
        }

        XRHandJoint joint = hand.GetJoint(jointId);

        if (!joint.TryGetPose(out Pose pose))
        {
            fingerStore.UpdateFinger(fingerId, Vector3.zero, false, dt);
            return;
        }

        Vector3 worldPos = xrOriginTransform.TransformPoint(pose.position);
        fingerStore.UpdateFinger(fingerId, worldPos, true, dt);
    }
}