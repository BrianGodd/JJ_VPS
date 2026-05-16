using System;
using System.Collections.Generic;

namespace MultiSet
{
	[Serializable]
	public class LocalizationResponseMultiFrame
	{
		public bool poseFound;

		public EstimatedPose estimatedPose;

		public TrackingPose trackingPose;

		public List<string> mapIds;

		public float confidence;
	}
}
