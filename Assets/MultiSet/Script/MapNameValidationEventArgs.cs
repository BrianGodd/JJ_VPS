using System;

namespace MultiSet
{
	public class MapNameValidationEventArgs : EventArgs
	{
		public bool IsValid { get; }

		public string ErrorMessage { get; }

		public MapNameValidationEventArgs(bool isValid, string errorMessage = null)
		{
			IsValid = isValid;
			ErrorMessage = errorMessage;
		}
	}
}
