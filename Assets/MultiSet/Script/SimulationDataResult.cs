using System;

namespace MultiSet
{
	[Serializable]
	public class SimulationDataResult
	{
		public string _id;

		public string name;

		public int fileSize;

		public string originalFilename;

		public string status;

		public string simulationCode;

		public DateTime createdAt;

		public string s3Key;
	}
}
