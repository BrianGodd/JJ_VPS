using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class MapsetAlignmentManager : MonoBehaviour
	{
		[Space(10f)]
		[Tooltip("Assign MapSpace GameObject")]
		public GameObject mapSpace;

		[SerializeField]
		[Tooltip("Enter MapSet Code that has to be localized.")]
		public string mapsetCode = string.Empty;

		[HideInInspector]
		public MapSet currentMapSet;

		[HideInInspector]
		public bool isLoadingMapSetInfo = false;

		[HideInInspector]
		public bool isDownloadingMeshes = false;

		[HideInInspector]
		public string loadingStatus = "";

		[HideInInspector]
		public List<GameObject> loadedMapMeshes = new List<GameObject>();

		private GameObject mapSetContainerObject;

		private int loadedMapsCount = 0;

		public Action<bool, string> onMapSetInfoLoaded;

		public Action<bool, string> onMeshesDownloaded;

		public Action<bool, string> onAlignmentSaved;

		public Action onRefreshAssetDatabase;

		public Action<UnityEngine.Object> onSetDirty;

		public Func<string, GameObject> onLoadAssetAtPath;

		public Func<UnityEngine.Object, UnityEngine.Object> onInstantiatePrefab;

		public Action<GameObject, string> onSaveAsPrefab;

		public Action<Scene> onMarkSceneDirty;

		public void GetMapSetInfo()
		{
			if (string.IsNullOrEmpty(mapsetCode))
			{
				Debug.LogError((object)"MapSet code is empty. Please enter a valid MapSet code.");
				onMapSetInfoLoaded?.Invoke(arg1: false, "MapSet code is empty");
			}
			else
			{
				isLoadingMapSetInfo = true;
				loadingStatus = "Loading MapSet info...";
				GetMapSetInfo(mapsetCode);
			}
		}

		private void GetMapSetInfo(string mapsetCode)
		{
			MultiSetApiManager.GetMapSetDetails(mapsetCode, MapSetDetailsCallback);
		}

		private void MapSetDetailsCallback(bool success, string data, long statusCode)
		{
			isLoadingMapSetInfo = false;
			if (string.IsNullOrEmpty(data))
			{
				Debug.LogError((object)"MapSet Details Callback: Empty or null data received.");
				loadingStatus = "Failed: Empty data received";
				onMapSetInfoLoaded?.Invoke(arg1: false, "Empty data received");
			}
			else if (success)
			{
				MapSetResult mapSetResult = JsonUtility.FromJson<MapSetResult>(data);
				if (mapSetResult != null && mapSetResult.mapSet != null)
				{
					currentMapSet = mapSetResult.mapSet;
					loadingStatus = "MapSet info loaded successfully";
					onMapSetInfoLoaded?.Invoke(arg1: true, "MapSet info loaded successfully");
					onSetDirty?.Invoke((Object)(object)this);
				}
				else
				{
					Debug.LogError((object)"Failed to parse MapSet data");
					loadingStatus = "Failed: Invalid data format";
					onMapSetInfoLoaded?.Invoke(arg1: false, "Invalid data format");
				}
			}
			else
			{
				string text = JsonUtility.FromJson<ErrorJSON>(data)?.error ?? "Unknown error";
				Debug.LogError((object)$"Load MapSet Info Failed: {text} (code: {statusCode})");
				loadingStatus = "Failed: " + text;
				onMapSetInfoLoaded?.Invoke(arg1: false, text);
			}
		}

		public void ViewMapSet()
		{
			if (currentMapSet == null)
			{
				Debug.LogError((object)"No MapSet loaded. Please load MapSet info first.");
			}
			else if ((Object)(object)mapSpace == (Object)null)
			{
				Debug.LogError((object)"MapSpace GameObject is not assigned.");
			}
			else
			{
				DownloadMapSetMeshes();
			}
		}

		private void DownloadMapSetMeshes()
		{
			isDownloadingMeshes = true;
			loadingStatus = "Downloading meshes...";
			loadedMapsCount = 0;
			ClearLoadedMeshes();
			List<MapSetData> mapSetData = currentMapSet.mapSetData;
			if (mapSetData == null || mapSetData.Count == 0)
			{
				Debug.LogWarning((object)"No maps found in MapSet");
				isDownloadingMeshes = false;
				loadingStatus = "No maps to download";
				onMeshesDownloaded?.Invoke(arg1: false, "No maps found");
				return;
			}
			foreach (MapSetData item in mapSetData)
			{
				string mapCode = item.map.mapCode;
				string text = Path.Combine(Application.dataPath, "MultiSet/MapData/" + currentMapSet.mapSetCode);
				string path = Path.Combine(text, mapCode + ".glb");
				string assetPath = Path.Combine("Assets/MultiSet/MapData/" + currentMapSet.mapSetCode, mapCode + ".glb");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				if (File.Exists(path))
				{
					ImportAndAttachMesh(assetPath, item.map._id);
				}
				else if (item.map?.mapMesh?.texturedMesh?.meshLink != null)
				{
					string meshLink = item.map.mapMesh.texturedMesh.meshLink;
					MultiSetApiManager.GetFileUrl(meshLink, FileUrlCallback);
				}
				else
				{
					Debug.LogWarning((object)("Map " + mapCode + " has no mesh link"));
				}
			}
		}

		private void FileUrlCallback(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				Debug.LogError((object)"File URL Callback: Empty or null data received!");
			}
			else if (success)
			{
				FileData meshUrl = JsonUtility.FromJson<FileData>(data);
				MultiSetHttpClient.DownloadFileAsync(meshUrl.url, delegate(byte[] fileData)
				{
					if (fileData != null)
					{
						try
						{
							string mapId = Util.GetMapId(meshUrl.url);
							string mapCodeFromMapSetData = GetMapCodeFromMapSetData(mapId);
							string path = Path.Combine(Application.dataPath, "MultiSet/MapData/" + currentMapSet.mapSetCode);
							string path2 = Path.Combine(path, mapCodeFromMapSetData + ".glb");
							string assetPath = Path.Combine("Assets/MultiSet/MapData/" + currentMapSet.mapSetCode, mapCodeFromMapSetData + ".glb");
							File.WriteAllBytes(path2, fileData);
							onRefreshAssetDatabase?.Invoke();
							ImportAndAttachMesh(assetPath, mapId);
							return;
						}
						catch (Exception ex)
						{
							Debug.LogError((object)("Failed to save mesh file: " + ex.Message));
							return;
						}
					}
					Debug.LogError((object)"Failed to download mesh file.");
				});
			}
			else
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				Debug.LogError((object)("Error: " + errorJSON.error));
			}
		}

		private string GetMapCodeFromMapSetData(string mapId)
		{
			if (currentMapSet != null && currentMapSet.mapSetData != null)
			{
				foreach (MapSetData mapSetDatum in currentMapSet.mapSetData)
				{
					if (mapSetDatum.map._id == mapId)
					{
						return mapSetDatum.map.mapCode;
					}
				}
			}
			return null;
		}

		private MapSetData GetMapSetDataByMapId(string mapId)
		{
			if (currentMapSet != null && currentMapSet.mapSetData != null)
			{
				foreach (MapSetData mapSetDatum in currentMapSet.mapSetData)
				{
					if (mapSetDatum.map._id == mapId)
					{
						return mapSetDatum;
					}
				}
			}
			return null;
		}

		public MapSetData GetMapSetDataByDataId(string dataId)
		{
			if (currentMapSet != null && currentMapSet.mapSetData != null)
			{
				foreach (MapSetData mapSetDatum in currentMapSet.mapSetData)
				{
					if (mapSetDatum._id == dataId)
					{
						return mapSetDatum;
					}
				}
			}
			return null;
		}

		private void ImportAndAttachMesh(string assetPath, string mapId)
		{
			//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00af: Expected O, but got Unknown
			//IL_038f: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_02f7: Expected O, but got Unknown
			if (onLoadAssetAtPath == null || onInstantiatePrefab == null)
			{
				Debug.LogError((object)"[MapsetAlignmentManager] Editor callbacks not initialized. Make sure MapsetAlignmentManagerEditor is active.");
				return;
			}
			GameObject val = onLoadAssetAtPath(assetPath);
			if ((Object)(object)val == (Object)null)
			{
				Debug.LogError((object)("Failed to load GLB file. Path: " + assetPath));
				return;
			}
			if ((Object)(object)mapSetContainerObject == (Object)null)
			{
				mapSetContainerObject = GameObject.Find(currentMapSet.mapSetCode);
				if ((Object)(object)mapSetContainerObject == (Object)null)
				{
					mapSetContainerObject = new GameObject(currentMapSet.mapSetCode);
					mapSetContainerObject.transform.SetParent(mapSpace.transform, false);
					mapSetContainerObject.tag = "EditorOnly";
				}
			}
			GameObject val2 = GameObject.Find(((Object)val).name);
			if ((Object)(object)val2 != (Object)null)
			{
				Debug.LogWarning((object)("Map Mesh with the name " + ((Object)val).name + " already exists in the hierarchy."));
				return;
			}
			Object obj = onInstantiatePrefab((Object)(object)val);
			GameObject val3 = (GameObject)(object)((obj is GameObject) ? obj : null);
			if ((Object)(object)val3 == (Object)null)
			{
				Debug.LogError((object)"Failed to instantiate mesh");
				return;
			}
			val3.transform.SetParent(mapSetContainerObject.transform, false);
			val3.tag = "EditorOnly";
			Util.UpdateMeshPoseAndRotation(val3, currentMapSet, mapId);
			MapSetData mapSetDataByMapId = GetMapSetDataByMapId(mapId);
			if (mapSetDataByMapId != null)
			{
				MapDataReference mapDataReference = val3.AddComponent<MapDataReference>();
				mapDataReference.Initialize(mapSetDataByMapId, currentMapSet, this);
			}
			else
			{
				Debug.LogWarning((object)("[MapsetAlignmentManager] Could not find MapSetData for mapId: " + mapId));
			}
			loadedMapMeshes.Add(val3);
			loadedMapsCount++;
			string name = ((Object)val3).name;
			loadingStatus = $"Downloading mesh {loadedMapsCount}/{currentMapSet.mapSetData.Count}...";
			if (loadedMapsCount != currentMapSet.mapSetData.Count)
			{
				return;
			}
			string text = Path.Combine("Assets/MultiSet/MapData/", currentMapSet.mapSetCode + ".prefab");
			onSaveAsPrefab?.Invoke(mapSetContainerObject, text);
			GameObject val4 = onLoadAssetAtPath(text);
			if ((Object)(object)val4 != (Object)null)
			{
				Object obj2 = onInstantiatePrefab((Object)(object)val4);
				GameObject val5 = (GameObject)(object)((obj2 is GameObject) ? obj2 : null);
				if ((Object)(object)val5 != (Object)null)
				{
					val5.transform.SetParent(mapSpace.transform, false);
					loadedMapMeshes.Clear();
					foreach (Transform item in val5.transform)
					{
						Transform val6 = item;
						loadedMapMeshes.Add(((Component)val6).gameObject);
					}
					Object.DestroyImmediate((Object)(object)mapSetContainerObject);
					mapSetContainerObject = val5;
				}
			}
			isDownloadingMeshes = false;
			loadingStatus = $"All meshes downloaded ({loadedMapsCount} maps)";
			onMeshesDownloaded?.Invoke(arg1: true, "All meshes downloaded successfully");
			onMarkSceneDirty?.Invoke(mapSpace.scene);
		}

		private void ClearLoadedMeshes()
		{
			foreach (GameObject loadedMapMesh in loadedMapMeshes)
			{
				if ((Object)(object)loadedMapMesh != (Object)null)
				{
					Object.DestroyImmediate((Object)(object)loadedMapMesh);
				}
			}
			loadedMapMeshes.Clear();
			if ((Object)(object)mapSetContainerObject != (Object)null)
			{
				Object.DestroyImmediate((Object)(object)mapSetContainerObject);
				mapSetContainerObject = null;
			}
			if (currentMapSet != null)
			{
				GameObject val = GameObject.Find(currentMapSet.mapSetCode);
				if ((Object)(object)val != (Object)null)
				{
					Object.DestroyImmediate((Object)(object)val);
				}
			}
		}

		public void CompleteAlignment()
		{
			//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			//IL_016e: Unknown result type (might be due to invalid IL or missing references)
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
			if (currentMapSet == null)
			{
				Debug.LogError((object)"No MapSet loaded");
				return;
			}
			if (loadedMapMeshes.Count == 0)
			{
				Debug.LogError((object)"No meshes loaded to save alignment");
				return;
			}
			for (int i = 0; i < loadedMapMeshes.Count && i < currentMapSet.mapSetData.Count; i++)
			{
				GameObject val = loadedMapMeshes[i];
				MapSetData mapSetData = currentMapSet.mapSetData[i];
				if (mapSetData.relativePose == null)
				{
					mapSetData.relativePose = new RelativePose();
				}
				if (mapSetData.relativePose.position == null)
				{
					mapSetData.relativePose.position = new Position();
				}
				if (mapSetData.relativePose.rotation == null)
				{
					mapSetData.relativePose.rotation = new RotationData();
				}
				mapSetData.relativePose.position.x = val.transform.localPosition.x;
				mapSetData.relativePose.position.y = val.transform.localPosition.y;
				mapSetData.relativePose.position.z = val.transform.localPosition.z;
				mapSetData.relativePose.rotation.qx = val.transform.localRotation.x;
				mapSetData.relativePose.rotation.qy = val.transform.localRotation.y;
				mapSetData.relativePose.rotation.qz = val.transform.localRotation.z;
				mapSetData.relativePose.rotation.qw = val.transform.localRotation.w;
			}
			SaveAlignmentToServer();
		}

		private void SaveAlignmentToServer()
		{
			string text = JsonUtility.ToJson((object)currentMapSet);
			Debug.LogWarning((object)"SaveMapSetAlignment API endpoint not implemented yet. Add it to MultiSetApiManager.");
			onAlignmentSaved?.Invoke(arg1: true, "Alignment data prepared (API not implemented)");
		}

		private void OnDestroy()
		{
			ClearLoadedMeshes();
		}

		public void UpdateMapPose(string dataId, RelativePose relativePose, Action<bool, string, long> onComplete)
		{
			if (string.IsNullOrEmpty(dataId))
			{
				Debug.LogError((object)"[MapsetAlignmentManager] UpdateMapPose: dataId is empty");
				onComplete?.Invoke(arg1: false, "Data ID is empty", 0L);
				return;
			}
			if (relativePose == null)
			{
				Debug.LogError((object)"[MapsetAlignmentManager] UpdateMapPose: relativePose is null");
				onComplete?.Invoke(arg1: false, "Relative pose is null", 0L);
				return;
			}
			var anon = new { relativePose };
			string dataPayload = JsonUtility.ToJson((object)new RelativePoseWrapper
			{
				relativePose = relativePose
			});
			MultiSetApiManager.UpdateMapSetData(dataId, dataPayload, delegate(bool success, string data, long statusCode)
			{
				if (success)
				{
					MapSetData mapSetDataByDataId = GetMapSetDataByDataId(dataId);
					if (mapSetDataByDataId != null)
					{
						mapSetDataByDataId.relativePose = relativePose;
					}
				}
				else
				{
					string arg = ApiErrorHelper.ParseErrorMessage(data);
					Debug.LogError((object)$"[MapsetAlignmentManager] Failed to update pose: {arg} (code: {statusCode})");
				}
				onComplete?.Invoke(success, data, statusCode);
			});
		}

		public void UpdateAllModifiedPoses(Action<int, int> onProgress, Action<bool, string> onComplete)
		{
			List<MapDataReference> list = new List<MapDataReference>();
			foreach (GameObject loadedMapMesh in loadedMapMeshes)
			{
				if ((Object)(object)loadedMapMesh != (Object)null)
				{
					MapDataReference component = loadedMapMesh.GetComponent<MapDataReference>();
					if ((Object)(object)component != (Object)null && component.hasUnsavedChanges)
					{
						list.Add(component);
					}
				}
			}
			if (list.Count == 0)
			{
				onComplete?.Invoke(arg1: true, "No modified maps to update");
				return;
			}
			int totalMaps = list.Count;
			int completedMaps = 0;
			int successCount = 0;
			List<string> errors = new List<string>();
			foreach (MapDataReference mapRef in list)
			{
				mapRef.isUpdating = true;
				RelativePose relativePosePayload = mapRef.GetRelativePosePayload();
				UpdateMapPose(mapRef.dataId, relativePosePayload, delegate(bool success, string data, long statusCode)
				{
					int num = completedMaps;
					completedMaps = num + 1;
					if (success)
					{
						num = successCount;
						successCount = num + 1;
						mapRef.OnPoseUpdateSuccess();
					}
					else
					{
						errors.Add(mapRef.mapName + ": " + data);
						mapRef.OnPoseUpdateFailed(data);
					}
					onProgress?.Invoke(completedMaps, totalMaps);
					if (completedMaps == totalMaps)
					{
						if (errors.Count == 0)
						{
							onComplete?.Invoke(arg1: true, $"Successfully updated {successCount} map(s)");
						}
						else
						{
							onComplete?.Invoke(arg1: false, $"Updated {successCount}/{totalMaps} maps. Errors:\n" + string.Join("\n", errors));
						}
					}
				});
			}
		}
	}
}
