using System;
using UnityEngine;

namespace MultiSet
{
	[Serializable]
	public class CameraPose
	{
		public Vector3 Position;

		public Quaternion Rotation;

		public CameraParams cameraParams;
	}
}
