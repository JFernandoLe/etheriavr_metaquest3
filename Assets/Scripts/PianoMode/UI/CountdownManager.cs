using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Controla el countdown 3-2-1-GO! antes de iniciar el juego.
/// </summary>
public class CountdownManager : MonoBehaviour
{
    private static readonly string[] CountdownNumbers = { "3", "2", "1" };

    [Header("Referencias UI")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Canvas countdownCanvas;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bipSound;
    [Tooltip("Opcional: si está vacío se reutiliza el bip")]
    [SerializeField] private AudioClip goSound;

    [Header("Configuración")]
    [SerializeField] private float countdownDuration = 1f;
    [SerializeField] private float bipPlaybackDuration = 0.2f;

    private bool isCountdownActive = false;

    public delegate void CountdownComplete();
    public event CountdownComplete OnCountdownComplete;

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        // El canvas se queda activo; solo se vacía el texto.
        if (countdownText != null) countdownText.text = "";
    }

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

        foreach (string number in CountdownNumbers)
        {
            ShowNumber(number);
            PlayBip();
            yield return new WaitForSeconds(countdownDuration);
        }

        ShowNumber("GO!");
        PlayGo();
        yield return new WaitForSeconds(countdownDuration * 0.5f);

        if (countdownText != null) countdownText.text = "";

        isCountdownActive = false;
        OnCountdownComplete?.Invoke();
    }

    private void ShowNumber(string text)
    {
        if (countdownText == null) return;

        countdownText.text = text;
        countdownText.fontSize = text == "GO!" ? 120 : 150;
    }

    private void PlayBip()
    {
        if (audioSource != null && bipSound != null) StartCoroutine(PlayTrimmedClip(bipSound, bipPlaybackDuration));
    }

    private void PlayGo()
    {
        if (audioSource == null) return;

        AudioClip soundToPlay = goSound != null ? goSound : bipSound;
        if (soundToPlay != null) audioSource.PlayOneShot(soundToPlay);
    }

    /// <summary>Reproduce solo los primeros segundos del clip para que el bip sea corto.</summary>
    private IEnumerator PlayTrimmedClip(AudioClip clip, float duration)
    {
        if (audioSource == null || clip == null) yield break;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.time = 0f;
        audioSource.Play();

        yield return new WaitForSeconds(Mathf.Min(duration, clip.length));

        if (audioSource.clip == clip) audioSource.Stop();
    }
}
