using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class MultisetSdkManager : MonoBehaviour
	{
		public const string version = "1.10.1";

		[Header("Credentials")]
		[SerializeField]
		[HideInInspector]
		[Tooltip("The clientId to use when authenticating using API Key.")]
		public string clientId = "";

		[SerializeField]
		[HideInInspector]
		[Tooltip("The clientSecret to use when authenticating using API Key.")]
		public string clientSecret = "";

		[Space(10f)]
		[Tooltip("Enable this if you like to update credentials manually and call AuthenticateMultiSetSDK() function")]
		public bool runtimeAuthentication = false;

		private bool showWatermark = false;

		[Tooltip("Show toast alert if SDK is not authenticated")]
		[SerializeField]
		private bool showAlert = true;

		private Texture2D watermark;

		private float dist = 100f;

		private float guiRatioX;

		private float guiRatioY;

		private float sWidth;

		private float sHeight;

		private float sizeguiw = 473f;

		private float sizeguih = 128f;

		public static MultisetSdkManager Instance { get; private set; }

		private void SdkInstantiate()
		{
			if ((Object)(object)Instance == (Object)null)
			{
				Instance = this;
			}
			UpdateWaterMark();
		}

		protected virtual void Awake()
		{
			Application.targetFrameRate = 90;
			Screen.sleepTimeout = -1;
			SdkInstantiate();
		}

		private void OnEnable()
		{
			LoadConfiguration();
		}

		protected virtual void LoadConfiguration()
		{
			MultiSetConfig multiSetConfig = Resources.Load<MultiSetConfig>("MultiSetConfig");
			if (!((Object)(object)multiSetConfig != (Object)null))
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(clientId))
			{
				clientId = multiSetConfig.clientId;
			}
			if (string.IsNullOrWhiteSpace(clientSecret))
			{
				clientSecret = multiSetConfig.clientSecret;
			}
			if (!runtimeAuthentication && HasValidCredentials())
			{
				((MonoBehaviour)this).StartCoroutine(MultiSetHttpClient.CheckInternetConnection(delegate(bool isConnected)
				{
					if (!isConnected && showAlert)
					{
						ToastManager.Instance.ShowToast("Network Error!");
					}
					else
					{
						((MonoBehaviour)this).StartCoroutine(SdkAuthenticate(clientId, clientSecret));
					}
				}));
			}
			else
			{
				Debug.LogError((object)"Please enter valid credentials in MultiSetConfig!");
				EventManager<EventData>.TriggerEvent("AuthCallBack", new EventData
				{
					AuthSuccess = false
				});
			}
		}

		public void AuthenticateMultiSetSDK()
		{
			if (HasValidCredentials())
			{
				((MonoBehaviour)this).StartCoroutine(MultiSetHttpClient.CheckInternetConnection(delegate(bool isConnected)
				{
					if (!isConnected && showAlert)
					{
						ToastManager.Instance.ShowToast("Network Error!");
					}
					else
					{
						((MonoBehaviour)this).StartCoroutine(SdkAuthenticate(clientId, clientSecret));
					}
				}));
			}
			else
			{
				ToastManager.Instance.ShowToast("Please update valid credentials.");
				Debug.LogError((object)"Please update valid credentials.");
			}
		}

		private bool HasValidCredentials()
		{
			return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
		}

		private IEnumerator SdkAuthenticate(string clientId, string clientSecret)
		{
			string authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId.Trim() + ":" + clientSecret.Trim()));
			UnityWebRequest request = new UnityWebRequest("https://api.multiset.ai/v1/m2m/token", "POST");
			request.SetRequestHeader("Username", clientId.Trim());
			request.SetRequestHeader("Password", clientSecret.Trim());
			request.SetRequestHeader("Authorization", "Basic " + authValue);
			byte[] bodyRaw = Encoding.UTF8.GetBytes("");
			request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "text/plain");
			yield return request.SendWebRequest();
			if ((int)request.result == 2 || (int)request.result == 3)
			{
				Debug.LogError((object)("Error: " + request.error));
				EventManager<EventData>.TriggerEvent("AuthCallBack", new EventData
				{
					AuthSuccess = false
				});
				showWatermark = true;
			}
			else
			{
				string data = request.downloadHandler.text;
				AccessToken accessToken = JsonUtility.FromJson<AccessToken>(data);
				PlayerPrefs.SetString("MultiSet.AccessToken", JsonUtility.ToJson((object)accessToken));
				EventManager<EventData>.TriggerEvent("AuthCallBack", new EventData
				{
					AuthSuccess = true
				});
				CheckPlanDetailsAPI();
			}
		}

		private void CheckPlanDetailsAPI()
		{
			if (Util.IsNetworkAvailable())
			{
				MultiSetApiManager.GetPlanDetails(PlanDetailsCallback);
			}
			else
			{
				Debug.LogWarning((object)"No network connection");
			}
		}

		private void PlanDetailsCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				showWatermark = true;
				Debug.LogError((object)"Error : Plan Details Callback: Empty or null data received!");
			}
			else if (success)
			{
				PlanDetails planDetails = JsonUtility.FromJson<PlanDetails>(data);
				showWatermark = planDetails.watermark;
			}
			else
			{
				showWatermark = true;
				Debug.LogError((object)"Get Plan Details failed!");
			}
		}

		public void UpdateWaterMark()
		{
			//IL_0004: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Expected O, but got Unknown
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Invalid comparison between Unknown and I4
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Invalid comparison between Unknown and I4
			watermark = new Texture2D(2, 2);
			ImageConversion.LoadImage(watermark, Base64ToImageConverter.LoadWatermark());
			sWidth = Screen.width;
			sHeight = Screen.height;
			if ((int)Screen.orientation == 3 || (int)Screen.orientation == 4 || Screen.width > Screen.height)
			{
				guiRatioX = sWidth / 2560f * sizeguiw;
				guiRatioY = sHeight / 1440f * sizeguih;
				float num = sWidth * 4f / 100f;
				dist = num;
			}
			else
			{
				guiRatioX = sWidth / 1440f * sizeguiw;
				guiRatioY = sHeight / 2560f * sizeguih;
				float num2 = sHeight * 4f / 100f;
				dist = num2;
			}
		}

		private void OnGUI()
		{
			//IL_004e: Unknown result type (might be due to invalid IL or missing references)
			if (showWatermark && !((Object)(object)watermark == (Object)null))
			{
				float num = 0f;
				float num2 = (float)Screen.height - guiRatioY - dist;
				GUI.DrawTexture(new Rect(num + dist, num2, guiRatioX, guiRatioY), (Texture)(object)watermark, (ScaleMode)2, true);
			}
		}
	}
}
