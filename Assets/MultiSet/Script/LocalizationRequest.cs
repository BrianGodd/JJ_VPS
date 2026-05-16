using System;

namespace MultiSet
{
	[Serializable]
	public class LocalizationRequest
	{
		public string mapId;

		public CameraParams cameraIntrinsics;

		public bool useMesh;

		public Resolution resolution;

		public string queryImage;
	}
}
