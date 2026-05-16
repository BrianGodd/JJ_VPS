using System;
using UnityEngine;

namespace JJ.Vps.Providers
{
    public class ImmersalVpsProviderAdapter : MonoBehaviour, IVpsProvider
    {
        [SerializeField] private ImmersalAPI immersalApi;

        public string ProviderId => "Immersal";

        public bool IsLocalizing => immersalApi != null && immersalApi.IsLocalizing;

        public event Action<VpsLocalizationResult> LocalizationSucceeded;

        public event Action<VpsLocalizationResult> LocalizationFailed;

        private void OnEnable()
        {
            if (immersalApi != null)
            {
                immersalApi.LocalizationCompleted += HandleLocalizationCompleted;
            }
        }

        private void OnDisable()
        {
            if (immersalApi != null)
            {
                immersalApi.LocalizationCompleted -= HandleLocalizationCompleted;
            }
        }

        public void Localize(VpsLocalizationRequest request)
        {
            if (immersalApi == null)
            {
                Debug.LogError("ImmersalVpsProviderAdapter: ImmersalAPI is missing.");
                return;
            }

            Vector4 intrinsics = new Vector4(
                request.intrinsics.fx,
                request.intrinsics.fy,
                request.intrinsics.px,
                request.intrinsics.py);

            immersalApi.LocalizeEncodedImage(request.encodedImageBytes, intrinsics, request.imageMimeType);
        }

        private void HandleLocalizationCompleted(ImmersalAPI.ImmersalLocalizationSnapshot snapshot)
        {
            VpsLocalizationResult result = new VpsLocalizationResult
            {
                success = snapshot.success,
                providerId = ProviderId,
                mapId = snapshot.mapId >= 0 ? snapshot.mapId.ToString() : null,
                hasLocalizedCameraPose = snapshot.success,
                localizedCameraPosition = snapshot.localizedPosition,
                localizedCameraRotation = snapshot.localizedRotation,
                hasAppliedWorldPose = snapshot.success,
                appliedWorldPosition = snapshot.localizedPosition,
                appliedWorldRotation = snapshot.localizedRotation,
                providerAppliedWorldPoseInternally = true,
                rawResponse = snapshot.rawResponse,
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
