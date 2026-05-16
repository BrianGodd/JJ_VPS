using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class SingleFrameLocalizationManager : MonoBehaviour
	{
		private class LocalizationResult
		{
			public string queryId;

			public Vector3 queryCameraPos;

			public Quaternion queryCameraRot;

			public float confidence;

			public LocalizationSuccessResponse response;
		}

		[Serializable]
		public class LocalizationSnapshot
		{
			public bool success;

			public string mapId;

			public string mapName;

			public float confidence;

			public float localizationTimeSeconds;

			public bool hasLocalizedCameraPose;

			public Vector3 localizedCameraPosition;

			public Quaternion localizedCameraRotation = Quaternion.identity;

			public bool hasMapSpacePose;

			public Vector3 mapSpacePosition;

			public Quaternion mapSpaceRotation = Quaternion.identity;

			public string errorMessage;
		}

		[Header("AR Components")]
		[SerializeField]
		private Camera arCamera;

		private ARCameraManager m_CameraManager;

		private int compressionRatio = 1;

		[Space(10f)]
		[Tooltip("Assign MapSpace GameObject")]
		[SerializeField]
		private GameObject mapSpace;

		[Tooltip("Select Localization Type")]
		[Header("Localization Type")]
		public LocalizationType localizationType = LocalizationType.Map;

		[SerializeField]
		[Tooltip("Enter Map Code or MapSet Code that has to be localized.")]
		public string mapOrMapsetCode = string.Empty;

		[Space(10f)]
		[Header("Localization Settings")]
		[Tooltip("Automatic Localize on start")]
		public bool autoLocalize = true;

		[Space(5f)]
		[Tooltip("Enable if you want to localize the map in the background.")]
		public bool backgroundLocalization = true;

		[Range(15f, 180f)]
		[Tooltip("Duration in seconds for background localization.")]
		public float bgLocalizationDuration = 60f;

		private Coroutine backgroundLocalizationCoroutine;

		private bool bgLocalizationRequest = false;

		[Space(10f)]
		[Tooltip("Enable if you want to re-localize the map after AR Tracking is lost.")]
		public bool relocalization = true;

		[Header("VPS Route")]
		[Tooltip("If enabled, VPS localization relies on ARSession/ARCore tracking. Disable this to use pure MultiSet localization with externally supplied image bytes and intrinsics.")]
		public bool useARSessionForLocalization = true;

		private ARSession aRSession;

		private TrackingState lastTrackingState;

		private bool isLocalizing = false;

		[Space(20f)]
		[Tooltip("Check for Confidence value post localization")]
		public bool confidenceCheck = false;

		[Tooltip("Localization Confidence Ranges")]
		[Range(0.2f, 0.8f)]
		public float _confidenceThreshold = 0.3f;

		[Tooltip("Localization Attempts")]
		[Range(1f, 5f)]
		public int _requestAttempts = 3;

		[Tooltip("Localization Attempts Interval in seconds")]
		[Range(1f, 5f)]
		public int localizationInterval = 1;

		private int m_currentLocalizeCount = 0;

		[Tooltip("Show Localization success or failure alert")]
		public bool showAlert = true;

		[Tooltip("Enable to detect blur in image during localization")]
		[Header("Blur Check Settings")]
		public bool enableBlurCheck = true;

		private float blurThreshold = 50f;

		private int maxBlurRetries = 10;

		private float blurRetryDelay = 0.3f;

		private int m_blurRetryCount = 0;

		[Header("Initial Request Handling")]
		[Tooltip("If enabled, the first localization request will retry silently until it succeeds without showing failure messages.")]
		public bool firstLocalizationUntilSuccess = true;

		[Space(16f)]
		[Tooltip("Localization Initialization Callback")]
		[Header("Localization Callbacks")]
		public UnityEvent LocalizationInit = new UnityEvent();

		[Tooltip("Localization Success Callback")]
		public UnityEvent LocalizationSuccess = new UnityEvent();

		[Tooltip("Localization Failure Callback")]
		public UnityEvent LocalizationFailure = new UnityEvent();

		[Header("Debug UI")]
		[SerializeField]
		[Tooltip("Optional TMP text used to display the running average localization time in seconds.")]
		private TextMeshProUGUI averageLocalizationTimeText;

		private VpsMap? vpsMap = null;

		private string localizedMapId = null;

		private Vector3 queryCameraPos;

		private Quaternion queryCameraRot;

		private string queryId;

		private List<LocalizationResult> localizationResults = new List<LocalizationResult>();

		private LocalizationResult bestResult = null;

		[Header("GeoHint and GeoCoordinates Settings")]
		[Tooltip("Enable this if want to pass GPS values in Localization request")]
		public bool passGeoPose = false;

		[Tooltip("Enable this to get Geo Coordinates in Localization response")]
		public bool geoCoordinatesInResponse = false;

		private GPSCoordinates coordinates = default(GPSCoordinates);

		[Header("Input Source")]
		[Tooltip("If enabled, localization uses externally supplied image bytes and intrinsics instead of ARCameraManager.")]
		public bool useExternalImageInput = false;

		private byte[] m_externalImageBytes;

		private CameraParams m_externalCameraParams;

		private Resolution m_externalResolution;

		private bool m_hasExternalPose;

		private Vector3 m_externalQueryCameraPos;

		private Quaternion m_externalQueryCameraRot;

		private float m_requestStartTime;

		private float m_totalLocalizationTimeSeconds;

		private int m_completedLocalizationCount;

		public bool IsLocalizing => isLocalizing;

		public LocalizationSnapshot LatestLocalization { get; private set; }

		public GameObject MapSpace => mapSpace;

		public float AverageLocalizationTimeSeconds => (m_completedLocalizationCount > 0) ? (m_totalLocalizationTimeSeconds / (float)m_completedLocalizationCount) : 0f;

		public event Action<LocalizationSnapshot> LocalizationCompleted;

		private void Awake()
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)arCamera != (Object)null)
			{
				m_CameraManager = ((Component)arCamera).GetComponent<ARCameraManager>();
			}
			aRSession = Object.FindFirstObjectByType<ARSession>();
			if (useARSessionForLocalization && (Object)(object)aRSession == (Object)null)
			{
				Debug.LogError((object)"ARSession is null. Please assign the ARSession component to the MapLocalizationManager script.");
			}
			lastTrackingState = (TrackingState)0;
		}

		private void Start()
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Invalid comparison between Unknown and I4
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			if (useARSessionForLocalization && (Object)(object)m_CameraManager == (Object)null && !useExternalImageInput)
			{
				Debug.LogError((object)"ARCameraManager component is missing!");
			}
			if ((int)Application.platform == 8)
			{
				compressionRatio = 2;
			}
			if (passGeoPose)
			{
				GpsCoordinateHandler gpsCoordinateHandler = Object.FindObjectOfType<GpsCoordinateHandler>();
				if ((Object)(object)gpsCoordinateHandler == (Object)null)
				{
					new GameObject("GpsCoordinateHandler").AddComponent<GpsCoordinateHandler>();
				}
				GpsCoordinateHandler.Instance?.EnableGpsHandler();
			}
		}

		private void OnEnable()
		{
			EventManager<EventData>.StartListening("AuthCallBack", AuthCallBack);
			if (useARSessionForLocalization && relocalization && (Object)(object)aRSession != (Object)null && aRSession.subsystem != null)
			{
				ARSession.stateChanged += OnARSessionStateChanged;
			}
		}

		private void OnDisable()
		{
			EventManager<EventData>.StopListening("AuthCallBack", AuthCallBack);
			if (useARSessionForLocalization && relocalization && (Object)(object)aRSession != (Object)null && aRSession.subsystem != null)
			{
				ARSession.stateChanged -= OnARSessionStateChanged;
			}
		}

		private void AuthCallBack(EventData @event)
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Invalid comparison between Unknown and I4
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Invalid comparison between Unknown and I4
			if (@event.AuthSuccess)
			{
				Debug.Log((object)"Auth Success.");
				if (string.IsNullOrWhiteSpace(mapOrMapsetCode))
				{
					ToastManager.Instance?.ShowToast("Map or MapSet Code Missing !!");
					Debug.LogError((object)"Map or MapSet Code Missing in MapLocalizationManager!!");
				}
				else if (!useARSessionForLocalization || useExternalImageInput || (int)Application.platform == 8 || (int)Application.platform == 11)
				{
					((MonoBehaviour)this).StartCoroutine(StartAutoLocalize(2f));
				}
			}
			else
			{
				Debug.LogError((object)"Auth Failed!");
				ToastManager.Instance?.ShowToast("Auth Failed!");
			}
		}

		private IEnumerator StartAutoLocalize(float delay)
		{
			yield return (object)new WaitForSeconds(delay);
			if (autoLocalize)
			{
				LocalizeFrame();
			}
		}

		public void LocalizeFrame()
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Invalid comparison between Unknown and I4
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Invalid comparison between Unknown and I4
			bgLocalizationRequest = false;
			localizationResults = new List<LocalizationResult>();
			bestResult = null;
			localizedMapId = null;
			m_blurRetryCount = 0;
			UnityEvent localizationInit = LocalizationInit;
			if (localizationInit != null)
			{
				localizationInit.Invoke();
			}
			if (useExternalImageInput || !useARSessionForLocalization || (int)Application.platform == 8 || (int)Application.platform == 11)
			{
				m_currentLocalizeCount = 0;
				RequestCurrentLocalization();
			}
			else
			{
				Debug.Log((object)"Localization not supported in Unity Editor. Using simulation data to test localization.");
				LocalizeSimulationData();
			}
		}

		public void LocalizeImageBytes(byte[] imageBytes, CameraParams cameraParams, Resolution resolution)
		{
			Vector3 cameraPosition;
			Quaternion cameraRotation;
			if (!TryGetCurrentQueryPose(out cameraPosition, out cameraRotation))
			{
				Debug.LogError((object)"No camera pose available. Assign arCamera or call the overload that provides camera pose explicitly.");
				return;
			}
			LocalizeImageBytes(imageBytes, cameraParams, resolution, cameraPosition, cameraRotation);
		}

		public void LocalizeImageBytes(byte[] imageBytes, CameraParams cameraParams, Resolution resolution, Vector3 cameraPosition, Quaternion cameraRotation)
		{
			if (!ValidateExternalLocalizationInput(imageBytes, cameraParams, resolution))
			{
				return;
			}
			m_externalImageBytes = (byte[])imageBytes.Clone();
			m_externalCameraParams = CloneCameraParams(cameraParams);
			m_externalResolution = CloneResolution(resolution);
			m_hasExternalPose = true;
			m_externalQueryCameraPos = cameraPosition;
			m_externalQueryCameraRot = cameraRotation;
			useExternalImageInput = true;
			bgLocalizationRequest = false;
			localizationResults = new List<LocalizationResult>();
			bestResult = null;
			localizedMapId = null;
			m_blurRetryCount = 0;
			m_currentLocalizeCount = 0;
			LocalizationInit?.Invoke();
			RequestCurrentLocalization();
		}

		private void RequestForLocalization()
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Invalid comparison between Unknown and I4
			//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fe: Invalid comparison between Unknown and I4
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			isLocalizing = true;
			if ((Object)(object)m_CameraManager == (Object)null)
			{
				Debug.LogError((object)"ARCameraManager component is missing!");
				isLocalizing = false;
				return;
			}
			XRCpuImage val = default(XRCpuImage);
			if (m_CameraManager.TryAcquireLatestCpuImage(out val))
			{
				queryCameraPos = ((Component)arCamera).transform.position;
				queryCameraRot = ((Component)arCamera).transform.rotation;
				if (passGeoPose && (Object)(object)GpsCoordinateHandler.Instance != (Object)null)
				{
					coordinates = GpsCoordinateHandler.Instance.gpsCoordinates;
				}
				queryId = "Query_" + (m_currentLocalizeCount + 1);
				XRCpuImage.ConversionParams val2 = default(XRCpuImage.ConversionParams);
				val2.inputRect = new RectInt(0, 0, val.width, val.height);
				val2.outputDimensions = new Vector2Int(val.width / compressionRatio, val.height / compressionRatio);
				val2.outputFormat = (TextureFormat)3;
				val2.transformation = (XRCpuImage.Transformation)(((int)Screen.orientation != 1 && (int)Screen.orientation != 2) ? 1 : 2);
				val.ConvertAsync(val2, (Action<XRCpuImage.AsyncConversionStatus, XRCpuImage.ConversionParams, NativeArray<byte>>)ProcessImage);
				val.Dispose();
			}
		}

		private void RequestCurrentLocalization()
		{
			if (!useARSessionForLocalization)
			{
				RequestLocalizationFromExternalInput();
			}
			else if (useExternalImageInput)
			{
				RequestLocalizationFromExternalInput();
			}
			else
			{
				RequestForLocalization();
			}
		}

		private void RequestLocalizationFromExternalInput()
		{
			if (!ValidateExternalLocalizationInput(m_externalImageBytes, m_externalCameraParams, m_externalResolution))
			{
				isLocalizing = false;
				return;
			}
			isLocalizing = true;
			if (passGeoPose && (Object)(object)GpsCoordinateHandler.Instance != (Object)null)
			{
				coordinates = GpsCoordinateHandler.Instance.gpsCoordinates;
			}
			queryId = "Query_" + (m_currentLocalizeCount + 1);
			if (m_hasExternalPose)
			{
				queryCameraPos = m_externalQueryCameraPos;
				queryCameraRot = m_externalQueryCameraRot;
			}
			else if (!TryGetCurrentQueryPose(out queryCameraPos, out queryCameraRot))
			{
				isLocalizing = false;
				Debug.LogError((object)"No camera pose available for external localization input.");
				return;
			}
			CallLocalizationAPI(m_externalImageBytes, m_externalCameraParams, m_externalResolution);
		}

		private void ProcessImage(XRCpuImage.AsyncConversionStatus status, XRCpuImage.ConversionParams conversionParams, NativeArray<byte> data)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Invalid comparison between Unknown and I4
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Expected O, but got Unknown
			//IL_0061: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Invalid comparison between Unknown and I4
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Invalid comparison between Unknown and I4
			try
			{
				if ((int)status != 3)
				{
					isLocalizing = false;
					Debug.LogErrorFormat("Async request failed with status {0}", new object[1] { status });
					return;
				}
				Vector2Int outputDimensions = conversionParams.outputDimensions;
				int x = outputDimensions.x;
				outputDimensions = conversionParams.outputDimensions;
				Texture2D val = new Texture2D(x, outputDimensions.y, conversionParams.outputFormat, false);
				val.LoadRawTextureData<byte>(data);
				val.Apply();
				if ((int)Screen.orientation == 1 || (int)Screen.orientation == 2)
				{
					val = Util.RotateTextureCounterClockwise(val);
				}
				if (enableBlurCheck && Util.IsImageBlur(val, blurThreshold))
				{
					Object.Destroy((Object)(object)val);
					m_blurRetryCount++;
					if (m_blurRetryCount >= maxBlurRetries)
					{
						m_blurRetryCount = 0;
						CheckAndScheduleNextRequest();
					}
					else
					{
						((MonoBehaviour)this).StartCoroutine(RetryAfterBlurDelay());
					}
				}
				else
				{
					m_blurRetryCount = 0;
					RequestLocalization(val);
				}
			}
			finally
			{
				data.Dispose();
			}
		}

		private IEnumerator RetryAfterBlurDelay()
		{
			yield return (object)new WaitForSeconds(blurRetryDelay);
			RequestCurrentLocalization();
		}

		private void RequestLocalization(Texture2D texture)
		{
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Invalid comparison between Unknown and I4
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Invalid comparison between Unknown and I4
			//IL_0109: Unknown result type (might be due to invalid IL or missing references)
			//IL_0123: Unknown result type (might be due to invalid IL or missing references)
			//IL_013d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Unknown result type (might be due to invalid IL or missing references)
			//IL_0172: Unknown result type (might be due to invalid IL or missing references)
			//IL_0177: Unknown result type (might be due to invalid IL or missing references)
			//IL_0190: Unknown result type (might be due to invalid IL or missing references)
			//IL_0195: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0091: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			byte[] imageBytes = ImageConversion.EncodeToJPG(texture, 80);
			XRCameraIntrinsics val = default(XRCameraIntrinsics);
			if (m_CameraManager.TryGetIntrinsics(out val))
			{
				CameraParams cameraParams = new CameraParams();
				Resolution resolution = new Resolution();
				Vector2Int resolution2;
				if ((int)Screen.orientation == 1 || (int)Screen.orientation == 2)
				{
					cameraParams.fx = val.focalLength.y / (float)compressionRatio;
					cameraParams.fy = val.focalLength.x / (float)compressionRatio;
					resolution2 = val.resolution;
					cameraParams.px = ((float)resolution2.y - val.principalPoint.y) / (float)compressionRatio;
					cameraParams.py = val.principalPoint.x / (float)compressionRatio;
					resolution2 = val.resolution;
					resolution.width = resolution2.y / compressionRatio;
					resolution2 = val.resolution;
					resolution.height = resolution2.x / compressionRatio;
				}
				else
				{
					cameraParams.fx = val.focalLength.x / (float)compressionRatio;
					cameraParams.fy = val.focalLength.y / (float)compressionRatio;
					cameraParams.px = val.principalPoint.x / (float)compressionRatio;
					cameraParams.py = val.principalPoint.y / (float)compressionRatio;
					resolution2 = val.resolution;
					resolution.width = resolution2.x / compressionRatio;
					resolution2 = val.resolution;
					resolution.height = resolution2.y / compressionRatio;
				}
				CallLocalizationAPI(imageBytes, cameraParams, resolution);
			}
		}

		private void CallLocalizationAPI(byte[] imageBytes, CameraParams cameraParams, Resolution resolution)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Expected O, but got Unknown
			WWWForm form = new WWWForm();
			if (localizationType.Equals(LocalizationType.Map))
			{
				form.AddField("mapCode", mapOrMapsetCode);
			}
			else
			{
				form.AddField("mapSetCode", mapOrMapsetCode);
			}
			form.AddField("isRightHanded", false.ToString());
			form.AddField("fx", cameraParams.fx.ToString(CultureInfo.InvariantCulture));
			form.AddField("fy", cameraParams.fy.ToString(CultureInfo.InvariantCulture));
			form.AddField("px", cameraParams.px.ToString(CultureInfo.InvariantCulture));
			form.AddField("py", cameraParams.py.ToString(CultureInfo.InvariantCulture));
			form.AddField("width", resolution.width.ToString());
			form.AddField("height", resolution.height.ToString());
			form.AddBinaryData("queryImage", imageBytes, "image.JPG", "image/jpeg");
			if (passGeoPose && coordinates.IsValid())
			{
				string text = coordinates.latitude.ToString(CultureInfo.InvariantCulture) + "," + coordinates.longitude.ToString(CultureInfo.InvariantCulture) + "," + coordinates.altitude.ToString(CultureInfo.InvariantCulture);
				form.AddField("geoHint", text);
			}
			if (geoCoordinatesInResponse)
			{
				form.AddField("convertToGeoCoordinates", "true");
			}
			m_requestStartTime = Time.realtimeSinceStartup;
			((MonoBehaviour)this).StartCoroutine(MultiSetHttpClient.CheckInternetConnection(delegate(bool isConnected)
			{
				if (!isConnected)
				{
					ToastManager.Instance?.ShowToast("Network Error!");
					LocalizationFailureCallback();
				}
				else
				{
					MultiSetApiManager.LocalizeRequest(form, LocalizeCallback);
				}
			}));
		}

		private void LocalizeCallback(bool success, string data, long statusCode)
		{
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009e: Unknown result type (might be due to invalid IL or missing references)
			isLocalizing = false;
			float localizationTimeSeconds = Time.realtimeSinceStartup - m_requestStartTime;
			if (string.IsNullOrEmpty(data))
			{
				LocalizationFailureCallback("Empty or null data received from MultiSet.", statusCode, data, localizationTimeSeconds);
				Debug.LogError((object)("Error : Localize Map Callback: Empty or null data received! statusCode=" + statusCode + ", localizationTimeSeconds=" + localizationTimeSeconds.ToString("F3")));
				return;
			}
			if (success)
			{
				LocalizeResponse localizeResponse = JsonUtility.FromJson<LocalizeResponse>(data);
				if (localizeResponse.poseFound)
				{
					RecordLocalizationTime(localizationTimeSeconds);
					LocalizationSuccessResponse localizationSuccessResponse = JsonUtility.FromJson<LocalizationSuccessResponse>(data);
					if (localizationSuccessResponse == null)
					{
						LocalizationFailureCallback();
						CheckAndScheduleNextRequest();
						Debug.LogError((object)"LocalizationSuccessResponse is null or not parsed correctly.");
						return;
					}
					LocalizationResult item = new LocalizationResult
					{
						queryId = queryId,
						queryCameraPos = queryCameraPos,
						queryCameraRot = queryCameraRot,
						confidence = localizationSuccessResponse.confidence,
						response = localizationSuccessResponse
					};
					localizationResults.Add(item);
				}
				else
				{
					LocalizationFailureResponse localizationFailureResponse = JsonUtility.FromJson<LocalizationFailureResponse>(data);
					string text = ((localizationFailureResponse != null) ? localizationFailureResponse.message : null);
					string text2 = string.IsNullOrEmpty(text) ? "poseFound=false" : ("poseFound=false, message=" + text);
					Debug.LogError((object)("Localization Failure. statusCode=" + statusCode + ", localizationTimeSeconds=" + localizationTimeSeconds.ToString("F3") + ", parsed=" + text2 + ", raw=" + data));
				}
			}
			else
			{
				LocalizationFailureCallback("MultiSet request failed. raw=" + data, statusCode, data, localizationTimeSeconds);
				Debug.LogError((object)("Localization Failed! statusCode=" + statusCode + ", localizationTimeSeconds=" + localizationTimeSeconds.ToString("F3") + ", raw=" + data));
			}
			CheckAndScheduleNextRequest();
		}

		private void CheckAndScheduleNextRequest()
		{
			m_currentLocalizeCount++;
			if (m_currentLocalizeCount < _requestAttempts)
			{
				((MonoBehaviour)this).StartCoroutine(RequestLocalizationCoroutine());
			}
			else
			{
				EvaluateLocalizationResults();
			}
		}

		private IEnumerator RequestLocalizationCoroutine()
		{
			yield return (object)new WaitForSeconds((float)localizationInterval);
			RequestCurrentLocalization();
		}

		private void EvaluateLocalizationResults()
		{
			if (backgroundLocalization)
			{
				if (backgroundLocalizationCoroutine != null)
				{
					((MonoBehaviour)this).StopCoroutine(backgroundLocalizationCoroutine);
				}
				backgroundLocalizationCoroutine = ((MonoBehaviour)this).StartCoroutine(RequestForBackgroundLocalization());
			}
			if (localizationResults.Count == 0)
			{
				LocalizationFailureCallback();
				return;
			}
			foreach (LocalizationResult localizationResult in localizationResults)
			{
				if (bestResult == null || localizationResult.confidence > bestResult.confidence)
				{
					bestResult = localizationResult;
				}
			}
			Debug.Log((object)(" Best Localization Result: Query ID = " + bestResult.queryId + ", Confidence = " + bestResult.confidence.ToString("F4")));
			if (confidenceCheck && bestResult.confidence < _confidenceThreshold)
			{
				LocalizationFailureCallback();
			}
			else
			{
				ApplyLocalizationPose(bestResult);
			}
		}

		private void ApplyLocalizationPose(LocalizationResult bestResult)
		{
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Invalid comparison between Unknown and I4
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Invalid comparison between Unknown and I4
			//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Invalid comparison between Unknown and I4
			//IL_014e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0155: Invalid comparison between Unknown and I4
			Vector3 val = new Vector3(bestResult.response.position.x, bestResult.response.position.y, bestResult.response.position.z);
			Quaternion val2 = new Quaternion(bestResult.response.rotation.x, bestResult.response.rotation.y, bestResult.response.rotation.z, bestResult.response.rotation.w);
			Matrix4x4 val3 = Matrix4x4.TRS(val, val2, Vector3.one);
			Matrix4x4 val4 = Matrix4x4.TRS(bestResult.queryCameraPos, bestResult.queryCameraRot, Vector3.one);
			Matrix4x4 val5 = val4 * val3.inverse;
			mapSpace.transform.rotation = val5.rotation;
			mapSpace.transform.position = new Vector3(val5[0, 3], val5[1, 3], val5[2, 3]);
			mapSpace.SetActive(true);
			LatestLocalization = new LocalizationSnapshot
			{
				success = true,
				mapId = ((bestResult.response.mapIds != null && bestResult.response.mapIds.Count > 0) ? bestResult.response.mapIds[0] : null),
				mapName = ((vpsMap != null) ? vpsMap.mapName : null),
				confidence = bestResult.confidence,
				localizationTimeSeconds = Time.realtimeSinceStartup - m_requestStartTime,
				hasLocalizedCameraPose = true,
				localizedCameraPosition = val,
				localizedCameraRotation = val2,
				hasMapSpacePose = (Object)(object)mapSpace != (Object)null,
				mapSpacePosition = mapSpace.transform.position,
				mapSpaceRotation = mapSpace.transform.rotation
			};
			Debug.Log((object)("Localization Success. " + BuildSuccessDebugMessage(LatestLocalization, bestResult)));
			LocalizationCompleted?.Invoke(LatestLocalization);
			LocalizationSuccessCallback();
			if (bestResult.response.mapIds != null && bestResult.response.mapIds.Count > 0)
			{
				GetMeshFile(bestResult.response.mapIds[0]);
			}
		}

		private IEnumerator RequestForBackgroundLocalization()
		{
			try
			{
				yield return (object)new WaitForSeconds(bgLocalizationDuration);
				if (!isLocalizing)
				{
					localizationResults = new List<LocalizationResult>();
					localizationResults.Clear();
					isLocalizing = true;
					bgLocalizationRequest = true;
					m_currentLocalizeCount = 0;
					Debug.Log((object)"Requesting background localization...");
					RequestCurrentLocalization();
				}
			}
			finally
			{
				backgroundLocalizationCoroutine = null;
			}
		}

		private void ShowSuccessAlert()
		{
			if (!bgLocalizationRequest && showAlert)
			{
				ToastManager.Instance?.ShowToast("Localization Success");
			}
		}

		private void ShowFailureAlert()
		{
			if (!bgLocalizationRequest && showAlert)
			{
				ToastManager.Instance?.ShowToast("Localization Failed!");
			}
		}

		public void LocalizationSuccessCallback()
		{
			ShowSuccessAlert();
			UnityEvent localizationSuccess = LocalizationSuccess;
			if (localizationSuccess != null)
			{
				localizationSuccess.Invoke();
			}
			MapMeshHandler.Instance?.LocalizationSuccessCallback();
			if (firstLocalizationUntilSuccess)
			{
				firstLocalizationUntilSuccess = false;
				Debug.Log((object)"First localization request succeeded. Future requests will use normal retry behavior.");
			}
		}

		public void LocalizationFailureCallback()
		{
			LocalizationFailureCallback("Localization failed.", 0L, null, 0f);
		}

		private void LocalizationFailureCallback(string reason, long statusCode, string rawResponse, float localizationTimeSeconds)
		{
			if (firstLocalizationUntilSuccess && !bgLocalizationRequest)
			{
				Debug.Log((object)("First localization failed! : Retrying silently... reason=" + reason + ", statusCode=" + statusCode + ", localizationTimeSeconds=" + localizationTimeSeconds.ToString("F3")));
				((MonoBehaviour)this).StartCoroutine(RetryFirstLocalization());
				return;
			}
			LatestLocalization = new LocalizationSnapshot
			{
				success = false,
				localizationTimeSeconds = localizationTimeSeconds,
				errorMessage = BuildFailureMessage(reason, statusCode, rawResponse)
			};
			LocalizationCompleted?.Invoke(LatestLocalization);
			ShowFailureAlert();
			UnityEvent localizationFailure = LocalizationFailure;
			if (localizationFailure != null)
			{
				localizationFailure.Invoke();
			}
		}

		private IEnumerator RetryFirstLocalization()
		{
			yield return (object)new WaitForEndOfFrame();
			if (!isLocalizing)
			{
				LocalizeFrame();
			}
		}

		private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			if (!useARSessionForLocalization)
			{
				return;
			}
			if (aRSession.subsystem == null)
			{
				return;
			}
			TrackingState trackingState = aRSession.subsystem.trackingState;
			if (trackingState != lastTrackingState)
			{
				if (relocalization)
				{
					HandleTrackingStateChange(trackingState);
				}
				lastTrackingState = trackingState;
			}
		}

		private void HandleTrackingStateChange(TrackingState newState)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Invalid comparison between Unknown and I4
			if ((int)newState != 0)
			{
				if ((int)newState == 1)
				{
					RelocalizeIfARTrackingChanged();
				}
			}
			else
			{
				RelocalizeIfARTrackingChanged();
			}
		}

		private void RelocalizeIfARTrackingChanged()
		{
			if (!isLocalizing)
			{
				((MonoBehaviour)this).StartCoroutine(StartAutoLocalize(1f));
			}
		}

		private void GetMeshFile(string selectedMapId)
		{
			localizedMapId = selectedMapId;
			if (!string.IsNullOrWhiteSpace(selectedMapId))
			{
				if (localizationType.Equals(LocalizationType.Map))
				{
					GetMapDetails(selectedMapId);
				}
				else
				{
					GetMapSetDetails(mapOrMapsetCode);
				}
			}
		}

		private void GetMapDetails(string mapId)
		{
			if (vpsMap == null)
			{
				MultiSetApiManager.GetMapDetails(mapId, MapDetailsCallback);
			}
		}

		private void MapDetailsCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.LogError((object)"Error : Map Details Callback: Empty or null data received!");
			}
			else if (success)
			{
				vpsMap = JsonUtility.FromJson<VpsMap>(data);
				mapOrMapsetCode = vpsMap.mapCode;
				if (LatestLocalization != null && LatestLocalization.success)
				{
					LatestLocalization.mapId = string.IsNullOrEmpty(LatestLocalization.mapId) ? vpsMap._id : LatestLocalization.mapId;
					LatestLocalization.mapName = vpsMap.mapName;
				}
				MapMeshHandler.Instance?.DownloadGlbFile(mapSpace, vpsMap);
			}
			else
			{
				Debug.LogError((object)"Get Map Details failed!");
			}
		}

		private void GetMapSetDetails(string mapOrMapsetCode)
		{
			if (Util.IsNetworkAvailable())
			{
				MultiSetApiManager.GetMapSetDetails(mapOrMapsetCode, MapSetDetailsCallback);
			}
			else
			{
				Debug.LogWarning((object)"No network connection!");
			}
		}

		private void MapSetDetailsCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.LogError((object)"MapSet Details Callback: Empty or null data received.");
			}
			else if (success)
			{
				MapSetResult mapSetResult = JsonUtility.FromJson<MapSetResult>(data);
				if (mapSetResult != null && mapSetResult.mapSet.mapSetData != null)
				{
					if (LatestLocalization != null && LatestLocalization.success)
					{
						VpsMap val = mapSetResult.mapSet.mapSetData.Select((MapSetData item) => item.map).FirstOrDefault((VpsMap map) => map != null && (map._id == localizedMapId || map.mapCode == localizedMapId));
						if (val != null)
						{
							LatestLocalization.mapId = string.IsNullOrEmpty(LatestLocalization.mapId) ? val._id : LatestLocalization.mapId;
							LatestLocalization.mapName = val.mapName;
						}
					}
					MapMeshHandler.Instance?.GetLocalizedMapDetails(mapSetResult.mapSet, localizedMapId, mapSpace);
				}
			}
			else
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				Debug.LogError((object)(" Load MapSet Info Failed: " + errorJSON.error + "  code: " + statusCode));
			}
		}

		private void OnDestroy()
		{
			if (backgroundLocalizationCoroutine != null)
			{
				((MonoBehaviour)this).StopCoroutine(backgroundLocalizationCoroutine);
				backgroundLocalizationCoroutine = null;
			}
			Util.CleanUp();
		}

		public void LocalizeSimulationData()
		{
			_requestAttempts = 1;
			backgroundLocalization = false;
			firstLocalizationUntilSuccess = false;
			if (SimulationDataManager.Instance.selectedSimulationIndex < 0 || SimulationDataManager.Instance.simulationDataResponse == null || SimulationDataManager.Instance.simulationDataResponse.simulationData == null || SimulationDataManager.Instance.selectedSimulationIndex >= SimulationDataManager.Instance.simulationDataResponse.simulationData.Count)
			{
				Debug.LogError((object)"No simulation selected or invalid selection for localization!");
				ToastManager.Instance?.ShowAlert("No Simulation data Selected!");
				return;
			}
			string simulationCode = SimulationDataManager.Instance.simulationDataResponse.simulationData[SimulationDataManager.Instance.selectedSimulationIndex].simulationCode;
			string text = Path.Combine(Application.persistentDataPath, SimulationDataManager.Instance.simulationDataDir);
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, simulationCode);
			if (text2 != null)
			{
				string[] files = Directory.GetFiles(text2, "*.jpg");
				string[] files2 = Directory.GetFiles(text2, "*.json");
				if (files.Length == 5 && files2.Length == 1)
				{
					LoadSimulationImagesAndProcess(files, files2[0]);
					return;
				}
				Debug.LogError((object)"Invalid Simulation Data! Try different simulation dataset.");
				LocalizationFailureCallback();
			}
			else
			{
				Debug.LogError((object)"No simulation data directory set for localization.");
				ToastManager.Instance?.ShowAlert("No Simulation data Selected!");
				LocalizationFailureCallback();
			}
		}

		private void LoadSimulationImagesAndProcess(string[] imagePaths, string jsonPath)
		{
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				string text = File.ReadAllText(jsonPath);
				SimulationData simulationData = JsonUtility.FromJson<SimulationData>(text);
				if (simulationData == null)
				{
					Debug.LogError((object)"Invalid simulation data structure");
					LocalizationFailureCallback();
					return;
				}
				queryCameraPos = ((Component)arCamera).transform.position;
				queryCameraRot = ((Component)arCamera).transform.rotation;
				CameraParams cameraParams = new CameraParams();
				Resolution resolution = new Resolution();
				cameraParams.fx = simulationData.fx;
				cameraParams.fy = simulationData.fy;
				cameraParams.px = simulationData.px;
				cameraParams.py = simulationData.py;
				byte[] imageBytes = File.ReadAllBytes(imagePaths[0]);
				resolution.width = simulationData.width;
				resolution.height = simulationData.height;
				CallLocalizationAPI(imageBytes, cameraParams, resolution);
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Error loading simulation data: " + ex.Message));
				LocalizationFailureCallback();
			}
		}

		private string BuildFailureMessage(string reason, long statusCode, string rawResponse)
		{
			string text = string.IsNullOrEmpty(reason) ? "Localization failed." : reason;
			if (statusCode > 0)
			{
				text = text + " statusCode=" + statusCode;
			}
			if (!string.IsNullOrEmpty(rawResponse))
			{
				text = text + " raw=" + rawResponse;
			}
			return text;
		}

		private string BuildSuccessDebugMessage(LocalizationSnapshot snapshot, LocalizationResult result)
		{
			string text = ((snapshot != null && !string.IsNullOrEmpty(snapshot.mapId)) ? snapshot.mapId : "null");
			string text3 = ((snapshot != null && !string.IsNullOrEmpty(snapshot.mapName)) ? snapshot.mapName : "null");
			string text2 = ((result != null && !string.IsNullOrEmpty(result.queryId)) ? result.queryId : "n/a");
			return "mapId=" + text + ", mapName=" + text3 + ", queryId=" + text2 + ", confidence=" + snapshot.confidence.ToString("F4") + ", localizationTimeSeconds=" + snapshot.localizationTimeSeconds.ToString("F3") + ", localizedCameraPosition=" + snapshot.localizedCameraPosition.ToString("F4") + ", localizedCameraRotation=" + snapshot.localizedCameraRotation.eulerAngles.ToString("F4") + ", mapSpacePosition=" + snapshot.mapSpacePosition.ToString("F4") + ", mapSpaceRotation=" + snapshot.mapSpaceRotation.eulerAngles.ToString("F4");
		}

		private void RecordLocalizationTime(float localizationTimeSeconds)
		{
			if (localizationTimeSeconds <= 0f)
			{
				return;
			}

			m_totalLocalizationTimeSeconds += localizationTimeSeconds;
			m_completedLocalizationCount++;
			UpdateAverageLocalizationTimeText();
		}

		private void UpdateAverageLocalizationTimeText()
		{
			if ((Object)(object)averageLocalizationTimeText != (Object)null)
			{
				averageLocalizationTimeText.text = "Avg: " + AverageLocalizationTimeSeconds.ToString("F3") + "s";
			}
		}

		private bool TryGetCurrentQueryPose(out Vector3 cameraPosition, out Quaternion cameraRotation)
		{
			Camera val = arCamera;
			if ((Object)(object)val == (Object)null)
			{
				val = Camera.main;
			}
			if ((Object)(object)val == (Object)null)
			{
				cameraPosition = Vector3.zero;
				cameraRotation = Quaternion.identity;
				return false;
			}
			cameraPosition = ((Component)val).transform.position;
			cameraRotation = ((Component)val).transform.rotation;
			return true;
		}

		private static CameraParams CloneCameraParams(CameraParams source)
		{
			return new CameraParams
			{
				fx = source.fx,
				fy = source.fy,
				px = source.px,
				py = source.py
			};
		}

		private static Resolution CloneResolution(Resolution source)
		{
			return new Resolution
			{
				width = source.width,
				height = source.height
			};
		}

		private bool ValidateExternalLocalizationInput(byte[] imageBytes, CameraParams cameraParams, Resolution resolution)
		{
			if (imageBytes == null || imageBytes.Length == 0)
			{
				Debug.LogError((object)"Localization image bytes are empty.");
				return false;
			}
			if (cameraParams == null)
			{
				Debug.LogError((object)"CameraParams is null.");
				return false;
			}
			if (resolution == null || resolution.width <= 0 || resolution.height <= 0)
			{
				Debug.LogError((object)"Resolution is invalid.");
				return false;
			}
			return true;
		}
	}
}
