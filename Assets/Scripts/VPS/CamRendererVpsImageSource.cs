using UnityEngine;
using UnityEngine.UI;

namespace JJ.Vps
{
    public class CamRendererVpsImageSource : MonoBehaviour, IVpsImageSource
    {
        private enum CameraParameterPreset
        {
            Current1280x720,
            Legacy960x1280,
            Custom
        }

        [Header("Source")]
        [SerializeField] private CamRenderer camRenderer;
        [SerializeField] private Camera poseCamera;

        [Header("Preset")]
        [SerializeField] private CameraParameterPreset parameterPreset = CameraParameterPreset.Current1280x720;

        [Header("Frame")]
        [SerializeField] private int imageWidth = 1280;
        [SerializeField] private int imageHeight = 720;
        [SerializeField, Range(40, 100)] private int jpegQuality = 80;

        [Header("Intrinsics")]
        [SerializeField] private float fx = 1161.352133f;
        [SerializeField] private float fy = 1162.871712f;
        [SerializeField] private float px = 630.465034f;
        [SerializeField] private float py = 368.853068f;

        [Header("Debug")]
        [SerializeField] private bool logRequests = true;
        [SerializeField, HideInInspector] private CameraParameterPreset lastAppliedPreset = CameraParameterPreset.Current1280x720;

        private Texture2D m_rgbTexture;
        private byte[] m_rgbBuffer;

        private void Reset()
        {
            ApplyPreset(parameterPreset);
        }

        private void OnValidate()
        {
            if (parameterPreset == CameraParameterPreset.Custom)
            {
                lastAppliedPreset = parameterPreset;
                return;
            }

            if (parameterPreset != lastAppliedPreset)
            {
                ApplyPreset(parameterPreset);
            }
        }

        public bool TryBuildLocalizationRequest(out VpsLocalizationRequest request)
        {
            request = null;
            if (camRenderer == null)
            {
                Debug.LogWarning("CamRendererVpsImageSource: CamRenderer is not assigned.");
                return false;
            }

            string imageSource = "CamRenderer.image.texture";
            byte[] encodedBytes = TryEncodeFromDebugImage();
            if (encodedBytes == null || encodedBytes.Length == 0)
            {
                imageSource = "CamRenderer.camBytes";
                if (camRenderer.camBytes == null)
                {
                    Debug.LogWarning("CamRendererVpsImageSource: camera bytes are not available yet.");
                    return false;
                }

                encodedBytes = EncodeCurrentFrame(camRenderer.camBytes);
            }

            if (encodedBytes == null || encodedBytes.Length == 0)
            {
                Debug.LogWarning("CamRendererVpsImageSource: failed to encode the current frame.");
                return false;
            }

            request = new VpsLocalizationRequest
            {
                encodedImageBytes = encodedBytes,
                imageMimeType = "image/jpeg",
                imageWidth = imageWidth,
                imageHeight = imageHeight,
                intrinsics = new VpsCameraIntrinsics
                {
                    fx = fx,
                    fy = fy,
                    px = px,
                    py = py
                },
                hasQueryPose = poseCamera != null,
                queryCameraPosition = poseCamera != null ? poseCamera.transform.position : Vector3.zero,
                queryCameraRotation = poseCamera != null ? poseCamera.transform.rotation : Quaternion.identity
            };

            if (logRequests)
            {
                Debug.Log(
                    $"CamRendererVpsImageSource request -> source={imageSource}, " +
                    $"encodedBytes={encodedBytes.Length}, size={imageWidth}x{imageHeight}, " +
                    $"intrinsics=({fx:F3}, {fy:F3}, {px:F3}, {py:F3}), " +
                    $"hasQueryPose={request.hasQueryPose}");
            }
            return true;
        }

        private byte[] TryEncodeFromDebugImage()
        {
            if (!camRenderer.debugMode || camRenderer.image == null)
            {
                return null;
            }

            Texture sourceTexture = camRenderer.image.texture;
            if (sourceTexture == null)
            {
                return null;
            }

            Texture2D rgbTexture = ConvertTextureToRgb24(sourceTexture);
            if (rgbTexture == null)
            {
                return null;
            }

            try
            {
                return rgbTexture.EncodeToJPG(jpegQuality);
            }
            finally
            {
                Object.Destroy(rgbTexture);
            }
        }

        private byte[] EncodeCurrentFrame(byte[] rgbaBytes)
        {
            int rgbaLength = imageWidth * imageHeight * 4;
            int rgbLength = imageWidth * imageHeight * 3;
            int rgbaOffset = rgbaBytes.Length >= rgbaLength + 4 ? 4 : 0;

            if (rgbaBytes.Length - rgbaOffset < rgbaLength)
            {
                Debug.LogError($"CamRendererVpsImageSource: invalid frame length. Got {rgbaBytes.Length}, expected at least {rgbaLength + rgbaOffset}.");
                return null;
            }

            if (m_rgbBuffer == null || m_rgbBuffer.Length != rgbLength)
            {
                m_rgbBuffer = new byte[rgbLength];
            }

            for (int i = 0; i < imageWidth * imageHeight; i++)
            {
                int rgbaIndex = rgbaOffset + i * 4;
                int rgbIndex = i * 3;
                m_rgbBuffer[rgbIndex] = rgbaBytes[rgbaIndex];
                m_rgbBuffer[rgbIndex + 1] = rgbaBytes[rgbaIndex + 1];
                m_rgbBuffer[rgbIndex + 2] = rgbaBytes[rgbaIndex + 2];
            }

            if (m_rgbTexture == null || m_rgbTexture.width != imageWidth || m_rgbTexture.height != imageHeight)
            {
                m_rgbTexture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
            }

            m_rgbTexture.LoadRawTextureData(m_rgbBuffer);
            m_rgbTexture.Apply(false, false);
            return m_rgbTexture.EncodeToJPG(jpegQuality);
        }

        private Texture2D ConvertTextureToRgb24(Texture sourceTexture)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(imageWidth, imageHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(sourceTexture, renderTexture);
                RenderTexture.active = renderTexture;

                Texture2D texture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private void OnDestroy()
        {
            if (m_rgbTexture != null)
            {
                Destroy(m_rgbTexture);
                m_rgbTexture = null;
            }
        }

        private void ApplyPreset(CameraParameterPreset preset)
        {
            switch (preset)
            {
                case CameraParameterPreset.Current1280x720:
                    imageWidth = 1280;
                    imageHeight = 720;
                    fx = 1161.352133f;
                    fy = 1162.871712f;
                    px = 630.465034f;
                    py = 368.853068f;
                    break;

                case CameraParameterPreset.Legacy960x1280:
                    imageWidth = 960;
                    imageHeight = 1280;
                    fx = 2205f;
                    fy = 2205f;
                    px = 1080f;
                    py = 1440f;
                    break;

                case CameraParameterPreset.Custom:
                default:
                    break;
            }

            lastAppliedPreset = preset;
        }
    }
}
