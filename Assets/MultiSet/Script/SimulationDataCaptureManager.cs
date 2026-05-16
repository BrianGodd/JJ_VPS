using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class SimulationDataCaptureManager : MonoBehaviour
	{
		public ARCameraManager m_CameraManager;

		private ARSession aRSession;

		private Camera ARCamera;

		private Vector3 camPos;

		private Quaternion camRot;

		[Header("Number of Frames to Capture")]
		[Range(4f, 6f)]
		public int numberOfFrames = 5;

		[Header("Frame Capture Interval In Sec")]
		[Range(0.4f, 0.8f)]
		public float frameCaptureInterval = 0.6f;

		private bool successNotified;

		private bool failureNotified;

		private bool isCapturing = false;

		private int compressionRatio = 1;

		private float blurThreshold = 30f;

		private List<ImageData> capturedImages = new List<ImageData>();

		private UploadData uploadData;

		[Space(20f)]
		[SerializeField]
		private TMP_InputField simulationDataName;

		[SerializeField]
		private GameObject dataNamePanel;

		[SerializeField]
		private GameObject dataNameError;

		[SerializeField]
		private GameObject dataUploadedDialog;

		[SerializeField]
		private GameObject mLoader;

		[SerializeField]
		private Text uploadPercText;

		private string rootDir;

		private string baseDirectory;

		private string simulationDataDirectory;

		private string dataDirectory;

		private string finalZipFilePath = null;

		private string simulationDataDir = "SimulationData";

		private string zipFileName = "SimulationData.zip";

		[Space(10f)]
		[Header("Localization Callbacks")]
		public UnityEvent DataCaptureInit = new UnityEvent();

		public UnityEvent DataCaptured = new UnityEvent();

		private void Awake()
		{
			aRSession = Object.FindFirstObjectByType<ARSession>();
			if ((Object)(object)aRSession == (Object)null)
			{
				Debug.LogError((object)"ARSession is null. Please assign the ARSession component to the SimulationDataManager script.");
			}
		}

		private void Start()
		{
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Invalid comparison between Unknown and I4
			rootDir = Application.persistentDataPath;
			if ((Object)(object)m_CameraManager == (Object)null)
			{
				Debug.LogError((object)"ARCameraManager is not assigned. Please assign it in the inspector.");
				return;
			}
			ARCamera = ((Component)m_CameraManager).GetComponent<Camera>();
			if ((Object)(object)ARCamera == (Object)null)
			{
				Debug.LogError((object)"Camera component not found on ARCameraManager GameObject.");
				return;
			}
			if ((int)Application.platform == 8)
			{
				compressionRatio = 2;
				blurThreshold = 100f;
			}
			Screen.sleepTimeout = -1;
		}

		private void OnEnable()
		{
			mLoader.SetActive(true);
			EventManager<EventData>.StartListening("AuthCallBack", AuthCallBack);
		}

		private void OnDisable()
		{
			EventManager<EventData>.StopListening("AuthCallBack", AuthCallBack);
		}

		private void AuthCallBack(EventData @event)
		{
			mLoader.SetActive(false);
			if (@event.AuthSuccess)
			{
				Debug.Log((object)"Auth Success.");
				return;
			}
			Debug.LogError((object)"Auth Failed!");
			ToastManager.Instance.ShowToast("Auth Failed!");
		}

		public void CaptureSimulationData()
		{
			baseDirectory = Path.Combine(rootDir, simulationDataDir);
			Util.CreateDirectory(baseDirectory);
			successNotified = false;
			failureNotified = false;
			UnityEvent dataCaptureInit = DataCaptureInit;
			if (dataCaptureInit != null)
			{
				dataCaptureInit.Invoke();
			}
			uploadData = null;
			capturedImages.Clear();
			isCapturing = true;
			AcquireLatestImage();
		}

		private void AcquireLatestImage()
		{
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_008e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d3: Invalid comparison between Unknown and I4
			//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Invalid comparison between Unknown and I4
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			XRCpuImage val = default(XRCpuImage);
			if (m_CameraManager.TryAcquireLatestCpuImage(out val))
			{
				if ((Object)(object)ARCamera == (Object)null)
				{
					Debug.LogError((object)"ARCamera is null. Cannot proceed with SimulationData Capture.");
					isCapturing = false;
					val.Dispose();
					return;
				}
				camPos = ((Component)ARCamera).transform.position;
				camRot = ((Component)ARCamera).transform.rotation;
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
					isCapturing = false;
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
				if (Util.IsImageBlur(val, blurThreshold))
				{
					Debug.Log((object)"Captured frame is blurred. Recapturing immediately.");
					Object.Destroy((Object)(object)val);
					AcquireLatestImage();
				}
				else
				{
					AddFrameDataForQuery(val);
					Object.Destroy((Object)(object)val);
				}
			}
			finally
			{
				data.Dispose();
			}
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
				Debug.Log((object)"All non-blurred frames captured. Calling Query API...");
				uploadData.images = new List<ImageData>(capturedImages);
				capturedImages.Clear();
				DataCaptureSuccessCallback();
				PrepareSimulationData();
			}
			else
			{
				((MonoBehaviour)this).StartCoroutine(WaitAndCaptureFrame());
			}
		}

		private void PrepareSimulationData()
		{
			string path = Guid.NewGuid().ToString();
			simulationDataDirectory = Path.Combine(baseDirectory, path);
			Util.CreateDirectory(simulationDataDirectory);
			dataDirectory = Path.Combine(simulationDataDirectory, "SimulationData");
			Util.CreateDirectory(dataDirectory);
			List<ImageMetadata> list = new List<ImageMetadata>();
			for (int i = 0; i < uploadData.images.Count; i++)
			{
				list.Add(uploadData.images[i].metadata);
			}
			SimulationData simulationData = new SimulationData
			{
				width = uploadData.width,
				height = uploadData.height,
				fx = uploadData.fx,
				fy = uploadData.fy,
				px = uploadData.px,
				py = uploadData.py,
				imageDataList = list
			};
			string text = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			for (int j = 0; j < uploadData.images.Count; j++)
			{
				byte[] imageBytes = uploadData.images[j].imageBytes;
				string path2 = "Image_" + j + "_" + text + ".jpg";
				string path3 = Path.Combine(dataDirectory, path2);
				File.WriteAllBytes(path3, imageBytes);
			}
			string contents = JsonUtility.ToJson((object)simulationData, true);
			string path4 = "SimulationData_" + text + ".json";
			string text2 = Path.Combine(dataDirectory, path4);
			File.WriteAllText(text2, contents);
			Debug.Log((object)("Simulation JSON saved to: " + text2));
			((MonoBehaviour)this).StartCoroutine(ZipDirectory());
		}

		private IEnumerator ZipDirectory()
		{
			if ((Object)(object)uploadPercText != (Object)null)
			{
				uploadPercText.text = "Preparing Map data, please wait...";
			}
			GameObject obj = mLoader;
			if (obj != null)
			{
				obj.SetActive(true);
			}
			yield return (object)new WaitForSeconds(2f);
			finalZipFilePath = Path.Combine(simulationDataDirectory, zipFileName);
			if (!Directory.Exists(dataDirectory))
			{
				GameObject obj2 = mLoader;
				if (obj2 != null)
				{
					obj2.SetActive(false);
				}
				Debug.LogError((object)("Source directory does not exist: " + dataDirectory));
			}
			Util.DeleteFile(finalZipFilePath);
			Task zipTask = Task.Run(delegate
			{
				try
				{
					ZipFile.CreateFromDirectory(dataDirectory, finalZipFilePath);
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("Error while zipping directory: " + ex.Message));
				}
			});
			yield return (object)new WaitUntil((Func<bool>)(() => zipTask.IsCompleted));
			if (zipTask.IsFaulted)
			{
				Debug.LogError((object)"Failed to zip directory.");
			}
			else
			{
				Debug.Log((object)("Directory zipped successfully: " + finalZipFilePath));
			}
			simulationDataName.text = string.Empty;
			GameObject obj3 = mLoader;
			if (obj3 != null)
			{
				obj3.SetActive(false);
			}
			GameObject obj4 = dataNamePanel;
			if (obj4 != null)
			{
				obj4.SetActive(true);
			}
			isCapturing = false;
		}

		private IEnumerator WaitAndCaptureFrame()
		{
			yield return (object)new WaitForSeconds(frameCaptureInterval);
			AcquireLatestImage();
		}

		public void UploadSimulationData()
		{
			//IL_0113: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Expected O, but got Unknown
			if (uploadData?.images == null || uploadData.images.Count == 0)
			{
				Debug.LogError((object)"No images to upload");
				ShowFailureAlert();
				return;
			}
			GameObject obj = dataNameError;
			if (obj != null)
			{
				obj.SetActive(false);
			}
			TMP_Text componentInChildren = dataNameError.GetComponentInChildren<TMP_Text>();
			string text = simulationDataName.text;
			if (string.IsNullOrEmpty(text))
			{
				Debug.Log((object)"Simulation Data name can not be empty !!");
				if ((Object)(object)componentInChildren != (Object)null)
				{
					componentInChildren.text = "Please enter Simulation Data name.";
				}
				GameObject obj2 = dataNameError;
				if (obj2 != null)
				{
					obj2.SetActive(true);
				}
			}
			else if (text.Length < 3)
			{
				Debug.Log((object)"Simulation Data name should be at-least 3 characters long !!");
				if ((Object)(object)componentInChildren != (Object)null)
				{
					componentInChildren.text = "Simulation Data name should be at-least 3 characters long!";
				}
				GameObject obj3 = dataNameError;
				if (obj3 != null)
				{
					obj3.SetActive(true);
				}
			}
			else
			{
				mLoader.SetActive(true);
				WWWForm val = new WWWForm();
				val.AddField("name", text);
				val.AddField("description", "Simulation Data uploaded from Unity AR app");
				byte[] array = File.ReadAllBytes(finalZipFilePath);
				val.AddBinaryData("file", array, "SimulationData.Zip", "application/zip");
				MultiSetApiManager.UploadSimulationData(val, UploadSimulationDataCallback);
			}
		}

		private void UploadSimulationDataCallback(bool success, string data, long statusCode)
		{
			isCapturing = false;
			mLoader.SetActive(false);
			if (string.IsNullOrEmpty(data))
			{
				ShowFailureAlert();
				ToastManager.Instance.ShowToast("Data Upload Failed!");
				Debug.LogError((object)"Data Upload Callback: Empty or null data received!");
			}
			else if (success)
			{
				simulationDataName.text = string.Empty;
				if ((Object)(object)dataNamePanel != (Object)null)
				{
					dataNamePanel.SetActive(false);
				}
				if ((Object)(object)dataUploadedDialog != (Object)null)
				{
					dataUploadedDialog.SetActive(true);
				}
				((MonoBehaviour)this).StartCoroutine(ClearCapturedData());
				ClearResources();
			}
			else
			{
				ShowFailureAlert();
				Debug.Log((object)("All data... " + data.ToString()));
			}
		}

		private IEnumerator ClearCapturedData()
		{
			yield return (object)new WaitForEndOfFrame();
			if (Directory.Exists(simulationDataDirectory))
			{
				Util.DeleteDirectory(simulationDataDirectory);
			}
			if (File.Exists(finalZipFilePath))
			{
				File.Delete(finalZipFilePath);
			}
		}

		private void ShowSuccessAlert()
		{
			if (!successNotified)
			{
				successNotified = true;
				ToastManager.Instance.ShowToast("DataCapture Success");
			}
		}

		private void ShowFailureAlert()
		{
			if (!failureNotified)
			{
				failureNotified = true;
				ToastManager.Instance.ShowToast("DataCapture Failed!");
			}
		}

		public void DataCaptureSuccessCallback()
		{
			ShowSuccessAlert();
			UnityEvent dataCaptured = DataCaptured;
			if (dataCaptured != null)
			{
				dataCaptured.Invoke();
			}
		}

		private void OnDestroy()
		{
			ClearResources();
			Screen.sleepTimeout = -2;
			Util.CleanUp();
		}

		private void ClearResources()
		{
			foreach (ImageData capturedImage in capturedImages)
			{
				if (capturedImage?.imageBytes != null)
				{
					capturedImage.imageBytes = null;
				}
			}
			capturedImages.Clear();
		}

		public void ReloadScene()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			Scene activeScene = SceneManager.GetActiveScene();
			SceneManager.LoadScene(activeScene.name);
		}
	}
}
