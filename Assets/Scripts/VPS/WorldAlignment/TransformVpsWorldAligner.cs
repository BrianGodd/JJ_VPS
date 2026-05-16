using UnityEngine;

namespace JJ.Vps.WorldAlignment
{
    public class TransformVpsWorldAligner : MonoBehaviour, IVpsWorldAligner
    {
        public enum PoseSource
        {
            AppliedWorldPose,
            LocalizedCameraPose
        }

        [SerializeField] private Transform targetTransform;
        [SerializeField] private PoseSource poseSource = PoseSource.AppliedWorldPose;

        public void ApplyLocalization(VpsLocalizationResult result)
        {
            if (targetTransform == null || result == null || !result.success)
            {
                return;
            }

            if (poseSource == PoseSource.AppliedWorldPose && result.hasAppliedWorldPose)
            {
                targetTransform.SetPositionAndRotation(result.appliedWorldPosition, result.appliedWorldRotation);
                return;
            }

            if (poseSource == PoseSource.LocalizedCameraPose && result.hasLocalizedCameraPose)
            {
                targetTransform.SetPositionAndRotation(result.localizedCameraPosition, result.localizedCameraRotation);
            }
        }
    }
}
