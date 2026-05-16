using System;
using UnityEngine;

namespace MultiSet
{
	public class GeoTransformer
	{
		private const double EarthRadiusMeters = 6378137.0;

		private readonly double m_originLatitudeRad;

		private readonly double m_originLongitudeRad;

		private readonly double m_originAltitude;

		private readonly Quaternion m_headingRotation;

		public GeoTransformer(double originLatitudeDeg, double originLongitudeDeg, double originAltitude, double headingDeg)
		{
			m_originLatitudeRad = DegreesToRadians(originLatitudeDeg);
			m_originLongitudeRad = DegreesToRadians(originLongitudeDeg);
			m_originAltitude = originAltitude;
			m_headingRotation = Quaternion.Euler(0f, (float)(-headingDeg), 0f);
		}

		public Vector3 GlobalToLocal(double latitudeDeg, double longitudeDeg, double altitude)
		{
			double latitudeRad = DegreesToRadians(latitudeDeg);
			double longitudeRad = DegreesToRadians(longitudeDeg);

			double deltaLat = latitudeRad - m_originLatitudeRad;
			double deltaLon = longitudeRad - m_originLongitudeRad;

			double eastMeters = deltaLon * Math.Cos((latitudeRad + m_originLatitudeRad) * 0.5) * EarthRadiusMeters;
			double northMeters = deltaLat * EarthRadiusMeters;
			double upMeters = altitude - m_originAltitude;

			Vector3 enuPosition = new Vector3((float)eastMeters, (float)upMeters, (float)northMeters);
			return m_headingRotation * enuPosition;
		}

		private static double DegreesToRadians(double degrees)
		{
			return degrees * Math.PI / 180.0;
		}
	}
}
