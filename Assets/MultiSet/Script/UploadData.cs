using System;
using System.Collections.Generic;

namespace MultiSet
{
	[Serializable]
	public class UploadData
	{
		public int width;

		public int height;

		public float px;

		public float py;

		public float fx;

		public float fy;

		public List<ImageData> images = new List<ImageData>();
	}
}
