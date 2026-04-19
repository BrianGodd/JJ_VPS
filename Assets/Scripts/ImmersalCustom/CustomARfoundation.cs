/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Immersal.XR
{
    public class ARFoundationSupport : MonoBehaviour, IPlatformSupport
    {
        //custom
        public Camera Main_Camera;
        public ImmersalAPI ImmersalAPI;
        public CamRenderer CamRenderer;
        private byte[] camBytes;
        private int width = 1280;
        private int height = 720;
        //
        [SerializeField, Tooltip("Maximum configuration attempts")]
        private int m_MaxConfigurationAttempts = 10;
        
        [SerializeField, Tooltip("Milliseconds to wait between configuration attempts")]
        private int m_MsBetweenConfigurationAttempts = 100;
        
        private ARCameraManager m_CameraManager;
        private ARSession m_ARSession;
        private Transform m_CameraTransform;

        private XRCameraConfiguration? m_InitialConfig;
        private IPlatformConfiguration m_Configuration;
        private bool m_ConfigDone = false;

        private bool m_OverrideScreenOrientation = false;
        private ScreenOrientation m_ScreenOrientationOverride = ScreenOrientation.Portrait;

        public ARCameraManager cameraManager
        {
            get
            {
                if (m_CameraManager == null)
                {
                    m_CameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();
                }
                return m_CameraManager;
            }
        }

        public ARSession arSession
        {
            get
            {
                if (m_ARSession == null)
                {
                    m_ARSession = UnityEngine.Object.FindObjectOfType<ARSession>();
                }
                return m_ARSession;
            }
        }

        public enum CameraResolution { Default, HD, FullHD, Max };	// With Huawei AR Engine SDK, only Default (640x480) and Max (1440x1080) are supported.
        
        [SerializeField]
        [Tooltip("Android resolution")]
        private CameraResolution m_AndroidResolution = CameraResolution.FullHD;
        
        [SerializeField]
        [Tooltip("iOS resolution")]
        private CameraResolution m_iOSResolution = CameraResolution.Default;

        [SerializeField]
        private CameraDataFormat m_CameraDataFormat = CameraDataFormat.SingleChannel;
        
        public CameraResolution androidResolution
        {
            get { return m_AndroidResolution; }
            set
            {
                m_AndroidResolution = value;
                ConfigureCamera();
            }
        }

        public CameraResolution iOSResolution
        {
            get { return m_iOSResolution; }
            set
            {
                m_iOSResolution = value;
                ConfigureCamera();
            }
        }

        private Task<(bool, CameraData)> m_CurrentCameraDataTask;
        private bool m_isTracking = false;

        public async Task<IPlatformConfigureResult> ConfigurePlatform()
        {
            PlatformConfiguration config = new PlatformConfiguration
            {
                CameraDataFormat = m_CameraDataFormat
            };
            return await ConfigurePlatform(config);
        }

        public async Task<IPlatformConfigureResult> ConfigurePlatform(IPlatformConfiguration configuration)
        {
            ImmersalLogger.Log("Configuring ARF Platform");
            
#if UNITY_EDITOR
            ImmersalLogger.LogWarning("Running AR Foundation Platform in Unity Editor will result in failed updates.");
#endif
            m_CameraManager = UnityEngine.Object.FindObjectOfType<ARCameraManager>();

            if (!m_CameraManager)
            {
                throw new ComponentTaskCriticalException("Could not find ARCameraManager.");
            }
            
            m_ARSession = UnityEngine.Object.FindObjectOfType<ARSession>();
            if (!m_ARSession)
            {
                throw new ComponentTaskCriticalException("Could not find ARSession.");
            }

            m_Configuration = configuration;
            
            if (Camera.main != null) m_CameraTransform = Main_Camera.transform;

            for (int i = 0; i < m_MaxConfigurationAttempts; i++)
            {
                m_ConfigDone = ConfigureCamera();

                if (m_ConfigDone)
                    break;

                await Task.Delay(m_MsBetweenConfigurationAttempts);
            }

            IPlatformConfigureResult r = new SimplePlatformConfigureResult
            { 
                Success = m_ConfigDone
            };
            
            return r;
        }

        private bool ConfigureCamera()
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
			var cameraSubsystem = cameraManager.subsystem;
			if (cameraSubsystem == null || !cameraSubsystem.running)
				return false;
			var configurations = cameraSubsystem.GetConfigurations(Allocator.Temp);
			if (!configurations.IsCreated || (configurations.Length <= 0))
				return false;
			int bestError = int.MaxValue;
			var currentConfig = cameraSubsystem.currentConfiguration;
			int dw = (int)currentConfig?.width;
			int dh = (int)currentConfig?.height;
			if (dw == 0 && dh == 0)
				return false;
#if UNITY_ANDROID
			CameraResolution reso = androidResolution;
#else
			CameraResolution reso = iOSResolution;
#endif

			if (!m_ConfigDone)
			{
				m_InitialConfig = currentConfig;
			}

			switch (reso)
			{
				case CameraResolution.Default:
					dw = (int)currentConfig?.width;
					dh = (int)currentConfig?.height;
					break;
				case CameraResolution.HD:
					dw = 1280;
					dh = 720;
					break;
				case CameraResolution.FullHD:
					dw = 1920;
					dh = 1080;
					break;
				case CameraResolution.Max:
					dw = 80000;
					dh = 80000;
					break;
			}

			foreach (var config in configurations)
			{
				int perror = config.width * config.height - dw * dh;
				if (Math.Abs(perror) < bestError)
				{
					bestError = Math.Abs(perror);
					currentConfig = config;
				}
			}

			if (reso != CameraResolution.Default) {
				ImmersalLogger.Log($"resolution = {(int)currentConfig?.width}x{(int)currentConfig?.height}");
				cameraSubsystem.currentConfiguration = currentConfig;
			}
			else
			{
				cameraSubsystem.currentConfiguration = m_InitialConfig;
			}
#endif
            return true;
        }
        
        public async Task<IPlatformUpdateResult> UpdatePlatform()
        {
            return await UpdateWithConfiguration(m_Configuration);
        }
        
        public async Task<IPlatformUpdateResult> UpdatePlatform(IPlatformConfiguration oneShotConfiguration)
        {
            return await UpdateWithConfiguration(oneShotConfiguration);
        }
        
        private async Task<IPlatformUpdateResult> UpdateWithConfiguration(IPlatformConfiguration configuration)
        {
            ImmersalLogger.Log("Updating ARF Platform");
            
            if (!m_ConfigDone)
                throw new ComponentTaskCriticalException("Trying to update platform before configuration.");
            
            // Status
            SimplePlatformStatus platformStatus = new SimplePlatformStatus
            {
                TrackingQuality = m_isTracking ? 1 : 0
            };

            m_CurrentCameraDataTask = GetCameraData(configuration.CameraDataFormat);
            (bool success, CameraData data) = await m_CurrentCameraDataTask;

            // UpdateResult
            SimplePlatformUpdateResult r = new SimplePlatformUpdateResult
            {
                Success = success,
                Status = platformStatus,
                CameraData = (ICameraData)data
            };
       
            return r;
        }

        private async Task<(bool, CameraData)> GetCameraData(CameraDataFormat cameraDataFormat)
        {
            if (!GetIntrinsics(out Vector4 intrinsics))
            {
                ImmersalLogger.LogError("Could not acquire camera intrinsics.");
                return (false, null);
            }

            if (m_CameraTransform == null)
            {
                ImmersalLogger.LogError("Could not acquire camera pose.");
                return (false, null);
            }
            
            // bool imageAcquired = false;
            // Task<XRCpuImage> t = Task.Run(() =>
            // {
            //     // XRCpuImage lifecycle will be managed by CameraData/ImageData
            //     imageAcquired = m_CameraManager.TryAcquireLatestCpuImage(out XRCpuImage image);
            //     return image;
            // });
            // XRCpuImage image = await t;
            
            // if (!imageAcquired)
            // {
            //     ImmersalLogger.LogError("Could not acquire camera image.");
            //     return (false, null);
            // }

            int pixelCount = width * height;
            byte[] rgbData = new byte[pixelCount * 3];

            for (int i = 0; i < pixelCount; i++)
            {
                int rgbaIndex = i * 4;
                int rgbIndex  = i * 3;

                rgbData[rgbIndex + 0] = CamRenderer.camBytes[rgbaIndex + 0];
                rgbData[rgbIndex + 1] = CamRenderer.camBytes[rgbaIndex + 1];
                rgbData[rgbIndex + 2] = CamRenderer.camBytes[rgbaIndex + 2];
            }

            SimpleImageData imageData = new SimpleImageData(rgbData);
            
            // ARFImageData imageData = new ARFImageData(image, cameraDataFormat);
            CameraData data = new CameraData(imageData)
            {
                Width = width,
                Height = height,
                Intrinsics = intrinsics,
                Format = cameraDataFormat,
                Channels = cameraDataFormat == CameraDataFormat.SingleChannel ? 1 : 3,
                CameraPositionOnCapture = m_CameraTransform.position,
                CameraRotationOnCapture = m_CameraTransform.rotation,
                Distortion = new double[] { 0.154663f, -0.734957f, 0.000000f, 0.000000f, 1.281777f },
                Orientation = GetOrientation()
            };

            return (true, data);
        }

        public bool GetIntrinsics(out Vector4 intrinsics)
        {
            intrinsics = Vector4.zero;
            // XRCameraIntrinsics intr = default;

            // bool success = m_CameraManager != null && m_CameraManager.TryGetIntrinsics(out intr);

            // if (success)
            // {
            //     intrinsics.x = intr.focalLength.x;
            //     intrinsics.y = intr.focalLength.y;
            //     intrinsics.z = intr.principalPoint.x;
            //     intrinsics.w = intr.principalPoint.y;
            // }

            // return success;
            
            intrinsics.x = 1161.352133f;
            intrinsics.y = 1162.871712f;
            intrinsics.z = 630.465034f;
            intrinsics.w = 368.853068f;

            return true;
        }

        public void SetOrientationOverride(ScreenOrientation newOrientation)
        {
            m_OverrideScreenOrientation = true;
            m_ScreenOrientationOverride = newOrientation;
        }

        public void DisableOrientationOverride()
        {
            m_OverrideScreenOrientation = false;
        }
        
        public Quaternion GetOrientation()
        {
            ScreenOrientation orientation =
                m_OverrideScreenOrientation ? m_ScreenOrientationOverride : Screen.orientation;
            float angle = orientation switch
            {
                ScreenOrientation.Portrait => 90f,
                ScreenOrientation.LandscapeLeft => 180f,
                ScreenOrientation.LandscapeRight => 0f,
                ScreenOrientation.PortraitUpsideDown => -90f,
                _ => 0f
            };
            return Quaternion.Euler(0f, 0f, angle);
        }

        private void OnEnable()
        {
#if !UNITY_EDITOR
			m_isTracking = ARSession.state == ARSessionState.SessionTracking;
			ARSession.stateChanged += ARSessionStateChanged;
#endif
        }
        
        private void OnDisable()
        {
#if !UNITY_EDITOR
			ARSession.stateChanged -= ARSessionStateChanged;
#endif
            m_isTracking = false;
        }

        private void ARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            m_isTracking = args.state == ARSessionState.SessionTracking;
        }
        
        public async Task StopAndCleanUp()
        {
	         // there is no cancellation token for the update procedure here, just wait
             await m_CurrentCameraDataTask;
             m_ConfigDone = false;
             m_isTracking = false;
        }
    }
    
    
}