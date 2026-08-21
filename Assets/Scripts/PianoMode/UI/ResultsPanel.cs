using UnityEngine;
using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Modal con los resultados finales de la partida de piano.
/// </summary>
public class ResultsPanel : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup;

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

    void Awake()
    {
        EnsureBindings();
        SetCanvasInteractive(false);

        if (gameObject.activeSelf) HideImmediate();
    }

    public void ShowResults(GameplayResults results)
    {
        EnsureBindings();

        currentResults = results;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        SetCanvasInteractive(true);

        UpdateDisplay();

        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    private void UpdateDisplay()
    {
        SetText(txtHeader, "Partida Finalizada");
        SetText(txtSubHeader, "Resumen de interpretacion");
        SetText(lblHarmony, "ARMONIA");
        SetText(lblRhythm, "RITMO");

        SetText(txtSongName, $"Cancion: {currentResults.song_name}");
        SetText(txtAccuracy, FormatPercent(currentResults.global_percentage));
        SetText(txtHarmony, FormatPercent(currentResults.harmony_percentage));
        SetText(txtRhythm, FormatPercent(currentResults.rhythm_percentage));
        SetText(txtDate, $"Fecha: {currentResults.timestamp.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");
        SetText(txtTime, $"Hora: {currentResults.timestamp.ToString("HH:mm", CultureInfo.InvariantCulture)}");
        SetText(txtDuration, $"Duracion: {FormatDuration(currentResults.game_duration)}");

        string modeName = string.IsNullOrWhiteSpace(currentResults.mode_name) ? "PIANO" : currentResults.mode_name;
        SetText(txtMode, $"Modo: {modeName}");

        SetText(txtStats,
            "<size=90%>" +
            $"Armonia: <color=green>{currentResults.harmony_percentage:F1}%</color>\n" +
            $"Ritmo: <color=cyan>{currentResults.rhythm_percentage:F1}%</color>\n" +
            $"Global: <color=yellow>{currentResults.global_percentage:F1}%</color>\n" +
            $"Cobertura lograda: <color=green>{currentResults.notes_hit:F1}</color>/{currentResults.total_notes:F1}\n" +
            $"<color=lime>🟢 Perfectas: {currentResults.perfect_notes}</color>\n" +
            $"<color=red>❌ Faltante: {currentResults.notes_missed:F1}</color>\n" +
            $"⏱️ Tiempo: {currentResults.game_duration:F2}s" +
            "</size>");

        (string grade, string color) = GetGrade(currentResults.global_percentage);
        SetText(txtGrade, $"<color={color}><size=120%>{grade}</size></color>");
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }

    private void SetCanvasInteractive(bool interactive)
    {
        if (canvasGroup == null) return;

        if (!interactive) canvasGroup.alpha = 0f;
        canvasGroup.interactable = interactive;
        canvasGroup.blocksRaycasts = interactive;
    }

    private void EnsureBindings()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        AutoBindSceneReferences();
        ConfigureButtons();
    }

    /// <summary>Enlaza por nombre los textos del prefab que no se hayan asignado en el inspector.</summary>
    private void AutoBindSceneReferences()
    {
        txtHeader ??= FindChildByName<TMP_Text>("H1");
        txtSubHeader ??= FindChildByName<TMP_Text>("H2");
        txtSongName ??= FindChildByName<TMP_Text>("H4 (1)");
        txtAccuracy ??= FindChildByName<TMP_Text>("GlobalText");
        txtHarmony ??= FindChildByName<TMP_Text>("PitchText");
        txtRhythm ??= FindChildByName<TMP_Text>("RhythmText");
        txtDate ??= FindChildByName<TMP_Text>("H4 (3)");
        txtTime ??= FindChildByName<TMP_Text>("H4 (4)");
        txtDuration ??= FindChildByName<TMP_Text>("H4 (5)");
        txtMode ??= FindChildByName<TMP_Text>("H4 (2)");
        lblHarmony ??= FindChildByName<TMP_Text>("H3");
        lblRhythm ??= FindChildByName<TMP_Text>("H3 (1)");

        btnRetry ??= FindChildByName<Button>("BtnReiniciar");
        btnBackToRepertorio ??= FindChildByName<Button>("BtnMenu");
    }

    // Se quita antes de añadir para no duplicar el listener si se reenlaza.
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

    private T FindChildByName<T>(string objectName) where T : Component
    {
        foreach (T component in GetComponentsInChildren<T>(true))
        {
            if (component != null && component.gameObject.name == objectName) return component;
        }

        return null;
    }

    private static string FormatPercent(float value) => $"{Mathf.Clamp(value, 0f, 100f):F1} %";

    private static string FormatDuration(float seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(seconds, 0f));
        return duration.TotalHours >= 1 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss");
    }

    /// <summary>Calificación y color según el porcentaje global.</summary>
    private static (string grade, string color) GetGrade(float accuracy) => accuracy switch
    {
        >= 95 => ("S", "gold"),
        >= 85 => ("A", "cyan"),
        >= 75 => ("B", "green"),
        >= 60 => ("C", "yellow"),
        >= 40 => ("D", "orange"),
        _ => ("F", "red")
    };

    private IEnumerator FadeIn()
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

    private void OnRetryPressed() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    private void OnBackToRepertorioPressed()
    {
        if (PianoGameManager.Instance != null)
        {
            PianoGameManager.Instance.SaveAndExitToRepertorio(currentResults);
            return;
        }

        // Sin manager: salida directa sin fade genérico.
        SceneManager.LoadScene("RepertorioScene");
    }

    public void Hide() => HideImmediate();

    public void HideImmediate()
    {
        SetCanvasInteractive(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
