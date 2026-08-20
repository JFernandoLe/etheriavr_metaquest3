using UnityEngine;
using System;
using System.Globalization;

/// <summary>
/// Flujo de la partida: carga de la canción seleccionada, countdown, arranque
/// sincronizado de audio/scoring/spawn, pausa, cierre y guardado de la sesión.
/// </summary>
public partial class PianoGameManager
{
    /// <summary>Carga el JSON indicado en el file_path de la canción elegida en el repertorio.</summary>
    private void LoadSelectedSong()
    {
        if (SelectedSongManager.Instance == null || SelectedSongManager.Instance.selectedSong == null)
        {
            Debug.LogError("[PianoGame] No se encontró ninguna canción seleccionada en SelectedSongManager.");
            return;
        }

        SongListarResponse selectedSong = SelectedSongManager.Instance.selectedSong;
        selectedSongMetadata = selectedSong;
        songLoader.LoadSong(selectedSong.file_path, onSuccess: OnSongLoaded, onError: OnSongLoadError);
    }

    private void OnSongLoaded(PianoSongData songData)
    {
        ApplySelectedSongMetadata(songData);
        currentSongData = songData;
        SelectedSongManager.Instance?.LogSongSelectionCheckpoint("Piano JSON listo");

        if (songData.backgroundAudioClip != null)
        {
            backgroundMusicSource.clip = songData.backgroundAudioClip;
            backgroundMusicSource.volume = songData.audio_file_volume;
        }
        else
        {
            Debug.LogWarning("[PianoGame] No se cargó audio de fondo");
        }

        MidiAudioManager sceneMidiAudioManager = FindObjectOfType<MidiAudioManager>();
        if (sceneMidiAudioManager != null) sceneMidiAudioManager.SetPianoVolume(songData.piano_volume);

        if (gameplayScoring != null && songData.all_notes != null) gameplayScoring.InitializeForSong(songData);

        PrepareGameplay();
        SelectedSongManager.Instance?.CompleteSongSelectionMeasurement("Piano listo para iniciar gameplay");
    }

    private void OnSongLoadError(string error) => Debug.LogError($"[PianoGame] Error cargando canción: {error}");

    /// <summary>Los metadatos de la BD tienen prioridad sobre los del JSON cuando están presentes.</summary>
    private void ApplySelectedSongMetadata(PianoSongData songData)
    {
        if (songData == null || selectedSongMetadata == null) return;

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

    /// <summary>Deja todo listo, pero sin arrancar: falta que el jugador confirme el área del piano.</summary>
    private void PrepareGameplay()
    {
        LoadSongIntoSpawner();

        if (noteSpawner != null) noteSpawner.ShowPreviewNotes(0f);

        SetupCountdown();
        countdownPending = false;
        countdownCompleted = false;
        gameplayReady = true;
    }

    private void LoadSongIntoSpawner()
    {
        if (noteSpawner != null && currentSongData != null) noteSpawner.LoadSong(currentSongData);
        else Debug.LogWarning("[PianoGame] No se pudo cargar la canción en el spawner");
    }

    // Se desuscribe antes de suscribir para evitar dobles invocaciones.
    private void SetupCountdown()
    {
        if (countdownManager == null)
        {
            Debug.LogWarning("[PianoGame] No hay CountdownManager asignado");
            return;
        }

        countdownManager.OnCountdownComplete -= OnCountdownFinished;
        countdownManager.OnCountdownComplete += OnCountdownFinished;
    }

    private void OnPianoConfigured_StartGame()
    {
        PianoCalibrator.OnPianoConfigured -= OnPianoConfigured_StartGame;
        StartCountdownSequence();
    }

    private void StartCountdownSequence()
    {
        if (countdownCompleted)
        {
            OnCountdownFinished();
            return;
        }

        if (countdownPending) return;

        if (countdownManager == null)
        {
            Debug.LogWarning("[PianoGame] No hay countdown, iniciando juego directamente");
            OnCountdownFinished();
            return;
        }

        countdownPending = true;
        SetupCountdown();
        countdownManager.StartCountdown();
    }

    private void OnCountdownFinished()
    {
        countdownPending = false;
        countdownCompleted = true;
        StartGameplayNow();
    }

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

        if (midiAudioManager != null && directMidiReceiver != null && midiAudioManager.directMidiReceiver == null)
            midiAudioManager.directMidiReceiver = directMidiReceiver;

        InitializeAndStartGameplay();
    }

    /// <summary>Audio, scoring y spawn deben arrancar en el mismo frame para no desincronizar.</summary>
    private void InitializeAndStartGameplay()
    {
        isPlaying = true;

        if (backgroundMusicSource.clip != null)
        {
            backgroundMusicSource.time = 0f;
            backgroundMusicSource.Play();
        }
        else
        {
            Debug.LogError("[PianoGame] backgroundMusicSource no tiene AudioClip asignado!");
        }

        if (gameplayScoring != null) gameplayScoring.StartScoring();
        if (noteSpawner != null) noteSpawner.StartSpawning();
    }

    public void PauseGame()
    {
        if (isPaused)
        {
            Debug.LogWarning("[PianoGame] El juego ya está pausado");
            return;
        }

        isPaused = true;
        isPlaying = false;

        if (backgroundMusicSource.isPlaying) backgroundMusicSource.Pause();
        if (gameplayScoring != null) gameplayScoring.PauseScoring();
        if (noteSpawner != null) noteSpawner.StopSpawning();

        SilenceAudienceApplause();
    }

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

        if (backgroundMusicSource.clip != null && !backgroundMusicSource.isPlaying) backgroundMusicSource.UnPause();
        if (gameplayScoring != null) gameplayScoring.ResumeScoring();
        if (noteSpawner != null) noteSpawner.ResumeSpawning();

        ResumeAudienceApplause();
    }

    private void OnGameFinished(GameplayResults results)
    {
        isPlaying = false;
        isPaused = false;
        gameStarted = false;
        waitingForMidiConnectionToStart = false;
        pausedByMidiDisconnect = false;
        MidiStatusWidgetController.Instance?.ClearGameplayPrompt();

        if (selectedSongMetadata != null) results.mode_name = selectedSongMetadata.mode;

        if (backgroundMusicSource.isPlaying) backgroundMusicSource.Stop();
        if (noteSpawner != null) noteSpawner.StopSpawning();

        SilenceAudienceApplause();

        if (resultsPanel != null) resultsPanel.ShowResults(results);
        else Debug.LogWarning("[PianoGame] No hay ResultsPanel para mostrar resultados");
    }

    /// <summary>
    /// Registra la sesión de práctica en el backend y vuelve al repertorio.
    /// Un fallo al guardar no bloquea la salida.
    /// </summary>
    public void SaveAndExitToRepertorio(GameplayResults results)
    {
        if (saveAndExitInProgress) return;

        saveAndExitInProgress = true;

        bool canSave = results != null
                       && UserSession.Instance != null
                       && UserSession.Instance.IsLoggedIn
                       && selectedSongMetadata != null;

        if (!canSave)
        {
            LoadRepertorioScene();
            return;
        }

        if (authService == null)
        {
            authService = FindObjectOfType<AuthService>(true);
            if (authService == null)
                authService = new GameObject("AuthService_Runtime").AddComponent<AuthService>();
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

        StartCoroutine(authService.SavePracticeSession(
            practiceRequest,
            onSuccess: _ => LoadRepertorioScene(),
            onError: error =>
            {
                Debug.LogWarning($"[PianoGame] No se pudo guardar la sesión de práctica: {error}");
                LoadRepertorioScene();
            }));
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

        if (backgroundMusicSource != null) backgroundMusicSource.Stop();
        if (noteSpawner != null) noteSpawner.StopSpawning();
        if (gameplayScoring != null) gameplayScoring.PauseScoring();

        isPlaying = false;
        isPaused = false;
    }
}
