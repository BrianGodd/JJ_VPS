namespace MultiSet
{
	public struct GPSCoordinates
	{
		public double latitude;

		public double longitude;

		public double altitude;

		public double trueHeading;

		public bool IsValid()
		{
			return latitude != 0.0 || longitude != 0.0 || altitude != 0.0;
		}
	}
}
