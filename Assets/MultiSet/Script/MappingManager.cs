using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;

namespace MultiSet
{
	[RequireComponent(typeof(iOSBackgroundUploader), typeof(UploadManager))]
	public class MappingManager : MonoBehaviour
	{
		[Header("AR Components")]
		[SerializeField]
		private ARSession m_arSession;

		[SerializeField]
		private XROrigin m_xrOrigin;

		[SerializeField]
		private ARCameraManager m_CameraManager;

		[SerializeField]
		private GameObject m_cameraOffset;

		[SerializeField]
		private MeshFilter meshPrefab;

		private GameObject meshingObject;

		private AROcclusionManager occlusionManager;

		private ARMeshManager arMeshManager;

		private RuntimeExporterMono runtimeExporter;

		private string m_sessionId;

		private string finalZipFilePath;

		private string draftListFilePath;

		private MappingState currentState = MappingState.Initializing;

		private bool meshingSupported;

		private const float BlurThreshold = 120f;

		private const float CaptureIntervalSeconds = 0.25f;

		private const float CaptureInitialDelaySeconds = 1f;

		private const float ZipPreparationDelaySeconds = 2f;

		private const float MeshingSupportCheckDelaySeconds = 0.5f;

		private const int RgbCompression = 90;

		private const int DownSampleFactor = 2;

		private const int MinMapNameLength = 3;

		private readonly string mappingDir = "MappingData";

		private readonly string rgbImagesDir = "RGB";

		private readonly string depthImagesDir = "Depth";

		private readonly string poseDir = "Pose";

		private readonly string meshDir = "Mesh";

		private readonly string rawDataDir = "RawData";

		private readonly string zipFileName = "MappingData.zip";

		private readonly string draftMapsFileName = "DraftMapList.json";

		private string baseDirectory;

		private string rootDir;

		private string rawDirPath;

		public MappingState CurrentState => currentState;

		public bool IsMapping => currentState == MappingState.Mapping;

		public bool IsMeshingSupported => meshingSupported;

		public string SessionId => m_sessionId;

		public event EventHandler<MappingStateChangedEventArgs> OnStateChanged;

		public event EventHandler<DeviceCapabilityEventArgs> OnDeviceCapabilityChecked;

		public event EventHandler<UploadProgressEventArgs> OnUploadProgress;

		public event EventHandler<UploadCompletedEventArgs> OnUploadCompleted;

		public event EventHandler<MappingErrorEventArgs> OnError;

		public event EventHandler<MapNameValidationEventArgs> OnMapNameValidated;

		public event EventHandler<AuthenticationEventArgs> OnAuthenticationChanged;

		public event EventHandler<DraftMapSelectedEventArgs> OnDraftSaved;

		private void Awake()
		{
			ConfigureMappingScene();
		}

		private void Start()
		{
			Screen.orientation = (ScreenOrientation)3;
			Screen.sleepTimeout = -1;
			if ((Object)(object)m_CameraManager != (Object)null)
			{
				occlusionManager = ((Component)m_CameraManager).GetComponent<AROcclusionManager>();
				rootDir = Application.persistentDataPath;
				draftListFilePath = Path.Combine(rootDir, draftMapsFileName);
				DisableMeshing();
				((MonoBehaviour)this).StartCoroutine(CheckForMappingSupport());
			}
			else
			{
				RaiseError("ARCameraManager not found!");
			}
		}

		private void OnEnable()
		{
			EventManager<EventData>.StartListening("AuthCallBack", HandleAuthCallback);
		}

		private void OnDisable()
		{
			EventManager<EventData>.StopListening("AuthCallBack", HandleAuthCallback);
		}

		private void OnDestroy()
		{
			Screen.sleepTimeout = -2;
		}

		public void StartMapping()
		{
			if (!meshingSupported)
			{
				RaiseError("Meshing is not supported on this device.");
				this.OnDeviceCapabilityChecked?.Invoke(this, new DeviceCapabilityEventArgs(meshingSupported: false, "Meshing not supported"));
				return;
			}
			m_sessionId = Guid.NewGuid().ToString();
			rawDirPath = Path.Combine(mappingDir, m_sessionId, rawDataDir);
			MappingDirectorySetup();
			((MonoBehaviour)this).StartCoroutine(ResetARSessionAndStartMapping());
		}

		public void StopMapping()
		{
			DisableMeshing();
			StopMappingFlow();
			SetState(MappingState.Processing, "Processing captured data...");
		}

		public bool ValidateMapName(string mapName)
		{
			string text = DraftMapHelper.ValidateMapName(mapName);
			bool flag = string.IsNullOrEmpty(text);
			this.OnMapNameValidated?.Invoke(this, new MapNameValidationEventArgs(flag, text));
			return flag;
		}

		public void UploadMap(string mapName)
		{
			if (ValidateMapName(mapName))
			{
				AddNewDraftMap(mapName, exitScene: false);
				if (!PlayerPrefs.HasKey("MultiSet.AccessToken"))
				{
					this.OnAuthenticationChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated: false, "Please authenticate to upload maps."));
				}
				else
				{
					UploadMapDataOnServer(mapName);
				}
			}
		}

		public void SaveAsDraft(string mapName)
		{
			if (ValidateMapName(mapName))
			{
				AddNewDraftMap(mapName, exitScene: true);
			}
		}

		public void ResetSession()
		{
			((MonoBehaviour)this).StartCoroutine(ARSessionReset());
		}

		public void ReloadScene()
		{
			((MonoBehaviour)this).StartCoroutine(DraftMapHelper.ReloadCurrentScene());
		}

		private void SetState(MappingState newState, string message = null)
		{
			MappingState previousState = currentState;
			currentState = newState;
			this.OnStateChanged?.Invoke(this, new MappingStateChangedEventArgs(previousState, newState, message));
		}

		private void RaiseError(string message, Exception ex = null)
		{
			Debug.LogError((object)message);
			SetState(MappingState.Error, message);
			this.OnError?.Invoke(this, new MappingErrorEventArgs(message, ex));
		}

		private IEnumerator CheckForMappingSupport()
		{
			yield return (object)new WaitForSeconds(0.5f);
			XRLoader activeLoader = LoaderUtility.GetActiveLoader();
			meshingSupported = (Object)(object)activeLoader != (Object)null && activeLoader.GetLoadedSubsystem<XRMeshSubsystem>() != null;
			this.OnDeviceCapabilityChecked?.Invoke(this, new DeviceCapabilityEventArgs(meshingSupported, meshingSupported ? "Device supports meshing" : "Meshing is not supported on this device"));
			if (meshingSupported)
			{
				SetState(MappingState.Ready);
			}
			else
			{
				SetState(MappingState.Error, "Meshing not supported");
			}
		}

		private void PrepareMapping()
		{
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			EnableMeshing();
			((MonoBehaviour)this).InvokeRepeating("AsyncMappingDataCapture", 1f, 0.25f);
			runtimeExporter?.StartMeshing(Path.Combine(m_sessionId, rawDataDir));
			XRCameraIntrinsics intrinsics = default(XRCameraIntrinsics);
			if (m_CameraManager.TryGetIntrinsics(out intrinsics))
			{
				SaveCameraIntrinsicsAsync("CameraIntrinsics.json", intrinsics);
			}
			SetState(MappingState.Mapping, "Mapping in progress...");
		}

		private async Task StopMappingFlow()
		{
			((MonoBehaviour)this).CancelInvoke("AsyncMappingDataCapture");
			try
			{
				if ((Object)(object)runtimeExporter != (Object)null)
				{
					await runtimeExporter.ExportAsync();
				}
			}
			catch (Exception ex)
			{
				Exception e = ex;
				RaiseError("Error during export: " + e.Message, e);
			}
			((MonoBehaviour)this).StartCoroutine(ZipDirectory());
		}

		private void AsyncMappingDataCapture()
		{
			try
			{
				if ((Object)(object)m_CameraManager != (Object)null)
				{
					CaptureMappingData();
				}
			}
			catch (Exception arg)
			{
				Debug.LogError((object)$"Exception in AsyncMappingDataCapture: {arg}");
			}
		}

		private void CaptureMappingData()
		{
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_008d: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
			string text = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
			XRCpuImage val = default(XRCpuImage);
			if (!m_CameraManager.TryAcquireLatestCpuImage(out val))
			{
				return;
			}
			XRCameraIntrinsics cameraIntrinsics = default(XRCameraIntrinsics);
			if (!m_CameraManager.TryGetIntrinsics(out cameraIntrinsics))
			{
				val.Dispose();
				return;
			}
			CameraPose cameraPose = PrepareCameraPose(cameraIntrinsics, ((Component)m_CameraManager).transform);
			XRCpuImage val2 = default(XRCpuImage);
			if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out val2))
			{
				val.Dispose();
				return;
			}
			if (!AreImagesFromSameFrame(val, val2))
			{
				val.Dispose();
				val2.Dispose();
				return;
			}
			Texture2D val3 = ConvertRGBImageToTexture2D(val, (TextureFormat)3);
			if (Util.IsImageBlur(val3, 120f))
			{
				Object.Destroy((Object)(object)val3);
				val2.Dispose();
			}
			else
			{
				SaveTextureAsync(val3, "Rgb_" + text + ".jpg", isRGB: true);
				ProcessAndSaveImageAsync(val2, "Depth_" + text + ".exr", (TextureFormat)18);
				SaveCameraPoseJSONAsync(cameraPose, "CameraPose_" + text + ".json");
			}
		}

		private async Task ProcessAndSaveImageAsync(XRCpuImage cpuImage, string fileName, TextureFormat format)
		{
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			XRCpuImage.ConversionParams val = default(XRCpuImage.ConversionParams);
			val.inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height);
			val.outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height);
			val.outputFormat = format;
			val.transformation = (XRCpuImage.Transformation)1;
			XRCpuImage.ConversionParams conversionParams = val;
			int size = cpuImage.GetConvertedDataSize(conversionParams);
			NativeArray<byte> buffer = new NativeArray<byte>(size, (Allocator)2, (NativeArrayOptions)1);
			try
			{
				cpuImage.Convert(conversionParams, buffer);
				Vector2Int outputDimensions = conversionParams.outputDimensions;
				int x = outputDimensions.x;
				outputDimensions = conversionParams.outputDimensions;
				Texture2D texture = new Texture2D(x, outputDimensions.y, format, false);
				texture.LoadRawTextureData<byte>(buffer);
				texture.Apply();
				SaveTextureAsync(texture, fileName, isRGB: false);
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Debug.LogError((object)("Error processing image: " + ex2.Message));
			}
			finally
			{
				if (buffer.IsCreated)
				{
					buffer.Dispose();
				}
				cpuImage.Dispose();
			}
		}

		private async Task SaveTextureAsync(Texture2D texture, string fileName, bool isRGB)
		{
			byte[] imageBytes;
			string filePath;
			if (isRGB)
			{
				imageBytes = ImageConversion.EncodeToJPG(texture, 90);
				filePath = Path.Combine(rootDir, rawDirPath, rgbImagesDir, fileName);
			}
			else
			{
				imageBytes = ImageConversion.EncodeToEXR(texture, Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);
				if (imageBytes == null || imageBytes.Length == 0)
				{
					Debug.LogError((object)"Failed to encode texture to EXR.");
					return;
				}
				filePath = Path.Combine(rootDir, rawDirPath, depthImagesDir, fileName);
			}
			await Task.Run(delegate
			{
				File.WriteAllBytes(filePath, imageBytes);
			});
			Object.Destroy((Object)(object)texture);
		}

		private async Task SaveCameraPoseJSONAsync(CameraPose cameraPose, string fileName)
		{
			try
			{
				string json = JsonUtility.ToJson((object)cameraPose, true);
				string filePath = Path.Combine(rootDir, rawDirPath, poseDir, fileName);
				await File.WriteAllTextAsync(filePath, json);
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Exception while saving camera pose: " + ex));
			}
		}

		private async Task SaveCameraIntrinsicsAsync(string fileName, XRCameraIntrinsics intrinsics)
		{
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				string filePath = Path.Combine(rootDir, rawDirPath, fileName);
				CameraIntrinsics obj = new CameraIntrinsics
				{
					camera_intrinsics = new CameraParams
					{
						fx = intrinsics.focalLength.x / 2f,
						fy = intrinsics.focalLength.y / 2f,
						px = intrinsics.principalPoint.x / 2f,
						py = intrinsics.principalPoint.y / 2f
					}
				};
				Resolution resolution = new Resolution();
				Vector2Int resolution2 = intrinsics.resolution;
				resolution.width = resolution2.x / 2;
				resolution2 = intrinsics.resolution;
				resolution.height = resolution2.y / 2;
				obj.resolution = resolution;
				CameraIntrinsics cameraData = obj;
				string jsonString = JsonUtility.ToJson((object)cameraData, true);
				await Task.Run(delegate
				{
					File.WriteAllText(filePath, jsonString);
				});
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Exception while saving camera intrinsics: " + ex));
			}
		}

		private bool AreImagesFromSameFrame(XRCpuImage colorImage, XRCpuImage depthImage)
		{
			return colorImage.timestamp == depthImage.timestamp;
		}

		private CameraPose PrepareCameraPose(XRCameraIntrinsics cameraIntrinsics, Transform cameraTransform)
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			return new CameraPose
			{
				Position = cameraTransform.position,
				Rotation = cameraTransform.rotation,
				cameraParams = new CameraParams
				{
					fx = cameraIntrinsics.focalLength.x / 2f,
					fy = cameraIntrinsics.focalLength.y / 2f,
					px = cameraIntrinsics.principalPoint.x / 2f,
					py = cameraIntrinsics.principalPoint.y / 2f
				}
			};
		}

		private Texture2D ConvertRGBImageToTexture2D(XRCpuImage cpuImage, TextureFormat format)
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_003a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_006e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0090: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a1: Expected O, but got Unknown
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			XRCpuImage.ConversionParams val = default(XRCpuImage.ConversionParams);
			val.inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height);
			val.outputDimensions = new Vector2Int(cpuImage.width / 2, cpuImage.height / 2);
			val.outputFormat = format;
			val.transformation = (XRCpuImage.Transformation)1;
			XRCpuImage.ConversionParams val2 = val;
			NativeArray<byte> val3 = new NativeArray<byte>(cpuImage.GetConvertedDataSize(val2), (Allocator)2, (NativeArrayOptions)1);
			cpuImage.Convert(val2, val3);
			Vector2Int outputDimensions = val2.outputDimensions;
			int x = outputDimensions.x;
			outputDimensions = val2.outputDimensions;
			Texture2D val4 = new Texture2D(x, outputDimensions.y, format, false);
			val4.LoadRawTextureData<byte>(val3);
			val4.Apply();
			val3.Dispose();
			cpuImage.Dispose();
			return val4;
		}

		private IEnumerator ZipDirectory()
		{
			SetState(MappingState.Processing, "Preparing map data, please wait...");
			yield return (object)new WaitForSeconds(2f);
			if (string.IsNullOrEmpty(m_sessionId))
			{
				RaiseError("Session ID is null or empty!");
				yield break;
			}
			baseDirectory = Path.Combine(rootDir, mappingDir, m_sessionId, rawDataDir);
			finalZipFilePath = Path.Combine(rootDir, mappingDir, m_sessionId, zipFileName);
			if (!Directory.Exists(baseDirectory))
			{
				RaiseError("Source directory does not exist: " + baseDirectory);
				yield break;
			}
			Util.DeleteFile(finalZipFilePath);
			Task zipTask = Task.Run(delegate
			{
				try
				{
					ZipFile.CreateFromDirectory(baseDirectory, finalZipFilePath);
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("Error while zipping directory: " + ex.Message));
				}
			});
			yield return (object)new WaitUntil((Func<bool>)(() => zipTask.IsCompleted));
			if (zipTask.IsFaulted)
			{
				RaiseError("Failed to zip directory.");
			}
			else
			{
				SetState(MappingState.WaitingForMapName, "Ready for map name input");
			}
		}

		private void UploadMapDataOnServer(string mapName)
		{
			try
			{
				SetState(MappingState.Uploading, "Creating map...");
				MapPayload mapPayload = new MapPayload
				{
					mapName = mapName,
					coordinates = GetCoordinates()
				};
				string mapPayload2 = JsonUtility.ToJson((object)mapPayload);
				MultiSetApiManager.CreateMap(mapPayload2, CreateMapApiCallback);
			}
			catch (Exception ex)
			{
				RaiseError("Map creation failed: " + ex.Message, ex);
			}
		}

		private Coordinates GetCoordinates()
		{
			if ((Object)(object)GpsCoordinateHandler.Instance == (Object)null)
			{
				Debug.LogError((object)"GpsCoordinateHandler instance is null!");
				return new Coordinates();
			}
			GPSCoordinates gpsCoordinates = GpsCoordinateHandler.Instance.gpsCoordinates;
			return new Coordinates
			{
				latitude = gpsCoordinates.latitude,
				longitude = gpsCoordinates.longitude,
				altitude = gpsCoordinates.altitude
			};
		}

		private void CreateMapApiCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				RaiseError("Create Map Callback: Empty or null data received!");
			}
			else if (success)
			{
				CreateMapResponse createMapResponse = JsonUtility.FromJson<CreateMapResponse>(data);
				if (createMapResponse != null)
				{
					PlayerPrefs.SetString("MAP_RESPONSE", JsonUtility.ToJson((object)createMapResponse));
					UploadMapZipFile(createMapResponse.uploadUrl);
					UpdateMapIdToDraftMap(createMapResponse);
				}
				else
				{
					RaiseError("Failed to parse CreateMapResponse!");
				}
			}
			else
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				string message = errorJSON?.message ?? "Unknown error";
				if (errorJSON?.error == "Invalid token")
				{
					this.OnAuthenticationChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated: false, "Invalid token, please re-authenticate."));
				}
				RaiseError(message);
			}
		}

		private void UploadMapZipFile(string uploadUrl)
		{
			if (string.IsNullOrEmpty(uploadUrl) || string.IsNullOrEmpty(finalZipFilePath) || string.IsNullOrEmpty(m_sessionId))
			{
				RaiseError("Invalid upload parameters!");
				return;
			}
			if ((Object)(object)UploadManager.Instance != (Object)null)
			{
				try
				{
					UploadManager.Instance.OnUploadProgressChanged += HandleUploadProgress;
					UploadManager.Instance.OnUploadCompleted += HandleUploadCompletion;
					UploadManager.Instance.StartBackgroundUpload(finalZipFilePath, uploadUrl, m_sessionId);
					return;
				}
				catch (Exception ex)
				{
					RaiseError("Error starting upload: " + ex.Message, ex);
					UnsubscribeFromUploadEvents();
					return;
				}
			}
			RaiseError("Upload manager could not be initialized.");
		}

		private void HandleUploadProgress(string uploadId, float progress)
		{
			if (!string.IsNullOrEmpty(uploadId) && !string.IsNullOrEmpty(m_sessionId) && !(uploadId != m_sessionId))
			{
				this.OnUploadProgress?.Invoke(this, new UploadProgressEventArgs(progress));
			}
		}

		private void HandleUploadCompletion(string uploadId, bool success, string errorMessage)
		{
			if (!string.IsNullOrEmpty(uploadId) && !string.IsNullOrEmpty(m_sessionId) && !(uploadId != m_sessionId))
			{
				UnsubscribeFromUploadEvents();
				if (success)
				{
					SetState(MappingState.Completed, "Upload completed successfully!");
					this.OnUploadCompleted?.Invoke(this, new UploadCompletedEventArgs(success: true));
					DraftMapHelper.DeleteMapFromDraftList(m_sessionId);
					string directory = Path.Combine(rootDir, mappingDir, m_sessionId);
					Util.DeleteDirectory(directory);
				}
				else
				{
					RaiseError("Failed to upload file: " + errorMessage);
					this.OnUploadCompleted?.Invoke(this, new UploadCompletedEventArgs(success: false, null, errorMessage));
				}
			}
		}

		private void UnsubscribeFromUploadEvents()
		{
			if ((Object)(object)UploadManager.Instance != (Object)null)
			{
				UploadManager.Instance.OnUploadProgressChanged -= HandleUploadProgress;
				UploadManager.Instance.OnUploadCompleted -= HandleUploadCompletion;
			}
		}

		private void AddNewDraftMap(string mapName, bool exitScene)
		{
			DraftMap draftMap = new DraftMap
			{
				id = m_sessionId,
				mapId = null,
				mapName = mapName,
				location = finalZipFilePath,
				thumbnail = null,
				coordinates = GetCoordinates()
			};
			UpdateDraftList(draftMap, exitScene);
		}

		private void UpdateMapIdToDraftMap(CreateMapResponse createMapResponse)
		{
			if (!File.Exists(draftListFilePath))
			{
				return;
			}
			string text = Util.LoadFromFile(draftMapsFileName);
			DraftMapList draftMapList = JsonUtility.FromJson<DraftMapList>(text);
			if (draftMapList != null)
			{
				DraftMap draftMap = draftMapList.draftMaps.FirstOrDefault((DraftMap c) => c.id == m_sessionId);
				if (draftMap != null)
				{
					draftMap.mapId = createMapResponse.mapId;
				}
				Util.WriteToFile(draftMapsFileName, JsonUtility.ToJson((object)draftMapList, true));
			}
		}

		private void UpdateDraftList(DraftMap draftMap, bool exitScene)
		{
			DraftMapList draftMapList;
			if (File.Exists(draftListFilePath))
			{
				string text = Util.LoadFromFile(draftMapsFileName);
				draftMapList = JsonUtility.FromJson<DraftMapList>(text) ?? new DraftMapList();
				DraftMap draftMap2 = draftMapList.draftMaps.FirstOrDefault((DraftMap c) => c.id == draftMap.id);
				if (draftMap2 != null)
				{
					draftMap2.mapName = draftMap.mapName;
				}
				else
				{
					draftMapList.draftMaps.Add(draftMap);
				}
			}
			else
			{
				draftMapList = new DraftMapList
				{
					draftMaps = new List<DraftMap> { draftMap }
				};
			}
			Util.WriteToFile(draftMapsFileName, JsonUtility.ToJson((object)draftMapList, true));
			if (exitScene)
			{
				this.OnDraftSaved?.Invoke(this, new DraftMapSelectedEventArgs(draftMap));
			}
		}

		private IEnumerator ARSessionReset()
		{
			if ((Object)(object)m_arSession != (Object)null)
			{
				XRSessionSubsystem subsystem = m_arSession.subsystem;
				if (subsystem != null)
				{
					subsystem.Stop();
				}
				((Behaviour)m_arSession).enabled = false;
				m_arSession.Reset();
				yield return null;
				((Behaviour)m_arSession).enabled = true;
				XRSessionSubsystem subsystem2 = m_arSession.subsystem;
				if (subsystem2 != null)
				{
					subsystem2.Start();
				}
				yield return (object)new WaitUntil((Func<bool>)(() => (int)ARSession.state == 7));
				ResetXROrigin();
			}
		}

		private IEnumerator ResetARSessionAndStartMapping()
		{
			if ((Object)(object)m_arSession != (Object)null)
			{
				XRSessionSubsystem subsystem = m_arSession.subsystem;
				if (subsystem != null)
				{
					subsystem.Stop();
				}
				((Behaviour)m_arSession).enabled = false;
				m_arSession.Reset();
				yield return null;
				((Behaviour)m_arSession).enabled = true;
				XRSessionSubsystem subsystem2 = m_arSession.subsystem;
				if (subsystem2 != null)
				{
					subsystem2.Start();
				}
				yield return (object)new WaitUntil((Func<bool>)(() => (int)ARSession.state == 7));
				ResetXROrigin();
				PrepareMapping();
			}
		}

		private void ResetXROrigin()
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)m_xrOrigin != (Object)null)
			{
				((Component)m_xrOrigin).transform.localPosition = Vector3.zero;
				((Component)m_xrOrigin).transform.localRotation = Quaternion.identity;
				if ((Object)(object)m_xrOrigin.CameraFloorOffsetObject != (Object)null)
				{
					m_xrOrigin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
					m_xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.identity;
				}
			}
		}

		private void EnableMeshing()
		{
			if ((Object)(object)arMeshManager == (Object)null)
			{
				GameObject obj = meshingObject;
				arMeshManager = ((obj != null) ? obj.GetComponent<ARMeshManager>() : null);
				if ((Object)(object)arMeshManager == (Object)null)
				{
					Debug.LogError((object)"ARMeshManager not found");
					return;
				}
			}
			((Behaviour)arMeshManager).enabled = true;
		}

		private void DisableMeshing()
		{
			if ((Object)(object)arMeshManager != (Object)null)
			{
				((Behaviour)arMeshManager).enabled = false;
			}
		}

		private void ConfigureMappingScene()
		{
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Expected O, but got Unknown
			if ((Object)(object)m_CameraManager != (Object)null && (Object)(object)((Component)m_CameraManager).GetComponent<AROcclusionManager>() == (Object)null)
			{
				((Component)m_CameraManager).gameObject.AddComponent<AROcclusionManager>();
				AROcclusionManager component = ((Component)m_CameraManager).GetComponent<AROcclusionManager>();
				if ((Object)(object)component != (Object)null)
				{
					component.requestedEnvironmentDepthMode = (EnvironmentDepthMode)1;
					component.environmentDepthTemporalSmoothingRequested = false;
					component.requestedHumanDepthMode = (HumanSegmentationDepthMode)0;
					component.requestedHumanStencilMode = (HumanSegmentationStencilMode)0;
					component.requestedOcclusionPreferenceMode = (OcclusionPreferenceMode)2;
				}
			}
			meshingObject = new GameObject("Meshing");
			meshingObject.transform.parent = (((Object)(object)m_cameraOffset != (Object)null) ? m_cameraOffset.transform : ((Component)m_xrOrigin).transform);
			if ((Object)(object)meshingObject.GetComponent<ARMeshManager>() == (Object)null)
			{
				meshingObject.AddComponent<ARMeshManager>();
				arMeshManager = meshingObject.GetComponent<ARMeshManager>();
				if ((Object)(object)arMeshManager != (Object)null)
				{
					arMeshManager.meshPrefab = meshPrefab;
					arMeshManager.density = 1f;
					arMeshManager.normals = true;
					arMeshManager.tangents = true;
					arMeshManager.textureCoordinates = true;
					arMeshManager.colors = true;
					arMeshManager.concurrentQueueSize = 25;
				}
			}
			if ((Object)(object)meshingObject.GetComponent<RuntimeExporterMono>() == (Object)null)
			{
				meshingObject.AddComponent<RuntimeExporterMono>();
				runtimeExporter = meshingObject.GetComponent<RuntimeExporterMono>();
				if ((Object)(object)runtimeExporter != (Object)null)
				{
					runtimeExporter.meshManager = ((Component)arMeshManager).gameObject;
					runtimeExporter.meshFileName = "Mesh.glb";
					runtimeExporter.origin = m_xrOrigin;
				}
			}
		}

		private void MappingDirectorySetup()
		{
			baseDirectory = Path.Combine(rootDir, rawDirPath);
			Util.CreateDirectory(Path.Combine(baseDirectory, rgbImagesDir));
			Util.CreateDirectory(Path.Combine(baseDirectory, depthImagesDir));
			Util.CreateDirectory(Path.Combine(baseDirectory, poseDir));
			Util.CreateDirectory(Path.Combine(baseDirectory, meshDir));
		}

		private void HandleAuthCallback(EventData @event)
		{
			if (@event.AuthSuccess)
			{
				this.OnAuthenticationChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated: true, "Authentication successful"));
				SetState(MappingState.Ready);
			}
			else
			{
				this.OnAuthenticationChanged?.Invoke(this, new AuthenticationEventArgs(isAuthenticated: false, "Authentication failed. Check credentials."));
			}
		}
	}
}
