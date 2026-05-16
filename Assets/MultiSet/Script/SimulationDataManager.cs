using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class SimulationDataManager : MonoBehaviour
	{
		[HideInInspector]
		public bool isDownloading = false;

		[HideInInspector]
		public bool isDownloadingFile = false;

		[HideInInspector]
		public bool isExtractingFile = false;

		[HideInInspector]
		public bool fileAlreadyExists = false;

		[HideInInspector]
		public string alertMessage = "";

		[HideInInspector]
		public float alertTimer = 0f;

		[HideInInspector]
		public SimulationDataResponse simulationDataResponse;

		[HideInInspector]
		public int selectedSimulationIndex = -1;

		[HideInInspector]
		public string simulationDataDir = "SimulationData";

		private string baseDirectory;

		private string zipFileName;

		private string dataDirectory = null;

		private string m_savePath;

		private SimulationDataResult selectedSimulation;

		public static SimulationDataManager Instance { get; private set; }

		private void Awake()
		{
			if ((Object)(object)Instance == (Object)null)
			{
				Instance = this;
			}
		}

		public void GetSimulationData()
		{
			if (Application.isPlaying)
			{
				return;
			}
			isDownloading = true;
			MultisetSdkManager multisetSdkManager = Object.FindFirstObjectByType<MultisetSdkManager>();
			MultiSetConfig multiSetConfig = Resources.Load<MultiSetConfig>("MultiSetConfig");
			if ((Object)(object)multiSetConfig != (Object)null)
			{
				multisetSdkManager.clientId = multiSetConfig.clientId;
				multisetSdkManager.clientSecret = multiSetConfig.clientSecret;
				if (!string.IsNullOrWhiteSpace(multisetSdkManager.clientId) && !string.IsNullOrWhiteSpace(multisetSdkManager.clientSecret))
				{
					EventManager<EventData>.StartListening("AuthCallBack", OnAuthCallBack);
					multisetSdkManager.AuthenticateMultiSetSDK();
				}
				else
				{
					isDownloading = false;
					Debug.LogError((object)"Please enter valid credentials in MultiSetConfig!");
				}
			}
			else
			{
				isDownloading = false;
				Debug.LogError((object)"MultiSetConfig not found!");
			}
		}

		private void OnDestroy()
		{
			EventManager<EventData>.StopListening("AuthCallBack", OnAuthCallBack);
		}

		private void OnAuthCallBack(EventData eventData)
		{
			if (eventData.AuthSuccess)
			{
				GetSimulationDataList();
			}
			else
			{
				isDownloading = false;
				Debug.LogError((object)"Authentication failed!");
			}
			EventManager<EventData>.StopListening("AuthCallBack", OnAuthCallBack);
		}

		public void GetSimulationDataList()
		{
			selectedSimulation = null;
			baseDirectory = Path.Combine(Application.persistentDataPath, simulationDataDir);
			Directory.CreateDirectory(baseDirectory);
			MultiSetApiManager.GetSimulationData(SimulationDataCallback);
		}

		private void SimulationDataCallback(bool success, string data, long statusCode)
		{
			isDownloading = false;
			if (string.IsNullOrEmpty(data))
			{
				Debug.LogError((object)"Error : SimulationData Callback: Empty or null data received!");
			}
			else if (success)
			{
				simulationDataResponse = JsonUtility.FromJson<SimulationDataResponse>(data);
			}
			else
			{
				Debug.LogError((object)("Get SimulationData failed!" + data + " code: " + statusCode));
			}
		}

		public void DownloadSelectedSimulation()
		{
			selectedSimulation = null;
			fileAlreadyExists = false;
			alertMessage = "";
			alertTimer = 0f;
			if (simulationDataResponse == null || simulationDataResponse.simulationData == null || selectedSimulationIndex < 0 || selectedSimulationIndex >= simulationDataResponse.simulationData.Count)
			{
				Debug.LogError((object)"No simulation selected or invalid selection!");
				return;
			}
			selectedSimulation = simulationDataResponse.simulationData[selectedSimulationIndex];
			if (string.IsNullOrEmpty(baseDirectory))
			{
				baseDirectory = Path.Combine(Application.persistentDataPath, simulationDataDir);
			}
			if (!Directory.Exists(baseDirectory))
			{
				Directory.CreateDirectory(baseDirectory);
			}
			dataDirectory = Path.Combine(baseDirectory, selectedSimulation.simulationCode);
			if (Directory.Exists(dataDirectory))
			{
				string[] files = Directory.GetFiles(dataDirectory, "*.jpg");
				string[] files2 = Directory.GetFiles(dataDirectory, "*.json");
				if (files.Length != 0 && files2.Length != 0)
				{
					fileAlreadyExists = true;
					alertMessage = "Simulation data '" + selectedSimulation.name + "' already exists!";
					alertTimer = 2f;
					((MonoBehaviour)this).StartCoroutine(HideAlertAfterDelay());
					return;
				}
			}
			((MonoBehaviour)this).StartCoroutine(DownloadSimulationFile(selectedSimulation));
		}

		private IEnumerator HideAlertAfterDelay()
		{
			while (alertTimer > 0f)
			{
				alertTimer -= Time.deltaTime;
				yield return null;
			}
			fileAlreadyExists = false;
			alertMessage = "";
		}

		private IEnumerator DownloadSimulationFile(SimulationDataResult simulation)
		{
			if (!Application.isPlaying)
			{
				isDownloadingFile = true;
				MultiSetApiManager.GetFileUrl(simulation.s3Key, DataFileUrlCallbackEditor);
			}
			yield break;
		}

		private void DataFileUrlCallbackEditor(bool success, string data, long statusCode)
		{
			if (string.IsNullOrEmpty(data))
			{
				isDownloadingFile = false;
				Debug.LogError((object)"File URL Callback: Empty or null data received!");
			}
			else if (success)
			{
				FileData fileData = JsonUtility.FromJson<FileData>(data);
				zipFileName = selectedSimulation.simulationCode + ".zip";
				if (string.IsNullOrEmpty(baseDirectory))
				{
					baseDirectory = Path.Combine(Application.persistentDataPath, simulationDataDir);
				}
				if (!Directory.Exists(baseDirectory))
				{
					Directory.CreateDirectory(baseDirectory);
				}
				m_savePath = Path.Combine(baseDirectory, zipFileName);
				MultiSetHttpClient.DownloadFileAsync(fileData.url, delegate(byte[] array)
				{
					if (array != null)
					{
						try
						{
							File.WriteAllBytes(m_savePath, array);
							isDownloadingFile = false;
							isExtractingFile = true;
							if (File.Exists(m_savePath))
							{
								ExtractAndVerifySimulationData(m_savePath, selectedSimulation.simulationCode);
							}
							else
							{
								isExtractingFile = false;
								Debug.LogError((object)("File not found at path: " + m_savePath));
							}
							return;
						}
						catch (Exception ex)
						{
							isDownloadingFile = false;
							isExtractingFile = false;
							Debug.LogError((object)("Failed to save mesh file: " + ex.Message));
							return;
						}
					}
					isDownloadingFile = false;
					Debug.LogError((object)"Failed to download mesh file.");
				});
			}
			else
			{
				isDownloadingFile = false;
				isExtractingFile = false;
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				Debug.LogError((object)("Error : " + errorJSON.error));
			}
		}

		private void ExtractAndVerifySimulationData(string zipFilePath, string simulationCode)
		{
			try
			{
				string text = Path.Combine(baseDirectory, simulationCode);
				ZipFile.ExtractToDirectory(zipFilePath, text, overwriteFiles: true);
				ProcessSimulationData(text);
				File.Delete(zipFilePath);
				isExtractingFile = false;
			}
			catch (Exception ex)
			{
				isExtractingFile = false;
				Debug.LogError((object)("Error extracting zip file: " + ex.Message));
			}
		}

		private void ProcessSimulationData(string extractPath)
		{
			string[] files = Directory.GetFiles(extractPath, "*.jpg");
			string[] files2 = Directory.GetFiles(extractPath, "*.json");
			if (files.Length == 5 && files2.Length == 1)
			{
				Debug.Log((object)"Simulation data downloaded and verified successfully.");
			}
			else
			{
				Debug.LogError((object)"Invalid Simulation Data!! Try different Simulation dataset.");
			}
		}

		public List<string> GetSimulationNames()
		{
			List<string> list = new List<string>();
			if (simulationDataResponse != null && simulationDataResponse.simulationData != null)
			{
				if (string.IsNullOrEmpty(baseDirectory))
				{
					baseDirectory = Path.Combine(Application.persistentDataPath, simulationDataDir);
				}
				foreach (SimulationDataResult simulationDatum in simulationDataResponse.simulationData)
				{
					string path = Path.Combine(baseDirectory, simulationDatum.simulationCode);
					string text = ((Directory.Exists(path) && Directory.GetFiles(path, "*.jpg").Length != 0 && Directory.GetFiles(path, "*.json").Length != 0) ? " ✓" : "");
					list.Add(simulationDatum.name + " (" + simulationDatum.simulationCode + ")" + text);
				}
			}
			return list;
		}
	}
}
