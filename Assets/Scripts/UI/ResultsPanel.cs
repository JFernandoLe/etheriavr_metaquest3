using UnityEngine;
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
    [SerializeField] private TextMeshProUGUI txtSongName;
    [SerializeField] private TextMeshProUGUI txtAccuracy;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private TextMeshProUGUI txtGrade;
    
    [Header("Botones")]
    [SerializeField] private Button btnRetry;
    [SerializeField] private Button btnBackToRepertorio;
    
    [Header("Animación")]
    [SerializeField] private float fadeInDuration = 0.5f;
    
    private GameplayResults currentResults;
    private bool isShowing = false;
    
    void Awake()
    {
        // Asegurar que el canvas group existe
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Configurar botones
        if (btnRetry != null)
            btnRetry.onClick.AddListener(OnRetryPressed);
            
        if (btnBackToRepertorio != null)
            btnBackToRepertorio.onClick.AddListener(OnBackToRepertorioPressed);
        
        // Inicialmente oculto
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }
    
    /// <summary>
    /// Muestra el panel con los resultados
    /// </summary>
    public void ShowResults(GameplayResults results)
    {
        if (isShowing) return;
        
        currentResults = results;
        isShowing = true;
        
        // Actualizar textos
        UpdateDisplay();
        
        // Animación fade in
        StartCoroutine(FadeIn());
        
        Debug.Log("[ResultsPanel] 🏆 Panel de resultados mostrado");
    }
    
    /// <summary>
    /// Actualiza los textos del panel
    /// </summary>
    private void UpdateDisplay()
    {
        // Nombre de la canción
        if (txtSongName != null)
        {
            txtSongName.text = $"<size=80%>{currentResults.song_name}</size>";
        }
        
        // Precisión (%)
        if (txtAccuracy != null)
        {
            // Determinar color basado en accuracy
            string accuracyColor = currentResults.accuracy_percentage >= 90 ? "green" :
                                  currentResults.accuracy_percentage >= 70 ? "yellow" : "red";
            
            txtAccuracy.text = $"<color={accuracyColor}><size=150%>{currentResults.accuracy_percentage:F1}%</size></color>";
        }
        
        // Estadísticas
        if (txtStats != null)
        {
            string stats = $"<size=90%>" +
                         $"Notas tocadas: <color=green>{currentResults.notes_hit}</color>/{currentResults.total_notes}\n" +
                         $"<color=lime>🟢 Perfectas: {currentResults.perfect_notes}</color>\n" +
                         $"<color=red>❌ Perdidas: {currentResults.notes_missed}</color>\n" +
                         $"⏱️ Tiempo: {currentResults.game_duration:F2}s" +
                         $"</size>";
            txtStats.text = stats;
        }
        
        // Calificación
        if (txtGrade != null)
        {
            string grade = GetGradeForAccuracy(currentResults.accuracy_percentage);
            string gradeColor = GetGradeColor(currentResults.accuracy_percentage);
            txtGrade.text = $"<color={gradeColor}><size=120%>{grade}</size></color>";
        }
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
        Debug.Log("[ResultsPanel] 📚 Volviendo al Repertorio");
        
        // Volver a la escena de repertorio
        SceneManager.LoadScene("Repertorio");
    }
    
    /// <summary>
    /// Oculta el panel
    /// </summary>
    public void Hide()
    {
        isShowing = false;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
