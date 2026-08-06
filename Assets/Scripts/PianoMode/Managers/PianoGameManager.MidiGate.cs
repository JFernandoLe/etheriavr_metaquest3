using UnityEngine;

/// <summary>
/// Puerta de entrada MIDI: el gameplay solo arranca (y solo continúa) con el
/// controlador conectado. También centraliza el control de los aplausos del público.
/// </summary>
public partial class PianoGameManager
{
    /// <summary>Reintento manual desde el widget de estado MIDI.</summary>
    public void ContinueAfterMidiReconnect()
    {
        if (!IsMidiReadyForGameplay())
        {
            PromptForMidi(waitingForMidiConnectionToStart
                ? "Conecta el controlador MIDI para iniciar la práctica."
                : "Reconecta el controlador MIDI para continuar la práctica.");
            return;
        }

        if (waitingForMidiConnectionToStart)
        {
            waitingForMidiConnectionToStart = false;
            StartGameplayNow();
            return;
        }

        if (!isPaused || !pausedByMidiDisconnect) return;

        if (pianoPauseMenu != null) pianoPauseMenu.HidePauseMenu();
        ResumeGame();
    }

    /// <summary>Muestra el aviso de reconexión con el texto de botón acorde al momento del flujo.</summary>
    private void PromptForMidi(string message)
    {
        MidiStatusWidgetController.Instance?.PromptGameplayReconnect(
            message,
            waitingForMidiConnectionToStart ? "Iniciar juego" : "Continuar juego",
            ContinueAfterMidiReconnect);
    }

    private void TryAttachMidiConnectionManager(bool forceLookup)
    {
        if (!forceLookup && Time.unscaledTime < nextMidiManagerLookupTime) return;

        nextMidiManagerLookupTime = Time.unscaledTime + MidiManagerLookupInterval;

        if (midiConnectionManager != null) return;

        midiConnectionManager = FindObjectOfType<MIDIConnectionManager>();
        if (midiConnectionManager == null) return;

        midiConnectionManager.OnMidiConnectionChanged -= HandleMidiConnectionChanged;
        midiConnectionManager.OnMidiConnectionChanged += HandleMidiConnectionChanged;
    }

    private void TryAttachPauseMenu(bool forceLookup)
    {
        if (pianoPauseMenu != null) return;
        if (!forceLookup && Time.unscaledTime < nextMidiManagerLookupTime) return;

        pianoPauseMenu = FindObjectOfType<PianoPauseMenu>(true);
    }

    private void HandleMidiConnectionChanged(bool isConnected)
    {
        if (isConnected)
        {
            if (waitingForMidiConnectionToStart || pausedByMidiDisconnect)
            {
                PromptForMidi(waitingForMidiConnectionToStart
                    ? "MIDI detectado. Ya puedes iniciar la práctica."
                    : "MIDI reconectado. Ya puedes continuar la práctica.");
            }
            return;
        }

        if (waitingForMidiConnectionToStart)
        {
            PromptForMidi("Conecta el controlador MIDI para iniciar la práctica.");
            return;
        }

        if (!isPlaying) return;

        // Desconexión en plena partida: se pausa y se pide reconectar.
        pausedByMidiDisconnect = true;
        if (pianoPauseMenu != null) pianoPauseMenu.ShowPauseMenu();
        else PauseGame();

        PromptForMidi("El controlador MIDI se desconectó. Reconéctalo para continuar la práctica.");
    }

    private bool IsMidiReadyForGameplay() => midiConnectionManager != null
        ? midiConnectionManager.IsMidiConnected
        : directMidiReceiver != null && directMidiReceiver.IsMidiConnected;

    private PianoPublicSystem ResolvePublicSystem()
    {
        if (cachedPublicSystem == null) cachedPublicSystem = FindObjectOfType<PianoPublicSystem>();
        return cachedPublicSystem;
    }

    private void SilenceAudienceApplause()
    {
        if (midiAudioManager == null) midiAudioManager = FindObjectOfType<MidiAudioManager>();
        if (midiAudioManager == null) return;

        midiAudioManager.SetApplauseVolume(0f);
        midiAudioManager.StopApplauseLoop();
    }

    private void ResumeAudienceApplause()
    {
        if (midiAudioManager == null) midiAudioManager = FindObjectOfType<MidiAudioManager>();
        if (midiAudioManager == null || !gameStarted || !isPlaying) return;

        midiAudioManager.InitializeApplauseSystem();
        midiAudioManager.StartApplauseLoop();

        PianoPublicSystem publicSystem = ResolvePublicSystem();
        if (publicSystem != null)
            midiAudioManager.SetApplauseVolume(publicSystem.GetCurrentPublicScoreForApplause());
    }
}
