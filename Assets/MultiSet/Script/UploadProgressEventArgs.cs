using System;

namespace MultiSet
{
	public class UploadProgressEventArgs : EventArgs
	{
		public float Progress { get; }

		public int ProgressPercent => (int)(Progress * 100f);

		public UploadProgressEventArgs(float progress)
		{
			Progress = progress;
		}
	}
}
