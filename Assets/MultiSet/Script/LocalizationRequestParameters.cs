using System;
using UnityEngine;

namespace MultiSet
{
	[Serializable]
	public class LocalizationRequestParameters
	{
		public string mapCode;

		public string mapSetCode;

		public bool convertToGeoCoordinates;

		public Vector4 cameraIntrinsics;

		public int actualWidth;

		public int actualHeight;

		public Vector3 hintPosition;
	}
}
