using System;
using System.Collections.Generic;

namespace MultiSet
{
	[Serializable]
	public class LocalizationSuccessResponse
	{
		public bool poseFound;

		public Position position;

		public Rotation rotation;

		public float confidence;

		public List<string> mapIds;
	}
}
