using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System;
using TMPro;

[Serializable]
public class RAGResponse
{
    public string message;
    public string audio_url;
}

[Serializable]
public class TtsPayload
{
    public string model;
    public string input;
    public string voice;
    public string responseFormat;
    public float speed;
}

public enum STT
{
    OCI,
    OpenAI
}

public enum TTS
{
    OCI,
    OpenAI
}

public enum TtsVoice
{
    Alloy,
    Echo,
    Fable,
    Onyx,
    Nova,
    Shimmer
}

public class RAGController : MonoBehaviour
{
    [Header("service_setting")]
    [SerializeField] private STT selectedSTT = STT.OCI;
    [SerializeField] private TTS selectedTTS = TTS.OCI;
    [SerializeField] private TtsVoice voice = TtsVoice.Alloy;
    [SerializeField, Range(0.25f, 4.0f)] private float speed = 1f;

    [Header("gateway_url")]
    public string serverUrl = "https://bf43-1-162-131-189.ngrok-free.app/process";
    public string baseUrl = "https://bf43-1-162-131-189.ngrok-free.app"; // 用來補全 audio_url
    public TMP_InputField urlInputField;

    [Header("openai_key")]
    public string _apiKey = "YOUR_OPENAI_API_KEY";

    [Header("音源設定")]
    public AudioSource audioSource;
    public string wavFilePath = "input.wav";

    [Header("Processing Sound")] [Tooltip("Assign an AudioSource that plays a waiting sound.")] [SerializeField]
    private AudioSource processingAudioSource;

    public TextMeshProUGUI resultTXT, spendTXT;

    private string STTOutput = "";

    public float pos_x, pos_z, rot;

    void Start()
    {
        
    }

    void Update()
    {
        string domain = urlInputField.text.Trim();

        if (!string.IsNullOrEmpty(domain))
        {
            serverUrl = "https://" + domain + ".ngrok-free.app/process";
            baseUrl = "https://" + domain + ".ngrok-free.app";
        }
    }

    public void StartProcessingSound()
    {
        if (!processingAudioSource)
        {
            return;
        }
        processingAudioSource.loop = true;
        processingAudioSource.Play();
    }

    public void StopProcessingSound()
    {
        if (processingAudioSource)
        {
            processingAudioSource.Stop();
        }
    }

    public void StartRAG()
    {
        STTOutput = "";

        int kind = 0;
        if(selectedSTT == STT.OpenAI && selectedTTS == TTS.OpenAI) kind = 3;
        else if(selectedSTT == STT.OpenAI) kind = 1;
        else if(selectedTTS == TTS.OpenAI) kind = 2;

        StartCoroutine(UploadAudioCoroutine(kind));
    }

    IEnumerator UploadAudioCoroutine(int kind = 0)
    {
        float startTime = Time.time;
        string fullPath = Path.Combine(Application.persistentDataPath, wavFilePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError("❌ 音訊檔案不存在：" + fullPath);
            yield break;
        }

        byte[] audioBytes = File.ReadAllBytes(fullPath);

        if(kind == 1 || kind == 3)
        {
            yield return StartCoroutine(STTOpenAI(audioBytes, (response) =>
            {
                STTOutput = response;
                Debug.Log("STT: " + STTOutput);
            }));
        }

        var form = new WWWForm();
        form.AddBinaryData("file", audioBytes, "input.wav", "audio/wav");
        form.AddField("param", kind);
        form.AddField("param_x", pos_x.ToString("F1"));
        form.AddField("param_z", pos_z.ToString("F1"));
        form.AddField("param_rot", rot.ToString("F1"));
        form.AddField("text", STTOutput);

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);

        Debug.Log("📤 上傳中...");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("❌ 上傳失敗：" + request.error);
        }
        else
        {
            Debug.Log("✅ 上傳成功，處理回傳...");

            // 解析 JSON 回應
            string jsonText = request.downloadHandler.text;
            Debug.Log("📩 回傳 JSON： " + jsonText);

            RAGResponse response = JsonUtility.FromJson<RAGResponse>(jsonText);

            if (response == null || string.IsNullOrEmpty(response.audio_url))
            {
                Debug.LogError("❌ 回傳格式錯誤或缺少 audio_url！");
                yield break;
            }

            // 顯示 AI 回覆
            Debug.Log("🧠 AI 回答內容：\n" + response.message);
            resultTXT.text = response.message;

            if(kind == 2 || kind == 3)
            {
                yield return StartCoroutine(TTSOpenAI(
                    response.message,
                    audioData =>
                    {
                        if (audioData == null)
                        {
                            return;
                        }

                        Debug.Log("Playing audio.");
                        PlayAudioFromBytes(audioData);
                    },
                    error => { Debug.LogError("Failed to get audio data from OpenAI: " + error); }, voice, speed
                ));
            }

            StopProcessingSound();

            // 補上完整 URL 並下載音訊
            if(kind != 2 && kind != 3)
            {
                string audioFullUrl = response.audio_url.StartsWith("http") ? response.audio_url : baseUrl + response.audio_url;
                StartCoroutine(DownloadAndPlayAudio(audioFullUrl));
            }

            float endTime = Time.time;
            float duration = endTime - startTime;

            spendTXT.text = $"Spends:{duration:F2} s";
        }
    }

    IEnumerator DownloadAndPlayAudio(string url)
    {
        Debug.Log("🔽 開始下載音訊：" + url);

        UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("❌ 音訊下載失敗：" + www.error);
        }
        else
        {
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log("▶️ 音訊播放開始！");
        }
    }

    public void PlayAudioFromBytes(byte[] audioData, string extension = "mp3")
    {
        string fileName = $"temp_audio.{extension}";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllBytes(path, audioData);
        StartCoroutine(PlayAudioCoroutine(path, extension));
    }

    private IEnumerator PlayAudioCoroutine(string path, string extension)
    {
        AudioType audioType = extension == "wav" ? AudioType.WAV : AudioType.MPEG;
        using var www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, audioType);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            audioSource.clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.Play();
        }
        else
        {
            Debug.LogError("Failed to load audio: " + www.error);
        }

        // 清理暫存檔案
        File.Delete(path);
    }

    public IEnumerator STTOpenAI(byte[] audioData, System.Action<string> callback)
    {
        string url = "https://api.openai.com/v1/audio/transcriptions";

        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogError("SendToOpenAI: Audio data is empty or null.");
            callback?.Invoke("Error: Audio file is empty.");
            yield break;
        }

        if (audioData.Length > 25 * 1024 * 1024)
        {
            Debug.LogError("SendToOpenAI: Audio file is too large.");
            callback?.Invoke("Error: File too large.");
            yield break;
        }

        string filePath = Path.Combine(Application.persistentDataPath, wavFilePath);
        File.WriteAllBytes(filePath, audioData);

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", audioData, wavFilePath, "audio/wav"),
            new MultipartFormDataSection("model", "whisper-1"),
            new MultipartFormDataSection("language", "en")
        };

        byte[] boundary = UnityWebRequest.GenerateBoundary();
        byte[] formDataBytes = UnityWebRequest.SerializeFormSections(formData, boundary);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(formDataBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + _apiKey);
        request.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + Encoding.UTF8.GetString(boundary));

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.ConnectionError &&
            request.result != UnityWebRequest.Result.ProtocolError)
        {
            callback?.Invoke(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("OpenAI API Error: " + request.error + "\nResponse: " + request.downloadHandler.text);
            callback?.Invoke("Error: " + request.downloadHandler.text);
        }
    }

    public IEnumerator TTSOpenAI(string text, Action<byte[]> onSuccess, Action<string> onError,
                                    TtsVoice voice = TtsVoice.Alloy, float speed = 1f)
    {
        Debug.Log("Sending new request to OpenAI TTS.");

        var payload = new TtsPayload
        {
            model = "tts-1",
            input = text,
            voice = voice.ToString().ToLower(),
            responseFormat = "mp3",
            speed = speed
        };

        var jsonPayload = JsonUtility.ToJson(payload);

        using var request = new UnityWebRequest("https://api.openai.com/v1/audio/speech", "POST");
        var bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + _apiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("TTS Request Error: " + request.error);
            onError?.Invoke(request.error);
        }
        else
        {
            var audioData = request.downloadHandler.data;
            onSuccess?.Invoke(audioData);
        }
    }
}
