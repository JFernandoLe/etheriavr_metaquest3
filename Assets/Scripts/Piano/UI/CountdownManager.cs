using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Controla el countdown 3-2-1-GO! antes de iniciar el juego
/// </summary>
public class CountdownManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Canvas countdownCanvas;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bipSound; // Sonido "bip" para 3, 2, 1
    [SerializeField] private AudioClip goSound;  // Sonido "GO!" (opcional, puede ser null)
    
    [Header("Configuración")]
    [SerializeField] private float countdownDuration = 1f; // 1 segundo por número
    [SerializeField] private float textScale = 2f; // Escala del texto
    [SerializeField] private float bipPlaybackDuration = 0.2f;
    
    private bool isCountdownActive = false;
    
    public delegate void CountdownComplete();
    public event CountdownComplete OnCountdownComplete;

    void Awake()
    {
        // Auto-crear AudioSource si no existe
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }
        
        // Canvas permanece activo, solo ocultamos el texto
        if (countdownText != null)
        {
            countdownText.text = "";
        }
    }

    /// <summary>
    /// Inicia el countdown 3-2-1-GO!
    /// </summary>
    public void StartCountdown()
    {
        if (isCountdownActive)
        {
            Debug.LogWarning("[Countdown] Countdown ya está activo, ignorando llamada múltiple");
            return;
        }
        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        isCountdownActive = true;
        
        // Canvas ya está activo, solo mostramos el texto

        // 3
        ShowNumber("3");
        PlayBip();
        yield return new WaitForSeconds(countdownDuration);

        // 2
        ShowNumber("2");
        PlayBip();
        yield return new WaitForSeconds(countdownDuration);

        // 1
        ShowNumber("1");
        PlayBip();
        yield return new WaitForSeconds(countdownDuration);

        // GO!
        ShowNumber("GO!");
        PlayGo();
        yield return new WaitForSeconds(countdownDuration * 0.5f); // Medio segundo para GO

        // Ocultar texto
        if (countdownText != null)
        {
            countdownText.text = "";
        }

        isCountdownActive = false;
        
        // Notificar que terminó
        OnCountdownComplete?.Invoke();
    }

    private void ShowNumber(string text)
    {
        if (countdownText != null)
        {
            countdownText.text = text;
            countdownText.fontSize = (text == "GO!") ? 120 : 150;
        }
    }

    private void PlayBip()
    {
        if (audioSource != null && bipSound != null)
        {
            StartCoroutine(PlayTrimmedClip(bipSound, bipPlaybackDuration));
        }
    }

    private void PlayGo()
    {
        if (audioSource != null)
        {
            // Si hay sonido especial de GO, usarlo, si no, usar bip
            AudioClip soundToPlay = goSound != null ? goSound : bipSound;
            if (soundToPlay != null)
            {
                audioSource.PlayOneShot(soundToPlay);
            }
        }
    }

    private IEnumerator PlayTrimmedClip(AudioClip clip, float duration)
    {
        if (audioSource == null || clip == null)
        {
            yield break;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.time = 0f;
        audioSource.Play();

        float safeDuration = Mathf.Min(duration, clip.length);
        yield return new WaitForSeconds(safeDuration);

        if (audioSource.clip == clip)
        {
            audioSource.Stop();
        }
    }
}
