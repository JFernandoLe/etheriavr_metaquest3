using UnityEngine;

/// <summary>
/// Decodifica MP3/M4A a WAV usando MediaCodec nativo en Android/Quest.
/// </summary>
public static class AndroidAudioDecoder
{
    public static string ConvertToWavIfNeeded(string inputPath)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (string.IsNullOrEmpty(inputPath))
            return null;

        try
        {
            using AndroidJavaClass bridge = new AndroidJavaClass("com.etheriavr.audiopicker.AudioDecodeBridge");
            string wavPath = bridge.CallStatic<string>("convertToWavIfNeeded", inputPath);
            if (string.IsNullOrEmpty(wavPath))
            {
                Debug.LogError("[AndroidAudioDecoder] Falló decodificación nativa para: " + inputPath);
                return null;
            }

            Debug.Log("[AndroidAudioDecoder] WAV listo: " + wavPath);
            return wavPath;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[AndroidAudioDecoder] Excepción: " + ex.Message);
            return null;
        }
#else
        return inputPath;
#endif
    }
}
