using UnityEngine;

/// <summary>
/// Orquestador del modo piano: resuelve referencias de escena, carga la canción y
/// coordina countdown, audio, scoring y spawn de notas.
/// Se reparte en archivos parciales: Gameplay y MidiGate.
/// </summary>
public partial class PianoGameManager : MonoBehaviour
{
    private static PianoGameManager instance;
    public static PianoGameManager Instance => instance;

    [Header("Referencias")]
    [SerializeField] private PianoSongLoader songLoader;
    [SerializeField] private AudioSource backgroundMusicSource;

    [Header("Sistema MIDI - Piano en vivo")]
    [SerializeField] private MidiAudioManager midiAudioManager;
    [SerializeField] private DirectMidiReceiver directMidiReceiver;

    [Header("Sistema de Gameplay")]
    [SerializeField] private GameplayScoring gameplayScoring;
    [SerializeField] private ResultsPanel resultsPanel;
    [SerializeField] private AuthService authService;

    [Header("Sistema Visual")]
    [SerializeField] private CountdownManager countdownManager;
    [Tooltip("Pentagrama de clave de Sol (arriba)")]
    [SerializeField] private StaffRenderer trebleStaff;
    [Tooltip("Pentagrama de clave de Fa (abajo)")]
    [SerializeField] private StaffRenderer bassStaff;
    [SerializeField] private NoteSpawner noteSpawner;
    [SerializeField] private ChordDetectorUI chordDetectorUI;

    [Header("VR")]
    [SerializeField] private Transform vrCamera;

    [Header("Estado del Juego")]
    public PianoSongData currentSongData;
    public bool isPlaying = false;
    public bool isPaused = false;
    public float gameTime = 0f;

    private SongListarResponse selectedSongMetadata;
    private bool gameplayReady = false;
    private bool gameStarted = false;
    private bool countdownPending = false;
    private bool countdownCompleted = false;
    private bool saveAndExitInProgress = false;
    private MIDIConnectionManager midiConnectionManager;
    private PianoPauseMenu pianoPauseMenu;
    private bool waitingForMidiConnectionToStart = false;
    private bool pausedByMidiDisconnect = false;
    private float nextMidiManagerLookupTime = 0f;
    private PianoPublicSystem cachedPublicSystem;

    private const float MidiManagerLookupInterval = 0.5f;

    /// <summary>Audio de fondo opcional. Si no hay clip, el reloj del juego usa <see cref="gameTime"/>.</summary>
    public AudioSource BackgroundMusicSource => backgroundMusicSource;

    /// <summary>
    /// Tiempo de reproducción de la canción: prioriza el AudioSource si hay pista;
    /// si no (modo MIDI puro), usa el reloj interno que sí respeta pausa.
    /// </summary>
    public float GetSongPlaybackTime()
    {
        if (backgroundMusicSource != null && backgroundMusicSource.clip != null &&
            (backgroundMusicSource.isPlaying || (isPaused && backgroundMusicSource.time > 0f)))
            return backgroundMusicSource.time;

        return gameTime;
    }

    public bool CanTogglePause => gameStarted && (isPlaying || isPaused);
    public bool HasGameplayStarted => gameStarted;
    public bool IsReadyToStartGameplay => gameplayReady && !gameStarted && currentSongData != null;

    void Awake()
    {
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

        // El gameplay no arranca hasta que el jugador confirme el área del piano.
        PianoCalibrator.OnPianoConfigured += OnPianoConfigured_StartGame;

        if (vrCamera == null)
        {
            vrCamera = Camera.main?.transform;
            if (vrCamera == null) Debug.LogError("[PianoGame] No se encontró cámara VR!");
        }

        if (songLoader == null) songLoader = gameObject.AddComponent<PianoSongLoader>();

        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = gameObject.AddComponent<AudioSource>();
            backgroundMusicSource.playOnAwake = false;
            backgroundMusicSource.loop = false;
        }

        if (gameplayScoring == null) gameplayScoring = gameObject.AddComponent<GameplayScoring>();

        if (resultsPanel == null) ResolveResultsPanel();

        ResolveMidiComponents();

        if (midiAudioManager.directMidiReceiver == null)
            midiAudioManager.directMidiReceiver = directMidiReceiver;

        midiAudioManager.InitializeApplauseSystem();

        if (gameplayScoring != null) gameplayScoring.OnGameFinished += OnGameFinished;

        if (noteSpawner == null)
        {
            noteSpawner = FindObjectOfType<NoteSpawner>();
            if (noteSpawner == null) Debug.LogWarning("[PianoGame] No se encontró NoteSpawner en la escena");
        }

        if (countdownManager == null)
        {
            countdownManager = FindObjectOfType<CountdownManager>();
            if (countdownManager == null) Debug.LogWarning("[PianoGame] No se encontró CountdownManager en la escena");
        }

        if (trebleStaff == null || bassStaff == null)
        {
            StaffRenderer[] staffs = FindObjectsOfType<StaffRenderer>();
            if (staffs.Length >= 2)
            {
                trebleStaff = staffs[0];
                bassStaff = staffs[1];
            }
        }

        LoadSelectedSong();
    }

    void Update()
    {
        TryAttachMidiConnectionManager(false);
        TryAttachPauseMenu(false);

        if (isPlaying) gameTime += Time.deltaTime;
    }

    void OnDestroy()
    {
        PianoCalibrator.OnPianoConfigured -= OnPianoConfigured_StartGame;

        if (midiConnectionManager != null)
            midiConnectionManager.OnMidiConnectionChanged -= HandleMidiConnectionChanged;

        if (countdownManager != null) countdownManager.OnCountdownComplete -= OnCountdownFinished;
        if (gameplayScoring != null) gameplayScoring.OnGameFinished -= OnGameFinished;

        SilenceAudienceApplause();
        MidiStatusWidgetController.Instance?.ClearGameplayPrompt();
    }

    private void ResolveResultsPanel()
    {
        GameObject endGameUi = FindSceneObjectByName("EndGameUI");
        if (endGameUi != null)
            resultsPanel = endGameUi.GetComponent<ResultsPanel>() ?? endGameUi.AddComponent<ResultsPanel>();

        // Se buscan también los inactivos: el panel de resultados nace oculto.
        if (resultsPanel == null) resultsPanel = FindObjectOfType<ResultsPanel>(true);

        if (resultsPanel == null) Debug.LogWarning("[PianoGame] No se encontró ResultsPanel en la escena");
        else resultsPanel.HideImmediate();
    }

    private void ResolveMidiComponents()
    {
        if (directMidiReceiver == null)
        {
            directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
            if (directMidiReceiver == null)
            {
                Debug.LogWarning("[PianoGame] Creando DirectMidiReceiver...");
                directMidiReceiver = gameObject.AddComponent<DirectMidiReceiver>();
            }
        }

        if (midiAudioManager != null) return;

        midiAudioManager = FindObjectOfType<MidiAudioManager>();
        if (midiAudioManager == null)
        {
            Debug.LogWarning("[PianoGame] Creando MidiAudioManager...");
            midiAudioManager = gameObject.AddComponent<MidiAudioManager>();
        }
    }

    /// <summary>
    /// Hereda las referencias del manager mejor configurado de la escena. Existe porque
    /// puede haber managers duplicados y solo uno tiene el cableado completo del inspector.
    /// </summary>
    private void AdoptConfiguredReferencesFromScene()
    {
        PianoGameManager bestConfiguredManager = null;
        int bestScore = -1;

        foreach (PianoGameManager manager in Resources.FindObjectsOfTypeAll<PianoGameManager>())
        {
            if (manager == null || manager == this || !manager.gameObject.scene.IsValid()) continue;

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

        if (bestConfiguredManager == null || bestScore <= 0) return;

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

    /// <summary>Busca por nombre un objeto real de la escena, descartando internos del editor.</summary>
    private GameObject FindSceneObjectByName(string objectName)
    {
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || candidate.name != objectName) continue;

            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject.scene.IsValid()) continue;

            if ((candidate.hideFlags & HideFlags.NotEditable) != 0
                || (candidate.hideFlags & HideFlags.HideAndDontSave) != 0) continue;

            return candidateObject;
        }

        return null;
    }

    /// <summary>Muestra un acorde detectado en la UI.</summary>
    public void ShowDetectedChord(string chordName, string notes = "")
    {
        if (chordDetectorUI != null) chordDetectorUI.ShowChord(chordName, notes);
    }
}
