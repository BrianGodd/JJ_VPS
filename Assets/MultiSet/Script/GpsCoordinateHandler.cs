using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class GpsCoordinateHandler : MonoBehaviour
	{
		public GPSCoordinates gpsCoordinates = default(GPSCoordinates);

		private bool isGpsEnabled = false;

		private static GpsCoordinateHandler instance;

		public double latitude { get; private set; } = 0.0;

		public double longitude { get; private set; } = 0.0;

		public double altitude { get; private set; } = 0.0;

		public double trueHeading { get; private set; } = 0.0;

		public bool isGpsOn => (int)Input.location.status == 2;

		public static GpsCoordinateHandler Instance
		{
			get
			{
				if (Application.isEditor && (Object)(object)instance == (Object)null && !Application.isPlaying)
				{
					instance = Object.FindObjectOfType<GpsCoordinateHandler>();
				}
				return instance;
			}
		}

		private void Awake()
		{
			if ((Object)(object)Instance == (Object)null)
			{
				instance = this;
				Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
			}
			else
			{
				Object.Destroy((Object)(object)((Component)this).gameObject);
			}
		}

		public void EnableGpsHandler()
		{
			isGpsEnabled = true;
			((MonoBehaviour)this).StartCoroutine(CheckForPermissions());
			StartGPS();
			EnableCompass();
		}

		private void EnableCompass()
		{
			if (!Input.compass.enabled)
			{
				Input.compass.enabled = true;
			}
		}

		private IEnumerator CheckForPermissions()
		{
			if ((int)Application.platform == 11)
			{
				yield return (object)new WaitUntil((Func<bool>)(() => !Permission.HasUserAuthorizedPermission("android.permission.CAMERA")));
				Permission.RequestUserPermission("android.permission.CAMERA");
				yield return (object)new WaitUntil((Func<bool>)(() => !Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION")));
				Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");
			}
			else if ((int)Application.platform == 8)
			{
				yield return Application.RequestUserAuthorization((UserAuthorization)1);
				((MonoBehaviour)this).StartCoroutine(EnableLocationServices());
			}
		}

		private void Update()
		{
			if (isGpsEnabled)
			{
				UpdateLocation();
			}
		}

		public bool IsPermissionGranted()
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Invalid comparison between Unknown and I4
			if (Application.isEditor)
			{
				return true;
			}
			if ((int)Input.location.status == 2)
			{
				return true;
			}
			return false;
		}

		public void StartGPS()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			//IL_000b: Invalid comparison between Unknown and I4
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Invalid comparison between Unknown and I4
			RuntimePlatform platform = Application.platform;
			RuntimePlatform val = platform;
			if ((int)val != 8)
			{
				if ((int)val == 11)
				{
					if (Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
					{
						((MonoBehaviour)this).StartCoroutine(EnableLocationServices());
						return;
					}
					Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");
					((MonoBehaviour)this).StartCoroutine(WaitForLocationPermission());
				}
			}
			else
			{
				((MonoBehaviour)this).StartCoroutine(EnableLocationServices());
			}
		}

		public void StopGPS()
		{
			Input.location.Stop();
			if (Input.compass.enabled)
			{
				Input.compass.enabled = false;
			}
		}

		private void UpdateLocation()
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			if (isGpsOn)
			{
				LocationInfo lastData = Input.location.lastData;
				latitude = lastData.latitude;
				lastData = Input.location.lastData;
				longitude = lastData.longitude;
				lastData = Input.location.lastData;
				altitude = lastData.altitude;
				if (Input.compass.enabled)
				{
					trueHeading = Input.compass.trueHeading;
				}
				gpsCoordinates.latitude = latitude;
				gpsCoordinates.longitude = longitude;
				gpsCoordinates.altitude = altitude;
				gpsCoordinates.trueHeading = trueHeading;
			}
		}

		private IEnumerator WaitForLocationPermission()
		{
			while (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
			{
				yield return null;
			}
			((MonoBehaviour)this).StartCoroutine(EnableLocationServices());
			yield return null;
		}

		public IEnumerator EnableLocationServices()
		{
			if (Input.location.isEnabledByUser)
			{
				Input.location.Start(0.001f, 0.001f);
				int maxWait = 20;
				while ((int)Input.location.status == 1 && maxWait > 0)
				{
					yield return (object)new WaitForSeconds(1f);
					maxWait--;
				}
				if (maxWait >= 1 && (int)Input.location.status != 3 && (int)Input.location.status == 2)
				{
					GpsCoordinateHandler gpsCoordinateHandler = this;
					LocationInfo lastData = Input.location.lastData;
					gpsCoordinateHandler.latitude = lastData.latitude;
					GpsCoordinateHandler gpsCoordinateHandler2 = this;
					lastData = Input.location.lastData;
					gpsCoordinateHandler2.longitude = lastData.longitude;
					GpsCoordinateHandler gpsCoordinateHandler3 = this;
					lastData = Input.location.lastData;
					gpsCoordinateHandler3.altitude = lastData.altitude;
				}
			}
		}
	}
}
