using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class XRIndexTipFollower : MonoBehaviour
{
    [SerializeField] private Transform xrOriginTransform;
    [SerializeField] private Transform pianoTransform;   // PianoA
    [SerializeField] private Transform topSurface;       // Key01/TopSurface
    [SerializeField] private bool useRightHand = true;

    private XRHandSubsystem handSubsystem;

    private void Update()
    {
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

            if (handSubsystem == null) return;
        }

        if (xrOriginTransform == null) return;

        XRHand hand = useRightHand ? handSubsystem.rightHand : handSubsystem.leftHand;
        if (!hand.isTracked) return;

        XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);
        if (!indexTip.TryGetPose(out Pose jointPose)) return;

        Vector3 p_World = xrOriginTransform.TransformPoint(jointPose.position);

        // 손끝 표시용 오브젝트 이동
        transform.position = p_World;

        // 피아노 전체 기준 로컬 좌표
        if (pianoTransform != null)
        {
            Vector3 pianoLocal = pianoTransform.InverseTransformPoint(p_World);
            Debug.Log($"Piano Local: {pianoLocal}");
        }

        // 건반 하나 기준 로컬 좌표
        if (topSurface != null)
        {
            Vector3 keyLocal = topSurface.InverseTransformPoint(p_World);
            Debug.Log($"Key Local: {keyLocal}");
        }
    }
}