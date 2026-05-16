using System;

namespace MultiSet
{
	public class MappingErrorEventArgs : EventArgs
	{
		public string ErrorMessage { get; }

		public Exception Exception { get; }

		public MappingErrorEventArgs(string errorMessage, Exception exception = null)
		{
			ErrorMessage = errorMessage;
			Exception = exception;
		}
	}
}
