using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Loading;
using GLTFast.Logging;
using GLTFast.Materials;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class MapMeshHandler : MonoBehaviour
	{
		[Tooltip("Select option for the map mesh visualization.")]
		[Header("Map Mesh Settings")]
		[SerializeField]
		private MeshVisualizationOption meshVisualizationOption = MeshVisualizationOption.EnableVisualization;
		[SerializeField]
		private bool preserveLabelsOverMesh = true;

		private bool m_localizeSuccess;

		private GameObject m_mapSpace;

		private GameObject m_mesh;

		private Material m_meshMaterial;

		private Material occlusionMaterial;
		private Material m_labelFriendlyMeshMaterial;

		private VpsMap m_vpsMap;

		private string m_savePath;

		private bool itsMapSet = false;

		private MapSet mapSet = null;

		public static MapMeshHandler Instance { get; private set; }

		private void Awake()
		{
			if ((Object)(object)Instance == (Object)null)
			{
				Instance = this;
			}
		}

		private void OnDestroy()
		{
			if ((Object)(object)m_labelFriendlyMeshMaterial != (Object)null)
			{
				Object.Destroy((Object)(object)m_labelFriendlyMeshMaterial);
				m_labelFriendlyMeshMaterial = null;
			}
		}

		internal void GetLocalizedMapDetails(MapSet mapSet, string localizedMapId, GameObject mapSpace)
		{
			this.mapSet = mapSet;
			m_mapSpace = mapSpace;
			m_vpsMap = getVpsMapFromMapSet(mapSet, localizedMapId);
			if (m_vpsMap != null)
			{
				itsMapSet = true;
				GameObject val = GameObject.Find(m_vpsMap._id);
				if (!((Object)(object)val != (Object)null))
				{
					GetMeshFileFromUrl(m_mapSpace, m_vpsMap);
				}
			}
		}

		private VpsMap getVpsMapFromMapSet(MapSet mapSet, string localizedMapId)
		{
			if (mapSet == null || mapSet.mapSetData == null || mapSet.mapSetData.Count == 0)
			{
				Debug.LogError((object)("Invalid MapSet or mapSetData is empty. LocalizedMapId: " + localizedMapId));
				return null;
			}
			VpsMap vpsMap = mapSet.mapSetData.Select((MapSetData data) => data.map).FirstOrDefault((VpsMap map) => map != null && map._id == localizedMapId);
			if (vpsMap == null)
			{
				vpsMap = mapSet.mapSetData.Select((MapSetData data) => data.map).FirstOrDefault((VpsMap map) => map != null && map.mapCode == localizedMapId);
				if (vpsMap == null)
				{
					Debug.LogError((object)("VpsMap with ID or Code '" + localizedMapId + "' not found in MapSet '" + mapSet._id + "'."));
				}
			}
			return vpsMap;
		}

		internal void DownloadGlbFile(GameObject mapSpace, VpsMap vpsMap)
		{
			itsMapSet = false;
			m_vpsMap = vpsMap;
			m_mapSpace = mapSpace;
			GetMeshFileFromUrl(m_mapSpace, vpsMap);
		}

		private void GetMeshFileFromUrl(GameObject mapSpace, VpsMap vpsMap)
		{
			if (meshVisualizationOption.Equals(MeshVisualizationOption.NoMesh))
			{
				return;
			}
			string path = vpsMap._id + ".glb";
			m_savePath = Path.Combine(Application.persistentDataPath, path);
			if (File.Exists(m_savePath))
			{
				LoadMeshInSceneAsync(m_savePath);
				return;
			}
			string meshLink = m_vpsMap.mapMesh.rawMesh.meshLink;
			if (!string.IsNullOrWhiteSpace(meshLink))
			{
				if (Util.IsNetworkAvailable())
				{
					MultiSetApiManager.GetFileUrl(meshLink, FileUrlCallback);
				}
				else
				{
					Debug.LogWarning((object)"No network connection");
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
				FileData fileData = JsonUtility.FromJson<FileData>(data);
				((MonoBehaviour)this).StartCoroutine(DownloadMeshCoroutine(fileData.url, m_savePath));
			}
			else
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				Debug.LogError((object)("Error : " + errorJSON.error));
			}
		}

		private IEnumerator DownloadMeshCoroutine(string url, string savePath)
		{
			bool downloadComplete = false;
			bool downloadSuccess = false;
			yield return MultiSetHttpClient.DownloadFileCoroutine(url, savePath, delegate(bool success, string errorMessage)
			{
				downloadComplete = true;
				downloadSuccess = success;
				if (!success)
				{
					Debug.LogError((object)("Failed to download mesh file: " + errorMessage));
				}
			});
			if (downloadSuccess)
			{
				LoadMeshInSceneAsync(savePath);
			}
		}

		private async Task LoadMeshInSceneAsync(string meshPath)
		{
			await LoadGltfBinaryFromMemory(meshPath);
		}

		private async Task LoadGltfBinaryFromMemory(string filePath)
		{
			byte[] data = File.ReadAllBytes(filePath);
			GltfImport gltf = new GltfImport((IDownloadProvider)null, (IDeferAgent)null, (IMaterialGenerator)null, (ICodeLogger)null);
			if (!(await ((GltfImportBase)gltf).LoadGltfBinary(data, new Uri(filePath), (ImportSettings)null, default(CancellationToken))))
			{
				return;
			}
			if ((Object)(object)GameObject.Find(m_vpsMap._id) != (Object)null)
			{
				Debug.LogWarning((object)"Map Mesh already exists in the hierarchy.");
				return;
			}
			m_mesh = new GameObject(m_vpsMap._id);
			await ((GltfImportBase)gltf).InstantiateMainSceneAsync(m_mesh.transform, default(CancellationToken));
			m_mesh.transform.SetParent(m_mapSpace.transform, false);
			if (itsMapSet)
			{
				Util.UpdateMeshPoseAndRotation(m_mesh, mapSet, m_vpsMap._id);
			}
			else
			{
				m_mesh.transform.localPosition = Vector3.zero;
				m_mesh.transform.localRotation = Quaternion.identity;
			}
			ApplyTransparentMaterial(m_mesh);
			if ((Object)(object)m_mesh != (Object)null)
			{
				m_mesh.SetActive(m_localizeSuccess);
			}
		}

		private void ApplyTransparentMaterial(GameObject meshObject)
		{
			m_meshMaterial = Resources.Load<Material>("Materials/MeshMat");
			occlusionMaterial = Resources.Load<Material>("Materials/OcclusionMat");
			MeshRenderer[] componentsInChildren = meshObject.GetComponentsInChildren<MeshRenderer>();
			MeshRenderer[] array = componentsInChildren;
			foreach (MeshRenderer val in array)
			{
				Material[] sharedMaterials = ((Renderer)val).sharedMaterials;
				for (int j = 0; j < sharedMaterials.Length; j++)
				{
					if (meshVisualizationOption.Equals(MeshVisualizationOption.EnableOcclusion))
					{
						Material runtimeOcclusionSafeMaterial = GetLabelFriendlyMeshMaterial();
						if ((Object)(object)runtimeOcclusionSafeMaterial != (Object)null)
						{
							sharedMaterials[j] = runtimeOcclusionSafeMaterial;
						}
						else if ((Object)(object)occlusionMaterial != (Object)null)
						{
							sharedMaterials[j] = occlusionMaterial;
						}
					}
					else if ((Object)(object)m_meshMaterial != (Object)null)
					{
						sharedMaterials[j] = m_meshMaterial;
					}
				}
				((Renderer)val).sharedMaterials = sharedMaterials;
				ShaderProgressAnimator component = meshObject.GetComponent<ShaderProgressAnimator>();
				if ((Object)(object)component == (Object)null)
				{
					component = meshObject.AddComponent<ShaderProgressAnimator>();
				}
			}
		}

		private Material GetLabelFriendlyMeshMaterial()
		{
			if (!preserveLabelsOverMesh)
			{
				return null;
			}
			if ((Object)(object)m_labelFriendlyMeshMaterial != (Object)null)
			{
				return m_labelFriendlyMeshMaterial;
			}
			if ((Object)(object)m_meshMaterial == (Object)null)
			{
				return null;
			}
			m_labelFriendlyMeshMaterial = new Material(m_meshMaterial);
			((Object)m_labelFriendlyMeshMaterial).name = ((Object)m_meshMaterial).name + "_LabelFriendlyRuntime";
			m_labelFriendlyMeshMaterial.renderQueue = 3990;
			if (m_labelFriendlyMeshMaterial.HasProperty("_ZWrite"))
			{
				m_labelFriendlyMeshMaterial.SetFloat("_ZWrite", 0f);
			}
			if (m_labelFriendlyMeshMaterial.HasProperty("_SrcBlend"))
			{
				m_labelFriendlyMeshMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
			}
			if (m_labelFriendlyMeshMaterial.HasProperty("_DstBlend"))
			{
				m_labelFriendlyMeshMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			}
			m_labelFriendlyMeshMaterial.SetOverrideTag("RenderType", "Transparent");
			return m_labelFriendlyMeshMaterial;
		}

		internal void LocalizationSuccessCallback()
		{
			if (!meshVisualizationOption.Equals(MeshVisualizationOption.NoMesh))
			{
				m_localizeSuccess = true;
				if ((Object)(object)m_mesh != (Object)null)
				{
					m_mesh.SetActive(true);
				}
			}
		}
	}
}
