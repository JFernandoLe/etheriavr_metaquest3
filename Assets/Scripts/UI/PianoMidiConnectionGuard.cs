using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PianoMidiConnectionGuard : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject blockingPanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI detailText;
    [SerializeField] private Button retryButton;
    [SerializeField] private PianoGameManager pianoGameManager;

    private MIDIConnectionManager midiManager;
    private bool midiSubscribed = false;
    private bool pausedByDisconnect = false;
    private bool pendingStartAfterReconnect = false;
    private float nextSearchTime = 0f;

    void Start()
    {
        if (pianoGameManager == null)
        {
            pianoGameManager = FindObjectOfType<PianoGameManager>();
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryConnection);
            retryButton.onClick.AddListener(RetryConnection);
        }

        if (blockingPanel != null)
        {
            blockingPanel.SetActive(false);
        }

        TrySubscribeToMidiManager();
        RefreshVisualState();
    }

    void Update()
    {
        if (!midiSubscribed && Time.unscaledTime >= nextSearchTime)
        {
            nextSearchTime = Time.unscaledTime + 0.5f;
            TrySubscribeToMidiManager();
            RefreshVisualState();
        }

        if (midiManager != null && !midiManager.IsMidiConnected && pianoGameManager != null && pianoGameManager.currentSongData != null)
        {
            bool shouldShowPanel = blockingPanel != null && !blockingPanel.activeSelf;
            bool canInterrupt = pianoGameManager.HasGameplayStarted || pianoGameManager.IsReadyToStartGameplay;

            if (shouldShowPanel && canInterrupt)
            {
                HandleMidiConnectionChanged(false);
            }
        }
    }

    public void RetryConnection()
    {
        TrySubscribeToMidiManager();
        bool isConnected = midiManager != null && midiManager.IsMidiConnected;

        if (!isConnected)
        {
            ShowBlockingPanel(
                "MIDI desconectado",
                "Reconecta el teclado MIDI y vuelve a presionar Reintentar.");
            return;
        }

        if (pendingStartAfterReconnect && pianoGameManager != null && pianoGameManager.IsReadyToStartGameplay)
        {
            pendingStartAfterReconnect = false;
            HideBlockingPanel();
            pianoGameManager.StartGame();
            return;
        }

        if (pausedByDisconnect && pianoGameManager != null && pianoGameManager.isPaused)
        {
            pausedByDisconnect = false;
            HideBlockingPanel();
            pianoGameManager.ResumeGame();
            return;
        }

        HideBlockingPanel();
    }

    private void HandleMidiConnectionChanged(bool isConnected)
    {
        if (!isConnected)
        {
            bool canBlock = pianoGameManager != null &&
                (pianoGameManager.HasGameplayStarted || pianoGameManager.IsReadyToStartGameplay || pianoGameManager.currentSongData != null);

            if (!canBlock)
            {
                return;
            }

            pendingStartAfterReconnect = pianoGameManager != null && !pianoGameManager.HasGameplayStarted;

            if (pianoGameManager != null && pianoGameManager.isPlaying)
            {
                pianoGameManager.PauseGame();
                pausedByDisconnect = true;
            }

            ShowBlockingPanel(
                "MIDI desconectado",
                "Reconecta el teclado MIDI y presiona Reintentar para continuar.");
            return;
        }

        if (blockingPanel != null && blockingPanel.activeSelf)
        {
            ShowBlockingPanel(
                "MIDI reconectado",
                "Presiona Reintentar para continuar la práctica.");
        }
    }

    private void RefreshVisualState()
    {
        bool isConnected = midiManager != null && midiManager.IsMidiConnected;
        if (!isConnected && pianoGameManager != null && pianoGameManager.currentSongData != null)
        {
            HandleMidiConnectionChanged(false);
        }
    }

    private void TrySubscribeToMidiManager()
    {
        midiManager = MIDIConnectionManager.Instance ?? FindObjectOfType<MIDIConnectionManager>();
        if (midiManager == null)
        {
            return;
        }

        if (!midiSubscribed)
        {
            midiManager.OnMidiConnectionChanged += HandleMidiConnectionChanged;
            midiSubscribed = true;
        }
    }

    private void ShowBlockingPanel(string title, string detail)
    {
        if (blockingPanel != null)
        {
            blockingPanel.SetActive(true);
        }

        if (statusText != null)
        {
            statusText.text = title;
        }

        if (detailText != null)
        {
            detailText.text = detail;
        }
    }

    private void HideBlockingPanel()
    {
        if (blockingPanel != null)
        {
            blockingPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (midiSubscribed && midiManager != null)
        {
            midiManager.OnMidiConnectionChanged -= HandleMidiConnectionChanged;
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryConnection);
        }
    }
}