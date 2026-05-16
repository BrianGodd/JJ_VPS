using System;

namespace MultiSet
{
	[Serializable]
	public class ModelSet
	{
		public string _id;

		public string objectName;

		public string accountId;

		public string status;

		public string trackingType;

		public string objectCode;

		public int storage;

		public Source source;

		public DateTime createdAt;

		public DateTime updatedAt;

		public ObjectMesh objectMesh;

		public string thumbnail;
	}
}
