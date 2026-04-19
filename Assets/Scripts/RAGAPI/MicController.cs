using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Collections.Generic;
using TMPro;

public class MicController : MonoBehaviour
{
    public RAGController RAGMaster;
    public event Action<string> OnTranscriptionComplete;
    [Header("UI Components")]
    [SerializeField] private Button recordButton;
    [SerializeField] private TMP_Dropdown microphoneDropdown;

    [Header("Events")] 
    public UnityEvent onRequestStarted;
    public UnityEvent onRequestSent;

    private AudioClip _clip;
    private bool _isRecording;

    private string _apiKey = "YOUR_OPENAI_API_KEY";
    private const string FileName = "input.wav";

    // Start is called before the first frame update
    void Start()
    {
        if (microphoneDropdown != null)
        {
            microphoneDropdown.ClearOptions();
            foreach (var device in Microphone.devices)
            {
                microphoneDropdown.options.Add(new TMP_Dropdown.OptionData(device));
            }

            microphoneDropdown.onValueChanged.AddListener(ChangeMicrophone);
        }

        if (recordButton != null)
        {
            recordButton.onClick.AddListener(ToggleRecording);
        }
    }

    private void ChangeMicrophone(int index)
    {
        PlayerPrefs.SetInt("user-mic-device-index", index);
    }

     private void ToggleRecording()
    {
        if (_isRecording)
        {
            EndRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        onRequestStarted.Invoke();
        _isRecording = true;

        var index = PlayerPrefs.GetInt("user-mic-device-index", 0);
        string selectedMic = "";
        if (microphoneDropdown && microphoneDropdown.options.Count > index)
        {
            selectedMic = microphoneDropdown.options[index].text;
        }
        else
        {
            if (Microphone.devices.Length > 0)
            {
                selectedMic = Microphone.devices[0];
            }
            else
            {
                Debug.LogError("No microphone devices found!");
                _isRecording = false;
                return;
            }
        }

        if (!Microphone.devices.Contains(selectedMic))
        {
            Debug.LogWarning("Selected microphone not found, using default.");
            selectedMic = Microphone.devices[0];
        }

        _clip = Microphone.Start(selectedMic, false, 10, 44100);

        if (_clip)
        {
            return;
        }

        Debug.LogError("Failed to start microphone recording!");
        
        _isRecording = false;
        if (recordButton)
        {
            recordButton.interactable = true;
        }
    }

    private async void EndRecording()
    {
        if (!_isRecording)
            return;

        onRequestSent.Invoke();
        _isRecording = false;

        int sampleRate = 44100;
        var index = PlayerPrefs.GetInt("user-mic-device-index", 0);
        string selectedMic = microphoneDropdown.options[index].text;
        int samples = Microphone.GetPosition(selectedMic) + 1000;
        Debug.Log("samples: " + samples);
        Microphone.End(selectedMic);

        _clip = ClipRecord(_clip, sampleRate, samples);

        if (!_clip)
        {
            Debug.LogError("AudioClip is null! Cannot save.");
            if (recordButton)
            {
                recordButton.interactable = true;
            }
            return;
        }

        byte[] audioData = SaveWavFile(FileName, _clip);
        RAGMaster.StartRAG();
    }

    private AudioClip ClipRecord(AudioClip _clip, int sampleRate, int samples)
    {
        float[] rawData = new float[_clip.samples * _clip.channels];
        _clip.GetData(rawData, 0);

        float[] trimmedData = new float[samples * _clip.channels];
        Array.Copy(rawData, trimmedData, trimmedData.Length);

        AudioClip trimmedClip = AudioClip.Create("TrimmedClip", samples, _clip.channels, sampleRate, false);
        trimmedClip.SetData(trimmedData, 0);
        return trimmedClip;
    }

    private byte[] SaveWavFile(string file_name, AudioClip clip)
    {
        string path = Path.Combine(Application.persistentDataPath, file_name);
        byte[] wavBytes = WavUtility.FromAudioClip(clip, out _, false); // false → 不自動存檔

        File.WriteAllBytes(path, wavBytes);
        Debug.Log("📁 Saved WAV to: " + path);
        //resultText.text = "📁 Saved to: " + path;

        return wavBytes;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
