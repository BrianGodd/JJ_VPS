using System;
using MultiSet;
using UnityEngine;

namespace JJ.Vps.Providers
{
    public class MultiSetVpsProviderAdapter : MonoBehaviour, IVpsProvider
    {
        [SerializeField] private SingleFrameLocalizationManager localizationManager;
        [SerializeField] private bool useArSessionForLocalization;
        [SerializeField] private bool logRequests = true;

        public string ProviderId => "MultiSet";

        public bool IsLocalizing => localizationManager != null && localizationManager.IsLocalizing;

        public event Action<VpsLocalizationResult> LocalizationSucceeded;

        public event Action<VpsLocalizationResult> LocalizationFailed;

        private void OnEnable()
        {
            if (localizationManager != null)
            {
                localizationManager.LocalizationCompleted += HandleLocalizationCompleted;
            }
        }

        private void OnDisable()
        {
            if (localizationManager != null)
            {
                localizationManager.LocalizationCompleted -= HandleLocalizationCompleted;
            }
        }

        public void Localize(VpsLocalizationRequest request)
        {
            if (localizationManager == null)
            {
                Debug.LogError("MultiSetVpsProviderAdapter: SingleFrameLocalizationManager is missing.");
                return;
            }

            CameraParams cameraParams = new CameraParams
            {
                fx = request.intrinsics.fx,
                fy = request.intrinsics.fy,
                px = request.intrinsics.px,
                py = request.intrinsics.py
            };
            MultiSet.Resolution resolution = new MultiSet.Resolution
            {
                width = request.imageWidth,
                height = request.imageHeight
            };

            localizationManager.useARSessionForLocalization = useArSessionForLocalization;
            localizationManager.useExternalImageInput = true;

            if (logRequests)
            {
                Debug.Log(
                    $"MultiSetVpsProviderAdapter -> bytes={request.encodedImageBytes?.Length ?? 0}, " +
                    $"size={request.imageWidth}x{request.imageHeight}, " +
                    $"intrinsics=({request.intrinsics.fx:F3}, {request.intrinsics.fy:F3}, {request.intrinsics.px:F3}, {request.intrinsics.py:F3}), " +
                    $"hasQueryPose={request.hasQueryPose}, useARSessionForLocalization={useArSessionForLocalization}");
            }

            if (request.hasQueryPose)
            {
                localizationManager.LocalizeImageBytes(request.encodedImageBytes, cameraParams, resolution, request.queryCameraPosition, request.queryCameraRotation);
            }
            else
            {
                localizationManager.LocalizeImageBytes(request.encodedImageBytes, cameraParams, resolution);
            }
        }

        private void HandleLocalizationCompleted(SingleFrameLocalizationManager.LocalizationSnapshot snapshot)
        {
            VpsLocalizationResult result = new VpsLocalizationResult
            {
                success = snapshot.success,
                providerId = ProviderId,
                mapId = snapshot.mapId,
                confidence = snapshot.confidence,
                hasLocalizedCameraPose = snapshot.hasLocalizedCameraPose,
                localizedCameraPosition = snapshot.localizedCameraPosition,
                localizedCameraRotation = snapshot.localizedCameraRotation,
                hasAppliedWorldPose = snapshot.hasMapSpacePose,
                appliedWorldPosition = snapshot.mapSpacePosition,
                appliedWorldRotation = snapshot.mapSpaceRotation,
                providerAppliedWorldPoseInternally = true,
                errorMessage = snapshot.errorMessage
            };

            if (snapshot.success)
            {
                LocalizationSucceeded?.Invoke(result);
            }
            else
            {
                LocalizationFailed?.Invoke(result);
            }
        }
    }
}
