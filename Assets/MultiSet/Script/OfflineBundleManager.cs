using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace MultiSet
{
	public class OfflineBundleManager
	{
		private LocalizationType localizationType;

		private string mapCode;

		private string mapSetCode;

		private string databasePath;

		private bool isDownloadingOfflineBundle = false;

		private bool isDownloadingMetadata = false;

		private bool isDownloadingLicense = false;

		private List<VpsMap> mapSetMaps = new List<VpsMap>();

		private const int FILE_COPY_BUFFER_SIZE = 1048576;

		public string OfflineBundlePath { get; private set; }

		public string MetadataPath { get; private set; }

		public string LicensePath { get; private set; }

		public int CurrentDownloadProgress { get; private set; }

		public string CurrentDownloadingFile { get; private set; }

		public event Action<DownloadProgressInfo> OnDownloadProgress;

		public OfflineBundleManager(LocalizationType localizationType, string mapCode, string mapSetCode)
		{
			this.localizationType = localizationType;
			this.mapCode = mapCode;
			this.mapSetCode = mapSetCode;
		}

		public string SetupDatabasePath()
		{
			string path = ((localizationType == LocalizationType.Map) ? mapCode : mapSetCode);
			string path2 = Path.Combine(Application.persistentDataPath, "MapData", path);
			if (!Directory.Exists(path2))
			{
				Directory.CreateDirectory(path2);
			}
			databasePath = path2;
			return databasePath;
		}

		public bool CheckLicenseExists()
		{
			if (string.IsNullOrEmpty(databasePath))
			{
				Debug.LogWarning((object)"Database path not set. Call SetupDatabasePath() first.");
				return false;
			}
			try
			{
				if (!Directory.Exists(databasePath))
				{
					return false;
				}
				string[] files = Directory.GetFiles(databasePath, "*license.bytes");
				if (files.Length != 0)
				{
					LicensePath = files[0];
					return true;
				}
				Debug.LogError((object)"License file not found !!");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Error checking license file: " + ex.Message));
				return false;
			}
		}

		public IEnumerator ValidateLicenseCoroutine()
		{
			if (!CheckLicenseExists())
			{
				yield return DownloadLicenseCoroutine();
			}
		}

		public async Task CopyFromStreamingAssetsAsync()
		{
			try
			{
				string streamingAssetsMapPath = Path.Combine(path3: (localizationType == LocalizationType.Map) ? mapCode : mapSetCode, path1: Application.streamingAssetsPath, path2: "MapData");
				if (!Directory.Exists(streamingAssetsMapPath))
				{
					string errorMsg = "Map folder not found in StreamingAssets: \nPlease download offline bundle in Editor mode before building the app!";
					Debug.LogError((object)errorMsg);
					ToastManager.Instance.ShowToast("Download offline bundle in Editor mode");
				}
				else if (localizationType == LocalizationType.Map)
				{
					await CopySingleMapFilesAsync(streamingAssetsMapPath, mapCode);
				}
				else
				{
					await CopyMapSetFilesAsync(streamingAssetsMapPath);
				}
			}
			catch (Exception ex)
			{
				Exception e = ex;
				string errorMsg2 = "Error copying map data: " + e.Message;
				Debug.LogError((object)errorMsg2);
				ToastManager.Instance.ShowToast(errorMsg2);
			}
		}

		private void CopyFileStreamed(string sourcePath, string targetPath, bool overwrite)
		{
			if (!overwrite && File.Exists(targetPath))
			{
				return;
			}
			using FileStream fileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.SequentialScan);
			using FileStream fileStream2 = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, FileOptions.WriteThrough);
			byte[] array = new byte[1048576];
			int count;
			while ((count = fileStream.Read(array, 0, array.Length)) > 0)
			{
				fileStream2.Write(array, 0, count);
			}
			fileStream2.Flush();
		}

		private async Task CopySingleMapFilesAsync(string streamingAssetsMapPath, string targetMapCode)
		{
			string bundleSourcePath = Path.Combine(streamingAssetsMapPath, targetMapCode + ".bytes");
			string metadataSourcePath = Path.Combine(streamingAssetsMapPath, targetMapCode + "_metadata.bytes");
			string bundleTargetPath = Path.Combine(databasePath, targetMapCode + ".bytes");
			string metadataTargetPath = Path.Combine(databasePath, targetMapCode + "_metadata.bytes");
			if (!File.Exists(bundleSourcePath))
			{
				string errorMsg = "Offline bundle not found: " + bundleSourcePath + "\nPlease download offline bundle in Editor mode before building the app!";
				Debug.LogError((object)errorMsg);
				ToastManager.Instance.ShowToast("Offline bundle not found!");
				return;
			}
			if (!File.Exists(bundleTargetPath))
			{
				await Task.Run(delegate
				{
					CopyFileStreamed(bundleSourcePath, bundleTargetPath, overwrite: false);
				});
				OfflineBundlePath = bundleTargetPath;
			}
			else
			{
				OfflineBundlePath = bundleTargetPath;
			}
			if (File.Exists(metadataSourcePath))
			{
				await Task.Run(delegate
				{
					CopyFileStreamed(metadataSourcePath, metadataTargetPath, overwrite: true);
				});
				MetadataPath = metadataTargetPath;
			}
			else
			{
				Debug.LogWarning((object)("Metadata file not found: " + metadataSourcePath));
			}
		}

		private async Task CopyMapSetFilesAsync(string streamingAssetsMapPath)
		{
			string[] bundleFiles = Directory.GetFiles(streamingAssetsMapPath, "*.bytes");
			if (bundleFiles.Length == 0)
			{
				string errorMsg = "No offline bundle files found in: " + streamingAssetsMapPath;
				Debug.LogError((object)errorMsg);
				ToastManager.Instance.ShowToast("No offline bundles found!");
				return;
			}
			int bundlesCopied = 0;
			int metadataCopied = 0;
			List<Task> copyTasks = new List<Task>();
			string[] array = bundleFiles;
			foreach (string sourceFilePath in array)
			{
				string fileName = Path.GetFileName(sourceFilePath);
				string targetFilePath = Path.Combine(databasePath, fileName);
				try
				{
					bool isMetadata = fileName.Contains("_metadata");
					if (!(!File.Exists(targetFilePath) || isMetadata))
					{
						continue;
					}
					copyTasks.Add(Task.Run(delegate
					{
						try
						{
							CopyFileStreamed(sourceFilePath, targetFilePath, isMetadata);
							if (isMetadata)
							{
								int num = metadataCopied;
								metadataCopied = num + 1;
							}
							else
							{
								int num = bundlesCopied;
								bundlesCopied = num + 1;
							}
						}
						catch (Exception ex2)
						{
							Debug.LogError((object)("Error copying " + fileName + ": " + ex2.Message));
						}
					}));
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("Error processing " + fileName + ": " + ex.Message));
				}
			}
			await Task.WhenAll(copyTasks);
		}

		public IEnumerator SetupRuntimeDownloadCoroutine(GameObject mapSpace)
		{
			if (localizationType == LocalizationType.Map)
			{
				yield return SetupSingleMapBundleDataCoroutine(mapSpace);
			}
			else
			{
				yield return SetupMapSetBundleDataCoroutine(mapSpace);
			}
		}

		private IEnumerator SetupSingleMapBundleDataCoroutine(GameObject mapSpace)
		{
			bool bundleExists = CheckOfflineBundleExists();
			bool mapDetailsFetched = false;
			bool mapDetailsFailed = false;
			VpsMap vpsMapData = null;
			MultiSetApiManager.GetMapDetails(mapCode, delegate(bool success, string data, long statusCode)
			{
				if (success && !string.IsNullOrEmpty(data))
				{
					try
					{
						vpsMapData = JsonUtility.FromJson<VpsMap>(data);
						mapDetailsFetched = true;
						return;
					}
					catch (Exception ex)
					{
						Debug.LogError((object)("Error parsing map data: " + ex.Message));
						mapDetailsFailed = true;
						return;
					}
				}
				Debug.LogError((object)$"Failed to get map details. Status: {statusCode}");
				mapDetailsFailed = true;
			});
			while (!mapDetailsFetched && !mapDetailsFailed)
			{
				yield return null;
			}
			if (mapDetailsFailed)
			{
				Debug.LogError((object)"Cannot proceed with runtime download - map details fetch failed");
				yield break;
			}
			if (!bundleExists)
			{
				yield return DownloadOfflineBundleCoroutine(vpsMapData, mapCode);
			}
			else
			{
				OfflineBundlePath = Path.Combine(databasePath, mapCode + ".bytes");
			}
			yield return DownloadMetadataCoroutine(mapCode);
			OfflineBundlePath = Path.Combine(databasePath, mapCode + ".bytes");
			MetadataPath = Path.Combine(databasePath, mapCode + "_metadata.bytes");
		}

		private IEnumerator SetupMapSetBundleDataCoroutine(GameObject mapSpace)
		{
			bool mapSetDetailsFetched = false;
			bool mapSetDetailsFailed = false;
			MapSet mapSetData = null;
			MultiSetApiManager.GetMapSetDetails(mapSetCode, delegate(bool success, string data, long statusCode)
			{
				if (success && !string.IsNullOrEmpty(data))
				{
					try
					{
						MapSetResult mapSetResult = JsonUtility.FromJson<MapSetResult>(data);
						mapSetData = mapSetResult.mapSet;
						mapSetDetailsFetched = true;
						return;
					}
					catch (Exception ex)
					{
						Debug.LogError((object)("Error parsing MapSet data: " + ex.Message));
						mapSetDetailsFailed = true;
						return;
					}
				}
				Debug.LogError((object)$"Failed to get MapSet details. Status: {statusCode}");
				mapSetDetailsFailed = true;
			});
			while (!mapSetDetailsFetched && !mapSetDetailsFailed)
			{
				yield return null;
			}
			if (mapSetDetailsFailed)
			{
				Debug.LogError((object)"Cannot proceed with runtime download - MapSet details fetch failed");
				yield break;
			}
			mapSetMaps.Clear();
			if (mapSetData.mapSetData != null && mapSetData.mapSetData.Count > 0)
			{
				foreach (MapSetData mapSetDataItem in mapSetData.mapSetData)
				{
					if (mapSetDataItem.map != null)
					{
						string bundleStatus = mapSetDataItem.map.offlineBundleStatus ?? "Not Available";
						if (bundleStatus.ToLower() == "active")
						{
							mapSetMaps.Add(mapSetDataItem.map);
						}
					}
				}
			}
			if (mapSetMaps.Count == 0)
			{
				Debug.LogError((object)"No maps with active offline bundles found in this MapSet!");
				ToastManager.Instance.ShowToast("No offline bundles available!");
				yield break;
			}
			foreach (VpsMap map in mapSetMaps)
			{
				string bundlePath = Path.Combine(databasePath, map.mapCode + ".bytes");
				if (!File.Exists(bundlePath))
				{
					yield return DownloadOfflineBundleCoroutine(map, map.mapCode);
				}
			}
			yield return DownloadMapSetMetadataCoroutine(mapSetCode);
		}

		private bool CheckOfflineBundleExists()
		{
			string path = Path.Combine(databasePath, mapCode + ".bytes");
			return File.Exists(path);
		}

		private IEnumerator DownloadOfflineBundleCoroutine(VpsMap mapData, string targetMapCode)
		{
			if (mapData == null || string.IsNullOrEmpty(mapData.offlineBundle))
			{
				Debug.LogError((object)("No offline bundle key available for " + targetMapCode + "!"));
				yield break;
			}
			if (mapData.offlineBundleStatus?.ToLower() != "active")
			{
				Debug.LogWarning((object)("Offline bundle is not active for " + targetMapCode));
				yield break;
			}
			isDownloadingOfflineBundle = true;
			bool downloadComplete = false;
			bool downloadFailed = false;
			bool urlFetched = false;
			string downloadUrl = null;
			string savePath = Path.Combine(databasePath, targetMapCode + ".bytes");
			MultiSetApiManager.GetFileUrl(mapData.offlineBundle, delegate(bool success, string data, long statusCode)
			{
				if (success && !string.IsNullOrEmpty(data))
				{
					try
					{
						FileData fileData = JsonUtility.FromJson<FileData>(data);
						downloadUrl = fileData.url;
						urlFetched = true;
						return;
					}
					catch (Exception ex)
					{
						Debug.LogError((object)("Error parsing file URL: " + ex.Message));
						downloadFailed = true;
						return;
					}
				}
				Debug.LogError((object)$"Failed to get file URL. Status: {statusCode}");
				downloadFailed = true;
			});
			while (!urlFetched && !downloadFailed)
			{
				yield return null;
			}
			if (downloadFailed || string.IsNullOrEmpty(downloadUrl))
			{
				isDownloadingOfflineBundle = false;
				yield break;
			}
			CurrentDownloadingFile = mapData.mapName;
			CurrentDownloadProgress = 0;
			int lastLoggedPercent = -1;
			yield return MultiSetHttpClient.DownloadFileCoroutine(downloadUrl, savePath, delegate(bool success, string errorMessage)
			{
				if (success)
				{
					OfflineBundlePath = savePath;
					downloadComplete = true;
					CurrentDownloadProgress = 100;
					this.OnDownloadProgress?.Invoke(new DownloadProgressInfo
					{
						MapCode = targetMapCode,
						ProgressPercent = 100,
						IsComplete = true,
						HasError = false
					});
				}
				else
				{
					Debug.LogError((object)("Failed to download offline bundle: " + errorMessage));
					downloadFailed = true;
					this.OnDownloadProgress?.Invoke(new DownloadProgressInfo
					{
						MapCode = targetMapCode,
						ProgressPercent = CurrentDownloadProgress,
						IsComplete = false,
						HasError = true,
						ErrorMessage = errorMessage
					});
				}
			}, delegate(float progress)
			{
				int num = (int)(progress * 100f);
				CurrentDownloadProgress = num;
				this.OnDownloadProgress?.Invoke(new DownloadProgressInfo
				{
					MapCode = targetMapCode,
					ProgressPercent = num,
					IsComplete = false,
					HasError = false
				});
				if (num >= lastLoggedPercent + 10 || num == 100)
				{
					lastLoggedPercent = num;
				}
			});
			isDownloadingOfflineBundle = false;
		}

		private IEnumerator DownloadMetadataCoroutine(string targetMapCode)
		{
			isDownloadingMetadata = true;
			bool downloadComplete = false;
			bool downloadFailed = false;
			string errorMessage = "";
			string savePath = Path.Combine(databasePath, targetMapCode + "_metadata.bytes");
			if (!Directory.Exists(databasePath))
			{
				Directory.CreateDirectory(databasePath);
			}
			string accessTokenJson = PlayerPrefs.GetString("MultiSet.AccessToken");
			if (string.IsNullOrEmpty(accessTokenJson))
			{
				Debug.LogError((object)"Authentication token not found!");
				isDownloadingMetadata = false;
				yield break;
			}
			AccessToken accessToken = JsonUtility.FromJson<AccessToken>(accessTokenJson);
			string metadataUrl = "https://api.multiset.ai/v1/vps/map/process-offline-metadata/" + targetMapCode;
			Task.Run(async delegate
			{
				try
				{
					using HttpClient httpClient = new HttpClient();
					httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken.token);
					httpClient.Timeout = TimeSpan.FromMinutes(5.0);
					using HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, metadataUrl), HttpCompletionOption.ResponseHeadersRead);
					if (response.IsSuccessStatusCode)
					{
						using (Stream contentStream = await response.Content.ReadAsStreamAsync())
						{
							using FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, FileOptions.Asynchronous | FileOptions.SequentialScan);
							byte[] buffer = new byte[1048576];
							while (true)
							{
								int num;
								int bytesRead = (num = await contentStream.ReadAsync(buffer, 0, buffer.Length));
								if (num <= 0)
								{
									break;
								}
								await fileStream.WriteAsync(buffer, 0, bytesRead);
							}
							await fileStream.FlushAsync();
						}
						downloadComplete = true;
					}
					else
					{
						errorMessage = string.Format(arg1: await response.Content.ReadAsStringAsync(), format: "Status: {0}, Body: {1}", arg0: response.StatusCode);
						Debug.LogError((object)("Metadata download failed: " + errorMessage));
						downloadFailed = true;
					}
				}
				catch (Exception ex)
				{
					Exception e = ex;
					errorMessage = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
					Debug.LogError((object)("Metadata download exception: " + errorMessage));
					downloadFailed = true;
					try
					{
						if (File.Exists(savePath))
						{
							File.Delete(savePath);
						}
					}
					catch
					{
					}
				}
			});
			while (!downloadComplete && !downloadFailed)
			{
				yield return null;
			}
			if (downloadFailed)
			{
				Debug.LogError((object)("Metadata download failed. Error: " + errorMessage));
				isDownloadingMetadata = false;
				yield break;
			}
			if (downloadComplete && File.Exists(savePath))
			{
				MetadataPath = savePath;
			}
			else
			{
				Debug.LogError((object)("Metadata download completed but file doesn't exist at: " + savePath));
			}
			isDownloadingMetadata = false;
		}

		private IEnumerator DownloadMapSetMetadataCoroutine(string targetMapSetCode)
		{
			isDownloadingMetadata = true;
			bool downloadComplete = false;
			bool downloadFailed = false;
			string errorMessage = "";
			string savePath = Path.Combine(databasePath, targetMapSetCode + "_metadata.bytes");
			if (!Directory.Exists(databasePath))
			{
				Directory.CreateDirectory(databasePath);
			}
			string accessTokenJson = PlayerPrefs.GetString("MultiSet.AccessToken");
			if (string.IsNullOrEmpty(accessTokenJson))
			{
				Debug.LogError((object)"Authentication token not found!");
				isDownloadingMetadata = false;
				yield break;
			}
			AccessToken accessToken = JsonUtility.FromJson<AccessToken>(accessTokenJson);
			string metadataUrl = "https://api.multiset.ai/v1/vps/map-set/process-offline-metadata/" + targetMapSetCode;
			Task.Run(async delegate
			{
				try
				{
					using HttpClient httpClient = new HttpClient();
					httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken.token);
					httpClient.Timeout = TimeSpan.FromMinutes(5.0);
					using HttpResponseMessage response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, metadataUrl), HttpCompletionOption.ResponseHeadersRead);
					if (response.IsSuccessStatusCode)
					{
						using (Stream contentStream = await response.Content.ReadAsStreamAsync())
						{
							using FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, FileOptions.Asynchronous | FileOptions.SequentialScan);
							byte[] buffer = new byte[1048576];
							while (true)
							{
								int num;
								int bytesRead = (num = await contentStream.ReadAsync(buffer, 0, buffer.Length));
								if (num <= 0)
								{
									break;
								}
								await fileStream.WriteAsync(buffer, 0, bytesRead);
							}
							await fileStream.FlushAsync();
						}
						downloadComplete = true;
					}
					else
					{
						errorMessage = string.Format(arg1: await response.Content.ReadAsStringAsync(), format: "Status: {0}, Body: {1}", arg0: response.StatusCode);
						Debug.LogError((object)("MapSet metadata download failed: " + errorMessage));
						downloadFailed = true;
					}
				}
				catch (Exception ex)
				{
					Exception e = ex;
					errorMessage = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
					Debug.LogError((object)("MapSet metadata download exception: " + errorMessage));
					downloadFailed = true;
					try
					{
						if (File.Exists(savePath))
						{
							File.Delete(savePath);
						}
					}
					catch
					{
					}
				}
			});
			while (!downloadComplete && !downloadFailed)
			{
				yield return null;
			}
			if (downloadFailed)
			{
				Debug.LogError((object)("MapSet metadata download failed. Error: " + errorMessage));
				isDownloadingMetadata = false;
				yield break;
			}
			if (downloadComplete && File.Exists(savePath))
			{
				MetadataPath = savePath;
			}
			else
			{
				Debug.LogError((object)("MapSet metadata download completed but file doesn't exist at: " + savePath));
			}
			isDownloadingMetadata = false;
		}

		private IEnumerator DownloadLicenseCoroutine()
		{
			isDownloadingLicense = true;
			bool downloadComplete = false;
			bool downloadFailed = false;
			string licenseFileName = "license.bytes";
			string errorMessage = "";
			string savePath = null;
			if (!Directory.Exists(databasePath))
			{
				Directory.CreateDirectory(databasePath);
			}
			string accessTokenJson = PlayerPrefs.GetString("MultiSet.AccessToken");
			if (string.IsNullOrEmpty(accessTokenJson))
			{
				Debug.LogError((object)"Authentication token not found!");
				isDownloadingLicense = false;
				yield break;
			}
			AccessToken accessToken = JsonUtility.FromJson<AccessToken>(accessTokenJson);
			string licenseUrl = "https://api.multiset.ai/v1/account/download-license";
			Task.Run(async delegate
			{
				try
				{
					using HttpClient httpClient = new HttpClient();
					httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken.token);
					httpClient.DefaultRequestHeaders.Add("accept", "application/octet-stream");
					httpClient.Timeout = TimeSpan.FromMinutes(5.0);
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, licenseUrl);
					using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
					if (response.IsSuccessStatusCode)
					{
						if (response.Content.Headers.ContentDisposition != null && !string.IsNullOrEmpty(response.Content.Headers.ContentDisposition.FileName))
						{
							licenseFileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
						}
						savePath = Path.Combine(databasePath, licenseFileName);
						using (Stream contentStream = await response.Content.ReadAsStreamAsync())
						{
							using FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 1048576, FileOptions.Asynchronous | FileOptions.SequentialScan);
							byte[] buffer = new byte[1048576];
							while (true)
							{
								int num;
								int bytesRead = (num = await contentStream.ReadAsync(buffer, 0, buffer.Length));
								if (num <= 0)
								{
									break;
								}
								await fileStream.WriteAsync(buffer, 0, bytesRead);
							}
							await fileStream.FlushAsync();
						}
						downloadComplete = true;
					}
					else
					{
						errorMessage = string.Format(arg1: await response.Content.ReadAsStringAsync(), format: "Status: {0}, Body: {1}", arg0: response.StatusCode);
						Debug.LogError((object)("License download failed: " + errorMessage));
						downloadFailed = true;
					}
				}
				catch (Exception ex)
				{
					Exception e = ex;
					errorMessage = e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace;
					Debug.LogError((object)("License download exception: " + errorMessage));
					downloadFailed = true;
					if (!string.IsNullOrEmpty(savePath))
					{
						try
						{
							if (File.Exists(savePath))
							{
								File.Delete(savePath);
							}
							return;
						}
						catch
						{
							return;
						}
					}
				}
			});
			while (!downloadComplete && !downloadFailed)
			{
				yield return null;
			}
			if (downloadFailed)
			{
				Debug.LogError((object)("License download failed. Error: " + errorMessage));
				isDownloadingLicense = false;
				yield break;
			}
			if (downloadComplete && !string.IsNullOrEmpty(savePath) && File.Exists(savePath))
			{
				LicensePath = savePath;
			}
			else
			{
				Debug.LogError((object)("License download completed but file doesn't exist at: " + savePath));
			}
			isDownloadingLicense = false;
		}

		public string GetMapDataPath()
		{
			string path = ((localizationType == LocalizationType.Map) ? mapCode : mapSetCode);
			return Path.Combine(Application.persistentDataPath, "MapData", path);
		}
	}
}
