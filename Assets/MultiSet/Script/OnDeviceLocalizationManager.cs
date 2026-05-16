using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class OnDeviceLocalizationManager : MonoBehaviour
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

		[Space(10f)]
		[Tooltip("Select Localization Type")]
		[Header("Localization Type")]
		public LocalizationType localizationType = LocalizationType.Map;

		[SerializeField]
		[Tooltip("Enter Map Code or MapSet Code that has to be localized.")]
		public string mapOrMapsetCode = string.Empty;

		[HideInInspector]
		public string mapCode = string.Empty;

		[HideInInspector]
		public string mapSetCode = string.Empty;

		[Space(20f)]
		[Tooltip("Check for Confidence value post localization")]
		public bool confidenceCheck = false;

		[Tooltip("Localization Confidence Ranges")]
		[Range(0.2f, 0.8f)]
		public float _confidenceThreshold = 0.3f;

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

		[Tooltip("Show Localization success or failure alert")]
		public bool showAlert = true;

		[Tooltip("Enable to detect blur in image during localization")]
		[Header("Blur Check Settings")]
		public bool enableBlurCheck = true;

		private float blurThreshold = 50f;

		[Space(10f)]
		[Tooltip("Called when app is ready for localization (all initialization complete)")]
		public UnityEvent ReadyForLocalization = new UnityEvent();

		[Space(16f)]
		[Tooltip("Localization Initialization Callback")]
		[Header("Localization Callbacks")]
		public UnityEvent LocalizationInit = new UnityEvent();

		[Tooltip("Localization Requested Callback")]
		public UnityEvent LocalizationRequested = new UnityEvent();

		[Tooltip("Localization Success Callback")]
		public UnityEvent LocalizationSuccess = new UnityEvent();

		[Tooltip("Localization Failure Callback")]
		public UnityEvent LocalizationFailure = new UnityEvent();

		[Space(10f)]
		[HideInInspector]
		[Header("Download Progress (Runtime Mode)")]
		[Tooltip("Called when download progress changes. Use this to update UI with download percentage.")]
		public UnityEvent<int> OnDownloadProgressChanged = new UnityEvent<int>();

		[HideInInspector]
		[Tooltip("Called when download completes or fails")]
		public UnityEvent<bool> OnDownloadComplete = new UnityEvent<bool>();

		private Vector3 queryCameraPos;

		private Quaternion queryCameraRot;

		private string databasePath;

		private OfflineBundleManager bundleManager;

		public GameObject loaderPanel;

		[Space(20f)]
		[Header("Offline Bundle Settings")]
		[Tooltip("Choose how to download offline bundle: Editor (pre-download) or Runtime (download on device)")]
		public OfflineBundleDownloadMode offlineBundleDownloadMode = OfflineBundleDownloadMode.Editor;

		private string localizedMapCode = null;

		private bool licenseExists = false;

		private bool isReadyForLocalization = false;

		private bool modelsLoaded = false;

		private bool isLoadingModels = false;

		public bool IsReadyForLocalization => isReadyForLocalization;

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
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Invalid comparison between Unknown and I4
			if ((Object)(object)m_CameraManager == (Object)null)
			{
				Debug.LogError((object)"ARCameraManager component is missing!");
				return;
			}
			if ((int)Application.platform == 8)
			{
				compressionRatio = 2;
			}
			mapOrMapsetCode = mapOrMapsetCode.Trim();
			if (string.IsNullOrWhiteSpace(mapOrMapsetCode))
			{
				Debug.LogError((object)"Map or MapSet Code Missing in MapLocalizationManager!!");
			}
			else
			{
				InitiateOnDeviceLocalization();
			}
		}

		private void InitiateOnDeviceLocalization()
		{
			SetupBundleManager();
			licenseExists = bundleManager.CheckLicenseExists();
			if (licenseExists)
			{
				((MonoBehaviour)this).StartCoroutine(ValidateLicenseAndProceed());
			}
		}

		public void ValidateMapOrMapSetCode(string inputMapOrMapset)
		{
			mapOrMapsetCode = inputMapOrMapset.Trim();
			if (string.IsNullOrWhiteSpace(mapOrMapsetCode))
			{
				Debug.LogError((object)"Map or MapSet Code Missing in MapLocalizationManager!!");
				return;
			}
			SetupBundleManager();
			((MonoBehaviour)this).StartCoroutine(ValidateLicenseAndProceed());
		}

		private void SetupBundleManager()
		{
			if (localizationType == LocalizationType.Map)
			{
				mapCode = mapOrMapsetCode;
				mapSetCode = string.Empty;
			}
			else
			{
				mapCode = string.Empty;
				mapSetCode = mapOrMapsetCode;
			}
			if (bundleManager != null)
			{
				bundleManager.OnDownloadProgress -= HandleDownloadProgress;
			}
			bundleManager = new OfflineBundleManager(localizationType, mapCode, mapSetCode);
			bundleManager.OnDownloadProgress += HandleDownloadProgress;
			databasePath = bundleManager.SetupDatabasePath();
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
			if (@event.AuthSuccess)
			{
				Debug.Log((object)"Auth Success.");
				if (string.IsNullOrWhiteSpace(mapOrMapsetCode))
				{
					ToastManager.Instance.ShowToast("Map or MapSet Code Missing !!");
					Debug.LogError((object)"Map or MapSet Code Missing in MapLocalizationManager!!");
				}
				else if (!licenseExists)
				{
					((MonoBehaviour)this).StartCoroutine(ValidateLicenseAndProceed());
				}
			}
			else
			{
				Debug.LogError((object)"Auth Failed!");
			}
		}

		private IEnumerator ValidateLicenseAndProceed()
		{
			if ((Object)(object)loaderPanel != (Object)null)
			{
				loaderPanel.SetActive(true);
			}
			yield return (object)new WaitForSeconds(0.2f);
			Debug.Log((object)"Validating license, please wait...");
			yield return bundleManager.ValidateLicenseCoroutine();
			if (offlineBundleDownloadMode == OfflineBundleDownloadMode.Editor)
			{
				yield return SetupDatabasePathWithLoaderInternal();
			}
			else
			{
				yield return bundleManager.SetupRuntimeDownloadCoroutine(mapSpace);
			}
			if ((int)Application.platform == 8)
			{
				yield return PreloadModelsCoroutine();
			}
			if ((Object)(object)loaderPanel != (Object)null)
			{
				loaderPanel.SetActive(false);
			}
			isReadyForLocalization = true;
			UnityEvent readyForLocalization = ReadyForLocalization;
			if (readyForLocalization != null)
			{
				readyForLocalization.Invoke();
			}
		}

		private IEnumerator SetupDatabasePathWithLoaderInternal()
		{
			Task copyTask = bundleManager.CopyFromStreamingAssetsAsync();
			while (!copyTask.IsCompleted)
			{
				yield return null;
			}
			if (copyTask.IsFaulted)
			{
				Debug.LogError((object)("File copy failed: " + copyTask.Exception?.GetBaseException().Message));
			}
		}

		private IEnumerator PreloadModelsCoroutine()
		{
			Task<bool> preloadTask = LoadModelAndMapDataAsync();
			while (!preloadTask.IsCompleted)
			{
				yield return null;
			}
			if (!preloadTask.Result)
			{
				Debug.LogWarning((object)"Model preload failed, will load on-demand");
			}
		}

		private async Task<bool> LoadModelAndMapDataAsync()
		{
			if (isLoadingModels)
			{
				Debug.LogWarning((object)"Models are already being loaded, waiting...");
				while (isLoadingModels)
				{
					await Task.Delay(100);
				}
				return modelsLoaded;
			}
			isLoadingModels = true;
			try
			{
				bool success = await Task.Run(() => LoadOnDeviceLocalizationModel()) & await Task.Run(() => LoadMapData());
				if (success)
				{
					modelsLoaded = true;
				}
				else
				{
					Debug.LogError((object)"Failed to load models or map data");
				}
				return success;
			}
			catch (Exception ex)
			{
				Exception e = ex;
				Debug.LogError((object)("LoadModelAndMapDataAsync exception: " + e.Message + "\n" + e.StackTrace));
				return false;
			}
			finally
			{
				isLoadingModels = false;
			}
		}

		private bool LoadOnDeviceLocalizationModel()
		{
			try
			{
				bool flag = ModelsFrameworkBridge.LoadModels();
				if (!flag)
				{
					Debug.LogWarning((object)"LoadModels failed!");
				}
				return flag;
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("LoadModels exception: " + ex.Message));
				return false;
			}
		}

		private bool LoadMapData()
		{
			try
			{
				string mapIdentifier = (string.IsNullOrEmpty(mapSetCode) ? mapCode : mapSetCode);
				bool flag = ModelsFrameworkBridge.LoadMaps(databasePath, mapIdentifier);
				if (!flag)
				{
					Debug.LogWarning((object)"Load Map data failed!");
				}
				return flag;
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Load Map data exception: " + ex.Message));
				return false;
			}
		}

		public void LocalizeFrame()
		{
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_005a: Invalid comparison between Unknown and I4
			if (!isReadyForLocalization)
			{
				Debug.LogWarning((object)"App is not ready for localization yet. Waiting for initialization to complete.");
				return;
			}
			if (isLocalizing)
			{
				Debug.LogWarning((object)"Localization already in progress, skipping request");
				return;
			}
			localizedMapCode = null;
			bgLocalizationRequest = false;
			UnityEvent localizationInit = LocalizationInit;
			if (localizationInit != null)
			{
				localizationInit.Invoke();
			}
			if ((int)Application.platform == 8)
			{
				((MonoBehaviour)this).StartCoroutine(CaptureFrameAndLocalize());
			}
			else
			{
				Debug.LogError((object)"Localization not supported in Unity Editor!");
			}
		}

		private IEnumerator CaptureFrameAndLocalize()
		{
			yield return (object)new WaitForSeconds(1.5f);
			RequestForLocalization();
		}

		private void RequestForLocalization()
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
			isLocalizing = true;
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
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Invalid comparison between Unknown and I4
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Expected O, but got Unknown
			//IL_0065: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Invalid comparison between Unknown and I4
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Invalid comparison between Unknown and I4
			Texture2D val = null;
			Texture2D val2 = null;
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
				val = new Texture2D(x, outputDimensions.y, conversionParams.outputFormat, false);
				val.LoadRawTextureData<byte>(data);
				val.Apply();
				if ((int)Screen.orientation == 1 || (int)Screen.orientation == 2)
				{
					val2 = val;
					val = Util.RotateTextureCounterClockwise(val);
					if ((Object)(object)val2 != (Object)null)
					{
						Object.Destroy((Object)(object)val2);
						val2 = null;
					}
				}
				if (enableBlurCheck && Util.IsImageBlur(val, blurThreshold))
				{
					((MonoBehaviour)this).StartCoroutine(DestroyAndRequest(val));
					return;
				}
				RequestLocalization(val);
				Object.Destroy((Object)(object)val);
			}
			catch (Exception ex)
			{
				isLocalizing = false;
				Debug.LogError((object)("ProcessImage exception: " + ex.Message));
				LocalizationFailureCallback();
				if ((Object)(object)val != (Object)null)
				{
					Object.Destroy((Object)(object)val);
				}
				if ((Object)(object)val2 != (Object)null)
				{
					Object.Destroy((Object)(object)val2);
				}
			}
			finally
			{
				data.Dispose();
			}
			IEnumerator DestroyAndRequest(Texture2D tex)
			{
				yield return (object)new WaitForEndOfFrame();
				Object.Destroy((Object)(object)tex);
				RequestForLocalization();
			}
		}

		private void RequestLocalization(Texture2D texture)
		{
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Invalid comparison between Unknown and I4
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Invalid comparison between Unknown and I4
			//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Unknown result type (might be due to invalid IL or missing references)
			//IL_0132: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0166: Unknown result type (might be due to invalid IL or missing references)
			//IL_016b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0183: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0088: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00df: Unknown result type (might be due to invalid IL or missing references)
			try
			{
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
					LocalizationRequestedCallback();
					((MonoBehaviour)this).StartCoroutine(OnDeviceLocalizationAsync(texture, cameraParams, resolution));
				}
				else
				{
					Debug.LogError((object)"Failed to get camera intrinsics");
					isLocalizing = false;
					if ((Object)(object)texture != (Object)null)
					{
						Object.Destroy((Object)(object)texture);
					}
				}
			}
			catch (Exception ex)
			{
				isLocalizing = false;
				Debug.LogError((object)("RequestLocalization exception: " + ex.Message));
				if ((Object)(object)texture != (Object)null)
				{
					Object.Destroy((Object)(object)texture);
				}
			}
		}

		private IEnumerator OnDeviceLocalizationAsync(Texture2D texture, CameraParams cameraParams, Resolution resolution)
		{
			if (!modelsLoaded && !isLoadingModels)
			{
				Debug.LogWarning((object)"Models not preloaded! Loading now asynchronously...");
				if ((Object)(object)loaderPanel != (Object)null)
				{
					loaderPanel.SetActive(true);
				}
				Task<bool> loadTask = LoadModelAndMapDataAsync();
				while (!loadTask.IsCompleted)
				{
					yield return null;
				}
				if ((Object)(object)loaderPanel != (Object)null)
				{
					loaderPanel.SetActive(false);
				}
				if (!loadTask.Result)
				{
					Debug.LogError((object)"Failed to load models and map data!");
					isLocalizing = false;
					if ((Object)(object)texture != (Object)null)
					{
						Object.Destroy((Object)(object)texture);
					}
					LocalizationFailureCallback();
					yield break;
				}
			}
			else if (isLoadingModels)
			{
				if ((Object)(object)loaderPanel != (Object)null)
				{
					loaderPanel.SetActive(true);
				}
				while (isLoadingModels)
				{
					yield return null;
				}
				if ((Object)(object)loaderPanel != (Object)null)
				{
					loaderPanel.SetActive(false);
				}
			}
			Texture2D normalizedQueryTexture = null;
			try
			{
				normalizedQueryTexture = NormalizeTexture(texture);
				yield return ((MonoBehaviour)this).StartCoroutine(CallLocalizationAsync(normalizedQueryTexture, cameraParams, resolution));
			}
			finally
			{
				if ((Object)(object)normalizedQueryTexture != (Object)null)
				{
					Object.Destroy((Object)(object)normalizedQueryTexture);
				}
				if ((Object)(object)texture != (Object)null)
				{
					Object.Destroy((Object)(object)texture);
					texture = null;
				}
			}
		}

		private Texture2D NormalizeTexture(Texture2D source)
		{
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0042: Expected O, but got Unknown
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			RenderTexture val = null;
			RenderTexture val2 = null;
			try
			{
				val = RenderTexture.GetTemporary(((Texture)source).width, ((Texture)source).height, 0, (RenderTextureFormat)7, (RenderTextureReadWrite)2);
				Graphics.Blit((Texture)(object)source, val);
				val2 = RenderTexture.active;
				RenderTexture.active = val;
				Texture2D val3 = new Texture2D(((Texture)source).width, ((Texture)source).height);
				val3.ReadPixels(new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), 0, 0);
				val3.Apply();
				return val3;
			}
			finally
			{
				if ((Object)(object)val2 != (Object)null)
				{
					RenderTexture.active = val2;
				}
				if ((Object)(object)val != (Object)null)
				{
					RenderTexture.ReleaseTemporary(val);
				}
			}
		}

		private IEnumerator CallLocalizationAsync(Texture2D texture, CameraParams cameraParams, Resolution resolution)
		{
			Color[] pixels = null;
			byte[] imageData = null;
			if ((Object)(object)texture == (Object)null)
			{
				Debug.LogError((object)"Texture is null");
				isLocalizing = false;
				LocalizationFailureCallback();
				yield break;
			}
			int actualWidth = ((Texture)texture).width;
			int actualHeight = ((Texture)texture).height;
			if (!((Texture)texture).isReadable)
			{
				Debug.LogError((object)"Texture is NOT readable! Need to normalize it first.");
				isLocalizing = false;
				LocalizationFailureCallback();
				yield break;
			}
			try
			{
				pixels = texture.GetPixels();
			}
			catch (Exception ex)
			{
				Exception e = ex;
				Debug.LogError((object)("GetPixels() exception: " + e.Message));
				isLocalizing = false;
				LocalizationFailureCallback();
				yield break;
			}
			Task<byte[]> bgConversionTask = Task.Run(delegate
			{
				try
				{
					byte[] array = new byte[actualWidth * actualHeight * 3];
					for (int i = 0; i < actualHeight; i++)
					{
						for (int j = 0; j < actualWidth; j++)
						{
							int num = i * actualWidth + j;
							int num2 = actualHeight - 1 - i;
							int num3 = (num2 * actualWidth + j) * 3;
							array[num3] = (byte)(pixels[num].r * 255f);
							array[num3 + 1] = (byte)(pixels[num].g * 255f);
							array[num3 + 2] = (byte)(pixels[num].b * 255f);
						}
					}
					return array;
				}
				catch (Exception ex2)
				{
					Debug.LogError((object)("Pixel conversion exception: " + ex2.Message));
					return (byte[])null;
				}
			});
			while (!bgConversionTask.IsCompleted)
			{
				yield return null;
			}
			imageData = bgConversionTask.Result;
			pixels = null;
			if (imageData == null || imageData.Length == 0)
			{
				Debug.LogError((object)"Error: Image data is null or empty");
				isLocalizing = false;
				LocalizationFailureCallback();
				yield break;
			}
			bool convertToGeoCoordinates = false;
			string hintMapCodes = "";
			string hintPosition = "";
			string geoCoordinates = "";
			Vector4 cameraIntrinsics = new Vector4(cameraParams.fx, cameraParams.fy, cameraParams.px, cameraParams.py);
			Task<ModelsFrameworkBridge.LocalizationResult> localizationTask = Task.Run(delegate
			{
				//IL_0020: Unknown result type (might be due to invalid IL or missing references)
				try
				{
					return ModelsFrameworkBridge.Localize(mapCode, mapSetCode, isRightHanded: false, convertToGeoCoordinates, cameraIntrinsics, imageData, actualWidth, actualHeight, hintMapCodes, hintPosition, geoCoordinates);
				}
				catch (Exception ex2)
				{
					Debug.LogError((object)("Localize() exception in background thread: " + ex2.Message));
					return new ModelsFrameworkBridge.LocalizationResult
					{
						poseFound = false
					};
				}
			});
			if (backgroundLocalization)
			{
				if (backgroundLocalizationCoroutine != null)
				{
					((MonoBehaviour)this).StopCoroutine(backgroundLocalizationCoroutine);
				}
				backgroundLocalizationCoroutine = ((MonoBehaviour)this).StartCoroutine(RequestForBackgroundLocalization());
			}
			while (!localizationTask.IsCompleted)
			{
				yield return null;
			}
			ModelsFrameworkBridge.LocalizationResult result = localizationTask.Result;
			imageData = null;
			isLocalizing = false;
			if (result.poseFound)
			{
				if (confidenceCheck && result.confidence < _confidenceThreshold)
				{
					LocalizationFailureCallback();
				}
				else
				{
					ApplyLocalizationPose(result);
				}
			}
			else
			{
				LocalizationFailureCallback();
				Debug.LogWarning((object)"Localize() failed");
				Debug.LogWarning((object)("Error: " + result.errorMessage));
			}
		}

		private void ApplyLocalizationPose(ModelsFrameworkBridge.LocalizationResult result)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Unknown result type (might be due to invalid IL or missing references)
			//IL_0034: Unknown result type (might be due to invalid IL or missing references)
			//IL_0039: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			//IL_007d: Unknown result type (might be due to invalid IL or missing references)
			Matrix4x4 val = Matrix4x4.TRS(result.Position, result.Rotation, Vector3.one);
			Matrix4x4 val2 = Matrix4x4.TRS(queryCameraPos, queryCameraRot, Vector3.one);
			Matrix4x4 val3 = val2 * val.inverse;
			mapSpace.transform.rotation = val3.rotation;
			mapSpace.transform.position = new Vector3(val3[0, 3], val3[1, 3], val3[2, 3]);
			mapSpace.SetActive(true);
			LocalizationSuccessCallback();
		}

		private IEnumerator RequestForBackgroundLocalization()
		{
			try
			{
				yield return (object)new WaitForSeconds(bgLocalizationDuration);
				if (!isLocalizing)
				{
					isLocalizing = true;
					bgLocalizationRequest = true;
					RequestForLocalization();
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
				ToastManager.Instance.ShowToast("Localization Success");
			}
		}

		private void ShowFailureAlert()
		{
			if (!bgLocalizationRequest && showAlert)
			{
				ToastManager.Instance.ShowToast("Localization Failed!");
			}
		}

		public void LocalizationRequestedCallback()
		{
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
		}

		public void LocalizationFailureCallback()
		{
			ShowFailureAlert();
			UnityEvent localizationFailure = LocalizationFailure;
			if (localizationFailure != null)
			{
				localizationFailure.Invoke();
			}
		}

		private void HandleDownloadProgress(DownloadProgressInfo progressInfo)
		{
			OnDownloadProgressChanged?.Invoke(progressInfo.ProgressPercent);
			if (progressInfo.IsComplete || progressInfo.HasError)
			{
				OnDownloadComplete?.Invoke(progressInfo.IsComplete && !progressInfo.HasError);
			}
		}

		public int GetDownloadProgress()
		{
			return bundleManager?.CurrentDownloadProgress ?? (-1);
		}

		public string GetCurrentDownloadingFile()
		{
			return bundleManager?.CurrentDownloadingFile;
		}

		private void OnDestroy()
		{
			if (bundleManager != null)
			{
				bundleManager.OnDownloadProgress -= HandleDownloadProgress;
			}
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

		private IEnumerator StartAutoLocalize(float delay)
		{
			yield return (object)new WaitForSeconds(delay);
			LocalizeFrame();
		}

		public string GetOfflineBundlePath()
		{
			return bundleManager?.OfflineBundlePath;
		}

		public string GetMetadataPath()
		{
			return bundleManager?.MetadataPath;
		}

		public string GetLicensePath()
		{
			return bundleManager?.LicensePath;
		}

		public string GetMapDataPath()
		{
			string path = ((localizationType == LocalizationType.Map) ? mapCode : mapSetCode);
			return Path.Combine(Application.persistentDataPath, "MapData", path);
		}
	}
}
