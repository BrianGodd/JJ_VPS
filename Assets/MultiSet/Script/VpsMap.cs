using System;

namespace MultiSet
{
	[Serializable]
	public class VpsMap
	{
		public string _id;

		public string accountId;

		public string mapName;

		public Location location;

		public string status;

		public double storage;

		public string mapCode;

		public Source source;

		public DateTime createdAt;

		public DateTime updatedAt;

		public CameraIntrinsics cameraIntrinsics;

		public MapMesh mapMesh;

		public Resolution resolution;

		public string thumbnail;

		public string globalFeature;

		public double heading;

		public string offlineBundleStatus;

		public string offlineBundle;
	}
}
