using UnityEngine;

public class PianoGameManager : MonoBehaviour
{
    // Singleton para evitar múltiples instancias
    private static PianoGameManager instance;
    public static PianoGameManager Instance => instance;
    
    [Header("Referencias")]
    [SerializeField] private PianoSongLoader songLoader;
    [SerializeField] private AudioSource backgroundMusicSource;
    
    [Header("FASE 3 - Sistema Visual")]
    [SerializeField] private CountdownManager countdownManager;
    [SerializeField] private StaffRenderer trebleStaff; // Pentagrama clave de Sol (arriba)
    [SerializeField] private StaffRenderer bassStaff; // Pentagrama clave de Fa (abajo)
    [SerializeField] private NoteSpawner noteSpawner;
    [SerializeField] private ChordDetectorUI chordDetectorUI;
    
    [Header("VR Positioning")]
    [SerializeField] private Transform vrCamera; // Cámara del jugador
    [SerializeField] private bool enableFollowCanvas = false; // DESACTIVADO - Pentagramas FIJOS (no rotan ni se mueven)
    [SerializeField] private float staffDistance = -500.0f; // Distancia del pentagrama (20cm - MUY MUY CERCA de la cara)
    [SerializeField] private float trebleYOffset = 0.5f; // Altura del pentagrama superior (clave de Sol)
    [SerializeField] private float bassYOffset = -0.5f; // Altura del pentagrama inferior (clave de Fa) - separación 1.0m
    [SerializeField] private float followSmoothSpeed = 5f; // Suavidad del seguimiento (menor = más suave)
    
    [Header("Estado del Juego")]
    public PianoSongData currentSongData;
    public bool isPlaying = false;
    public float gameTime = 0f;
    private bool gameplayReady = false;
    private bool gameStarted = false; // Bandera para evitar inicio múltiple
    
    void Awake()
    {
        // Implementar Singleton
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[PianoGame] Ya existe una instancia, destruyendo duplicado");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    
    void Start()
    {
        // Auto-detectar cámara VR si no está asignada
        if (vrCamera == null)
        {
            vrCamera = Camera.main?.transform;
            if (vrCamera == null)
            {
                Debug.LogError("[PianoGame] No se encontró cámara VR!");
            }
        }
        
        // Verificar que tengamos los componentes necesarios
        if (songLoader == null)
        {
            songLoader = gameObject.AddComponent<PianoSongLoader>();
        }
        
        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = gameObject.AddComponent<AudioSource>();
            backgroundMusicSource.playOnAwake = false;
            backgroundMusicSource.loop = false;
        }
        
        // Cargar datos de la canción seleccionada
        LoadSelectedSong();
    }
    
    void Update()
    {
        // Actualizar el tiempo del juego cuando está en reproducción
        if (isPlaying)
        {
            gameTime += Time.deltaTime;
        }
        
        // FOLLOW CANVAS COMPLETAMENTE DESACTIVADO
        // Los pentagramas son 100% FIJOS - no se mueven ni rotan NUNCA
        // Usa las posiciones manuales configuradas en la jerarquía de Unity
    }
    
    /// <summary>
    /// [DESACTIVADO] Actualiza la posición de los pentagramas para seguir la cámara del jugador
    /// Este método está COMPLETAMENTE DESACTIVADO - los pentagramas son 100% FIJOS
    /// </summary>
    private void UpdateStaffPositions()
    {
        // ESTE MÉTODO HA SIDO DESACTIVADO
        // Los pentagramas NO SE MUEVEN - permanecen en la posición fija de la jerarquía
        return;
        
        /* CÓDIGO COMENTADO - NO SE EJECUTA
        if (vrCamera == null) return;
        
        // Solo actualizar si los pentagramas existen
        if (trebleStaff == null && bassStaff == null) return;
        
        // Calcular posición frente al jugador (follow canvas)
        Vector3 forward = vrCamera.forward;
        forward.y = 0; // Mantener horizontal
        forward.Normalize();
        
        // Posición base frente al jugador a la distancia configurada
        Vector3 basePosition = vrCamera.position + forward * staffDistance;
        
        // Actualizar posición del pentagrama de clave de Sol (arriba)
        if (trebleStaff != null)
        {
            Vector3 targetPos = basePosition + Vector3.up * trebleYOffset;
            
            // Suavizar movimiento
            trebleStaff.transform.position = Vector3.Lerp(
                trebleStaff.transform.position, 
                targetPos, 
                Time.deltaTime * followSmoothSpeed
            );
            
            // IMPORTANTE: Mantener el pentagrama VERTICAL (líneas horizontales)
            // Rotar para que las notas se muevan HACIA el jugador (de izquierda a derecha)
            Vector3 directionToCamera = vrCamera.position - trebleStaff.transform.position;
            directionToCamera.y = 0; // Solo rotación horizontal
            
            if (directionToCamera.magnitude > 0.01f)
            {
                // Calcular rotación para que mire AL jugador
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                // Mantener solo rotación en Y (horizontal)
                targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
                
                trebleStaff.transform.rotation = Quaternion.Slerp(
                    trebleStaff.transform.rotation,
                    targetRotation,
                    Time.deltaTime * followSmoothSpeed
                );
            }
        }
        
        // Actualizar posición del pentagrama de clave de Fa (abajo)
        if (bassStaff != null)
        {
            Vector3 targetPos = basePosition + Vector3.up * bassYOffset;
            
            // Suavizar movimiento
            bassStaff.transform.position = Vector3.Lerp(
                bassStaff.transform.position,
                targetPos,
                Time.deltaTime * followSmoothSpeed
            );
            
            // IMPORTANTE: Mantener el pentagrama VERTICAL (líneas horizontales)
            Vector3 directionToCamera = vrCamera.position - bassStaff.transform.position;
            directionToCamera.y = 0;
            
            if (directionToCamera.magnitude > 0.01f)
            {
                // Calcular rotación para que mire AL jugador
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                // Mantener solo rotación en Y (horizontal)
                targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
                
                bassStaff.transform.rotation = Quaternion.Slerp(
                    bassStaff.transform.rotation,
                    targetRotation,
                    Time.deltaTime * followSmoothSpeed
                );
            }
        }
        */
    }
    
    /// <summary>
    /// Carga la canción seleccionada desde SelectedSongManager
    /// </summary>
    private void LoadSelectedSong()
    {
        if (SelectedSongManager.Instance == null || SelectedSongManager.Instance.selectedSong == null)
        {
            Debug.LogError("<color=red>[PianoGame]</color> No se encontró ninguna canción seleccionada.");
            return;
        }
        
        var selectedSong = SelectedSongManager.Instance.selectedSong;
        
        Debug.Log($"<color=green>[PianoGame]</color> Cargando canción: {selectedSong.title}");
        Debug.Log($"<color=cyan>[PianoGame]</color> Detalles: {selectedSong.artist_name} | {selectedSong.musical_genre} | {selectedSong.tempo} BPM");
        Debug.Log($"<color=cyan>[PianoGame]</color> Archivo: {selectedSong.file_path}");
        
        // Cargar el archivo JSON y el audio
        songLoader.LoadSong(
            selectedSong.file_path,
            onSuccess: OnSongLoaded,
            onError: OnSongLoadError
        );
    }
    
    /// <summary>
    /// Callback cuando la canción se carga exitosamente
    /// </summary>
    private void OnSongLoaded(PianoSongData songData)
    {
        currentSongData = songData;
        
        Debug.Log($"<color=green>[PianoGame]</color> ✅ Canción cargada: {songData.song_title}");
        Debug.Log($"<color=green>[PianoGame]</color> Melodía: {songData.TotalMelodyNotes} notas | Acordes: {songData.TotalChords}");
        Debug.Log($"<color=green>[PianoGame]</color> Mano derecha: {songData.GetRightHandMelody().Count} notas | Mano izquierda: {songData.GetLeftHandMelody().Count} notas");
        
        // Configurar el audio de fondo
        if (songData.backgroundAudioClip != null)
        {
            backgroundMusicSource.clip = songData.backgroundAudioClip;
            Debug.Log($"<color=green>[PianoGame]</color> Audio listo: {songData.backgroundAudioClip.length:F1}s");
        }
        else
        {
            Debug.LogWarning("<color=yellow>[PianoGame]</color> No se cargó audio de fondo");
        }
        
        // Inicializar el pentagrama y las notas visuales
        PrepareGameplay();
    }
    
    /// <summary>
    /// Callback cuando hay un error cargando la canción
    /// </summary>
    private void OnSongLoadError(string error)
    {
        Debug.LogError($"<color=red>[PianoGame]</color> ❌ Error cargando canción: {error}");
        // TODO: Mostrar mensaje al usuario y volver al menú
    }
    
    /// <summary>
    /// Prepara el juego para empezar
    /// </summary>
    private void PrepareGameplay()
    {
        Debug.Log("<color=cyan>[PianoGame]</color> Preparando gameplay...");
        
        // FASE 3: Configurar sistema visual
        LoadSongIntoSpawner();
        SetupCountdown();
        
        gameplayReady = true;
        
        // Log de configuración de follow canvas
        if (enableFollowCanvas)
        {
            Debug.Log($"<color=yellow>[PianoGame]</color> Follow Canvas: ACTIVADO (distancia={staffDistance}m)");
        }
        else
        {
            Debug.Log("<color=green>[PianoGame]</color> Follow Canvas: DESACTIVADO - Usando posiciones manuales de jerarquía");
        }
        
        // Iniciar countdown automáticamente
        Debug.Log("<color=green>[PianoGame]</color> ✅ Todo listo, iniciando countdown...");
        StartCountdownSequence();
    }
    
    // PositionStaffsInVR() ELIMINADA - ahora usa UpdateStaffPositions() en Update()
    
    /// <summary>
    /// Carga la canción en el spawner
    /// </summary>
    private void LoadSongIntoSpawner()
    {
        if (noteSpawner != null && currentSongData != null)
        {
            noteSpawner.LoadSong(currentSongData);
            Debug.Log("[PianoGame] Canción cargada en NoteSpawner");
        }
        else
        {
            Debug.LogWarning("[PianoGame] No se pudo cargar la canción en el spawner");
        }
    }
    
    /// <summary>
    /// Configura el countdown para iniciar el juego
    /// </summary>
    private void SetupCountdown()
    {
        if (countdownManager != null)
        {
            // IMPORTANTE: Desuscribirse primero para evitar suscripciones múltiples
            countdownManager.OnCountdownComplete -= OnCountdownFinished;
            // Suscribirse al evento de finalización del countdown
            countdownManager.OnCountdownComplete += OnCountdownFinished;
            Debug.Log("[PianoGame] Countdown configurado");
        }
        else
        {
            Debug.LogWarning("[PianoGame] No hay CountdownManager asignado");
        }
    }
    
    /// <summary>
    /// Inicia la secuencia de countdown 3-2-1-GO
    /// </summary>
    private void StartCountdownSequence()
    {
        if (countdownManager != null)
        {
            countdownManager.StartCountdown();
        }
        else
        {
            // Si no hay countdown, iniciar directamente
            Debug.LogWarning("[PianoGame] No hay countdown, iniciando juego directamente");
            OnCountdownFinished();
        }
    }
    
    /// <summary>
    /// Se llama cuando el countdown termina
    /// </summary>
    private void OnCountdownFinished()
    {
        Debug.Log("<color=green>[PianoGame]</color> 🎵 ¡Countdown terminado! Iniciando juego...");
        StartGame();
    }
    
    /// <summary>
    /// Inicia la reproducción del juego
    /// </summary>
    public void StartGame()
    {
        // Evitar inicio múltiple
        if (gameStarted)
        {
            Debug.LogWarning("[PianoGame] El juego ya está iniciado, ignorando llamada múltiple");
            return;
        }
        
        if (currentSongData == null)
        {
            Debug.LogError("<color=red>[PianoGame]</color> No hay datos de canción cargados");
            return;
        }
        
        gameStarted = true;
        isPlaying = true;
        gameTime = 0f;
        
        // Reproducir música de fondo
        if (backgroundMusicSource.clip != null)
        {
            backgroundMusicSource.Play();
            Debug.Log("[PianoGame] 🎵 Audio de fondo iniciado");
        }
        
        // Iniciar spawn de notas
        if (noteSpawner != null)
        {
            noteSpawner.StartSpawning();
            Debug.Log("[PianoGame] 🎶 Spawn de notas activado");
        }
        
        Debug.Log("<color=green>[PianoGame]</color> 🎹 ¡Juego iniciado!");
    }
    
    /// <summary>
    /// Pausa el juego
    /// </summary>
    public void PauseGame()
    {
        isPlaying = false;
        
        if (backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Pause();
        }
        
        if (noteSpawner != null)
        {
            noteSpawner.StopSpawning();
        }
        
        Debug.Log("<color=yellow>[PianoGame]</color> ⏸️ Juego pausado");
    }
    
    /// <summary>
    /// Reanuda el juego
    /// </summary>
    public void ResumeGame()
    {
        isPlaying = true;
        
        if (backgroundMusicSource.clip != null && !backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.UnPause();
        }
        
        if (noteSpawner != null)
        {
            noteSpawner.StartSpawning();
        }
        
        Debug.Log("<color=green>[PianoGame]</color> ▶️ Juego reanudado");
    }
    
    /// <summary>
    /// Muestra un acorde detectado en la UI
    /// </summary>
    public void ShowDetectedChord(string chordName, string notes = "")
    {
        if (chordDetectorUI != null)
        {
            chordDetectorUI.ShowChord(chordName, notes);
        }
    }
    
    void OnDestroy()
    {
        // Desuscribirse del evento
        if (countdownManager != null)
        {
            countdownManager.OnCountdownComplete -= OnCountdownFinished;
        }
    }
}
