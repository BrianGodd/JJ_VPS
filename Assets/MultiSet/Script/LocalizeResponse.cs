using System;

namespace MultiSet
{
	[Serializable]
	public class LocalizeResponse
	{
		public bool poseFound;

		public LocalizationSuccessResponse localizationSuccess;

		public LocalizationFailureResponse localizationFailure;
	}
}
