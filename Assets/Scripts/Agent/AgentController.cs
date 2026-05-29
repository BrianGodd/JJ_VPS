using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MultiSet;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AgentController : MonoBehaviour
{
    private enum PlayerSituation
    {
        None,
        Near,
        Inside
    }

    private enum AgentRequestKind
    {
        Manual,
        Passive
    }

    [Header("References")]
    [SerializeField] private LocalizedMapContextController mapContextController;
    [SerializeField] private SingleFrameLocalizationManager localizationManager;
    [SerializeField] private Camera userCamera;
    [SerializeField] private Transform userTransformOverride;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TMP_Text simpleMSGText;
    [SerializeField] private TMP_Text nearestOutputText;
    [SerializeField] private TMP_Text directionOutputText;
    [SerializeField] private TMP_Text insideOutputText;
    [SerializeField] private TMP_Text mapNameText;
    [SerializeField] private TMP_Text agentResponseText;
    [SerializeField] private TMP_Text playerMapPoseText;
    [SerializeField] private AudioSource processingAudioSource;

    [Header("Agent Settings")]
    public AgentApiSettings apiSettings = new AgentApiSettings();

    [Header("Events")]
    public UnityEvent OnResponseReady = new UnityEvent();

    [Header("Passive Guidance")]
    [SerializeField] private bool enablePassiveGuidance = true;
    [SerializeField] private Scrollbar passiveGuidanceScrollbar;
    [SerializeField] private float stableSeconds = 3f;
    [SerializeField] private float cooldownSeconds = 10f;
    [SerializeField] private bool preferPoiTriggerSettings = true;
    [SerializeField] private float maxPoiDistanceMeters = 8f;
    [SerializeField] private float facingAngleThreshold = 80f;
    [SerializeField] private int maxContextPoiCount = 12;

    [Header("Debug")]
    [Tooltip("When true, pressing Space will trigger a test question using the current scene context.")]
    [SerializeField] private bool enableSpaceTest = false;
    [SerializeField] private string testMessage = "Where am I now, and what should I pay attention to nearby?";

    private float m_stableTimer;
    private float m_cooldownTimer;
    private string m_previousNearestLabel;
    private string m_lastHandledPassiveLabel;
    private bool m_isBusy;
    private PlayerSituation m_previousSituation = PlayerSituation.None;
    private PlayerSituation m_currentSituation = PlayerSituation.None;
    private string m_currentStatusMessage = "No mark nearby.";

    private IAgentTextService m_textService;
    private IAgentSpeechService m_speechService;
    private Coroutine m_activeRequestCoroutine;
    private AgentRequestKind? m_activeRequestKind;
    private string m_activePassiveLabel;
    private bool m_pendingPassiveCooldownAfterManual;
    private string m_pendingPassiveCooldownLabel;

    private struct PoiObservation
    {
        public LocalizedMapPoiRecord poiRecord;
        public Vector3 worldPosition;
        public float distanceMeters;
        public float signedAngleDegrees;
        public float detectionAngleDegrees;
    }

    public bool IsBusy => m_isBusy;

    private void Start()
    {
        if (mapContextController == null)
        {
            mapContextController = FindFirstObjectByType<LocalizedMapContextController>();
        }

        if (localizationManager == null)
        {
            localizationManager = FindFirstObjectByType<SingleFrameLocalizationManager>();
        }

        if (userCamera == null)
        {
            userCamera = Camera.main;
        }

        if (apiSettings == null)
        {
            apiSettings = new AgentApiSettings();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        InitializeServices();
        BindPassiveGuidanceScrollbar();
        RefreshMapNameUi();
    }

    private void OnDestroy()
    {
        UnbindPassiveGuidanceScrollbar();
    }

    private void Update()
    {
        RefreshMapNameUi();
        RefreshPlayerMapPoseUi();

        if (enableSpaceTest && Input.GetKeyDown(KeyCode.Space))
        {
            AskQuestion(testMessage);
        }

        if (m_cooldownTimer > 0f)
        {
            m_cooldownTimer -= Time.deltaTime;
        }

        if (!enablePassiveGuidance || m_isBusy)
        {
            return;
        }

        UpdatePassiveGuidance();
    }

    public void AskQuestion(string userQuestion)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            Debug.LogWarning("AgentController: user question was empty.");
            return;
        }

        string prompt = BuildManualQuestionPrompt(userQuestion);
        StartAgentRequest(prompt, AgentRequestKind.Manual);
    }

    public void SetPassiveGuidanceEnabledFromScrollbar(float value)
    {
        bool shouldEnable = Mathf.Approximately(value, 1f);
        SetPassiveGuidanceEnabled(shouldEnable);
    }

    public void SetPassiveGuidanceEnabled(bool enabled)
    {
        if (enablePassiveGuidance == enabled)
        {
            return;
        }

        enablePassiveGuidance = enabled;
        if (!enablePassiveGuidance)
        {
            ResetPassiveTracking();
            m_cooldownTimer = 0f;
            if (m_activeRequestKind == AgentRequestKind.Passive)
            {
                InterruptCurrentRequest(clearPendingPassiveCooldown: true);
            }
        }
        else
        {
            ResetPassiveTracking();
            m_cooldownTimer = 0f;
        }
    }

    public void StartProcessingSound()
    {
        if (processingAudioSource == null)
        {
            return;
        }

        processingAudioSource.loop = true;
        processingAudioSource.Play();
    }

    public void StopProcessingSound()
    {
        if (processingAudioSource != null)
        {
            processingAudioSource.Stop();
        }
    }

    private void UpdatePassiveGuidance()
    {
        if (!TryEvaluatePlayerStatus(out PoiObservation nearestObservation, out string directionText, out string statusMessage, out PlayerSituation currentSituation))
        {
            ResetPassiveTracking();
            return;
        }

        if (currentSituation == PlayerSituation.None)
        {
            ResetPassiveTracking();
            return;
        }

        string currentLabel = nearestObservation.poiRecord.label;
        bool labelChanged = !string.Equals(currentLabel, m_previousNearestLabel, StringComparison.Ordinal);
        bool situationChanged = currentSituation != m_previousSituation;
        if (labelChanged || situationChanged)
        {
            m_previousNearestLabel = currentLabel;
            m_previousSituation = currentSituation;
            m_lastHandledPassiveLabel = null;
            m_stableTimer = 0f;
            return;
        }

        m_stableTimer += Time.deltaTime;
        if (m_stableTimer < stableSeconds || m_cooldownTimer > 0f)
        {
            return;
        }

        if (string.Equals(m_lastHandledPassiveLabel, currentLabel, StringComparison.Ordinal))
        {
            return;
        }

        string prompt = BuildPassiveGuidancePrompt(statusMessage, nearestObservation, directionText);
        StartAgentRequest(prompt, AgentRequestKind.Passive, currentLabel);
    }

    private void ResetPassiveTracking()
    {
        m_stableTimer = 0f;
        m_previousNearestLabel = null;
        m_lastHandledPassiveLabel = null;
        m_previousSituation = PlayerSituation.None;
    }

    private void InitializeServices()
    {
        m_textService = new OpenAITextService(apiSettings);
        m_speechService = new FallbackSpeechService(
            new LocalSpeechService(),
            new OpenAITtsSpeechService(apiSettings));
    }

    private string BuildManualQuestionPrompt(string userQuestion)
    {
        return BuildPrompt(
            userQuestion,
            null,
            null,
            apiSettings.languageCode,
            includeSceneContext: true,
            extraInstruction: "Answer the user's question based on the current player status and all visible POI relations in the scene. Do not assume there is only one target unless the user explicitly mentions one.",
            sceneContextPoiCountOverride: int.MaxValue);
    }

    private string BuildPassiveGuidancePrompt(string statusMessage, PoiObservation nearestObservation, string directionText)
    {
        return BuildPrompt(
            statusMessage,
            nearestObservation.poiRecord != null ? nearestObservation.poiRecord.label : null,
            nearestObservation.poiRecord,
            apiSettings.languageCode,
            includeSceneContext: true,
            extraInstruction: $"Current target relation: distance={FormatDistanceForPrompt(nearestObservation.distanceMeters)}, direction={Sanitize(directionText)}.",
            sceneContextPoiCountOverride: maxContextPoiCount);
    }

    private string BuildPrompt(string userMsg, string label, LocalizedMapPoiRecord poiRecord, string lang, bool includeSceneContext, string extraInstruction = null, int? sceneContextPoiCountOverride = null)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"You are a helpful guide. The user message: '{Sanitize(userMsg)}'.");

        if (!string.IsNullOrEmpty(lang))
        {
            builder.AppendLine($"Preferred language code: {Sanitize(lang)}.");
        }

        if (!string.IsNullOrEmpty(label))
        {
            builder.AppendLine($"Target Place: {Sanitize(label)}.");
            if (poiRecord != null)
            {
                if (!string.IsNullOrEmpty(poiRecord.keyword))
                {
                    builder.AppendLine($"Keywords: {Sanitize(poiRecord.keyword)}.");
                }

                if (!string.IsNullOrEmpty(poiRecord.details))
                {
                    builder.AppendLine($"Details: {Sanitize(poiRecord.details)}.");
                }
            }
        }

        if (includeSceneContext)
        {
            AppendSceneContext(builder, sceneContextPoiCountOverride ?? maxContextPoiCount);
        }

        if (!string.IsNullOrWhiteSpace(extraInstruction))
        {
            builder.AppendLine(extraInstruction);
        }

        builder.AppendLine("Provide a guidance for user to know how to get to this place, and short introduce about the place. Keep it under 50 words. Be concise and friendly.");
        builder.AppendLine("Return only the text of the introduction (no extra metadata or quotes).");
        return builder.ToString();
    }

    private void AppendSceneContext(StringBuilder builder, int maxPoiCount)
    {
        builder.AppendLine($"Player status: {Sanitize(m_currentStatusMessage)}");

        if (mapContextController != null && mapContextController.CurrentContext != null)
        {
            builder.AppendLine($"Localized map: {Sanitize(mapContextController.CurrentContext.mapName)} (mapId={Sanitize(mapContextController.CurrentContext.mapId)})");
        }
        else
        {
            builder.AppendLine("Localized map: unavailable.");
        }

        Transform userTransform = ResolveUserTransform();
        if (userTransform != null)
        {
            Vector3 userWorldPosition = userTransform.position;
            builder.AppendLine($"User world position: ({userWorldPosition.x:F2}, {userWorldPosition.y:F2}, {userWorldPosition.z:F2})");

            if (localizationManager != null && localizationManager.MapSpace != null)
            {
                Vector3 userLocalPosition = localizationManager.MapSpace.transform.InverseTransformPoint(userWorldPosition);
                builder.AppendLine($"User map-local position: ({userLocalPosition.x:F2}, {userLocalPosition.y:F2}, {userLocalPosition.z:F2})");
            }

            Vector3 forward = Vector3.ProjectOnPlane(userTransform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f)
            {
                float yaw = Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
                builder.AppendLine($"User facing yaw: {yaw:F1} degrees");
            }
        }

        List<PoiObservation> observations = BuildSortedObservations();
        if (observations.Count == 0)
        {
            builder.AppendLine("Nearby POIs: unavailable.");
            return;
        }

        builder.AppendLine("POI relations from user:");
        int count = Mathf.Min(maxPoiCount, observations.Count);
        for (int i = 0; i < count; i++)
        {
            PoiObservation observation = observations[i];
            builder.Append("- ");
            builder.Append(Sanitize(observation.poiRecord.label));
            builder.Append(": distance=");
            builder.Append(FormatDistanceForPrompt(observation.distanceMeters));
            builder.Append(", direction=");
            builder.Append(DescribeDirection(observation.signedAngleDegrees));

            if (!string.IsNullOrWhiteSpace(observation.poiRecord.keyword))
            {
                builder.Append(", keyword=");
                builder.Append(Sanitize(observation.poiRecord.keyword));
            }

            if (!string.IsNullOrWhiteSpace(observation.poiRecord.details))
            {
                builder.Append(", details=");
                builder.Append(Sanitize(observation.poiRecord.details));
            }

            builder.AppendLine();
        }
    }

    private List<PoiObservation> BuildSortedObservations()
    {
        List<PoiObservation> observations = new List<PoiObservation>();
        if (mapContextController == null || mapContextController.CurrentContext == null)
        {
            return observations;
        }

        Transform userTransform = ResolveUserTransform();
        Transform labelParent = mapContextController.LabelParent;
        if (userTransform == null || labelParent == null)
        {
            return observations;
        }

        Vector3 userForward = Vector3.ProjectOnPlane(userTransform.forward, Vector3.up).normalized;
        if (userForward.sqrMagnitude < 0.0001f)
        {
            userForward = userTransform.forward.normalized;
        }

        foreach (LocalizedMapPoiRecord poiRecord in mapContextController.CurrentContext.poiRecords)
        {
            Vector3 worldPosition = labelParent.TransformPoint(poiRecord.localPosition);
            Vector3 toPoi = worldPosition - userTransform.position;
            Vector2 worldPlaneOffset = new Vector2(
                worldPosition.x - userTransform.position.x,
                worldPosition.z - userTransform.position.z);
            Vector3 flatToPoi = Vector3.ProjectOnPlane(toPoi, Vector3.up);
            float angle = flatToPoi.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(userForward, flatToPoi.normalized, Vector3.up)
                : 0f;
            Vector3 fromPoiToUser = userTransform.position - worldPosition;
            Vector3 flatFromPoiToUser = Vector3.ProjectOnPlane(fromPoiToUser, Vector3.up);
            Vector3 poiForward = Vector3.ProjectOnPlane(labelParent.TransformDirection(Vector3.forward), Vector3.up);
            if (poiForward.sqrMagnitude < 0.0001f)
            {
                poiForward = Vector3.forward;
            }
            float detectionAngle = flatFromPoiToUser.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(poiForward.normalized, flatFromPoiToUser.normalized, Vector3.up)
                : 0f;

            observations.Add(new PoiObservation
            {
                poiRecord = poiRecord,
                worldPosition = worldPosition,
                distanceMeters = worldPlaneOffset.magnitude,
                signedAngleDegrees = angle,
                detectionAngleDegrees = detectionAngle
            });
        }

        observations.Sort((left, right) => left.distanceMeters.CompareTo(right.distanceMeters));
        return observations;
    }

    private bool TryGetNearestObservation(out PoiObservation nearestObservation)
    {
        List<PoiObservation> observations = BuildSortedObservations();
        if (observations.Count > 0)
        {
            nearestObservation = observations[0];
            return true;
        }

        nearestObservation = default;
        return false;
    }

    private bool TryEvaluatePlayerStatus(out PoiObservation nearestObservation, out string directionText, out string statusMessage, out PlayerSituation situation)
    {
        directionText = "null";
        statusMessage = "No mark nearby.";
        situation = PlayerSituation.None;

        List<PoiObservation> observations = BuildSortedObservations();
        if (observations.Count == 0)
        {
            UpdateStatusUi("null", "null", "null", statusMessage, PlayerSituation.None);
            nearestObservation = default;
            return false;
        }

        bool foundCandidate = false;
        nearestObservation = default;
        float nearestDistance = float.MaxValue;

        foreach (PoiObservation observation in observations)
        {
            bool isInside = IsInsidePoi(observation.poiRecord);
            bool isNear = isInside || IsInsidePoiTriggerArea(observation);
            if (!isNear)
            {
                continue;
            }

            if (observation.distanceMeters < nearestDistance)
            {
                nearestDistance = observation.distanceMeters;
                nearestObservation = observation;
                foundCandidate = true;
                situation = isInside ? PlayerSituation.Inside : PlayerSituation.Near;
            }
        }

        if (!foundCandidate)
        {
            UpdateStatusUi("null", "null", "null", statusMessage, PlayerSituation.None);
            return false;
        }

        directionText = GetRelatedDirection(nearestObservation.signedAngleDegrees);
        string insideText = situation == PlayerSituation.Inside ? "Inside" : "Outside";

        if (situation == PlayerSituation.Inside)
        {
            statusMessage = $"The user is right inside the {nearestObservation.poiRecord.label}.";
        }
        else
        {
            statusMessage = $"The user is now near {nearestObservation.poiRecord.label}, the {nearestObservation.poiRecord.label} is {directionText} of the user.";
        }

        UpdateStatusUi(nearestObservation.poiRecord.label, directionText, insideText, statusMessage, situation);
        return true;
    }

    private Transform ResolveUserTransform()
    {
        if (userTransformOverride != null)
        {
            return userTransformOverride;
        }

        if (userCamera != null)
        {
            return userCamera.transform;
        }

        return null;
    }

    private static string DescribeDirection(float signedAngleDegrees)
    {
        float absAngle = Mathf.Abs(signedAngleDegrees);
        if (absAngle <= 20f)
        {
            return "ahead";
        }

        if (absAngle >= 160f)
        {
            return "behind";
        }

        return signedAngleDegrees < 0f ? "left" : "right";
    }

    private string GetRelatedDirection(float signedAngleDegrees)
    {
        float rel = signedAngleDegrees;
        if (rel < 0f)
        {
            rel += 360f;
        }

        if (InSector(rel, 337.5f, 360f) || InSector(rel, 0f, 22.5f)) return "Forward";
        if (InSector(rel, 22.5f, 67.5f)) return "Forward-Right";
        if (InSector(rel, 67.5f, 112.5f)) return "Right";
        if (InSector(rel, 112.5f, 157.5f)) return "Backward-Right";
        if (InSector(rel, 157.5f, 202.5f)) return "Backward";
        if (InSector(rel, 202.5f, 247.5f)) return "Backward-Left";
        if (InSector(rel, 247.5f, 292.5f)) return "Left";
        if (InSector(rel, 292.5f, 337.5f)) return "Forward-Left";
        return "Forward";
    }

    private static bool InSector(float angle, float minAngle, float maxAngle)
    {
        return angle >= minAngle && angle < maxAngle;
    }

    private float ResolveDistanceLimit(LocalizedMapPoiRecord poiRecord)
    {
        if (preferPoiTriggerSettings && poiRecord != null && poiRecord.margin > 0f)
        {
            return poiRecord.margin;
        }

        return maxPoiDistanceMeters;
    }

    private bool IsWithinFacingWindow(PoiObservation observation)
    {
        if (!preferPoiTriggerSettings || observation.poiRecord == null)
        {
            return Mathf.Abs(observation.detectionAngleDegrees) <= facingAngleThreshold;
        }

        float minAngle = observation.poiRecord.angle1;
        float maxAngle = observation.poiRecord.angle2;
        if (minAngle > maxAngle)
        {
            (minAngle, maxAngle) = (maxAngle, minAngle);
        }

        bool hasCustomRange = !Mathf.Approximately(minAngle, 0f) || !Mathf.Approximately(maxAngle, 0f);
        if (!hasCustomRange)
        {
            return Mathf.Abs(observation.detectionAngleDegrees) <= facingAngleThreshold;
        }

        return observation.detectionAngleDegrees >= minAngle && observation.detectionAngleDegrees <= maxAngle;
    }

    private bool IsInsidePoi(LocalizedMapPoiRecord poiRecord)
    {
        return IsWithinPoiBounds(poiRecord, 0f);
    }

    private bool IsInsidePoiTriggerArea(PoiObservation observation)
    {
        if (observation.poiRecord == null)
        {
            return false;
        }

        if (IsInsidePoi(observation.poiRecord))
        {
            return false;
        }

        float expansion = ResolveDistanceLimit(observation.poiRecord);
        return IsWithinPoiBounds(observation.poiRecord, expansion) && IsWithinFacingWindow(observation);
    }

    private bool IsWithinPoiBounds(LocalizedMapPoiRecord poiRecord, float marginExpansion)
    {
        if (poiRecord == null || localizationManager == null || localizationManager.MapSpace == null)
        {
            return false;
        }

        Transform userTransform = ResolveUserTransform();
        if (userTransform == null)
        {
            return false;
        }

        Vector3 userLocal = localizationManager.MapSpace.transform.InverseTransformPoint(userTransform.position);
        Vector3 center = poiRecord.localPosition;
        float expansion = Mathf.Max(0f, marginExpansion);
        float halfX = Mathf.Max(0.01f, poiRecord.localScale.x * 0.5f) + expansion;
        float halfZ = Mathf.Max(0.01f, poiRecord.localScale.z * 0.5f) + expansion;

        return Mathf.Abs(userLocal.x - center.x) <= halfX + 1e-6f &&
               Mathf.Abs(userLocal.z - center.z) <= halfZ + 1e-6f;
    }

    private void UpdateStatusUi(string nearestLabel, string directionText, string insideText, string statusMessage, PlayerSituation situation)
    {
        if (nearestOutputText != null)
        {
            nearestOutputText.text = nearestLabel;
        }

        if (directionOutputText != null)
        {
            directionOutputText.text = directionText;
        }

        if (insideOutputText != null)
        {
            insideOutputText.text = insideText;
        }

        if (simpleMSGText != null)
        {
            simpleMSGText.text = statusMessage;
        }

        m_currentSituation = situation;
        m_currentStatusMessage = statusMessage;
    }

    private void RefreshMapNameUi()
    {
        if (mapNameText == null)
        {
            return;
        }

        string mapName = null;
        if (mapContextController != null && mapContextController.CurrentContext != null)
        {
            mapName = mapContextController.CurrentContext.mapName;
        }

        mapNameText.text = string.IsNullOrWhiteSpace(mapName) ? "Map" : $"{mapName}";
    }

    private static string Sanitize(string value)
    {
        return (value ?? string.Empty).Replace("\n", " ").Replace("\r", " ").Replace("\"", "'");
    }

    private static string FormatDistanceForPrompt(float distanceMeters)
    {
        if (distanceMeters < 1f)
        {
            return "less than 1 meter";
        }

        int roundedMeters = Mathf.RoundToInt(distanceMeters);
        return roundedMeters == 1 ? "about 1 meter" : $"about {roundedMeters} meters";
    }

    private void StartAgentRequest(string prompt, AgentRequestKind requestKind, string passiveLabel = null)
    {
        if (requestKind == AgentRequestKind.Manual)
        {
            InterruptCurrentRequest();
        }
        else if (m_isBusy)
        {
            return;
        }

        m_activeRequestKind = requestKind;
        m_activePassiveLabel = requestKind == AgentRequestKind.Passive ? passiveLabel : null;
        m_activeRequestCoroutine = StartCoroutine(GenerateAndSpeak(prompt, requestKind, passiveLabel));
    }

    private void InterruptCurrentRequest(bool clearPendingPassiveCooldown = false)
    {
        bool interruptedPassiveRequest = m_activeRequestKind == AgentRequestKind.Passive && !string.IsNullOrEmpty(m_activePassiveLabel);
        if (m_activeRequestCoroutine != null)
        {
            StopCoroutine(m_activeRequestCoroutine);
            m_activeRequestCoroutine = null;
        }

        m_speechService?.Stop();
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        StopProcessingSound();
        m_isBusy = false;

        if (interruptedPassiveRequest)
        {
            m_pendingPassiveCooldownAfterManual = true;
            m_pendingPassiveCooldownLabel = m_activePassiveLabel;
        }

        if (clearPendingPassiveCooldown)
        {
            m_pendingPassiveCooldownAfterManual = false;
            m_pendingPassiveCooldownLabel = null;
        }

        m_activeRequestKind = null;
        m_activePassiveLabel = null;
    }

    private void BindPassiveGuidanceScrollbar()
    {
        if (passiveGuidanceScrollbar == null)
        {
            return;
        }

        passiveGuidanceScrollbar.onValueChanged.RemoveListener(SetPassiveGuidanceEnabledFromScrollbar);
        passiveGuidanceScrollbar.onValueChanged.AddListener(SetPassiveGuidanceEnabledFromScrollbar);
        SetPassiveGuidanceEnabledFromScrollbar(passiveGuidanceScrollbar.value);
    }

    private void UnbindPassiveGuidanceScrollbar()
    {
        if (passiveGuidanceScrollbar == null)
        {
            return;
        }

        passiveGuidanceScrollbar.onValueChanged.RemoveListener(SetPassiveGuidanceEnabledFromScrollbar);
    }

    private IEnumerator GenerateAndSpeak(string prompt, AgentRequestKind requestKind, string passiveLabel)
    {
        if (m_textService == null || m_speechService == null)
        {
            InitializeServices();
        }

        m_isBusy = true;

        string generatedText = null;
        string generationError = null;
        yield return m_textService.GenerateText(
            prompt,
            text => generatedText = text,
            error => generationError = error);

        if (!string.IsNullOrEmpty(generationError))
        {
            m_isBusy = false;
            m_activeRequestCoroutine = null;
            m_activeRequestKind = null;
            m_activePassiveLabel = null;
            StopProcessingSound();
            Debug.LogWarning("AgentController: text generation failed: " + generationError);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(generatedText))
        {
            m_isBusy = false;
            m_activeRequestCoroutine = null;
            m_activeRequestKind = null;
            m_activePassiveLabel = null;
            StopProcessingSound();
            Debug.LogWarning("AgentController: generated text was empty.");
            yield break;
        }

        Debug.Log("AgentController: generated text: " + generatedText);
        UpdateAgentResponseUi(generatedText);
        StopProcessingSound();
        OnResponseReady.Invoke();

        string speechError = null;
        yield return m_speechService.Speak(
            generatedText,
            audioSource,
            null,
            error => speechError = error);

        m_isBusy = false;
        m_activeRequestCoroutine = null;

        if (requestKind == AgentRequestKind.Passive && string.IsNullOrEmpty(speechError))
        {
            m_lastHandledPassiveLabel = passiveLabel;
            m_cooldownTimer = cooldownSeconds;
        }
        else if (requestKind == AgentRequestKind.Manual && m_pendingPassiveCooldownAfterManual)
        {
            m_lastHandledPassiveLabel = m_pendingPassiveCooldownLabel;
            m_cooldownTimer = cooldownSeconds;
            m_pendingPassiveCooldownAfterManual = false;
            m_pendingPassiveCooldownLabel = null;
            m_stableTimer = 0f;
        }

        m_activeRequestKind = null;
        m_activePassiveLabel = null;

        if (!string.IsNullOrEmpty(speechError))
        {
            Debug.LogWarning("AgentController: speech failed: " + speechError);
        }
    }

    private void UpdateAgentResponseUi(string responseText)
    {
        if (agentResponseText != null)
        {
            agentResponseText.text = responseText ?? string.Empty;
        }
    }

    private void RefreshPlayerMapPoseUi()
    {
        if (playerMapPoseText == null)
        {
            return;
        }

        Transform userTransform = ResolveUserTransform();
        if (userTransform == null || localizationManager == null || localizationManager.MapSpace == null)
        {
            playerMapPoseText.text = "Player Map Pose: Unknown";
            return;
        }

        Transform mapTransform = localizationManager.MapSpace.transform;
        Vector3 localPosition = mapTransform.InverseTransformPoint(userTransform.position);
        Quaternion localRotation = Quaternion.Inverse(mapTransform.rotation) * userTransform.rotation;
        Vector3 localEuler = localRotation.eulerAngles;

        playerMapPoseText.text =
            $"Player Map Pos: ({localPosition.x:F2}, {localPosition.y:F2}, {localPosition.z:F2})\n" +
            $"Player Map Rot: ({localEuler.x:F1}, {localEuler.y:F1}, {localEuler.z:F1})";
    }
}
