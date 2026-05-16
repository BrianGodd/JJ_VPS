using System;
using System.Collections.Generic;

namespace MultiSet
{
	[Serializable]
	public class SimulationData
	{
		public int width;

		public int height;

		public float px;

		public float py;

		public float fx;

		public float fy;

		public List<ImageMetadata> imageDataList;
	}
}
