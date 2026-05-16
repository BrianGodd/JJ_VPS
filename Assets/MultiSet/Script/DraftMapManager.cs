using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MultiSet
{
	public class DraftMapManager : MonoBehaviour
	{
		private DraftMap selectedDraft;

		private string zipFilePath;

		private string rootDir;

		private string draftListFilePath;

		private readonly string mappingDir = "MappingData";

		private readonly string draftMapsFileName = "DraftMapList.json";

		public DraftMap SelectedDraft => selectedDraft;

		public bool HasDrafts { get; private set; }

		public event EventHandler<DraftMapListEventArgs> OnDraftListLoaded;

		public event EventHandler<DraftMapSelectedEventArgs> OnDraftSelected;

		public event EventHandler<UploadProgressEventArgs> OnUploadProgress;

		public event EventHandler<UploadCompletedEventArgs> OnUploadCompleted;

		public event EventHandler<MappingErrorEventArgs> OnError;

		public event EventHandler<MapNameValidationEventArgs> OnMapNameValidated;

		public event EventHandler<AuthenticationEventArgs> OnAuthenticationRequired;

		public event EventHandler<string> OnDraftDeleted;

		public event EventHandler<bool> OnProcessingStateChanged;

		private void Start()
		{
			rootDir = Application.persistentDataPath;
			draftListFilePath = Path.Combine(rootDir, draftMapsFileName);
			LoadDraftList();
		}

		public void LoadDraftList()
		{
			if (!File.Exists(draftListFilePath))
			{
				HasDrafts = false;
				this.OnDraftListLoaded?.Invoke(this, new DraftMapListEventArgs(null));
				return;
			}
			string text = Util.LoadFromFile(draftMapsFileName);
			if (string.IsNullOrEmpty(text))
			{
				Debug.LogError((object)"DraftMapList JSON data is null or empty.");
				HasDrafts = false;
				this.OnDraftListLoaded?.Invoke(this, new DraftMapListEventArgs(null));
				return;
			}
			DraftMapList draftMapList = JsonUtility.FromJson<DraftMapList>(text);
			if (draftMapList == null || draftMapList.draftMaps == null)
			{
				Debug.LogError((object)"DraftMapList or draftMaps is null after deserialization.");
				HasDrafts = false;
				this.OnDraftListLoaded?.Invoke(this, new DraftMapListEventArgs(null));
			}
			else
			{
				HasDrafts = draftMapList.draftMaps.Count > 0;
				this.OnDraftListLoaded?.Invoke(this, new DraftMapListEventArgs(draftMapList.draftMaps));
			}
		}

		public void SelectDraft(DraftMap draftMap)
		{
			selectedDraft = draftMap;
			zipFilePath = Path.Combine(mappingDir, draftMap.id, "MappingData.zip");
			string text = Path.Combine(rootDir, zipFilePath);
			string zipCreationDate = GetZipCreationDate(text);
			this.OnDraftSelected?.Invoke(this, new DraftMapSelectedEventArgs(draftMap, zipCreationDate));
		}

		public bool ValidateMapName(string mapName)
		{
			string text = DraftMapHelper.ValidateMapName(mapName);
			bool flag = string.IsNullOrEmpty(text);
			this.OnMapNameValidated?.Invoke(this, new MapNameValidationEventArgs(flag, text));
			return flag;
		}

		public void UploadSelectedDraft(string mapName)
		{
			if (selectedDraft == null)
			{
				RaiseError("No draft selected.");
			}
			else
			{
				if (!ValidateMapName(mapName))
				{
					return;
				}
				if (!PlayerPrefs.HasKey("MultiSet.AccessToken"))
				{
					this.OnAuthenticationRequired?.Invoke(this, new AuthenticationEventArgs(isAuthenticated: false, "Authentication required to upload maps."));
					return;
				}
				selectedDraft.mapName = mapName;
				if (!string.IsNullOrWhiteSpace(selectedDraft.mapId))
				{
					ReUploadMapData(selectedDraft.mapId);
				}
				else
				{
					UploadNewMap(mapName);
				}
			}
		}

		public void DeleteDraft(string draftId)
		{
			if (DraftMapHelper.DeleteMapFromDraftList(draftId))
			{
				string directory = Path.Combine(rootDir, mappingDir, draftId);
				Util.DeleteDirectory(directory);
				this.OnDraftDeleted?.Invoke(this, draftId);
			}
			else
			{
				RaiseError("Failed to delete draft map.");
			}
		}

		public void DeleteSelectedDraftAndReload()
		{
			if (selectedDraft == null)
			{
				RaiseError("No draft selected.");
				return;
			}
			DeleteDraft(selectedDraft.id);
			((MonoBehaviour)this).StartCoroutine(DraftMapHelper.ReloadCurrentScene());
		}

		public string GetDraftCreationDate(string draftId)
		{
			string text = Path.Combine(rootDir, mappingDir, draftId, "MappingData.zip");
			return GetZipCreationDate(text);
		}

		private void RaiseError(string message, Exception ex = null)
		{
			Debug.LogError((object)message);
			this.OnError?.Invoke(this, new MappingErrorEventArgs(message, ex));
		}

		private void SetProcessing(bool isProcessing)
		{
			this.OnProcessingStateChanged?.Invoke(this, isProcessing);
		}

		private string GetZipCreationDate(string zipFilePath)
		{
			if (File.Exists(zipFilePath))
			{
				return File.GetCreationTime(zipFilePath).ToString("dd MMM yyyy, hh:mmtt", CultureInfo.InvariantCulture).ToLower();
			}
			Debug.LogError((object)("Zip file not found at path: " + zipFilePath));
			return DateTime.MinValue.ToString("dd MMM yyyy, hh:mmtt", CultureInfo.InvariantCulture).ToLower();
		}

		private void ReUploadMapData(string mapId)
		{
			try
			{
				SetProcessing(isProcessing: true);
				MultiSetApiManager.ReUploadMapData(mapId, ReUploadMapApiCallback);
			}
			catch (Exception ex)
			{
				SetProcessing(isProcessing: false);
				RaiseError("Re-Upload Map failed: " + ex.Message, ex);
			}
		}

		private void ReUploadMapApiCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				SetProcessing(isProcessing: false);
				RaiseError("Re-Upload Map Callback: Empty or null data received!");
			}
			else if (success)
			{
				ReUploadMapResponse reUploadMapResponse = JsonUtility.FromJson<ReUploadMapResponse>(data);
				if (reUploadMapResponse == null)
				{
					SetProcessing(isProcessing: false);
					RaiseError("ReUploadMapResponse deserialization failed!");
				}
				else
				{
					PlayerPrefs.SetString("RE-Upload_MAP_RESPONSE", JsonUtility.ToJson((object)reUploadMapResponse));
					((MonoBehaviour)this).StartCoroutine(UploadZipFile(reUploadMapResponse.uploadUrl));
				}
			}
			else
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				if (errorJSON != null && !string.IsNullOrEmpty(errorJSON.message) && errorJSON.message.Equals("Map not found"))
				{
					UploadNewMap(selectedDraft.mapName);
					return;
				}
				SetProcessing(isProcessing: false);
				RaiseError(errorJSON?.message ?? "Unknown error during re-upload");
			}
		}

		private void UploadNewMap(string mapName)
		{
			try
			{
				SetProcessing(isProcessing: true);
				MapPayload mapPayload = new MapPayload
				{
					mapName = mapName,
					coordinates = selectedDraft.coordinates
				};
				string mapPayload2 = JsonUtility.ToJson((object)mapPayload);
				MultiSetApiManager.CreateMap(mapPayload2, CreateMapApiCallback);
			}
			catch (Exception ex)
			{
				SetProcessing(isProcessing: false);
				RaiseError("Map Creation failed: " + ex.Message, ex);
			}
		}

		private void CreateMapApiCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				SetProcessing(isProcessing: false);
				RaiseError("Create Map Callback: Empty or null data received!");
			}
			else if (success)
			{
				CreateMapResponse createMapResponse = JsonUtility.FromJson<CreateMapResponse>(data);
				if (createMapResponse == null)
				{
					SetProcessing(isProcessing: false);
					RaiseError("CreateMapApi deserialization failed!");
				}
				else
				{
					PlayerPrefs.SetString("MAP_RESPONSE", JsonUtility.ToJson((object)createMapResponse));
					((MonoBehaviour)this).StartCoroutine(UploadZipFile(createMapResponse.uploadUrl));
				}
			}
			else
			{
				SetProcessing(isProcessing: false);
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				if (errorJSON?.error == "Invalid token")
				{
					this.OnAuthenticationRequired?.Invoke(this, new AuthenticationEventArgs(isAuthenticated: false, "Invalid token, please re-authenticate."));
				}
				else
				{
					RaiseError(errorJSON?.message ?? "Unknown error");
				}
			}
		}

		private IEnumerator UploadZipFile(string uploadUrl)
		{
			string finalZipFilePath = Path.Combine(rootDir, zipFilePath);
			if (!File.Exists(finalZipFilePath))
			{
				SetProcessing(isProcessing: false);
				RaiseError("File not found at: " + finalZipFilePath);
				yield break;
			}
			byte[] fileData = File.ReadAllBytes(finalZipFilePath);
			UnityWebRequest request = UnityWebRequest.Put(uploadUrl, fileData);
			try
			{
				request.SetRequestHeader("Content-Type", "application/zip");
				request.SendWebRequest();
				while (!request.isDone)
				{
					this.OnUploadProgress?.Invoke(this, new UploadProgressEventArgs(request.uploadProgress));
					yield return null;
				}
				SetProcessing(isProcessing: false);
				if ((int)request.result == 1)
				{
					this.OnUploadCompleted?.Invoke(this, new UploadCompletedEventArgs(success: true));
					DraftMapHelper.DeleteMapFromDraftList(selectedDraft.id);
					string draftMapDir = Path.Combine(rootDir, mappingDir, selectedDraft.id);
					Util.DeleteDirectory(draftMapDir);
				}
				else
				{
					RaiseError($"Failed to upload file: {request.responseCode} - {request.error}");
					this.OnUploadCompleted?.Invoke(this, new UploadCompletedEventArgs(success: false, null, request.error));
				}
			}
			finally
			{
				((IDisposable)request)?.Dispose();
			}
		}
	}
}
