using System;
using System.Collections.Generic;
using MultiSet;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JJ.Menu
{
    public class MultiSetMapSelectionController : MonoBehaviour
    {
        [Serializable]
        private class MenuMapInfo
        {
            public string id;
            public string mapCode;
            public string mapName;

            public string GetDisplayLabel()
            {
                return $"{mapName} ({mapCode})";
            }
        }

        [Header("Dependencies")]
        [SerializeField] private MultisetSdkManager multisetSdkManager;
        [SerializeField] private SelectedMapContext selectedMapContext;

        [Header("UI")]
        [SerializeField] private TMP_Dropdown mapDropdown;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text selectedMapText;

        [Header("Scene Routing")]
        [SerializeField] private string localizationSceneName = "MultiSet_JJVPS_RAG";
        [SerializeField] private bool autoLoadMapsAfterAuth = true;

        private readonly List<MenuMapInfo> m_maps = new List<MenuMapInfo>();
        private bool m_isLoading;

        private void Awake()
        {
            if (multisetSdkManager == null)
            {
                multisetSdkManager = FindFirstObjectByType<MultisetSdkManager>();
            }

            if (selectedMapContext == null)
            {
                selectedMapContext = SelectedMapContext.Instance;
            }

            if (selectedMapContext == null)
            {
                GameObject contextObject = new GameObject("SelectedMapContext");
                selectedMapContext = contextObject.AddComponent<SelectedMapContext>();
            }

            selectedMapContext = ResolveSelectedMapContext();
        }

        private void OnEnable()
        {
            EventManager<EventData>.StartListening("AuthCallBack", HandleAuthCallback);

            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(RefreshMaps);
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(StartLocalizationScene);
            }

            if (mapDropdown != null)
            {
                mapDropdown.onValueChanged.AddListener(HandleMapSelectionChanged);
            }
        }

        private void Start()
        {
            UpdateStatus("Waiting for MultiSet authentication...");
            SetStartButtonEnabled(false);

            if (HasStoredAccessToken())
            {
                if (autoLoadMapsAfterAuth)
                {
                    LoadMaps();
                }
            }
            else if (multisetSdkManager != null)
            {
                multisetSdkManager.AuthenticateMultiSetSDK();
            }
            else
            {
                UpdateStatus("MultisetSdkManager is missing.");
            }
        }

        private void OnDisable()
        {
            EventManager<EventData>.StopListening("AuthCallBack", HandleAuthCallback);

            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(RefreshMaps);
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(StartLocalizationScene);
            }

            if (mapDropdown != null)
            {
                mapDropdown.onValueChanged.RemoveListener(HandleMapSelectionChanged);
            }
        }

        public void RefreshMaps()
        {
            if (m_isLoading)
            {
                return;
            }

            if (!HasStoredAccessToken())
            {
                UpdateStatus("Authenticating with MultiSet...");
                multisetSdkManager?.AuthenticateMultiSetSDK();
                return;
            }

            LoadMaps();
        }

        public void StartLocalizationScene()
        {
            SyncCurrentDropdownSelection();

            if (selectedMapContext == null || !selectedMapContext.HasSelection)
            {
                UpdateStatus("Please select a map before starting.");
                return;
            }

            if (string.IsNullOrWhiteSpace(localizationSceneName))
            {
                UpdateStatus("Localization scene name is empty.");
                return;
            }

            SceneManager.LoadScene(localizationSceneName);
        }

        private async void LoadMaps()
        {
            m_isLoading = true;
            SetStartButtonEnabled(false);
            UpdateStatus("Loading MultiSet maps...");

            try
            {
                string accessTokenJson = PlayerPrefs.GetString("MultiSet.AccessToken");
                AccessToken accessToken = JsonUtility.FromJson<AccessToken>(accessTokenJson);
                if (accessToken == null || string.IsNullOrWhiteSpace(accessToken.token))
                {
                    throw new InvalidOperationException("Access token is missing. Authenticate first.");
                }

                m_maps.Clear();

                bool completed = false;
                bool success = false;
                string data = null;
                long statusCode = 0;

                MultiSetApiManager.ApiRequest(
                    Method.GET,
                    "/v1/vps/map?page=1&limit=100",
                    null,
                    (requestSuccess, requestData, requestStatusCode) =>
                    {
                        success = requestSuccess;
                        data = requestData;
                        statusCode = requestStatusCode;
                        completed = true;
                    },
                    authRequired: true);

                while (!completed)
                {
                    await System.Threading.Tasks.Task.Yield();
                }

                if (!success)
                {
                    throw new InvalidOperationException($"Failed to load maps. statusCode={statusCode}, data={data}");
                }

                ParseMapsResponse(data);
                PopulateDropdown();

                UpdateStatus($"Loaded {m_maps.Count} map(s).");
            }
            catch (Exception ex)
            {
                Debug.LogError("MultiSetMapSelectionController: " + ex.Message);
                UpdateStatus("Failed to load maps.");
            }
            finally
            {
                m_isLoading = false;
            }
        }

        private void ParseMapsResponse(string json)
        {
            m_maps.Clear();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return;
            }

            JObject root = JObject.Parse(json);
            JArray mapsArray = root["maps"] as JArray;
            if (mapsArray == null)
            {
                return;
            }

            foreach (JToken token in mapsArray)
            {
                string mapCode = token["mapCode"]?.Value<string>();
                string mapName = token["mapName"]?.Value<string>();
                string mapId = token["_id"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(mapCode))
                {
                    continue;
                }

                m_maps.Add(new MenuMapInfo
                {
                    id = mapId,
                    mapCode = mapCode,
                    mapName = string.IsNullOrWhiteSpace(mapName) ? mapCode : mapName
                });
            }
        }

        private void PopulateDropdown()
        {
            if (mapDropdown == null)
            {
                return;
            }

            mapDropdown.ClearOptions();

            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
            foreach (MenuMapInfo mapInfo in m_maps)
            {
                options.Add(new TMP_Dropdown.OptionData(mapInfo.GetDisplayLabel()));
            }

            mapDropdown.AddOptions(options);

            if (m_maps.Count > 0)
            {
                mapDropdown.SetValueWithoutNotify(0);
                mapDropdown.RefreshShownValue();
                HandleMapSelectionChanged(0);
            }
            else
            {
                ResolveSelectedMapContext()?.Clear();
                SetStartButtonEnabled(false);
                UpdateSelectedMapText(null);
            }
        }

        private void HandleMapSelectionChanged(int index)
        {
            selectedMapContext = ResolveSelectedMapContext();
            if (selectedMapContext == null || index < 0 || index >= m_maps.Count)
            {
                SetStartButtonEnabled(false);
                return;
            }

            MenuMapInfo selectedMap = m_maps[index];
            selectedMapContext.SetSelectedMap(selectedMap.id, selectedMap.mapCode, selectedMap.mapName, LocalizationType.Map);
            UpdateSelectedMapText(selectedMap);
            SetStartButtonEnabled(true);
        }

        private void SyncCurrentDropdownSelection()
        {
            if (mapDropdown == null)
            {
                return;
            }

            int resolvedIndex = ResolveCurrentDropdownIndex();
            HandleMapSelectionChanged(resolvedIndex);
        }

        private void HandleAuthCallback(EventData eventData)
        {
            if (eventData == null || !eventData.AuthSuccess)
            {
                UpdateStatus("MultiSet authentication failed.");
                return;
            }

            UpdateStatus("MultiSet authentication succeeded.");
            if (autoLoadMapsAfterAuth && !m_isLoading)
            {
                LoadMaps();
            }
        }

        private bool HasStoredAccessToken()
        {
            string accessTokenJson = PlayerPrefs.GetString("MultiSet.AccessToken");
            if (string.IsNullOrWhiteSpace(accessTokenJson))
            {
                return false;
            }

            AccessToken accessToken = JsonUtility.FromJson<AccessToken>(accessTokenJson);
            return accessToken != null && !string.IsNullOrWhiteSpace(accessToken.token);
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private void UpdateSelectedMapText(MenuMapInfo selectedMap)
        {
            if (selectedMapText == null)
            {
                return;
            }

            if (selectedMap == null)
            {
                selectedMapText.text = "Selected Map: None";
                return;
            }

            selectedMapText.text = $"Selected Map: {selectedMap.mapName}";
        }

        private void SetStartButtonEnabled(bool enabled)
        {
            if (startButton != null)
            {
                startButton.interactable = enabled;
            }
        }

        private SelectedMapContext ResolveSelectedMapContext()
        {
            if (SelectedMapContext.Instance != null)
            {
                return SelectedMapContext.Instance;
            }

            return selectedMapContext;
        }

        private int ResolveCurrentDropdownIndex()
        {
            if (mapDropdown == null || m_maps.Count == 0)
            {
                return 0;
            }

            TMP_Text captionText = mapDropdown.captionText;
            string shownLabel = captionText != null ? captionText.text : null;
            if (!string.IsNullOrWhiteSpace(shownLabel))
            {
                for (int i = 0; i < m_maps.Count; i++)
                {
                    if (string.Equals(m_maps[i].GetDisplayLabel(), shownLabel, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return Mathf.Clamp(mapDropdown.value, 0, m_maps.Count - 1);
        }
    }
}
