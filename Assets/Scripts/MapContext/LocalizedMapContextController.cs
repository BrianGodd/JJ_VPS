using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MultiSet;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
public class LocalizedMapPoiRecord
{
    public string sourceTitle;
    public string label;
    public Vector3 localPosition;
    public Vector3 localScale = Vector3.one;
    public float margin;
    public float angle1 = -30f;
    public float angle2 = 30f;
    public string keyword;
    public string details;
}

[Serializable]
public class LocalizedMapContextData
{
    public string mapId;
    public string mapName;
    public List<string> sourceTitles = new List<string>();
    public List<LocalizedMapPoiRecord> poiRecords = new List<LocalizedMapPoiRecord>();

    public string BuildPromptContext()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"mapName: {mapName}");

        foreach (LocalizedMapPoiRecord poiRecord in poiRecords)
        {
            builder.AppendLine($"- {poiRecord.label}: pos=({poiRecord.localPosition.x:F3},{poiRecord.localPosition.y:F3},{poiRecord.localPosition.z:F3}), keyword={poiRecord.keyword}, details={poiRecord.details}");
        }

        return builder.ToString();
    }
}

public class LocalizedMapContextController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private SingleFrameLocalizationManager localizationManager;
    [SerializeField] private Transform labelRootOverride;
    [SerializeField] private Camera facingCamera;

    [Header("Firebase")]
    [SerializeField] private string firebaseDatabaseUrl = FirebaseConfig.DatabaseUrl;
    [SerializeField] private bool autoLoadOnLocalization = true;
    [SerializeField] private bool logLoadedContext = true;

    [Header("Label UI")]
    [SerializeField] private Vector2 panelSize = new Vector2(220f, 64f);
    [SerializeField] private Vector3 panelScale = new Vector3(0.0025f, 0.0025f, 0.0025f);
    [SerializeField] private Vector3 panelOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.11f, 0.16f, 0.88f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float fontSize = 22f;
    [SerializeField] private int canvasSortingOrder = 5000;
    [SerializeField] private TMP_FontAsset labelFontAsset;
    [SerializeField] private bool renderLabelsOnTop = true;

    [Header("POI Visualization")]
    [SerializeField] private bool showPoiVisualization = true;
    [SerializeField] private bool showPoiFovGizmos = true;
    [SerializeField] private Color poiInsideCubeColor = new Color(0.15f, 0.75f, 1f, 0.2f);
    [SerializeField] private Color poiNearCubeColor = new Color(1f, 0.7f, 0.2f, 0.08f);
    [SerializeField] private Color poiFovGizmoColor = new Color(1f, 0.7f, 0.2f, 0.9f);
    [SerializeField] private float poiFovGizmoHeight = 0.05f;

    public LocalizedMapContextData CurrentContext { get; private set; }
    public Transform LabelParent => ResolveLabelParent();
    public SingleFrameLocalizationManager LocalizationManager => localizationManager;

    public event Action<LocalizedMapContextData> ContextLoaded;

    private GameObject m_labelRoot;
    private GameObject m_poiVisualizationRoot;
    private int m_loadVersion;
    private Material m_panelOverlayMaterial;
    private Material m_textOverlayMaterial;
    private Material m_poiInsideCubeMaterial;
    private Material m_poiNearCubeMaterial;
    private string m_currentLoadedMapKey;

    private void Awake()
    {
        if (localizationManager == null)
        {
            localizationManager = FindFirstObjectByType<SingleFrameLocalizationManager>();
        }
    }

    private void OnEnable()
    {
        if (localizationManager != null)
        {
            localizationManager.LocalizationCompleted += HandleLocalizationCompleted;
        }
    }

    private void OnDisable()
    {
        if (localizationManager != null)
        {
            localizationManager.LocalizationCompleted -= HandleLocalizationCompleted;
        }

        ResetLoadedContext();
        ClearRuntimeObjects();
    }

    private void OnDestroy()
    {
        ResetLoadedContext();
        ClearRuntimeObjects();
        ReleaseRuntimeMaterials();
    }

    public async void ReloadCurrentMapContext()
    {
        if (localizationManager == null || localizationManager.LatestLocalization == null || !localizationManager.LatestLocalization.success)
        {
            Debug.LogWarning("LocalizedMapContextController: no successful localization is available yet.");
            return;
        }

        await LoadContextForLocalizationAsync(localizationManager.LatestLocalization);
    }

    public string GetCurrentPromptContext()
    {
        return CurrentContext != null ? CurrentContext.BuildPromptContext() : string.Empty;
    }

    private async void HandleLocalizationCompleted(SingleFrameLocalizationManager.LocalizationSnapshot snapshot)
    {
        if (!autoLoadOnLocalization || snapshot == null || !snapshot.success)
        {
            return;
        }

        await LoadContextForLocalizationAsync(snapshot);
    }

    private async Task LoadContextForLocalizationAsync(SingleFrameLocalizationManager.LocalizationSnapshot snapshot)
    {
        int loadVersion = ++m_loadVersion;
        string mapId = snapshot.mapId;
        string mapName = snapshot.mapName;

        if (string.IsNullOrWhiteSpace(mapName) && !string.IsNullOrWhiteSpace(mapId))
        {
            mapName = await ResolveMapNameAsync(mapId);
        }

        if (loadVersion != m_loadVersion)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(mapName))
        {
            Debug.LogWarning("LocalizedMapContextController: could not resolve mapName from localization.");
            return;
        }

        string nextMapKey = BuildMapKey(mapId, mapName);
        if (string.IsNullOrWhiteSpace(nextMapKey))
        {
            Debug.LogWarning("LocalizedMapContextController: localization did not provide a usable map key.");
            return;
        }

        // Keep existing labels when relocalizing to the same map.
        if (!string.IsNullOrWhiteSpace(m_currentLoadedMapKey) &&
            string.Equals(m_currentLoadedMapKey, nextMapKey, StringComparison.Ordinal) &&
            m_labelRoot != null &&
            CurrentContext != null)
        {
            return;
        }

        LocalizedMapContextData context = await BuildContextForMapAsync(mapId, mapName);
        if (loadVersion != m_loadVersion)
        {
            return;
        }

        CurrentContext = context;
        m_currentLoadedMapKey = nextMapKey;
        SpawnLabels(context);

        if (logLoadedContext)
        {
            Debug.Log($"LocalizedMapContext loaded -> mapName={context.mapName}, mapId={context.mapId}, titles={context.sourceTitles.Count}, poiCount={context.poiRecords.Count}");
        }

        ContextLoaded?.Invoke(context);
    }

    private async Task<string> ResolveMapNameAsync(string mapId)
    {
        TaskCompletionSource<(bool success, string data)> completionSource = new TaskCompletionSource<(bool success, string data)>();
        MultiSetApiManager.GetMapDetails(mapId, (success, data, statusCode) =>
        {
            completionSource.TrySetResult((success, data));
        });

        (bool success, string data) result = await completionSource.Task;
        if (!result.success || string.IsNullOrWhiteSpace(result.data))
        {
            return null;
        }

        VpsMap vpsMap = JsonUtility.FromJson<VpsMap>(result.data);
        return vpsMap != null ? vpsMap.mapName : null;
    }

    private async Task<LocalizedMapContextData> BuildContextForMapAsync(string mapId, string mapName)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        LocalizedMapContextData context = new LocalizedMapContextData
        {
            mapId = mapId,
            mapName = mapName
        };

        List<string> titles = await GetSavedTitlesForMapAsync(mapName);
        context.sourceTitles.AddRange(titles);

        foreach (string title in titles)
        {
            List<LocalizedMapPoiRecord> poiRecords = await LoadPoisByTitleAsync(title);
            context.poiRecords.AddRange(poiRecords);
        }

        stopwatch.Stop();
        Debug.Log($"LocalizedMapContextController: BuildContextForMapAsync took {stopwatch.Elapsed.TotalSeconds:F3}s for mapName={mapName}, mapId={mapId}, titles={context.sourceTitles.Count}, poiCount={context.poiRecords.Count}");

        return context;
    }

    private async Task<List<string>> GetSavedTitlesForMapAsync(string mapName)
    {
        List<string> matchedTitles = new List<string>();
        string json = await GetFirebaseJsonAsync("/Marks.json");
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            return matchedTitles;
        }

        JObject root = JObject.Parse(json);
        foreach (JProperty child in root.Properties())
        {
            JObject entry = child.Value as JObject;
            if (entry == null)
            {
                continue;
            }

            string savedMapName = ExtractMapName(entry["map"]);
            if (string.Equals(savedMapName, mapName, StringComparison.Ordinal))
            {
                matchedTitles.Add(child.Name);
            }
        }

        return matchedTitles;
    }

    private async Task<List<LocalizedMapPoiRecord>> LoadPoisByTitleAsync(string title)
    {
        string safeTitle = UnityWebRequest.EscapeURL(title);
        string json = await GetFirebaseJsonAsync($"/Marks/{safeTitle}.json");

        List<LocalizedMapPoiRecord> result = new List<LocalizedMapPoiRecord>();
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            return result;
        }

        JObject root = JObject.Parse(json);
        foreach (JProperty child in root.Properties())
        {
            if (child.Name == "map")
            {
                continue;
            }

            JObject markObject = child.Value as JObject;
            if (markObject == null)
            {
                continue;
            }

            result.Add(new LocalizedMapPoiRecord
            {
                sourceTitle = title,
                label = child.Name,
                localPosition = ReadVector3(markObject["position"] as JObject),
                localScale = ReadVector3(markObject["scale"] as JObject, Vector3.one),
                margin = markObject["margin"] != null ? markObject["margin"].Value<float>() : 0f,
                angle1 = markObject["angle1"] != null ? markObject["angle1"].Value<float>() : -30f,
                angle2 = markObject["angle2"] != null ? markObject["angle2"].Value<float>() : 30f,
                keyword = markObject["keyword"] != null ? markObject["keyword"].Value<string>() : string.Empty,
                details = markObject["details"] != null ? markObject["details"].Value<string>() : string.Empty
            });
        }

        return result;
    }

    private async Task<string> GetFirebaseJsonAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(firebaseDatabaseUrl))
        {
            throw new InvalidOperationException("LocalizedMapContextController: firebaseDatabaseUrl is null or empty.");
        }

        string url = $"{firebaseDatabaseUrl.TrimEnd('/')}{path}";
        using UnityWebRequest request = UnityWebRequest.Get(url);
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new InvalidOperationException($"Firebase request failed: {request.error}");
        }

        return request.downloadHandler.text;
    }

    private void SpawnLabels(LocalizedMapContextData context)
    {
        ClearRuntimeObjects();

        Transform parent = ResolveLabelParent();
        m_labelRoot = new GameObject("LocalizedMapLabels");
        m_labelRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        m_labelRoot.transform.SetParent(parent, false);
        m_labelRoot.transform.localPosition = Vector3.zero;
        m_labelRoot.transform.localRotation = Quaternion.identity;
        m_labelRoot.transform.localScale = Vector3.one;
        m_labelRoot.transform.SetAsLastSibling();

        foreach (LocalizedMapPoiRecord poiRecord in context.poiRecords)
        {
            CreateLabel(poiRecord, m_labelRoot.transform);
        }

        SpawnPoiVisualization(context, parent);
    }

    private Transform ResolveLabelParent()
    {
        if (labelRootOverride != null)
        {
            return labelRootOverride;
        }

        if (localizationManager != null && localizationManager.MapSpace != null)
        {
            return localizationManager.MapSpace.transform;
        }

        return ((Component)this).transform;
    }

    private void ClearRuntimeObjects()
    {
        if (m_labelRoot != null)
        {
            Destroy(m_labelRoot);
            m_labelRoot = null;
        }

        if (m_poiVisualizationRoot != null)
        {
            Destroy(m_poiVisualizationRoot);
            m_poiVisualizationRoot = null;
        }
    }

    private void ResetLoadedContext()
    {
        CurrentContext = null;
        m_currentLoadedMapKey = null;
    }

    private void ReleaseRuntimeMaterials()
    {
        if (m_panelOverlayMaterial != null)
        {
            Destroy(m_panelOverlayMaterial);
            m_panelOverlayMaterial = null;
        }

        if (m_textOverlayMaterial != null)
        {
            Destroy(m_textOverlayMaterial);
            m_textOverlayMaterial = null;
        }

        if (m_poiInsideCubeMaterial != null)
        {
            Destroy(m_poiInsideCubeMaterial);
            m_poiInsideCubeMaterial = null;
        }

        if (m_poiNearCubeMaterial != null)
        {
            Destroy(m_poiNearCubeMaterial);
            m_poiNearCubeMaterial = null;
        }
    }

    private void SpawnPoiVisualization(LocalizedMapContextData context, Transform parent)
    {
        if (!showPoiVisualization || context == null || context.poiRecords == null || context.poiRecords.Count == 0)
        {
            return;
        }

        m_poiVisualizationRoot = new GameObject("LocalizedMapPoiVisualization");
        m_poiVisualizationRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        m_poiVisualizationRoot.transform.SetParent(parent, false);
        m_poiVisualizationRoot.transform.localPosition = Vector3.zero;
        m_poiVisualizationRoot.transform.localRotation = Quaternion.identity;
        m_poiVisualizationRoot.transform.localScale = Vector3.one;

        Material insideCubeMaterial = GetPoiCubeMaterial(isNearCube: false);
        Material nearCubeMaterial = GetPoiCubeMaterial(isNearCube: true);
        foreach (LocalizedMapPoiRecord poiRecord in context.poiRecords)
        {
            CreatePoiCube(poiRecord, m_poiVisualizationRoot.transform, nearCubeMaterial, includeMargin: true, $"POI_Near_{poiRecord.label}");
            CreatePoiCube(poiRecord, m_poiVisualizationRoot.transform, insideCubeMaterial, includeMargin: false, $"POI_Inside_{poiRecord.label}");
        }
    }

    private void CreatePoiCube(LocalizedMapPoiRecord poiRecord, Transform parent, Material cubeMaterial, bool includeMargin, string objectName)
    {
        if (poiRecord == null)
        {
            return;
        }

        GameObject cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeObject.name = objectName;
        cubeObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        cubeObject.transform.SetParent(parent, false);
        cubeObject.transform.localPosition = poiRecord.localPosition;
        cubeObject.transform.localRotation = Quaternion.identity;
        float marginExpansion = includeMargin ? Mathf.Max(0f, poiRecord.margin) * 2f : 0f;
        cubeObject.transform.localScale = new Vector3(
            Mathf.Max(0.02f, poiRecord.localScale.x + marginExpansion),
            Mathf.Max(0.02f, poiRecord.localScale.y),
            Mathf.Max(0.02f, poiRecord.localScale.z + marginExpansion));

        Collider cubeCollider = cubeObject.GetComponent<Collider>();
        if (cubeCollider != null)
        {
            Destroy(cubeCollider);
        }

        MeshRenderer meshRenderer = cubeObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null && cubeMaterial != null)
        {
            meshRenderer.sharedMaterial = cubeMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }

    private Material GetPoiCubeMaterial(bool isNearCube)
    {
        Material existingMaterial = isNearCube ? m_poiNearCubeMaterial : m_poiInsideCubeMaterial;
        if (existingMaterial != null)
        {
            existingMaterial.color = isNearCube ? poiNearCubeColor : poiInsideCubeColor;
            return existingMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogWarning("LocalizedMapContextController: could not find a shader for POI visualization.");
            return null;
        }

        Material createdMaterial = new Material(shader)
        {
            name = isNearCube ? "LocalizedMapPoiNearVisualizationMaterial" : "LocalizedMapPoiInsideVisualizationMaterial",
            color = isNearCube ? poiNearCubeColor : poiInsideCubeColor,
            renderQueue = 3000
        };
        SetupTransparentMaterial(createdMaterial);
        if (isNearCube)
        {
            m_poiNearCubeMaterial = createdMaterial;
        }
        else
        {
            m_poiInsideCubeMaterial = createdMaterial;
        }
        return createdMaterial;
    }

    private static void SetupTransparentMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
    }

    private void CreateLabel(LocalizedMapPoiRecord poiRecord, Transform parent)
    {
        GameObject panelObject = new GameObject($"POI_{poiRecord.label}", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image), typeof(WorldSpaceBillboard));
        panelObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        panelObject.transform.SetParent(parent, false);
        panelObject.transform.localPosition = poiRecord.localPosition + panelOffset;
        panelObject.transform.localRotation = Quaternion.identity;
        panelObject.transform.localScale = panelScale;
        panelObject.transform.SetAsLastSibling();
        panelObject.layer = LayerMask.NameToLayer("LabelItems");

        Canvas canvas = panelObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = canvasSortingOrder;
        canvas.worldCamera = facingCamera != null ? facingCamera : Camera.main;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.sizeDelta = panelSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;
        if (renderLabelsOnTop)
        {
            Material overlayMaterial = GetPanelOverlayMaterial();
            if (overlayMaterial != null)
            {
                panelImage.material = overlayMaterial;
            }
        }

        WorldSpaceBillboard billboard = panelObject.GetComponent<WorldSpaceBillboard>();
        billboard.SetTargetCamera(facingCamera != null ? facingCamera : Camera.main);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        textObject.transform.SetParent(panelObject.transform, false);
        textObject.layer = LayerMask.NameToLayer("LabelItems");

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = labelFontAsset;
        text.text = poiRecord.label;
        text.color = textColor;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        if (renderLabelsOnTop)
        {
            Material overlayMaterial = GetTextOverlayMaterial(text.fontSharedMaterial);
            if (overlayMaterial != null)
            {
                text.fontSharedMaterial = overlayMaterial;
            }
        }
    }

    private Material GetPanelOverlayMaterial()
    {
        if (m_panelOverlayMaterial != null)
        {
            return m_panelOverlayMaterial;
        }

        Shader shader = Shader.Find("JJ/WorldSpaceUiOverlay");
        if (shader == null)
        {
            Debug.LogWarning("LocalizedMapContextController: could not find shader 'JJ/WorldSpaceUiOverlay'. Labels will use the default UI material.");
            return null;
        }

        m_panelOverlayMaterial = new Material(shader)
        {
            name = "LocalizedMapLabelOverlayMaterial",
            renderQueue = 4000
        };
        return m_panelOverlayMaterial;
    }

    private Material GetTextOverlayMaterial(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return null;
        }

        if (m_textOverlayMaterial != null)
        {
            return m_textOverlayMaterial;
        }

        Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlayShader != null)
        {
            m_textOverlayMaterial = new Material(sourceMaterial)
            {
                shader = overlayShader,
                name = $"{sourceMaterial.name}_Overlay",
                renderQueue = 4000
            };
            return m_textOverlayMaterial;
        }

        m_textOverlayMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name}_OverlayFallback",
            renderQueue = 4000
        };

        if (m_textOverlayMaterial.HasProperty("_ZTest"))
        {
            m_textOverlayMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        }

        return m_textOverlayMaterial;
    }

    private void OnDrawGizmos()
    {
        if (!showPoiFovGizmos || CurrentContext == null || CurrentContext.poiRecords == null || CurrentContext.poiRecords.Count == 0)
        {
            return;
        }

        Transform parent = ResolveLabelParent();
        if (parent == null)
        {
            return;
        }

        Gizmos.color = poiFovGizmoColor;
        foreach (LocalizedMapPoiRecord poiRecord in CurrentContext.poiRecords)
        {
            DrawPoiFovGizmos(parent, poiRecord);
        }
    }

    private void DrawPoiFovGizmos(Transform parent, LocalizedMapPoiRecord poiRecord)
    {
        if (poiRecord == null)
        {
            return;
        }

        Vector3 origin = parent.TransformPoint(poiRecord.localPosition + Vector3.up * poiFovGizmoHeight);
        float halfExtent = Mathf.Max(poiRecord.localScale.x * 0.5f, poiRecord.localScale.z * 0.5f);
        float range = Mathf.Max(0.05f, halfExtent + Mathf.Max(0f, poiRecord.margin));
        float minAngle = Mathf.Min(poiRecord.angle1, poiRecord.angle2);
        float maxAngle = Mathf.Max(poiRecord.angle1, poiRecord.angle2);

        Vector3 leftDirection = parent.TransformDirection(DirectionFromAngle(minAngle));
        Vector3 rightDirection = parent.TransformDirection(DirectionFromAngle(maxAngle));
        Gizmos.DrawLine(origin, origin + leftDirection * range);
        Gizmos.DrawLine(origin, origin + rightDirection * range);

        const int segmentCount = 20;
        Vector3 previousPoint = origin + leftDirection * range;
        for (int i = 1; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float angle = Mathf.Lerp(minAngle, maxAngle, t);
            Vector3 nextPoint = origin + parent.TransformDirection(DirectionFromAngle(angle)) * range;
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

    private static Vector3 DirectionFromAngle(float angleDegrees)
    {
        return Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward;
    }

    private static Vector3 ReadVector3(JObject obj, Vector3? fallback = null)
    {
        Vector3 defaultValue = fallback ?? Vector3.zero;
        if (obj == null)
        {
            return defaultValue;
        }

        return new Vector3(
            obj["x"] != null ? obj["x"].Value<float>() : defaultValue.x,
            obj["y"] != null ? obj["y"].Value<float>() : defaultValue.y,
            obj["z"] != null ? obj["z"].Value<float>() : defaultValue.z);
    }

    private static string ExtractMapName(JToken token)
    {
        if (token == null)
        {
            return null;
        }

        if (token.Type == JTokenType.String)
        {
            return token.Value<string>();
        }

        JObject obj = token as JObject;
        if (obj == null)
        {
            return token.ToString();
        }

        string[] candidateKeys = { "name", "mapName", "title", "value" };
        foreach (string key in candidateKeys)
        {
            JToken value = obj[key];
            if (value != null && value.Type == JTokenType.String)
            {
                return value.Value<string>();
            }
        }

        foreach (JProperty property in obj.Properties())
        {
            if (property.Value.Type == JTokenType.String)
            {
                return property.Value.Value<string>();
            }
        }

        return obj.ToString();
    }

    private static string BuildMapKey(string mapId, string mapName)
    {
        if (!string.IsNullOrWhiteSpace(mapId))
        {
            return $"id:{mapId.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            return $"name:{mapName.Trim()}";
        }

        return null;
    }
}
