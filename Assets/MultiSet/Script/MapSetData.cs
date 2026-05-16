using System;

namespace MultiSet
{
	[Serializable]
	public class MapSetData
	{
		public string _id;

		public int order;

		public RelativePose relativePose;

		public DateTime createdAt;

		public DateTime updatedAt;

		public VpsMap map;
	}
}
