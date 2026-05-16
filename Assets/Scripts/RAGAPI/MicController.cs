using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Networking;

public enum VoiceRequestTarget
{
    RAG,
    Agent
}

[Serializable]
public class OpenAiTranscriptionResponse
{
    public string text;
}

public class MicController : MonoBehaviour
{
    [Header("Services")]
    [SerializeField] private VoiceRequestTarget requestTarget = VoiceRequestTarget.RAG;
    [FormerlySerializedAs("RAGMaster")]
    [SerializeField] private RAGController ragController;
    [SerializeField] private AgentController agentController;

    public event Action<string> OnTranscriptionComplete;

    [Header("UI Components")]
    [SerializeField] private Button recordButton;
    [SerializeField] private TMP_Dropdown microphoneDropdown;

    [Header("Events")]
    public UnityEvent onRequestStarted;
    public UnityEvent onRequestSent;

    [Header("OpenAI STT")]
    [SerializeField] private string transcriptionModel = "whisper-1";
    [SerializeField] private string transcriptionLanguage = "zh";

    private AudioClip m_clip;
    private bool m_isRecording;

    private const string FileName = "input.wav";
    private const int SampleRate = 44100;
    private const int MaxRecordSeconds = 10;

    public VoiceRequestTarget RequestTarget => requestTarget;

    private void Start()
    {
        PopulateMicrophoneDropdown();

        if (recordButton != null)
        {
            recordButton.onClick.AddListener(ToggleRecording);
        }
    }

    private void PopulateMicrophoneDropdown()
    {
        if (microphoneDropdown == null)
        {
            return;
        }

        microphoneDropdown.ClearOptions();
        foreach (string device in Microphone.devices)
        {
            microphoneDropdown.options.Add(new TMP_Dropdown.OptionData(device));
        }

        int savedIndex = Mathf.Clamp(PlayerPrefs.GetInt("user-mic-device-index", 0), 0, Mathf.Max(0, microphoneDropdown.options.Count - 1));
        microphoneDropdown.value = savedIndex;
        microphoneDropdown.onValueChanged.AddListener(ChangeMicrophone);
    }

    private void ChangeMicrophone(int index)
    {
        PlayerPrefs.SetInt("user-mic-device-index", index);
    }

    private void ToggleRecording()
    {
        if (m_isRecording)
        {
            EndRecording();
        }
        else
        {
            StartRecording();
        }
    }

    public void SetRequestTargetToRag()
    {
        requestTarget = VoiceRequestTarget.RAG;
    }

    public void SetRequestTargetToAgent()
    {
        requestTarget = VoiceRequestTarget.Agent;
    }

    private void StartRecording()
    {
        onRequestStarted?.Invoke();
        m_isRecording = true;

        string selectedMic = GetSelectedMicrophoneName();
        if (string.IsNullOrEmpty(selectedMic))
        {
            Debug.LogError("MicController: no microphone devices found.");
            m_isRecording = false;
            return;
        }

        m_clip = Microphone.Start(selectedMic, false, MaxRecordSeconds, SampleRate);
        if (m_clip != null)
        {
            return;
        }

        Debug.LogError("MicController: failed to start microphone recording.");
        m_isRecording = false;
    }

    private void EndRecording()
    {
        if (!m_isRecording)
        {
            return;
        }

        onRequestSent?.Invoke();
        m_isRecording = false;

        string selectedMic = GetSelectedMicrophoneName();
        if (string.IsNullOrEmpty(selectedMic))
        {
            Debug.LogError("MicController: selected microphone was unavailable when stopping.");
            return;
        }

        int samples = Mathf.Max(1, Microphone.GetPosition(selectedMic));
        Microphone.End(selectedMic);

        m_clip = ClipRecord(m_clip, SampleRate, samples);
        if (m_clip == null)
        {
            Debug.LogError("MicController: AudioClip is null after trimming.");
            return;
        }

        byte[] audioData = SaveWavFile(FileName, m_clip);
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("MicController: failed to create wav bytes.");
            return;
        }

        switch (requestTarget)
        {
            case VoiceRequestTarget.Agent:
                StartCoroutine(HandleAgentVoiceRequest(audioData));
                break;
            case VoiceRequestTarget.RAG:
            default:
                if (ragController == null)
                {
                    Debug.LogError("MicController: RAG target selected but no RAGController is assigned.");
                    return;
                }

                ragController.StartRAG();
                break;
        }
    }

    private IEnumerator HandleAgentVoiceRequest(byte[] audioData)
    {
        if (agentController == null)
        {
            Debug.LogError("MicController: Agent target selected but no AgentController is assigned.");
            yield break;
        }

        string resolvedApiKey = ResolveOpenAiApiKey();
        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            agentController.StopProcessingSound();
            Debug.LogError("MicController: AgentController.apiSettings.apiKey is empty, cannot transcribe audio for Agent.");
            yield break;
        }

        string transcript = null;
        string errorMessage = null;
        yield return TranscribeAudioWithOpenAi(
            audioData,
            resolvedApiKey,
            text => transcript = text,
            error => errorMessage = error);

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            agentController.StopProcessingSound();
            Debug.LogError("MicController: transcription failed for Agent request: " + errorMessage);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            agentController.StopProcessingSound();
            Debug.LogWarning("MicController: transcription was empty for Agent request.");
            yield break;
        }

        OnTranscriptionComplete?.Invoke(transcript);
        agentController.AskQuestion(transcript);
    }

    private IEnumerator TranscribeAudioWithOpenAi(byte[] audioData, string resolvedApiKey, Action<string> onSuccess, Action<string> onError)
    {
        if (audioData == null || audioData.Length == 0)
        {
            onError?.Invoke("Audio data is empty.");
            yield break;
        }

        if (audioData.Length > 25 * 1024 * 1024)
        {
            onError?.Invoke("Audio file is larger than 25MB.");
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, FileName, "audio/wav");
        form.AddField("model", transcriptionModel);
        if (!string.IsNullOrWhiteSpace(transcriptionLanguage))
        {
            form.AddField("language", transcriptionLanguage);
        }

        using UnityWebRequest request = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form);
        request.SetRequestHeader("Authorization", "Bearer " + resolvedApiKey);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{request.error} - {request.downloadHandler.text}");
            yield break;
        }

        OpenAiTranscriptionResponse response = JsonUtility.FromJson<OpenAiTranscriptionResponse>(request.downloadHandler.text);
        string transcript = response != null ? response.text : null;
        if (string.IsNullOrWhiteSpace(transcript))
        {
            onError?.Invoke("Could not parse transcription text.");
            yield break;
        }

        Debug.Log("MicController: transcription -> " + transcript);
        onSuccess?.Invoke(transcript.Trim());
    }

    private string ResolveOpenAiApiKey()
    {
        if (agentController != null && agentController.apiSettings != null && !string.IsNullOrWhiteSpace(agentController.apiSettings.apiKey))
        {
            return agentController.apiSettings.apiKey;
        }

        return null;
    }

    private AudioClip ClipRecord(AudioClip clip, int sampleRate, int samples)
    {
        if (clip == null)
        {
            return null;
        }

        float[] rawData = new float[clip.samples * clip.channels];
        clip.GetData(rawData, 0);

        int trimmedLength = Mathf.Min(samples * clip.channels, rawData.Length);
        float[] trimmedData = new float[trimmedLength];
        Array.Copy(rawData, trimmedData, trimmedLength);

        AudioClip trimmedClip = AudioClip.Create("TrimmedClip", trimmedLength / clip.channels, clip.channels, sampleRate, false);
        trimmedClip.SetData(trimmedData, 0);
        return trimmedClip;
    }

    private byte[] SaveWavFile(string fileName, AudioClip clip)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        byte[] wavBytes = WavUtility.FromAudioClip(clip, out _, false);
        File.WriteAllBytes(path, wavBytes);
        Debug.Log("MicController: saved wav to " + path);
        return wavBytes;
    }

    private string GetSelectedMicrophoneName()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            return null;
        }

        int index = PlayerPrefs.GetInt("user-mic-device-index", 0);
        if (microphoneDropdown != null && microphoneDropdown.options.Count > 0)
        {
            index = Mathf.Clamp(index, 0, microphoneDropdown.options.Count - 1);
            string micName = microphoneDropdown.options[index].text;
            if (Microphone.devices.Contains(micName))
            {
                return micName;
            }
        }

        return Microphone.devices[0];
    }
}
