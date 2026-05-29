using System;
using System.Collections;

public class LocalSpeechService : IAgentSpeechService
{
    private object m_currentSynth;
    private bool m_stopRequested;

    public IEnumerator Speak(string text, UnityEngine.AudioSource audioSource, Action onSuccess, Action<string> onError)
    {
        m_stopRequested = false;
        var synthType = Type.GetType("System.Speech.Synthesis.SpeechSynthesizer, System.Speech");
        if (synthType == null)
        {
            onError?.Invoke("Local Windows speech synthesizer is not available.");
            yield break;
        }

        object synth = null;
        var speakMethod = synthType.GetMethod("SpeakAsync", new[] { typeof(string) });
        if (speakMethod == null)
        {
            onError?.Invoke("SpeakAsync method is not available.");
            yield break;
        }

        var stateProperty = synthType.GetProperty("State");
        try
        {
            synth = Activator.CreateInstance(synthType);
            m_currentSynth = synth;
            speakMethod.Invoke(synth, new object[] { text });
            onSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            CleanupSynth();
            yield break;
        }

        while (!m_stopRequested)
        {
            string stateName = null;
            try
            {
                stateName = stateProperty?.GetValue(synth)?.ToString();
            }
            catch
            {
            }

            if (!string.Equals(stateName, "Speaking", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            yield return null;
        }

        if (m_stopRequested)
        {
            TryCancelCurrentSynth();
        }

        CleanupSynth();
    }

    public void Stop()
    {
        m_stopRequested = true;

        if (m_currentSynth == null)
        {
            return;
        }

        TryCancelCurrentSynth();
    }

    private void TryCancelCurrentSynth()
    {
        try
        {
            var cancelMethod = m_currentSynth.GetType().GetMethod("SpeakAsyncCancelAll", Type.EmptyTypes);
            cancelMethod?.Invoke(m_currentSynth, null);
        }
        catch
        {
        }
    }

    private void CleanupSynth()
    {
        if (m_currentSynth is IDisposable disposable)
        {
            disposable.Dispose();
        }

        m_currentSynth = null;
        m_stopRequested = false;
    }
}
