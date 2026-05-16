using System;
using System.Collections.Generic;

namespace MultiSet
{
	[Serializable]
	public class ObjectTrackingResponse
	{
		public bool poseFound;

		public Position position;

		public Rotation rotation;

		public double confidence;

		public List<string> objectCodes;
	}
}
