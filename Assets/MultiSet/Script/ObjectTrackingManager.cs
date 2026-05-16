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
	public class ObjectTrackingManager : MonoBehaviour
	{
		[Header("AR Components")]
		[SerializeField]
		private Camera arCamera;

		private ARCameraManager m_CameraManager;

		private int compressionRatio = 1;

		private readonly Dictionary<string, GameObject> objectSpaces = new Dictionary<string, GameObject>();

		private GameObject trackedObjectSpace;

		private const int MaxObjectCodes = 10;

		[SerializeField]
		[Tooltip("Enter Object Codes that have to be tracked. Maximum 10 codes allowed.")]
		[Header("Enter Object Codes (Max 10)")]
		public string[] objectCodes = new string[10];

		[Space(10f)]
		[Header("Object Tracking Settings")]
		[Tooltip("Automatic Tracking on start")]
		public bool autoTracking = true;

		[Space(10f)]
		[Tooltip("Enable if you want to re-track the object after AR Tracking is lost.")]
		public bool restartTracking = true;

		[Space(20f)]
		[Tooltip("Check for Confidence value post localization")]
		public bool confidenceCheck = true;

		[Tooltip("Localization Confidence Ranges")]
		[Range(0.2f, 0.8f)]
		public float _confidenceThreshold = 0.3f;

		private ARSession aRSession;

		private TrackingState lastTrackingState;

		private bool isTracking = false;

		[Space(10f)]
		[Tooltip("Show Tracking success or failure alert")]
		public bool showAlert = true;

		[Header("Capture Settings")]
		public float captureDelay = 1f;

		[Tooltip("Enable to detect blur in image during localization")]
		[Header("Blur Check Settings")]
		public bool enableBlurCheck = true;

		private float blurThreshold = 50f;

		private int maxBlurRetries = 10;

		private float blurRetryDelay = 0.3f;

		private int m_blurRetryCount = 0;

		[Header("Initial Request Handling")]
		[Tooltip("If enabled, the first tracking request will retry silently until it succeeds without showing failure messages.")]
		public bool firstTrackingUntilSuccess = true;

		[Space(16f)]
		[Tooltip("Object Tracking Initialization Callback")]
		[Header("Object Tracking Callbacks")]
		public UnityEvent ObjectTrackingInit = new UnityEvent();

		[Tooltip("Object Tracking Requested Callback")]
		public UnityEvent ObjectTrackingRequested = new UnityEvent();

		[Tooltip("Object Tracking Success Callback")]
		public UnityEvent ObjectTrackingSuccess = new UnityEvent();

		[Tooltip("Object Tracking Failure Callback")]
		public UnityEvent ObjectTrackingFailure = new UnityEvent();

		private ModelSet? modelSet = null;

		private Vector3 queryCameraPos;

		private Quaternion queryCameraRot;

		private void Awake()
		{
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			m_CameraManager = ((Component)arCamera).GetComponent<ARCameraManager>();
			aRSession = Object.FindFirstObjectByType<ARSession>();
			if ((Object)(object)aRSession == (Object)null)
			{
				Debug.LogError((object)"ARSession is null. Please assign the ARSession component to the ObjectTrackingManager script.");
			}
			lastTrackingState = (TrackingState)0;
		}

		private void Start()
		{
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Invalid comparison between Unknown and I4
			if ((Object)(object)m_CameraManager == (Object)null)
			{
				Debug.LogError((object)"ARCameraManager component is missing!");
			}
			if ((int)Application.platform == 8)
			{
				compressionRatio = 2;
			}
			Screen.sleepTimeout = -1;
		}

		private void OnEnable()
		{
			EventManager<EventData>.StartListening("AuthCallBack", AuthCallBack);
			if (restartTracking && (Object)(object)aRSession != (Object)null && aRSession.subsystem != null)
			{
				ARSession.stateChanged += OnARSessionStateChanged;
			}
		}

		private void OnDisable()
		{
			EventManager<EventData>.StopListening("AuthCallBack", AuthCallBack);
			if (restartTracking && (Object)(object)aRSession != (Object)null && aRSession.subsystem != null)
			{
				ARSession.stateChanged -= OnARSessionStateChanged;
			}
		}

		private void AuthCallBack(EventData @event)
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Invalid comparison between Unknown and I4
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Invalid comparison between Unknown and I4
			if (@event.AuthSuccess)
			{
				Debug.Log((object)"Auth Success.");
				if (objectCodes == null || objectCodes.Length == 0)
				{
					ToastManager.Instance.ShowToast("Object Codes Missing !!");
					Debug.LogError((object)"Object Codes Missing in ObjectTrackingManager!!");
				}
				else if ((int)Application.platform == 8 || (int)Application.platform == 11)
				{
					((MonoBehaviour)this).StartCoroutine(StartAutoTracking(2f));
				}
			}
			else
			{
				Debug.LogError((object)"Auth Failed!");
				ToastManager.Instance.ShowToast("Auth Failed!");
			}
		}

		private IEnumerator StartAutoTracking(float delay)
		{
			yield return (object)new WaitForSeconds(delay);
			if (autoTracking)
			{
				StartObjectTracking();
			}
		}

		public void StartObjectTracking()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Invalid comparison between Unknown and I4
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Invalid comparison between Unknown and I4
			m_blurRetryCount = 0;
			if ((int)Application.platform == 8 || (int)Application.platform == 11)
			{
				UnityEvent objectTrackingInit = ObjectTrackingInit;
				if (objectTrackingInit != null)
				{
					objectTrackingInit.Invoke();
				}
				((MonoBehaviour)this).StartCoroutine(CaptureFrameForTracking());
			}
			else
			{
				Debug.Log((object)"Object Tracking not supported in Unity Editor. Using simulation data to test Object Tracking.");
				ObjectTrackingForSimulationData();
			}
		}

		private IEnumerator CaptureFrameForTracking()
		{
			yield return (object)new WaitForSeconds(captureDelay);
			RequestForObjectTracking();
		}

		private void RequestForObjectTracking()
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
			//IL_00aa: Invalid comparison between Unknown and I4
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Invalid comparison between Unknown and I4
			//IL_00be: Unknown result type (might be due to invalid IL or missing references)
			isTracking = true;
			XRCpuImage val = default(XRCpuImage);
			if (m_CameraManager.TryAcquireLatestCpuImage(out val))
			{
				queryCameraPos = ((Component)arCamera).transform.position;
				queryCameraRot = ((Component)arCamera).transform.rotation;
				XRCpuImage.ConversionParams val2 = default(XRCpuImage.ConversionParams);
				val2.inputRect = new RectInt(0, 0, val.width, val.height);
				val2.outputDimensions = new Vector2Int(val.width / compressionRatio, val.height / compressionRatio);
				val2.outputFormat = (TextureFormat)3;
				val2.transformation = (XRCpuImage.Transformation)(((int)Screen.orientation != 1 && (int)Screen.orientation != 2) ? 1 : 2);
				val.ConvertAsync(val2, (Action<XRCpuImage.AsyncConversionStatus, XRCpuImage.ConversionParams, NativeArray<byte>>)ProcessImage);
				val.Dispose();
			}
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
					isTracking = false;
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
						isTracking = false;
						ObjectTrackingFailureCallback();
					}
					else
					{
						((MonoBehaviour)this).StartCoroutine(RetryAfterBlurDelay());
					}
				}
				else
				{
					m_blurRetryCount = 0;
					RequestObjectTracking(val);
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
			RequestForObjectTracking();
		}

		private void RequestObjectTracking(Texture2D texture)
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
				CallObjectTrackingAPI(imageBytes, cameraParams, resolution);
			}
		}

		private void CallObjectTrackingAPI(byte[] imageBytes, CameraParams cameraParams, Resolution resolution)
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Expected O, but got Unknown
			WWWForm form = new WWWForm();
			string[] array = objectCodes;
			foreach (string text in array)
			{
				Debug.Log((object)("Adding Object Code for tracking: " + text));
				form.AddField("objectCode", text);
			}
			form.AddField("isRightHanded", false.ToString());
			form.AddField("fx", cameraParams.fx.ToString(CultureInfo.InvariantCulture));
			form.AddField("fy", cameraParams.fy.ToString(CultureInfo.InvariantCulture));
			form.AddField("px", cameraParams.px.ToString(CultureInfo.InvariantCulture));
			form.AddField("py", cameraParams.py.ToString(CultureInfo.InvariantCulture));
			form.AddField("width", resolution.width.ToString());
			form.AddField("height", resolution.height.ToString());
			form.AddBinaryData("queryImage", imageBytes, "image.JPG", "image/jpeg");
			((MonoBehaviour)this).StartCoroutine(MultiSetHttpClient.CheckInternetConnection(delegate(bool isConnected)
			{
				if (!isConnected)
				{
					ToastManager.Instance.ShowToast("Network Error!");
					ObjectTrackingFailureCallback();
				}
				else
				{
					ObjectTrackingRequestedCallback();
					MultiSetApiManager.LocalizeObject(form, ObjectTrackingCallback);
				}
			}));
		}

		private void ObjectTrackingCallback(bool success, string data, long statusCode)
		{
			isTracking = false;
			if (string.IsNullOrEmpty(data))
			{
				ObjectTrackingFailureCallback();
				Debug.LogError((object)"Error : Track ModelSet Callback: Empty or null data received!");
			}
			else if (success)
			{
				LocalizeResponse localizeResponse = JsonUtility.FromJson<LocalizeResponse>(data);
				if (localizeResponse.poseFound)
				{
					ObjectTrackingResponse objectTrackingResponse = JsonUtility.FromJson<ObjectTrackingResponse>(data);
					if (objectTrackingResponse == null)
					{
						ObjectTrackingFailureCallback();
						Debug.LogError((object)"ObjectTracking SuccessResponse is null or not parsed correctly.");
					}
					else if (localizeResponse.poseFound)
					{
						if (confidenceCheck && objectTrackingResponse.confidence < (double)_confidenceThreshold)
						{
							ObjectTrackingFailureCallback();
						}
						else
						{
							ApplyObjectTrackingPose(objectTrackingResponse);
						}
					}
				}
				else
				{
					ObjectTrackingFailureCallback();
					Debug.LogError((object)("ObjectTracking Failure.. " + data));
				}
			}
			else
			{
				ObjectTrackingFailureCallback();
				Debug.LogError((object)("ObjectTracking Failed! Try Again. : Code: " + statusCode));
			}
		}

		private void ApplyObjectTrackingPose(ObjectTrackingResponse trackingResponse)
		{
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0075: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0083: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
			Vector3 val = new Vector3(trackingResponse.position.x, trackingResponse.position.y, trackingResponse.position.z);
			Quaternion val2 = new Quaternion(trackingResponse.rotation.x, trackingResponse.rotation.y, trackingResponse.rotation.z, trackingResponse.rotation.w);
			Matrix4x4 val3 = Matrix4x4.TRS(val, val2, Vector3.one);
			Matrix4x4 val4 = Matrix4x4.TRS(queryCameraPos, queryCameraRot, Vector3.one);
			Matrix4x4 val5 = val4 * val3.inverse;
			if (trackingResponse.objectCodes != null && trackingResponse.objectCodes.Count > 0)
			{
				string objectCode = trackingResponse.objectCodes[0];
				GameObject orCreateObjectSpace = GetOrCreateObjectSpace(objectCode);
				orCreateObjectSpace.transform.rotation = val5.rotation;
				orCreateObjectSpace.transform.position = new Vector3(val5[0, 3], val5[1, 3], val5[2, 3]);
				orCreateObjectSpace.SetActive(true);
				trackedObjectSpace = orCreateObjectSpace;
				GetObjectDetails(objectCode);
			}
			ObjectTrackingSuccessCallback();
		}

		private GameObject GetOrCreateObjectSpace(string objectCode)
		{
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Expected O, but got Unknown
			if (objectSpaces.TryGetValue(objectCode, out GameObject value))
			{
				return value;
			}
			GameObject val = new GameObject(objectCode ?? "");
			objectSpaces[objectCode] = val;
			return val;
		}

		private void ShowSuccessAlert()
		{
			if (showAlert)
			{
				ToastManager.Instance.ShowToast("Object Tracking Success");
			}
		}

		private void ShowFailureAlert()
		{
			if (showAlert)
			{
				ToastManager.Instance.ShowToast("Object Tracking Failed!");
			}
		}

		private void ObjectTrackingRequestedCallback()
		{
			if (firstTrackingUntilSuccess)
			{
				Debug.Log((object)"Ignore First Tracking Requested event.");
				return;
			}
			UnityEvent objectTrackingRequested = ObjectTrackingRequested;
			if (objectTrackingRequested != null)
			{
				objectTrackingRequested.Invoke();
			}
		}

		private void ObjectTrackingSuccessCallback()
		{
			ShowSuccessAlert();
			UnityEvent objectTrackingSuccess = ObjectTrackingSuccess;
			if (objectTrackingSuccess != null)
			{
				objectTrackingSuccess.Invoke();
			}
			ObjectMeshHandler.Instance?.TrackingSuccessCallback();
			if (firstTrackingUntilSuccess)
			{
				firstTrackingUntilSuccess = false;
				Debug.Log((object)"First Tracking request succeeded. Future requests will use normal retry behavior.");
			}
		}

		private void ObjectTrackingFailureCallback()
		{
			if (firstTrackingUntilSuccess)
			{
				((MonoBehaviour)this).StartCoroutine(RetryFirstObjectTracking());
				Debug.Log((object)"First Object Tracking failed! : Retrying silently...");
				return;
			}
			ShowFailureAlert();
			UnityEvent objectTrackingFailure = ObjectTrackingFailure;
			if (objectTrackingFailure != null)
			{
				objectTrackingFailure.Invoke();
			}
		}

		private IEnumerator RetryFirstObjectTracking()
		{
			yield return (object)new WaitForEndOfFrame();
			if (!isTracking)
			{
				StartObjectTracking();
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
			if (aRSession.subsystem == null)
			{
				return;
			}
			TrackingState trackingState = aRSession.subsystem.trackingState;
			if (trackingState != lastTrackingState)
			{
				if (restartTracking)
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
					ReTrackIfARTrackingChanged();
				}
			}
			else
			{
				ReTrackIfARTrackingChanged();
			}
		}

		private void ReTrackIfARTrackingChanged()
		{
			if (!isTracking)
			{
				((MonoBehaviour)this).StartCoroutine(StartAutoTracking(1f));
			}
		}

		private void GetObjectDetails(string objectCode)
		{
			MultiSetApiManager.GetObjectDetails(objectCode, ObjectDetailsCallback);
		}

		private void ObjectDetailsCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.LogError((object)"Error : Object Details Callback: Empty or null data received!");
			}
			else if (success)
			{
				modelSet = JsonUtility.FromJson<ModelSet>(data);
				ObjectMeshHandler.Instance?.DownloadObjectGlbFile(modelSet, trackedObjectSpace);
			}
			else
			{
				Debug.LogError((object)"Get Object Details failed!");
			}
		}

		private void OnDestroy()
		{
			Util.CleanUp();
		}

		public void ObjectTrackingForSimulationData()
		{
			firstTrackingUntilSuccess = false;
			if (SimulationDataManager.Instance.selectedSimulationIndex < 0 || SimulationDataManager.Instance.simulationDataResponse == null || SimulationDataManager.Instance.simulationDataResponse.simulationData == null || SimulationDataManager.Instance.selectedSimulationIndex >= SimulationDataManager.Instance.simulationDataResponse.simulationData.Count)
			{
				Debug.LogError((object)"No simulation selected or invalid selection for object Tracking!");
				ToastManager.Instance.ShowAlert("No Simulation data Selected!");
				return;
			}
			string simulationCode = SimulationDataManager.Instance.simulationDataResponse.simulationData[SimulationDataManager.Instance.selectedSimulationIndex].simulationCode;
			string text = Path.Combine(Application.persistentDataPath, SimulationDataManager.Instance.simulationDataDir);
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, simulationCode);
			if (text2 != null)
			{
				ProcessSimulationData(text2);
				return;
			}
			Debug.LogError((object)"No simulation data directory set for object Tracking.");
			ToastManager.Instance.ShowAlert("No Simulation data Selected!");
			ObjectTrackingFailureCallback();
		}

		private void ProcessSimulationData(string extractPath)
		{
			string[] files = Directory.GetFiles(extractPath, "*.jpg");
			string[] files2 = Directory.GetFiles(extractPath, "*.json");
			if (files.Length == 5 && files2.Length == 1)
			{
				LoadSimulationImagesAndProcess(files, files2[0]);
				return;
			}
			Debug.LogError((object)$"Invalid file count - Images: {files.Length}, JSON: {files2.Length}");
			ObjectTrackingFailureCallback();
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
					ObjectTrackingFailureCallback();
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
				resolution.width = simulationData.width;
				resolution.height = simulationData.height;
				byte[] imageBytes = File.ReadAllBytes(imagePaths[0]);
				CallObjectTrackingAPI(imageBytes, cameraParams, resolution);
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Error loading simulation data: " + ex.Message));
				ObjectTrackingFailureCallback();
			}
		}
	}
}
