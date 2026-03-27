using UnityEngine;
using System;
using System.Globalization;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Modal que muestra los resultados finales del juego de piano
/// </summary>
public class ResultsPanel : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup; // Para fade in/out
    
    [Header("Textos")]
    [SerializeField] private TMP_Text txtSongName;
    [SerializeField] private TMP_Text txtAccuracy;
    [SerializeField] private TMP_Text txtStats;
    [SerializeField] private TMP_Text txtGrade;
    [SerializeField] private TMP_Text txtHarmony;
    [SerializeField] private TMP_Text txtRhythm;
    [SerializeField] private TMP_Text txtDate;
    [SerializeField] private TMP_Text txtTime;
    [SerializeField] private TMP_Text txtDuration;
    [SerializeField] private TMP_Text txtMode;
    [SerializeField] private TMP_Text txtHeader;
    [SerializeField] private TMP_Text txtSubHeader;
    [SerializeField] private TMP_Text lblHarmony;
    [SerializeField] private TMP_Text lblRhythm;
    
    [Header("Botones")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnBackToRepertorio;
    
    [Header("Animación")]
    [SerializeField] private float fadeInDuration = 0.5f;
    
    private GameplayResults currentResults;
    private bool isShowing = false;
    
    void Awake()
    {
        EnsureBindings();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (gameObject.activeSelf)
        {
            HideImmediate();
        }
    }
    
    /// <summary>
    /// Muestra el panel con los resultados
    /// </summary>
    public void ShowResults(GameplayResults results)
    {
        EnsureBindings();
        
        currentResults = results;
        isShowing = true;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        
        // Actualizar textos
        UpdateDisplay();
        
        // Animación fade in
        StopAllCoroutines();
        StartCoroutine(FadeIn());
        
        Debug.Log("[ResultsPanel] 🏆 Panel de resultados mostrado");
    }
    
    /// <summary>
    /// Actualiza los textos del panel
    /// </summary>
    private void UpdateDisplay()
    {
        if (txtHeader != null)
        {
            txtHeader.text = "Partida Finalizada";
        }

        if (txtSubHeader != null)
        {
            txtSubHeader.text = "Resumen de interpretacion";
        }

        if (lblHarmony != null)
        {
            lblHarmony.text = "ARMONIA";
        }

        if (lblRhythm != null)
        {
            lblRhythm.text = "RITMO";
        }

        // Nombre de la canción
        if (txtSongName != null)
        {
            txtSongName.text = $"Cancion: {currentResults.song_name}";
        }
        
        // Puntaje global (%)
        if (txtAccuracy != null)
        {
            txtAccuracy.text = FormatPercent(currentResults.global_percentage);
        }

        if (txtHarmony != null)
        {
            txtHarmony.text = FormatPercent(currentResults.harmony_percentage);
        }

        if (txtRhythm != null)
        {
            txtRhythm.text = FormatPercent(currentResults.rhythm_percentage);
        }

        if (txtDate != null)
        {
            txtDate.text = $"Fecha: {currentResults.timestamp.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
        }

        if (txtTime != null)
        {
            txtTime.text = $"Hora: {currentResults.timestamp.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        }

        if (txtDuration != null)
        {
            txtDuration.text = $"Duracion: {FormatDuration(currentResults.game_duration)}";
        }

        if (txtMode != null)
        {
            string modeName = string.IsNullOrWhiteSpace(currentResults.mode_name) ? "PIANO" : currentResults.mode_name;
            txtMode.text = $"Modo: {modeName}";
        }
        
        // Estadísticas legacy
        if (txtStats != null)
        {
            string stats = $"<size=90%>" +
                         $"Armonia: <color=green>{currentResults.harmony_percentage:F1}%</color>\n" +
                         $"Ritmo: <color=cyan>{currentResults.rhythm_percentage:F1}%</color>\n" +
                         $"Global: <color=yellow>{currentResults.global_percentage:F1}%</color>\n" +
                         $"Cobertura lograda: <color=green>{currentResults.notes_hit:F1}</color>/{currentResults.total_notes:F1}\n" +
                         $"<color=lime>🟢 Perfectas: {currentResults.perfect_notes}</color>\n" +
                         $"<color=red>❌ Faltante: {currentResults.notes_missed:F1}</color>\n" +
                         $"⏱️ Tiempo: {currentResults.game_duration:F2}s" +
                         $"</size>";
            txtStats.text = stats;
        }
        
        // Calificación
        if (txtGrade != null)
        {
            string grade = GetGradeForAccuracy(currentResults.global_percentage);
            string gradeColor = GetGradeColor(currentResults.global_percentage);
            txtGrade.text = $"<color={gradeColor}><size=120%>{grade}</size></color>";
        }
    }

    private void EnsureBindings()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        AutoBindSceneReferences();
        ConfigureButtons();
    }

    private void AutoBindSceneReferences()
    {
        txtHeader ??= FindTextByName("H1");
        txtSubHeader ??= FindTextByName("H2");
        txtSongName ??= FindTextByName("H4 (1)");
        txtAccuracy ??= FindTextByName("GlobalText");
        txtHarmony ??= FindTextByName("PitchText");
        txtRhythm ??= FindTextByName("RhythmText");
        txtDate ??= FindTextByName("H4 (3)");
        txtTime ??= FindTextByName("H4 (4)");
        txtDuration ??= FindTextByName("H4 (5)");
        txtMode ??= FindTextByName("H4 (2)");
        lblHarmony ??= FindTextByName("H3");
        lblRhythm ??= FindTextByName("H3 (1)");

        btnRetry ??= FindButtonByName("BtnReiniciar");
        btnBackToRepertorio ??= FindButtonByName("BtnMenu");
    }

    private void ConfigureButtons()
    {
        if (btnRetry != null)
        {
            btnRetry.onClick.RemoveListener(OnRetryPressed);
            btnRetry.onClick.AddListener(OnRetryPressed);
        }

        if (btnBackToRepertorio != null)
        {
            btnBackToRepertorio.onClick.RemoveListener(OnBackToRepertorioPressed);
            btnBackToRepertorio.onClick.AddListener(OnBackToRepertorioPressed);
        }
    }

    private TMP_Text FindTextByName(string objectName)
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.gameObject.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private Button FindButtonByName(string objectName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button != null && button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private string FormatPercent(float value)
    {
        return $"{Mathf.Clamp(value, 0f, 100f):F1} %";
    }

    private string FormatDuration(float seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(seconds, 0f));
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }
    
    /// <summary>
    /// Obtiene la calificación basada en accuracy
    /// </summary>
    private string GetGradeForAccuracy(float accuracy)
    {
        if (accuracy >= 95) return "S";      // Excepcional
        if (accuracy >= 85) return "A";      // Excelente
        if (accuracy >= 75) return "B";      // Bueno
        if (accuracy >= 60) return "C";      // Regular
        if (accuracy >= 40) return "D";      // Deficiente
        return "F";                          // Fallo
    }
    
    /// <summary>
    /// Obtiene el color de la calificación
    /// </summary>
    private string GetGradeColor(float accuracy)
    {
        if (accuracy >= 95) return "gold";      // Dorado
        if (accuracy >= 85) return "cyan";      // Cyan
        if (accuracy >= 75) return "green";     // Verde
        if (accuracy >= 60) return "yellow";    // Amarillo
        if (accuracy >= 40) return "orange";    // Naranja
        return "red";                           // Rojo
    }
    
    /// <summary>
    /// Animación fade in
    /// </summary>
    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
    }
    
    /// <summary>
    /// Cuando presiona "Reintentar"
    /// </summary>
    private void OnRetryPressed()
    {
        Debug.Log("[ResultsPanel] 🔄 Reintentar canción");
        
        // Reiniciar la escena
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Cuando presiona "Volver al Repertorio"
    /// </summary>
    private void OnBackToRepertorioPressed()
    {
        Debug.Log("[ResultsPanel] 💾 Guardar y salir solicitado");

        if (PianoGameManager.Instance != null)
        {
            PianoGameManager.Instance.SaveAndExitToRepertorio(currentResults);
            return;
        }

        SceneManager.LoadScene("RepertorioScene");
    }
    
    /// <summary>
    /// Oculta el panel
    /// </summary>
    public void Hide()
    {
        isShowing = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        isShowing = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}
