using System;
using System.Collections.Generic;

namespace MultiSet
{
	[Serializable]
	public class MapSet
	{
		public string _id;

		public string name;

		public string accountId;

		public DateTime createdAt;

		public DateTime updatedAt;

		public string mapSetCode;

		public string status;

		public List<MapSetData> mapSetData;
	}
}
