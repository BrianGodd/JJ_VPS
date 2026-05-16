using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class MapLocalizationManager : MonoBehaviour
	{
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

		private ARSession aRSession;

		private TrackingState lastTrackingState;

		private bool isLocalizing = false;

		[Header("Number of Frames to Capture")]
		[Range(4f, 6f)]
		public int numberOfFrames = 4;

		[Header("Frame Capture Interval In Sec")]
		[Range(0.4f, 0.8f)]
		public float frameCaptureInterval = 0.3f;

		[Tooltip("Enable to detect blur in image during localization")]
		[Header("Blur Check Settings")]
		public bool enableBlurCheck = true;

		[Space(20f)]
		[Tooltip("Check for Confidence value post localization")]
		public bool confidenceCheck = false;

		[Tooltip("Localization Confidence Ranges")]
		[Range(0.2f, 0.8f)]
		public float _confidenceThreshold = 0.3f;

		[Header("Toast Alert Settings")]
		[Tooltip("Enable this to show an alert on localization success or failure.")]
		public bool showAlert = true;

		[Header("Initial Request Handling")]
		[Tooltip("If enabled, the first localization request will retry silently until it succeeds without showing failure messages.")]
		public bool firstLocalizationUntilSuccess = true;

		[Space(16f)]
		[Header("Localization Callbacks")]
		[Tooltip("Localization Initialization Callback")]
		public UnityEvent LocalizationInit = new UnityEvent();

		[Tooltip("Localization Requested Callback")]
		public UnityEvent LocalizationRequested = new UnityEvent();

		[Tooltip("Localization Success Callback")]
		public UnityEvent LocalizationSuccess = new UnityEvent();

		[Tooltip("Localization Failure Callback")]
		public UnityEvent LocalizationFailure = new UnityEvent();

		private VpsMap? vpsMap = null;

		private string localizedMapId = null;

		private List<ImageData> capturedImages = new List<ImageData>();

		private UploadData? uploadData;

		[HideInInspector]
		public List<string>? hintMapCodes = null;

		[HideInInspector]
		public string? hintPosition = null;

		private Vector3 camPos;

		private Quaternion camRot;

		private float blurThreshold = 50f;

		private int maxBlurRetries = 10;

		private float blurRetryDelay = 0.3f;

		private int m_blurRetryCount = 0;

		[Header("GeoHint and GeoCoordinates Settings")]
		[Tooltip("Enable this if want to pass GPS values in Localization request")]
		public bool passGeoPose = false;

		[Tooltip("Enable this to get Geo Coordinates in Localization response")]
		public bool geoCoordinatesInResponse = false;

		private GPSCoordinates coordinates = default(GPSCoordinates);

		private void Awake()
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			m_CameraManager = ((Component)arCamera).GetComponent<ARCameraManager>();
			aRSession = Object.FindFirstObjectByType<ARSession>();
			if ((Object)(object)aRSession == (Object)null)
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
			if ((Object)(object)m_CameraManager == (Object)null)
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
			if (relocalization && (Object)(object)aRSession != (Object)null && aRSession.subsystem != null)
			{
				ARSession.stateChanged += OnARSessionStateChanged;
			}
		}

		private void OnDisable()
		{
			EventManager<EventData>.StopListening("AuthCallBack", AuthCallBack);
			if (relocalization && (Object)(object)aRSession != (Object)null && aRSession.subsystem != null)
			{
				ARSession.stateChanged -= OnARSessionStateChanged;
			}
		}

		private void AuthCallBack(EventData @event)
		{
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Invalid comparison between Unknown and I4
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Invalid comparison between Unknown and I4
			if (@event.AuthSuccess)
			{
				Debug.Log((object)"Auth Success.");
				if (string.IsNullOrWhiteSpace(mapOrMapsetCode))
				{
					ToastManager.Instance.ShowToast("Map or MapSet Code Missing !!");
					Debug.LogError((object)"Map or MapSet Code Missing in MapLocalizationManager!!");
				}
				else if ((int)Application.platform == 8 || (int)Application.platform == 11)
				{
					((MonoBehaviour)this).StartCoroutine(StartAutoLocalize(2f));
				}
			}
			else
			{
				Debug.LogError((object)"Auth Failed!");
				ToastManager.Instance.ShowToast("Auth Failed!");
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
			//IL_0042: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Invalid comparison between Unknown and I4
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Invalid comparison between Unknown and I4
			bgLocalizationRequest = false;
			hintPosition = null;
			localizedMapId = null;
			m_blurRetryCount = 0;
			UnityEvent localizationInit = LocalizationInit;
			if (localizationInit != null)
			{
				localizationInit.Invoke();
			}
			capturedImages.Clear();
			isLocalizing = true;
			if ((int)Application.platform == 8 || (int)Application.platform == 11)
			{
				RequestForLocalization();
				return;
			}
			Debug.Log((object)"Localization not supported in Unity Editor. Using simulation data to test localization.");
			LocalizeSimulationData();
		}

		private void RequestForLocalization()
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Invalid comparison between Unknown and I4
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d7: Invalid comparison between Unknown and I4
			//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
				XRCpuImage val = default(XRCpuImage);
			if (m_CameraManager.TryAcquireLatestCpuImage(out val))
			{
				camPos = ((Component)arCamera).transform.position;
				camRot = ((Component)arCamera).transform.rotation;
				if (passGeoPose && (Object)(object)GpsCoordinateHandler.Instance != (Object)null)
				{
					coordinates = GpsCoordinateHandler.Instance.gpsCoordinates;
				}
				XRCpuImage.ConversionParams val2 = default(XRCpuImage.ConversionParams);
				val2.inputRect = new RectInt(0, 0, val.width, val.height);
				val2.outputDimensions = new Vector2Int(val.width / compressionRatio, val.height / compressionRatio);
				val2.outputFormat = (TextureFormat)3;
				val2.transformation = (XRCpuImage.Transformation)(((int)Screen.orientation != 1 && (int)Screen.orientation != 2) ? 1 : 2);
				val.ConvertAsync(val2, (Action<XRCpuImage.AsyncConversionStatus, XRCpuImage.ConversionParams, NativeArray<byte>>)ProcessImage);
				val.Dispose();
			}
			else
			{
				Debug.LogWarning((object)"Failed to acquire CPU image.");
				((MonoBehaviour)this).StartCoroutine(WaitAndCaptureFrame());
			}
		}

		private IEnumerator WaitAndCaptureFrame()
		{
			yield return (object)new WaitForSeconds(frameCaptureInterval);
			RequestForLocalization();
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
						isLocalizing = false;
						LocalizationFailureCallback();
					}
					else
					{
						((MonoBehaviour)this).StartCoroutine(RetryAfterBlurDelay());
					}
				}
				else
				{
					m_blurRetryCount = 0;
					AddFrameDataForQuery(val);
					Object.Destroy((Object)(object)val);
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
			RequestForLocalization();
		}

		private void AddFrameDataForQuery(Texture2D texture)
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Invalid comparison between Unknown and I4
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0053: Invalid comparison between Unknown and I4
			//IL_0127: Unknown result type (might be due to invalid IL or missing references)
			//IL_0142: Unknown result type (might be due to invalid IL or missing references)
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0178: Unknown result type (might be due to invalid IL or missing references)
			//IL_0193: Unknown result type (might be due to invalid IL or missing references)
			//IL_0198: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0066: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_009c: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_0102: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			byte[] imageBytes = ImageConversion.EncodeToJPG(texture, 80);
				XRCameraIntrinsics val = default(XRCameraIntrinsics);
			if (m_CameraManager.TryGetIntrinsics(out val))
			{
				if (capturedImages.Count == 0)
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
					uploadData = new UploadData
					{
						width = resolution.width,
						height = resolution.height,
						px = cameraParams.px,
						py = cameraParams.py,
						fx = cameraParams.fx,
						fy = cameraParams.fy
					};
				}
				ImageData item = new ImageData
				{
					imageBytes = imageBytes,
					metadata = new ImageMetadata
					{
						x = camPos.x,
						y = camPos.y,
						z = camPos.z,
						qx = camRot.x,
						qy = camRot.y,
						qz = camRot.z,
						qw = camRot.w
					}
				};
				capturedImages.Add(item);
			}
			if (capturedImages.Count >= numberOfFrames)
			{
				uploadData.images = new List<ImageData>(capturedImages);
				capturedImages.Clear();
				CallQueryAPI();
			}
			else
			{
				((MonoBehaviour)this).StartCoroutine(WaitAndCaptureFrame());
			}
		}

		private void CallQueryAPI()
		{
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Expected O, but got Unknown
			if (uploadData?.images == null || uploadData.images.Count == 0)
			{
				LocalizationFailureCallback();
				return;
			}
			WWWForm form = new WWWForm();
			if (localizationType.Equals(LocalizationType.Map))
			{
				form.AddField("mapCode", mapOrMapsetCode);
			}
			else
			{
				form.AddField("mapSetCode", mapOrMapsetCode);
				if (hintMapCodes != null)
				{
					foreach (string hintMapCode in hintMapCodes)
					{
						form.AddField("hintMapCodes", hintMapCode);
					}
				}
			}
			form.AddField("width", uploadData.width.ToString());
			form.AddField("isRightHanded", "false");
			form.AddField("height", uploadData.height.ToString());
			form.AddField("fx", uploadData.fx.ToString(CultureInfo.InvariantCulture));
			form.AddField("fy", uploadData.fy.ToString(CultureInfo.InvariantCulture));
			form.AddField("px", uploadData.px.ToString(CultureInfo.InvariantCulture));
			form.AddField("py", uploadData.py.ToString(CultureInfo.InvariantCulture));
			if (passGeoPose && coordinates.IsValid())
			{
				string text = coordinates.latitude.ToString(CultureInfo.InvariantCulture) + "," + coordinates.longitude.ToString(CultureInfo.InvariantCulture) + "," + coordinates.altitude.ToString(CultureInfo.InvariantCulture);
				form.AddField("geoHint", text);
			}
			if (geoCoordinatesInResponse)
			{
				form.AddField("convertToGeoCoordinates", "true");
			}
			for (int i = 0; i < uploadData.images.Count; i++)
			{
				string text2 = "image" + (i + 1);
				string text3 = text2 + "_data";
				byte[] imageBytes = uploadData.images[i].imageBytes;
				form.AddBinaryData(text2, imageBytes, "image_" + i + ".JPG", "image/jpeg");
				string text4 = JsonUtility.ToJson((object)uploadData.images[i].metadata);
				form.AddField(text3, text4);
			}
			((MonoBehaviour)this).StartCoroutine(MultiSetHttpClient.CheckInternetConnection(delegate(bool isConnected)
			{
				if (!isConnected)
				{
					ToastManager.Instance.ShowToast("Network Error!");
					LocalizationFailureCallback();
				}
				else
				{
					LocalizationRequestedCallback();
					MultiSetApiManager.LocalizeRequestMultiQuery(form, LocalizeCallback);
				}
			}));
		}

		private void LocalizeCallback(bool success, string data, long statusCode)
		{
			isLocalizing = false;
			if (string.IsNullOrEmpty(data))
			{
				LocalizationFailureCallback();
				ToastManager.Instance.ShowToast("Localization Failed! ");
				return;
			}
			if (success)
			{
				LocalizeResponse localizeResponse = JsonUtility.FromJson<LocalizeResponse>(data);
				if (localizeResponse.poseFound)
				{
					LocalizationResponseMultiFrame localizationResponseMultiFrame = JsonUtility.FromJson<LocalizationResponseMultiFrame>(data);
					if (localizationResponseMultiFrame == null)
					{
						Debug.LogError((object)("localization Response is null! " + statusCode + " data: " + data));
						LocalizationFailureCallback();
						return;
					}
					if (confidenceCheck && localizationResponseMultiFrame.confidence < _confidenceThreshold)
					{
						LocalizationFailureCallback();
						return;
					}
					poseHandler(localizationResponseMultiFrame);
				}
				else
				{
					Debug.LogError((object)("Localization Pose not found! responseCode " + statusCode + " data: " + data));
					LocalizationFailureCallback();
				}
			}
			else
			{
				Debug.LogError((object)("Localization Failed! responseCode " + statusCode + " data: " + data));
				LocalizationFailureCallback();
			}
			if (backgroundLocalization)
			{
				if (backgroundLocalizationCoroutine != null)
				{
					((MonoBehaviour)this).StopCoroutine(backgroundLocalizationCoroutine);
				}
				backgroundLocalizationCoroutine = ((MonoBehaviour)this).StartCoroutine(RequestForBackgroundLocalization());
			}
		}

		private IEnumerator RequestForBackgroundLocalization()
		{
			try
			{
				yield return (object)new WaitForSeconds(bgLocalizationDuration);
				if (!isLocalizing)
				{
					uploadData = null;
					capturedImages.Clear();
					isLocalizing = true;
					bgLocalizationRequest = true;
					Debug.Log((object)"Requesting background localization...");
					RequestForLocalization();
				}
			}
			finally
			{
				backgroundLocalizationCoroutine = null;
			}
		}

		private void poseHandler(LocalizationResponseMultiFrame response)
		{
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008c: Invalid comparison between Unknown and I4
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Invalid comparison between Unknown and I4
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Unknown result type (might be due to invalid IL or missing references)
			//IL_013c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0141: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0146: Unknown result type (might be due to invalid IL or missing references)
			//IL_014b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0150: Unknown result type (might be due to invalid IL or missing references)
			//IL_015e: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b4: Invalid comparison between Unknown and I4
			//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01bd: Invalid comparison between Unknown and I4
			Matrix4x4 val = Matrix4x4.TRS(new Vector3(response.estimatedPose.position.x, response.estimatedPose.position.y, response.estimatedPose.position.z), new Quaternion(response.estimatedPose.rotation.x, response.estimatedPose.rotation.y, response.estimatedPose.rotation.z, response.estimatedPose.rotation.w), Vector3.one);
			Matrix4x4 val2 = (((int)Application.platform != 8 && (int)Application.platform != 11) ? Matrix4x4.TRS(camPos, camRot, Vector3.one) : Matrix4x4.TRS(new Vector3(response.trackingPose.position.x, response.trackingPose.position.y, response.trackingPose.position.z), new Quaternion(response.trackingPose.rotation.x, response.trackingPose.rotation.y, response.trackingPose.rotation.z, response.trackingPose.rotation.w), Vector3.one));
			Matrix4x4 val3 = val2 * val.inverse;
			mapSpace.transform.rotation = val3.rotation;
			mapSpace.transform.position = new Vector3(val3[0, 3], val3[1, 3], val3[2, 3]);
			mapSpace.SetActive(true);
			LocalizationSuccessCallback();
			if ((int)Application.platform == 8 || (int)Application.platform == 11)
			{
				GetMeshFile(response.mapIds[0]);
			}
		}

		private void ShowSuccessAlert()
		{
			if (showAlert && !bgLocalizationRequest)
			{
				ToastManager.Instance.ShowToast("Localization Success");
			}
		}

		private void ShowFailureAlert()
		{
			if (showAlert && !bgLocalizationRequest)
			{
				ToastManager.Instance.ShowToast("Localization Failed!");
			}
		}

		public void LocalizationRequestedCallback()
		{
			if (firstLocalizationUntilSuccess && !bgLocalizationRequest)
			{
				Debug.Log((object)"First localization request: Ignore LocalizationRequested event.");
				return;
			}
			UnityEvent localizationRequested = LocalizationRequested;
			if (localizationRequested != null)
			{
				localizationRequested.Invoke();
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
			if (firstLocalizationUntilSuccess && !bgLocalizationRequest)
			{
				Debug.Log((object)"First localization failed! : Retrying silently...");
				((MonoBehaviour)this).StartCoroutine(RetryFirstLocalization());
				return;
			}
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

		private void OnDestroy()
		{
			if (backgroundLocalizationCoroutine != null)
			{
				((MonoBehaviour)this).StopCoroutine(backgroundLocalizationCoroutine);
				backgroundLocalizationCoroutine = null;
			}
			Util.CleanUp();
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
				Debug.LogWarning((object)"No network connection");
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
					MapMeshHandler.Instance?.GetLocalizedMapDetails(mapSetResult.mapSet, localizedMapId, mapSpace);
				}
			}
			else
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				Debug.LogError((object)(" Load MapSet Info Failed: " + errorJSON.error + "  code: " + statusCode));
			}
		}

		public void LocalizeSimulationData()
		{
			firstLocalizationUntilSuccess = false;
			backgroundLocalization = false;
			if (SimulationDataManager.Instance.selectedSimulationIndex < 0 || SimulationDataManager.Instance.simulationDataResponse == null || SimulationDataManager.Instance.simulationDataResponse.simulationData == null || SimulationDataManager.Instance.selectedSimulationIndex >= SimulationDataManager.Instance.simulationDataResponse.simulationData.Count)
			{
				Debug.LogError((object)"No simulation selected or invalid selection for localization!");
				ToastManager.Instance.ShowAlert("No Simulation data Selected!");
				LocalizationFailureCallback();
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
				ToastManager.Instance.ShowAlert("No Simulation data Selected!");
				LocalizationFailureCallback();
			}
		}

		private void LoadSimulationImagesAndProcess(string[] imagePaths, string jsonPath)
		{
			//IL_008f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				string text = File.ReadAllText(jsonPath);
				SimulationData simulationData = JsonUtility.FromJson<SimulationData>(text);
				if (simulationData == null)
				{
					Debug.LogError((object)"Invalid simulation data structure! Try Again.");
					LocalizationFailureCallback();
					return;
				}
				uploadData = new UploadData
				{
					width = simulationData.width,
					height = simulationData.height,
					px = simulationData.px,
					py = simulationData.py,
					fx = simulationData.fx,
					fy = simulationData.fy
				};
				camPos = ((Component)arCamera).transform.position;
				camRot = ((Component)arCamera).transform.rotation;
				List<ImageData> list = new List<ImageData>(imagePaths.Length);
				for (int i = 0; i < imagePaths.Length; i++)
				{
					list.Add(new ImageData
					{
						imageBytes = File.ReadAllBytes(imagePaths[i]),
						metadata = simulationData.imageDataList[i]
					});
				}
				uploadData.images = list;
				CallQueryAPI();
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Error loading simulation data: " + ex.Message));
				LocalizationFailureCallback();
			}
		}
	}
}
