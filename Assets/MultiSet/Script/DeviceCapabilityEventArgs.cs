using System;

namespace MultiSet
{
	public class DeviceCapabilityEventArgs : EventArgs
	{
		public bool MeshingSupported { get; }

		public string Message { get; }

		public DeviceCapabilityEventArgs(bool meshingSupported, string message = null)
		{
			MeshingSupported = meshingSupported;
			Message = message;
		}
	}
}
