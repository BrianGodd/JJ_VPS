using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiSet
{
	public static class DraftMapHelper
	{
		private const string DraftMapsFileName = "DraftMapList.json";

		private const int MinMapNameLength = 3;

		public static string ValidateMapName(string mapName)
		{
			if (string.IsNullOrEmpty(mapName))
			{
				return "Please enter map name.";
			}
			if (mapName.Length < 3)
			{
				return $"MapName should be at-least {3} characters long!";
			}
			return null;
		}

		public static bool DeleteMapFromDraftList(string draftId)
		{
			string path = Path.Combine(Application.persistentDataPath, "DraftMapList.json");
			if (!File.Exists(path))
			{
				return false;
			}
			string text = Util.LoadFromFile("DraftMapList.json");
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			DraftMapList draftMapList = JsonUtility.FromJson<DraftMapList>(text);
			if (draftMapList == null || draftMapList.draftMaps == null)
			{
				return false;
			}
			DraftMap draftMap = draftMapList.draftMaps.FirstOrDefault((DraftMap c) => c.id == draftId);
			if (draftMap != null)
			{
				draftMapList.draftMaps.Remove(draftMap);
			}
			string a_FileContents = JsonUtility.ToJson((object)draftMapList, true);
			return Util.WriteToFile("DraftMapList.json", a_FileContents);
		}

		public static IEnumerator ReloadCurrentScene()
		{
			Scene activeScene = SceneManager.GetActiveScene();
			AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(activeScene.buildIndex);
			asyncLoad.allowSceneActivation = false;
			while (asyncLoad.progress < 0.9f)
			{
				yield return null;
			}
			asyncLoad.allowSceneActivation = true;
		}
	}
}
