using UnityEngine;
using System;
using System.Globalization;

public class PianoGameManager : MonoBehaviour
{
    // Singleton para evitar múltiples instancias
    private static PianoGameManager instance;
    public static PianoGameManager Instance => instance;
    public bool CanTogglePause => gameStarted && (isPlaying || isPaused);
    public bool HasGameplayStarted => gameStarted;
    public bool IsReadyToStartGameplay => gameplayReady && !gameStarted && currentSongData != null;
    
    [Header("Referencias")]
    [SerializeField] private PianoSongLoader songLoader;
    [SerializeField] private AudioSource backgroundMusicSource;
    
    // Exponer backgroundMusicSource públicamente para sincronización de notas
    public AudioSource BackgroundMusicSource => backgroundMusicSource;
    
    [Header("Sistema MIDI - Piano en vivo")]
    [SerializeField] private MidiAudioManager midiAudioManager;
    [SerializeField] private DirectMidiReceiver directMidiReceiver;
    
    [Header("Sistema de Gameplay")]
    [SerializeField] private GameplayScoring gameplayScoring;
    [SerializeField] private ResultsPanel resultsPanel;
    [SerializeField] private AuthService authService;
    
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
    public bool isPaused = false;
    public float gameTime = 0f;
    private SongListarResponse selectedSongMetadata;
    private bool gameplayReady = false;
    private bool gameStarted = false; // Bandera para evitar inicio múltiple
    private bool countdownPending = false;
    private bool countdownCompleted = false;
    private float pauseStartTime = 0f;
    private float totalPausedTime = 0f;
    private bool saveAndExitInProgress = false;
    private MIDIConnectionManager midiConnectionManager;
    private PianoPauseMenu pianoPauseMenu;
    private bool waitingForMidiConnectionToStart = false;
    private bool pausedByMidiDisconnect = false;
    private float nextMidiManagerLookupTime = 0f;
    private PianoPublicSystem cachedPublicSystem;

    private const float MidiManagerLookupInterval = 0.5f;
    
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
        AdoptConfiguredReferencesFromScene();
        TryAttachMidiConnectionManager(true);
        TryAttachPauseMenu(true);

        // Suscribirse al evento del PianoCalibrator
        // El juego NO iniciará hasta que el usuario confirme la posición del piano (presione X)
        PianoCalibrator.OnPianoConfigured += OnPianoConfigured_StartGame;
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
        
        // AUTO-DETECTAR SCORING Y RESULTADOS
        if (gameplayScoring == null)
        {
            gameplayScoring = gameObject.AddComponent<GameplayScoring>();
        }
        
        if (resultsPanel == null)
        {
            GameObject endGameUi = FindSceneObjectByName("EndGameUI");
            if (endGameUi != null)
            {
                resultsPanel = endGameUi.GetComponent<ResultsPanel>();
                if (resultsPanel == null)
                {
                    resultsPanel = endGameUi.AddComponent<ResultsPanel>();
                }
            }

            if (resultsPanel == null)
            {
                resultsPanel = FindObjectOfType<ResultsPanel>(true); // Incluir inactivos
            }

            if (resultsPanel == null)
            {
                Debug.LogWarning("[PianoGame] ⚠️ No se encontró ResultsPanel en la escena");
            }
            else
            {
                resultsPanel.HideImmediate();
            }
        }
        
        // AUTO-DETECTAR Y CONECTAR COMPONENTES MIDI
        if (directMidiReceiver == null)
        {
            directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
            if (directMidiReceiver == null)
            {
                Debug.LogWarning("[PianoGame] Creando DirectMidiReceiver...");
                directMidiReceiver = gameObject.AddComponent<DirectMidiReceiver>();
            }
        }
        
        if (midiAudioManager == null)
        {
            midiAudioManager = FindObjectOfType<MidiAudioManager>();
            if (midiAudioManager == null)
            {
                Debug.LogWarning("[PianoGame] Creando MidiAudioManager...");
                midiAudioManager = gameObject.AddComponent<MidiAudioManager>();
            }
        }
        
        // Conectar componentes
        if (midiAudioManager.directMidiReceiver == null)
        {
            midiAudioManager.directMidiReceiver = directMidiReceiver;
        }
        
        // Inicializar sistema de aplausos
        midiAudioManager.InitializeApplauseSystem();
        
        // Conectar GameplayScoring a eventos
        if (gameplayScoring != null)
        {
            gameplayScoring.OnGameFinished += OnGameFinished;
        }
        
        // AUTO-DETECTAR NOTESOAWNER si no está asignado
        if (noteSpawner == null)
        {
            noteSpawner = FindObjectOfType<NoteSpawner>();
            if (noteSpawner == null)
            {
                Debug.LogWarning("[PianoGame] No se encontró NoteSpawner en la escena");
            }
            else
            {
            }
        }
        
        // AUTO-DETECTAR COUNTDOWNMANAGER si no está asignado
        if (countdownManager == null)
        {
            countdownManager = FindObjectOfType<CountdownManager>();
            if (countdownManager == null)
            {
                Debug.LogWarning("[PianoGame] No se encontró CountdownManager en la escena");
            }
            else
            {
            }
        }
        
        // AUTO-DETECTAR STAFFRENDERERS si no están asignados
        if (trebleStaff == null || bassStaff == null)
        {
            StaffRenderer[] staffs = FindObjectsOfType<StaffRenderer>();
            if (staffs.Length >= 2)
            {
                // Asumir que el primero es treble y el segundo es bass
                trebleStaff = staffs[0];
                bassStaff = staffs[1];
            }
        }
        
        // Cargar datos de la canción seleccionada (pero NO iniciar countdown aún)
        LoadSelectedSong();
    }
    
    void Update()
    {
        TryAttachMidiConnectionManager(false);
        TryAttachPauseMenu(false);

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
    /// Carga la canción seleccionada desde SelectedSongManager (que viene del Repertorio)
    /// Lee el archivo JSON especificado en file_path de la BD
    /// </summary>
    private void LoadSelectedSong()
    {
        if (SelectedSongManager.Instance == null || SelectedSongManager.Instance.selectedSong == null)
        {
            Debug.LogError("[PianoGame] No se encontró ninguna canción seleccionada en SelectedSongManager.");
            return;
        }
        
        var selectedSong = SelectedSongManager.Instance.selectedSong;
        selectedSongMetadata = selectedSong;
        
        // Cargar el archivo JSON especificado en file_path
        // El file_path es algo como "songs/rocketman.json"
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
        ApplySelectedSongMetadata(songData);
        currentSongData = songData;
        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Piano JSON listo");
        
        // Configurar el audio de fondo - usar nuevo formato primero (audio_file)
        if (songData.backgroundAudioClip != null)
        {
            backgroundMusicSource.clip = songData.backgroundAudioClip;
            
            // Aplicar volumen del audio de fondo desde JSON
            backgroundMusicSource.volume = songData.audio_file_volume;
        }
        else
        {
            Debug.LogWarning("[PianoGame] No se cargó audio de fondo");
        }
        
        // Aplicar volumen del piano/MIDI desde JSON
        MidiAudioManager midiAudioManager = FindObjectOfType<MidiAudioManager>();
        if (midiAudioManager != null)
        {
            midiAudioManager.SetPianoVolume(songData.piano_volume);
        }
        
        // Inicializar el sistema de scoring
        if (gameplayScoring != null && songData.all_notes != null)
        {
            gameplayScoring.InitializeForSong(songData);
        }
        
        // Inicializar el pentagrama y las notas visuales
        PrepareGameplay();
        SelectedSongManager.Instance?.CompleteSongSelectionMeasurement("Piano listo para iniciar gameplay");
    }
    
    /// <summary>
    /// Callback cuando hay un error cargando la canción
    /// </summary>
    private void OnSongLoadError(string error)
    {
        Debug.LogError($"[PianoGame] Error cargando canción: {error}");
        // TODO: Mostrar mensaje al usuario y volver al menú
    }

    private void ApplySelectedSongMetadata(PianoSongData songData)
    {
        if (songData == null || selectedSongMetadata == null)
        {
            return;
        }

        songData.song_title = string.IsNullOrWhiteSpace(selectedSongMetadata.title)
            ? songData.song_title
            : selectedSongMetadata.title;
        songData.song_name = songData.song_title;
        songData.artist = string.IsNullOrWhiteSpace(selectedSongMetadata.artist_name)
            ? songData.artist
            : selectedSongMetadata.artist_name;
        songData.tempo = selectedSongMetadata.tempo > 0 ? selectedSongMetadata.tempo : songData.tempo;
        songData.duration = selectedSongMetadata.duration > 0 ? selectedSongMetadata.duration : songData.duration;
    }
    
    /// <summary>
    /// Prepara el juego para empezar
    /// </summary>
    private void PrepareGameplay()
    {
        // FASE 3: Configurar sistema visual
        LoadSongIntoSpawner();
        ShowCountdownPreview();
        SetupCountdown();
        countdownPending = false;
        countdownCompleted = false;
        
        gameplayReady = true;
        
        // ⏸️  NO iniciar countdown aquí - esperar a que PianoCalibrator confirme
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
        }
        else
        {
            Debug.LogWarning("[PianoGame] No se pudo cargar la canción en el spawner");
        }
    }

    private void ShowCountdownPreview()
    {
        if (noteSpawner != null)
        {
            noteSpawner.ShowPreviewNotes(0f);
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
        }
        else
        {
            Debug.LogWarning("[PianoGame] No hay CountdownManager asignado");
        }
    }
    
    /// <summary>
    /// Se llama cuando el usuario presiona X en PianoCalibrator
    /// Inicia el countdown y el juego
    /// </summary>
    private void OnPianoConfigured_StartGame()
    {
        // Desuscribirse para evitar múltiples llamadas
        PianoCalibrator.OnPianoConfigured -= OnPianoConfigured_StartGame;
        
        StartCountdownSequence();
    }
    
    /// <summary>
    /// Inicia la secuencia de countdown 3-2-1-GO
    /// </summary>
    private void StartCountdownSequence()
    {
        if (countdownCompleted)
        {
            OnCountdownFinished();
            return;
        }

        if (countdownPending)
        {
            return;
        }

        if (countdownManager != null)
        {
            countdownPending = true;
            SetupCountdown();
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
        countdownPending = false;
        countdownCompleted = true;
        StartGameplayNow();
    }
    
    /// <summary>
    /// Inicia la reproducción del juego
    /// </summary>
    public void StartGame()
    {
        if (!countdownCompleted && countdownManager != null)
        {
            StartCountdownSequence();
            return;
        }

        StartGameplayNow();
    }

    private void StartGameplayNow()
    {
        // Evitar inicio múltiple
        if (gameStarted)
        {
            Debug.LogWarning("[PianoGame] El juego ya está iniciado, ignorando llamada múltiple");
            return;
        }
        
        if (currentSongData == null)
        {
            Debug.LogError("[PianoGame] No hay datos de canción cargados");
            return;
        }

        if (!IsMidiReadyForGameplay())
        {
            waitingForMidiConnectionToStart = true;
            pausedByMidiDisconnect = false;

            Debug.LogWarning("[PianoGame] MIDI no disponible. Esperando reconexión antes de iniciar el gameplay.");
            MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
                "Conecta el controlador MIDI para iniciar la práctica.",
                "Iniciar juego",
                ContinueAfterMidiReconnect);
            return;
        }

        waitingForMidiConnectionToStart = false;
        pausedByMidiDisconnect = false;
        MidiStatusWidgetController.Instance?.ClearGameplayPrompt();
        
        gameStarted = true;
        isPlaying = false;
        isPaused = false;
        gameTime = 0f;
        totalPausedTime = 0f;
        
        // ✅ VERIFICAR QUE MIDI ESTÁ COMPLETAMENTE LISTO
        if (midiAudioManager != null && directMidiReceiver != null)
        {
            if (midiAudioManager.directMidiReceiver == null)
            {
                midiAudioManager.directMidiReceiver = directMidiReceiver;
            }
        }
        
        InitializeAndStartGameplay();
    }
    
    /// <summary>
    /// Comienza el gameplay - AUDIO + SCORING + SPAWN SIMULTÁNEAMENTE
    /// </summary>
    private void InitializeAndStartGameplay()
    {
        isPlaying = true;
        
        // 🎵 Comenzar audio -  ANTES de todo
        if (backgroundMusicSource.clip != null)
        {
            backgroundMusicSource.time = 0f; // Asegurar que comience en 0
            backgroundMusicSource.Play();
        }
        else
        {
            Debug.LogError("[PianoGame] ❌ ¡backgroundMusicSource NO tiene AudioClip asignado!");
        }
        
        // 📊 Comenzar gameplay scoring - EXACTAMENTE AL MISMO TIEMPO
        if (gameplayScoring != null)
        {
            gameplayScoring.StartScoring();
        }
        
        // 🎶 Comenzar spawn de notas - EXACTAMENTE AL MISMO TIEMPO  
        if (noteSpawner != null)
        {
            noteSpawner.StartSpawning();
        }
    }
    
    /// <summary>
    /// Pausa el juego
    /// </summary>
    public void PauseGame()
    {
        if (isPaused)
        {
            Debug.LogWarning("[PianoGame] El juego ya está pausado");
            return;
        }
        
        isPaused = true;
        isPlaying = false;
        pauseStartTime = Time.time;
        
        if (backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Pause();
        }
        
        if (gameplayScoring != null)
        {
            gameplayScoring.PauseScoring();
        }
        
        if (noteSpawner != null)
        {
            noteSpawner.StopSpawning();
        }

        SilenceAudienceApplause();
        
        // TODO: Mostrar UI de pausa
    }
    
    /// <summary>
    /// Reanuda el juego
    /// </summary>
    public void ResumeGame()
    {
        if (!isPaused)
        {
            Debug.LogWarning("[PianoGame] El juego no está pausado");
            return;
        }

        if (!IsMidiReadyForGameplay())
        {
            pausedByMidiDisconnect = true;
            MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
                "Reconecta el controlador MIDI para continuar la práctica.",
                "Continuar juego",
                ContinueAfterMidiReconnect);
            return;
        }
        
        isPaused = false;
        isPlaying = true;
        pausedByMidiDisconnect = false;
        MidiStatusWidgetController.Instance?.ClearGameplayPrompt();
        
        // Contar el tiempo pausado para no afectar la sincronización
        totalPausedTime += Time.time - pauseStartTime;
        
        if (backgroundMusicSource.clip != null && !backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.UnPause();
        }
        
        if (gameplayScoring != null)
        {
            gameplayScoring.ResumeScoring();
        }
        
        if (noteSpawner != null)
        {
            noteSpawner.ResumeSpawning();
        }

        ResumeAudienceApplause();
        
        // TODO: Ocultar UI de pausa
    }
    
    /// <summary>
    /// Se llama cuando el GameplayScoring termina
    /// Muestra el modal de resultados
    /// </summary>
    private void OnGameFinished(GameplayResults results)
    {
        isPlaying = false;
        isPaused = false;
        gameStarted = false;
        waitingForMidiConnectionToStart = false;
        pausedByMidiDisconnect = false;
        MidiStatusWidgetController.Instance?.ClearGameplayPrompt();

        if (selectedSongMetadata != null)
        {
            results.mode_name = selectedSongMetadata.mode;
        }
        
        // Detener la música
        if (backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Stop();
        }
        
        // Detener spawn de notas
        if (noteSpawner != null)
        {
            noteSpawner.StopSpawning();
        }

        SilenceAudienceApplause();

        // Mostrar modal de resultados
        if (resultsPanel != null)
        {
            resultsPanel.ShowResults(results);
        }
        else
        {
            Debug.LogWarning("[PianoGame] No hay ResultsPanel para mostrar resultados");
        }
    }

    public void SaveAndExitToRepertorio(GameplayResults results)
    {
        if (saveAndExitInProgress)
        {
            return;
        }

        saveAndExitInProgress = true;

        if (results == null)
        {
            LoadRepertorioScene();
            return;
        }

        if (UserSession.Instance == null || !UserSession.Instance.IsLoggedIn || selectedSongMetadata == null)
        {
            LoadRepertorioScene();
            return;
        }

        if (authService == null)
        {
            authService = FindObjectOfType<AuthService>(true);
            if (authService == null)
            {
                GameObject runtimeAuthService = new GameObject("AuthService_Runtime");
                authService = runtimeAuthService.AddComponent<AuthService>();
            }
        }

        PracticeSessionRequest practiceRequest = new PracticeSessionRequest
        {
            user_id = UserSession.Instance.userId,
            song_id = selectedSongMetadata.id,
            practice_datetime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            practice_mode = string.IsNullOrWhiteSpace(results.mode_name) ? "PIANO" : results.mode_name,
            rhythm_score = results.rhythm_percentage,
            harmony_score = results.harmony_percentage,
            tuning_score = null
        };

        Debug.Log($"[SessionAudit] Finalizando sesion piano | song={selectedSongMetadata.title} | songId={selectedSongMetadata.id} | mode={practiceRequest.practice_mode}");

        StartCoroutine(authService.SavePracticeSession(
            practiceRequest,
            onSuccess: (_) =>
            {
                Debug.Log($"[SessionAudit] Sesion piano registrada | song={selectedSongMetadata.title} | songId={selectedSongMetadata.id}");
                LoadRepertorioScene();
            },
            onError: (error) =>
            {
                Debug.LogError($"[SessionAudit] Error registrando sesion piano | songId={selectedSongMetadata.id} | detalle={error}");
                Debug.LogWarning($"[PianoGame] No se pudo guardar la sesión de práctica: {error}");
                LoadRepertorioScene();
            }
        ));
    }

    private void LoadRepertorioScene()
    {
        saveAndExitInProgress = false;
        SilenceAudienceApplause();
        UnityEngine.SceneManagement.SceneManager.LoadScene("RepertorioScene");
    }

    public void PrepareForSceneExit()
    {
        SilenceAudienceApplause();

        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.Stop();
        }

        if (noteSpawner != null)
        {
            noteSpawner.StopSpawning();
        }

        if (gameplayScoring != null)
        {
            gameplayScoring.PauseScoring();
        }

        isPlaying = false;
        isPaused = false;
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
        PianoCalibrator.OnPianoConfigured -= OnPianoConfigured_StartGame;

        if (midiConnectionManager != null)
        {
            midiConnectionManager.OnMidiConnectionChanged -= HandleMidiConnectionChanged;
        }

        if (countdownManager != null)
        {
            countdownManager.OnCountdownComplete -= OnCountdownFinished;
        }

        if (gameplayScoring != null)
        {
            gameplayScoring.OnGameFinished -= OnGameFinished;
        }

        SilenceAudienceApplause();
        MidiStatusWidgetController.Instance?.ClearGameplayPrompt();
    }

    public void ContinueAfterMidiReconnect()
    {
        if (!IsMidiReadyForGameplay())
        {
            MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
                waitingForMidiConnectionToStart
                    ? "Conecta el controlador MIDI para iniciar la práctica."
                    : "Reconecta el controlador MIDI para continuar la práctica.",
                waitingForMidiConnectionToStart ? "Iniciar juego" : "Continuar juego",
                ContinueAfterMidiReconnect);
            return;
        }

        if (waitingForMidiConnectionToStart)
        {
            waitingForMidiConnectionToStart = false;
            StartGameplayNow();
            return;
        }

        if (isPaused && pausedByMidiDisconnect)
        {
            if (pianoPauseMenu != null)
            {
                pianoPauseMenu.HidePauseMenu();
            }

            ResumeGame();
        }
    }

    private void TryAttachMidiConnectionManager(bool forceLookup)
    {
        if (!forceLookup && Time.unscaledTime < nextMidiManagerLookupTime)
        {
            return;
        }

        nextMidiManagerLookupTime = Time.unscaledTime + MidiManagerLookupInterval;

        if (midiConnectionManager == null)
        {
            midiConnectionManager = FindObjectOfType<MIDIConnectionManager>();
            if (midiConnectionManager != null)
            {
                midiConnectionManager.OnMidiConnectionChanged -= HandleMidiConnectionChanged;
                midiConnectionManager.OnMidiConnectionChanged += HandleMidiConnectionChanged;
            }
        }
    }

    private void TryAttachPauseMenu(bool forceLookup)
    {
        if (pianoPauseMenu != null)
        {
            return;
        }

        if (!forceLookup && Time.unscaledTime < nextMidiManagerLookupTime)
        {
            return;
        }

        pianoPauseMenu = FindObjectOfType<PianoPauseMenu>(true);
    }

    private void HandleMidiConnectionChanged(bool isConnected)
    {
        if (isConnected)
        {
            if (waitingForMidiConnectionToStart || pausedByMidiDisconnect)
            {
                MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
                    waitingForMidiConnectionToStart
                        ? "MIDI detectado. Ya puedes iniciar la práctica."
                        : "MIDI reconectado. Ya puedes continuar la práctica.",
                    waitingForMidiConnectionToStart ? "Iniciar juego" : "Continuar juego",
                    ContinueAfterMidiReconnect);
            }
            return;
        }

        if (waitingForMidiConnectionToStart)
        {
            MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
                "Conecta el controlador MIDI para iniciar la práctica.",
                "Iniciar juego",
                ContinueAfterMidiReconnect);
            return;
        }

        if (isPlaying)
        {
            pausedByMidiDisconnect = true;
            if (pianoPauseMenu != null)
            {
                pianoPauseMenu.ShowPauseMenu();
            }
            else
            {
                PauseGame();
            }

            MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
                "El controlador MIDI se desconectó. Reconéctalo para continuar la práctica.",
                "Continuar juego",
                ContinueAfterMidiReconnect);
        }
    }

    private bool IsMidiReadyForGameplay()
    {
        if (midiConnectionManager != null)
        {
            return midiConnectionManager.IsMidiConnected;
        }

        return directMidiReceiver != null && directMidiReceiver.IsMidiConnected;
    }

    private PianoPublicSystem ResolvePublicSystem()
    {
        if (cachedPublicSystem == null)
        {
            cachedPublicSystem = FindObjectOfType<PianoPublicSystem>();
        }

        return cachedPublicSystem;
    }

    private void SilenceAudienceApplause()
    {
        if (midiAudioManager == null)
        {
            midiAudioManager = FindObjectOfType<MidiAudioManager>();
        }

        if (midiAudioManager == null)
        {
            return;
        }

        midiAudioManager.SetApplauseVolume(0f);
        midiAudioManager.StopApplauseLoop();
    }

    private void ResumeAudienceApplause()
    {
        if (midiAudioManager == null)
        {
            midiAudioManager = FindObjectOfType<MidiAudioManager>();
        }

        if (midiAudioManager == null || !gameStarted || !isPlaying)
        {
            return;
        }

        midiAudioManager.InitializeApplauseSystem();
        midiAudioManager.StartApplauseLoop();

        PianoPublicSystem publicSystem = ResolvePublicSystem();
        if (publicSystem != null)
        {
            midiAudioManager.SetApplauseVolume(publicSystem.GetCurrentPublicScoreForApplause());
        }
    }

    private void AdoptConfiguredReferencesFromScene()
    {
        PianoGameManager[] managers = Resources.FindObjectsOfTypeAll<PianoGameManager>();
        PianoGameManager bestConfiguredManager = null;
        int bestScore = -1;

        foreach (PianoGameManager manager in managers)
        {
            if (manager == null || manager == this || !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            int score = 0;
            if (manager.countdownManager != null) score += 4;
            if (manager.noteSpawner != null) score += 3;
            if (manager.trebleStaff != null) score += 2;
            if (manager.bassStaff != null) score += 2;
            if (manager.vrCamera != null) score += 1;
            if (manager.chordDetectorUI != null) score += 1;

            if (score > bestScore)
            {
                bestScore = score;
                bestConfiguredManager = manager;
            }
        }

        if (bestConfiguredManager == null || bestScore <= 0)
        {
            return;
        }

        if (countdownManager == null) countdownManager = bestConfiguredManager.countdownManager;
        if (noteSpawner == null) noteSpawner = bestConfiguredManager.noteSpawner;
        if (trebleStaff == null) trebleStaff = bestConfiguredManager.trebleStaff;
        if (bassStaff == null) bassStaff = bestConfiguredManager.bassStaff;
        if (chordDetectorUI == null) chordDetectorUI = bestConfiguredManager.chordDetectorUI;
        if (vrCamera == null) vrCamera = bestConfiguredManager.vrCamera;
        if (backgroundMusicSource == null) backgroundMusicSource = bestConfiguredManager.backgroundMusicSource;
        if (songLoader == null) songLoader = bestConfiguredManager.songLoader;
        if (midiAudioManager == null) midiAudioManager = bestConfiguredManager.midiAudioManager;
        if (directMidiReceiver == null) directMidiReceiver = bestConfiguredManager.directMidiReceiver;
        if (gameplayScoring == null) gameplayScoring = bestConfiguredManager.gameplayScoring;
        if (resultsPanel == null) resultsPanel = bestConfiguredManager.resultsPanel;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject.scene.IsValid())
            {
                continue;
            }

            if ((candidate.hideFlags & HideFlags.NotEditable) != 0 || (candidate.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                continue;
            }

            return candidateObject;
        }

        return null;
    }
}
