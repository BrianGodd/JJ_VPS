using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class MultiSetHttpClient
	{
		public delegate void ActionCallback(bool success, string data, long statusCode);

		public AccessToken m_accessToken = new AccessToken();

		private bool m_HasAccessToken => !string.IsNullOrEmpty(m_accessToken.token);

		private static bool IsValid(long code)
		{
			return (int)code / 100 == 2;
		}

		public static IEnumerator CheckInternetConnection(Action<bool> action)
		{
			UnityWebRequest request = new UnityWebRequest("https://google.com");
			yield return request.SendWebRequest();
			if ((int)request.result != 1)
			{
				action(obj: false);
			}
			else
			{
				action(obj: true);
			}
		}

		private void GetAccessToken(UnityAction callback, bool isTokenRequired)
		{
			if (!isTokenRequired)
			{
				callback.Invoke();
				return;
			}
			if (!m_HasAccessToken)
			{
				if (!PlayerPrefs.HasKey("MultiSet.AccessToken"))
				{
					Debug.Log((object)"Token Error!");
					callback.Invoke();
					return;
				}
				string text = PlayerPrefs.GetString("MultiSet.AccessToken");
				if (string.IsNullOrWhiteSpace(text))
				{
					SceneManager.LoadSceneAsync(0);
					callback.Invoke();
					return;
				}
				AccessToken accessToken = JsonUtility.FromJson<AccessToken>(text);
				m_accessToken.token = accessToken.token;
				m_accessToken.expiresOn = accessToken.expiresOn;
			}
			if (!IsTokenExpired(m_accessToken.expiresOn))
			{
				callback.Invoke();
				return;
			}
			Debug.Log((object)"Re-Authenticate required!");
			ReAuthenticate(callback);
		}

		public bool IsTokenExpired(string expiryDate)
		{
			DateTime value = DateTime.Parse(expiryDate).ToUniversalTime();
			return DateTime.UtcNow.CompareTo(value) > 0;
		}

		private void ReAuthenticate(UnityAction callback)
		{
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008f: Expected O, but got Unknown
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Expected O, but got Unknown
			//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
			//IL_0104: Expected O, but got Unknown
			//IL_0121: Unknown result type (might be due to invalid IL or missing references)
			//IL_012b: Expected O, but got Unknown
			string text = null;
			string text2 = null;
			MultiSetConfig multiSetConfig = Resources.Load<MultiSetConfig>("MultiSetConfig");
			if ((Object)(object)multiSetConfig != (Object)null)
			{
				if (string.IsNullOrWhiteSpace(text))
				{
					text = multiSetConfig.clientId;
				}
				if (string.IsNullOrWhiteSpace(text2))
				{
					text2 = multiSetConfig.clientSecret;
				}
			}
			string text3 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text + ":" + text2));
			UnityWebRequest request = new UnityWebRequest("https://api.multiset.ai/v1/m2m/token", "POST");
			request.SetRequestHeader("Username", text);
			request.SetRequestHeader("Password", text2);
			request.SetRequestHeader("Authorization", "Basic " + text3);
			byte[] bytes = Encoding.UTF8.GetBytes("");
			request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bytes);
			request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "text/plain");
			request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
			((AsyncOperation)request.SendWebRequest()).completed += delegate
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000d: Invalid comparison between Unknown and I4
				if ((int)request.result == 1)
				{
					string text4 = request.downloadHandler.text;
					AccessToken accessToken = JsonUtility.FromJson<AccessToken>(text4);
					m_accessToken.token = accessToken.token;
					m_accessToken.expiresOn = accessToken.expiresOn;
					PlayerPrefs.SetString("MultiSet.AccessToken", JsonUtility.ToJson((object)accessToken));
					callback.Invoke();
				}
				else
				{
					Debug.LogError((object)("Something wrong while token refresh " + request.error.ToString()));
					callback.Invoke();
				}
			};
		}

		public void Request(Method method, string endPoint, string jsonData, WWWForm formData, UnityAction<long, string> callback, bool isTokenRequired = true, Dictionary<string, string> headers = null)
		{
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Expected O, but got Unknown
			GetAccessToken(new UnityAction(MakeApiCall), isTokenRequired);
			void MakeApiCall()
			{
				//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ff: Expected O, but got Unknown
				//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
				//IL_00ad: Expected O, but got Unknown
				//IL_020b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0215: Expected O, but got Unknown
				//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
				//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
				//IL_0202: Expected O, but got Unknown
				string text = "https://api.multiset.ai" + endPoint;
				if (formData != null)
				{
					UnityWebRequest request = UnityWebRequest.Post(text, formData);
					request.timeout = 120;
					if (isTokenRequired)
					{
						request.SetRequestHeader("Authorization", "Bearer " + m_accessToken.token);
					}
					request.SetRequestHeader("accept", "*/*");
					request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
					((AsyncOperation)request.SendWebRequest()).completed += delegate
					{
						callback.Invoke(request.responseCode, request.downloadHandler.text);
					};
				}
				else
				{
					UnityWebRequest request2 = new UnityWebRequest(text, method.ToString());
					request2.timeout = 120;
					if (isTokenRequired)
					{
						request2.SetRequestHeader("Authorization", "Bearer " + m_accessToken.token);
					}
					if (headers != null)
					{
						foreach (KeyValuePair<string, string> header in headers)
						{
							request2.SetRequestHeader(header.Key, header.Value);
						}
					}
					if (!string.IsNullOrEmpty(jsonData))
					{
						request2.SetRequestHeader("Content-Type", "application/json");
						request2.uploadHandler = (UploadHandler)new UploadHandlerRaw(Encoding.ASCII.GetBytes(jsonData))
						{
							contentType = "application/json"
						};
					}
					request2.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
					((AsyncOperation)request2.SendWebRequest()).completed += delegate
					{
						callback.Invoke(request2.responseCode, request2.downloadHandler.text);
					};
				}
			}
		}

		public static void CallWebAPI(Method method, string endPoint, string jsonData, WWWForm formData = null, ActionCallback onComplete = null, bool isTokenRequired = true, Dictionary<string, string> headers = null)
		{
			new MultiSetHttpClient().CreateAPIResponse(method, endPoint, jsonData, onComplete, isTokenRequired, formData, headers);
		}

		private void CreateAPIResponse(Method method, string endPoint, string jsonData, ActionCallback onComplete, bool isTokenRequired = true, WWWForm formData = null, Dictionary<string, string> headers = null)
		{
			Request(method, endPoint, jsonData, formData, delegate(long status, string res)
			{
				if (!IsValid(status))
				{
					onComplete?.Invoke(success: false, res, status);
				}
				else
				{
					onComplete?.Invoke(success: true, res, status);
				}
			}, isTokenRequired, headers);
		}

		public static async void DownloadFileAsync(string fileUrl, Action<byte[]> callback)
		{
			int timeoutSeconds = 180;
			using HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
			try
			{
				HttpResponseMessage response = await client.SendAsync(request);
				response.EnsureSuccessStatusCode();
				byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
				callback?.Invoke(fileBytes);
			}
			catch (TaskCanceledException)
			{
				Debug.LogError((object)$"Request timed out after {timeoutSeconds} seconds: {fileUrl}");
				callback?.Invoke(null);
			}
			catch (Exception ex2)
			{
				Exception ex3 = ex2;
				Debug.LogError((object)("Error downloading file: " + ex3.Message));
				callback?.Invoke(null);
			}
		}

		public static IEnumerator DownloadFileCoroutine(string fileUrl, string savePath, Action<bool, string> callback, Action<float> progressCallback = null)
		{
			string directory = Path.GetDirectoryName(savePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			UnityWebRequest request = new UnityWebRequest(fileUrl, "GET");
			try
			{
				request.downloadHandler = (DownloadHandler)new DownloadHandlerFile(savePath)
				{
					removeFileOnAbort = true
				};
				request.timeout = 1800;
				UnityWebRequestAsyncOperation operation = request.SendWebRequest();
				int lastReportedPercent = -1;
				while (!((AsyncOperation)operation).isDone)
				{
					if (progressCallback != null)
					{
						int currentPercent = (int)(request.downloadProgress * 100f);
						if (currentPercent != lastReportedPercent && currentPercent >= 0)
						{
							lastReportedPercent = currentPercent;
							progressCallback(request.downloadProgress);
						}
					}
					yield return null;
				}
				if ((int)request.result == 1)
				{
					progressCallback?.Invoke(1f);
					callback?.Invoke(arg1: true, null);
				}
				else
				{
					callback?.Invoke(arg1: false, "Download failed: " + request.error);
				}
			}
			finally
			{
				((IDisposable)request)?.Dispose();
			}
		}

		private static void TryDeleteFile(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning((object)("Failed to delete partial file " + path + ": " + ex.Message));
			}
		}
	}
}
