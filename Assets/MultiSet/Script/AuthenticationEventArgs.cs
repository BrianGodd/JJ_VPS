using System;

namespace MultiSet
{
	public class AuthenticationEventArgs : EventArgs
	{
		public bool IsAuthenticated { get; }

		public string Message { get; }

		public AuthenticationEventArgs(bool isAuthenticated, string message = null)
		{
			IsAuthenticated = isAuthenticated;
			Message = message;
		}
	}
}
