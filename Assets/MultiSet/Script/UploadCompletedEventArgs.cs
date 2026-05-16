using System;

namespace MultiSet
{
	public class UploadCompletedEventArgs : EventArgs
	{
		public bool Success { get; }

		public string ErrorMessage { get; }

		public string MapId { get; }

		public UploadCompletedEventArgs(bool success, string mapId = null, string errorMessage = null)
		{
			Success = success;
			MapId = mapId;
			ErrorMessage = errorMessage;
		}
	}
}
