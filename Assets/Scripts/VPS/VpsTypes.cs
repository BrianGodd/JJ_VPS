using System;
using UnityEngine;

namespace JJ.Vps
{
    public enum VpsProviderType
    {
        Immersal,
        MultiSet
    }

    [Serializable]
    public class VpsCameraIntrinsics
    {
        public float fx;
        public float fy;
        public float px;
        public float py;
    }

    [Serializable]
    public class VpsLocalizationRequest
    {
        public byte[] encodedImageBytes;
        public string imageMimeType = "image/jpeg";
        public int imageWidth;
        public int imageHeight;
        public VpsCameraIntrinsics intrinsics = new VpsCameraIntrinsics();
        public bool hasQueryPose;
        public Vector3 queryCameraPosition;
        public Quaternion queryCameraRotation = Quaternion.identity;
    }

    [Serializable]
    public class VpsLocalizationResult
    {
        public bool success;
        public string providerId;
        public string mapId;
        public float confidence;
        public bool hasLocalizedCameraPose;
        public Vector3 localizedCameraPosition;
        public Quaternion localizedCameraRotation = Quaternion.identity;
        public bool hasAppliedWorldPose;
        public Vector3 appliedWorldPosition;
        public Quaternion appliedWorldRotation = Quaternion.identity;
        public bool providerAppliedWorldPoseInternally;
        public string rawResponse;
        public string errorMessage;
    }
}
