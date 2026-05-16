using System;
using System.Collections;
using System.IO;
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
	public class ObjectMeshHandler : MonoBehaviour
	{
		[Tooltip("Select option for the map mesh visualization.")]
		[Header("Map Mesh Settings")]
		[SerializeField]
		private MeshVisualizationOption meshVisualizationOption = MeshVisualizationOption.EnableVisualization;

		private Material outlineMaterial;

		private Material occlusionMaterial;

		private GameObject m_mapSpace;

		[HideInInspector]
		public GameObject m_mesh;

		private ModelSet modelSet;

		private string m_savePath;

		private bool localizeSuccess = false;

		private string meshId = string.Empty;

		public static ObjectMeshHandler Instance { get; private set; }

		private void Awake()
		{
			if ((Object)(object)Instance == (Object)null)
			{
				Instance = this;
			}
		}

		internal void DownloadObjectGlbFile(ModelSet _modelSet, GameObject mapSpace)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Expected O, but got Unknown
			m_mapSpace = mapSpace;
			modelSet = _modelSet;
			meshId = modelSet._id;
			string meshLink = modelSet.objectMesh.meshLink;
			if ((Object)(object)m_mapSpace == (Object)null)
			{
				m_mapSpace = new GameObject("MapSpace");
			}
			string path = meshId + "_textured.glb";
			m_savePath = Path.Combine(Application.persistentDataPath, path);
			if (File.Exists(m_savePath))
			{
				LoadMeshInSceneAsync(m_savePath);
			}
			else
			{
				MultiSetApiManager.GetFileUrl(meshLink, FileUrlCallback);
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
				Debug.LogError((object)("Error : " + JsonUtility.ToJson((object)errorJSON)));
			}
		}

		private IEnumerator DownloadMeshCoroutine(string url, string savePath)
		{
			bool downloadSuccess = false;
			yield return MultiSetHttpClient.DownloadFileCoroutine(url, savePath, delegate(bool success, string errorMessage)
			{
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
			if (await ((GltfImportBase)gltf).LoadGltfBinary(data, new Uri(filePath), (ImportSettings)null, default(CancellationToken)))
			{
				if ((Object)(object)GameObject.Find(meshId) != (Object)null)
				{
					Debug.LogWarning((object)"Object Mesh already exists in the hierarchy.");
					return;
				}
				m_mesh = new GameObject(meshId);
				await ((GltfImportBase)gltf).InstantiateMainSceneAsync(m_mesh.transform, default(CancellationToken));
				m_mesh.transform.SetParent(m_mapSpace.transform, false);
				m_mesh.transform.localPosition = Vector3.zero;
				m_mesh.transform.localRotation = Quaternion.identity;
				ApplyTransparentMaterial(m_mesh);
				if ((Object)(object)m_mesh != (Object)null)
				{
					if (localizeSuccess)
					{
						m_mesh.SetActive(true);
					}
					else
					{
						m_mesh.SetActive(false);
					}
				}
			}
			else
			{
				Debug.LogError((object)"Failed to load GLTF binary.");
			}
		}

		private void ApplyTransparentMaterial(GameObject meshObject)
		{
			outlineMaterial = Resources.Load<Material>("Materials/OutlineMat");
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
						sharedMaterials[j] = occlusionMaterial;
					}
					else
					{
						sharedMaterials[j] = outlineMaterial;
					}
				}
				((Renderer)val).sharedMaterials = sharedMaterials;
			}
		}

		internal void TrackingSuccessCallback()
		{
			localizeSuccess = true;
			if ((Object)(object)m_mesh != (Object)null)
			{
				GameObject mesh = m_mesh;
				if (mesh != null)
				{
					mesh.SetActive(true);
				}
			}
		}

		public void ResetLocalization()
		{
			localizeSuccess = false;
			if ((Object)(object)m_mesh != (Object)null)
			{
				m_mesh.SetActive(false);
			}
		}
	}
}
