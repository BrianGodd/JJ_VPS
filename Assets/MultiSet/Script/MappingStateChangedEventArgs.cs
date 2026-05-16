using System;

namespace MultiSet
{
	public class MappingStateChangedEventArgs : EventArgs
	{
		public MappingState PreviousState { get; }

		public MappingState CurrentState { get; }

		public string Message { get; }

		public MappingStateChangedEventArgs(MappingState previousState, MappingState currentState, string message = null)
		{
			PreviousState = previousState;
			CurrentState = currentState;
			Message = message;
		}
	}
}
