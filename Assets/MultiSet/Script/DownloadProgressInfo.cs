namespace MultiSet
{
	public class DownloadProgressInfo
	{
		public string MapCode { get; set; }

		public int ProgressPercent { get; set; }

		public long DownloadedBytes { get; set; }

		public long TotalBytes { get; set; }

		public bool IsComplete { get; set; }

		public bool HasError { get; set; }

		public string ErrorMessage { get; set; }
	}
}
