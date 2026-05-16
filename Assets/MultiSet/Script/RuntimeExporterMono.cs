using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using GLTFast.Export;
using GLTFast.Logging;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class RuntimeExporterMono : MonoBehaviour
	{
		public GameObject meshManager;

		public XROrigin origin;

		public string meshFileName = "Mesh.glb";

		[HideInInspector]
		public string m_meshSubDir = null;

		public void StartMeshing(string m_meshSubDir)
		{
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003e: Expected O, but got Unknown
			this.m_meshSubDir = m_meshSubDir;
			if ((Object)(object)origin.TrackablesParent != (Object)null)
			{
				foreach (Transform item in origin.TrackablesParent)
				{
					Transform val = item;
					Object.Destroy((Object)(object)((Component)val).gameObject);
				}
			}
			meshManager.SetActive(true);
		}

		public async Task ExportAsync()
		{
			meshManager.SetActive(false);
			await AdvancedExport();
		}

		private async Task AdvancedExport()
		{
			CollectingLogger logger = new CollectingLogger();
			ExportSettings exportSettings = new ExportSettings
			{
				Format = (GltfFormat)1,
				FileConflictResolution = (FileConflictResolution)1,
				ComponentMask = (ComponentType)(-13),
				LightIntensityFactor = 100f
			};
			GameObjectExportSettings gameObjectExportSettings = new GameObjectExportSettings
			{
				OnlyActiveInHierarchy = false,
				DisabledComponents = true
			};
			GameObjectExport export = new GameObjectExport(exportSettings, gameObjectExportSettings, (IMaterialExport)null, (IDeferAgent)null, (ICodeLogger)(object)logger);
			List<GameObject> rootLevelNodes = new List<GameObject>();
			if ((Object)(object)origin != (Object)null && (Object)(object)origin.TrackablesParent != (Object)null)
			{
				rootLevelNodes = GetChildwithMesh(((Component)origin.TrackablesParent).gameObject);
			}
			string fullFileName = Path.Combine(Application.persistentDataPath, "MappingData/" + m_meshSubDir + "/Mesh", meshFileName);
			export.AddScene(rootLevelNodes.ToArray(), "MultiSet_Mesh");
			if (!(await export.SaveToFileAndDispose(fullFileName, default(CancellationToken))))
			{
				Debug.LogError((object)"Something went wrong exporting a glTF");
				logger.LogAll();
			}
			await Task.CompletedTask;
		}

		private List<GameObject> GetChildwithMesh(GameObject obj)
		{
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Expected O, but got Unknown
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)null == (Object)(object)obj)
			{
				return null;
			}
			List<GameObject> list = new List<GameObject>();
			int num = 0;
			foreach (Transform item in obj.transform)
			{
				Transform val = item;
				if ((Object)null == (Object)(object)val || !((Object)(object)((Component)val).GetComponent<MeshFilter>() != (Object)null))
				{
					continue;
				}
				num++;
				Mesh.MeshDataArray val2 = Mesh.AcquireReadOnlyMeshData(((Component)val).GetComponent<MeshFilter>().mesh);
				if (val2.Length > 0)
				{
					Mesh.MeshData val3 = val2[0];
					if (val3.vertexCount > 0)
					{
						list.Add(((Component)val).gameObject);
					}
				}
			}
			return list;
		}
	}
}
