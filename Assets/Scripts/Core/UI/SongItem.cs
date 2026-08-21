using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SongItem : MonoBehaviour
{
    [Header("UI de Información")]
    public TMP_Text txtTituloArtista;
    public TMP_Text txtTonalidadTempo;
    public TMP_Text txtDetalles;
    public TMP_Text txtAdvertencia;

    [Header("Botón de Acción")]
    [SerializeField] private Button btnJugar;

    private SongListarResponse _myFullData;
    private string _customSongId;
    private Action<string, string> _onDeleteCustomSong;

    private bool _playLayoutCached;
    private RectTransform _playRect;
    private Vector2 _defaultPlayAnchoredPos;
    private Vector2 _defaultPlaySizeDelta;
    private Vector2 _defaultPlayAnchorMin;
    private Vector2 _defaultPlayAnchorMax;
    private Vector2 _defaultPlayPivot;
    private float _defaultInfoStackWidth = -1f;

    public string SongMode => _myFullData?.mode;
    public bool IsPianoMode => string.Equals(_myFullData?.mode, "PIANO", StringComparison.OrdinalIgnoreCase);

    public void Setup(SongListarResponse data)
    {
        _myFullData = data;
        _customSongId = null;
        _onDeleteCustomSong = null;

        EnsurePlayButtonReference();
        RestoreDefaultPlayLayout();
        RemoveCustomDeleteButton();

        if (txtTituloArtista != null)
            txtTituloArtista.text = data.is_custom ? data.title : $"{data.title} - {data.artist_name}";

        if (txtTonalidadTempo != null)
        {
            string tempoText = data.tempo > 0 ? $"{data.tempo} BPM" : "—";
            txtTonalidadTempo.text = $"Tonalidad: {data.musical_key} | Tempo: {tempoText}\n";
        }

        if (txtDetalles != null)
        {
            string modeLabel = data.is_custom ? "Cancion personalizada" : data.mode;
            txtDetalles.text = $"Duracion: {FormatDuration(data.duration)} | Modo: {modeLabel}";
        }

        VerificarCompatibilidadVocal(data.musical_key, data.mode);

        if (btnJugar == null)
            return;

        btnJugar.gameObject.SetActive(true);
        SetButtonLabel(btnJugar, "INICIAR");
        HomeMenuAddSongsBootstrap.SetButtonClick(btnJugar, CargarCancionEnJuego);
    }

    public void ConfigureCustomSongActions(string customSongId, Action<string, string> onDelete)
    {
        _customSongId = customSongId;
        _onDeleteCustomSong = onDelete;

        EnsurePlayButtonReference();
        if (btnJugar == null)
            return;

        CacheDefaultPlayLayout();
        RemoveCustomDeleteButton();

        SetButtonLabel(btnJugar, "INICIAR");
        HomeMenuAddSongsBootstrap.SetButtonClick(btnJugar, CargarCancionEnJuego);

        if (UsesHorizontalLayoutGroup())
            ConfigureCustomActionsForLayoutGroup();
        else
            ConfigureCustomActionsForAnchoredCard();
    }

    private void ConfigureCustomActionsForAnchoredCard()
    {
        const float rowHeight = 68f;
        const float spacing = 6f;

        _playRect.sizeDelta = new Vector2(_defaultPlaySizeDelta.x, rowHeight);
        _playRect.anchoredPosition = new Vector2(
            _defaultPlayAnchoredPos.x,
            _defaultPlayAnchoredPos.y + rowHeight * 0.5f + spacing * 0.5f);

        GameObject deleteGo = Instantiate(btnJugar.gameObject, _playRect.parent);
        deleteGo.name = "btn_EliminarCustom";

        RectTransform deleteRect = deleteGo.GetComponent<RectTransform>();
        deleteRect.anchorMin = _defaultPlayAnchorMin;
        deleteRect.anchorMax = _defaultPlayAnchorMax;
        deleteRect.pivot = _defaultPlayPivot;
        deleteRect.sizeDelta = new Vector2(_defaultPlaySizeDelta.x, rowHeight);
        deleteRect.anchoredPosition = new Vector2(
            _defaultPlayAnchoredPos.x,
            _defaultPlayAnchoredPos.y - rowHeight * 0.5f - spacing * 0.5f);

        StyleDeleteButton(deleteGo.GetComponent<Button>());
    }

    private void ConfigureCustomActionsForLayoutGroup()
    {
        Transform infoStack = transform.Find("Info_Stack");
        if (infoStack != null)
        {
            RectTransform infoRect = infoStack.GetComponent<RectTransform>();
            if (_defaultInfoStackWidth < 0f)
                _defaultInfoStackWidth = infoRect.sizeDelta.x;

            infoRect.sizeDelta = new Vector2(640f, infoRect.sizeDelta.y);
        }

        GameObject deleteGo = Instantiate(btnJugar.gameObject, transform);
        deleteGo.name = "btn_EliminarCustom";
        deleteGo.transform.SetSiblingIndex(_playRect.GetSiblingIndex() + 1);

        RectTransform deleteRect = deleteGo.GetComponent<RectTransform>();
        deleteRect.anchorMin = new Vector2(0f, 0f);
        deleteRect.anchorMax = new Vector2(0f, 0f);
        deleteRect.pivot = new Vector2(0.5f, 0.5f);
        deleteRect.sizeDelta = new Vector2(160f, 0f);
        deleteRect.anchoredPosition = Vector2.zero;

        LayoutElement layout = deleteGo.GetComponent<LayoutElement>() ?? deleteGo.AddComponent<LayoutElement>();
        layout.preferredWidth = 160f;
        layout.minWidth = 160f;

        StyleDeleteButton(deleteGo.GetComponent<Button>());
    }

    private void StyleDeleteButton(Button deleteButton)
    {
        if (deleteButton == null)
            return;

        Image deleteImage = deleteButton.GetComponent<Image>();
        if (deleteImage != null)
            deleteImage.color = new Color(0.55f, 0.22f, 0.16f, 1f);

        SetButtonLabel(deleteButton, "ELIMINAR");
        HomeMenuAddSongsBootstrap.SetButtonClick(deleteButton,
            () => _onDeleteCustomSong?.Invoke(_customSongId, _myFullData.title));
    }

    private void EnsurePlayButtonReference()
    {
        if (btnJugar != null)
            return;

        Transform playTransform = transform.Find("btn_JugarReal");
        if (playTransform == null)
            playTransform = transform.Find("Button");

        if (playTransform != null)
            btnJugar = playTransform.GetComponent<Button>();
    }

    private void CacheDefaultPlayLayout()
    {
        if (_playLayoutCached || btnJugar == null)
            return;

        _playRect = btnJugar.GetComponent<RectTransform>();
        _defaultPlayAnchoredPos = _playRect.anchoredPosition;
        _defaultPlaySizeDelta = _playRect.sizeDelta;
        _defaultPlayAnchorMin = _playRect.anchorMin;
        _defaultPlayAnchorMax = _playRect.anchorMax;
        _defaultPlayPivot = _playRect.pivot;
        _playLayoutCached = true;
    }

    private void RestoreDefaultPlayLayout()
    {
        if (!_playLayoutCached || _playRect == null)
            return;

        _playRect.anchorMin = _defaultPlayAnchorMin;
        _playRect.anchorMax = _defaultPlayAnchorMax;
        _playRect.pivot = _defaultPlayPivot;
        _playRect.sizeDelta = _defaultPlaySizeDelta;
        _playRect.anchoredPosition = _defaultPlayAnchoredPos;

        if (_defaultInfoStackWidth >= 0f)
        {
            Transform infoStack = transform.Find("Info_Stack");
            if (infoStack != null)
            {
                RectTransform infoRect = infoStack.GetComponent<RectTransform>();
                infoRect.sizeDelta = new Vector2(_defaultInfoStackWidth, infoRect.sizeDelta.y);
            }
        }
    }

    private void RemoveCustomDeleteButton()
    {
        Transform oldColumn = transform.Find("CustomActionsColumn");
        if (oldColumn != null)
        {
            Transform play = oldColumn.Find("btn_JugarReal");
            if (play != null)
            {
                play.SetParent(transform, false);
                play.SetAsLastSibling();
            }

            Destroy(oldColumn.gameObject);
        }

        Transform oldDelete = transform.Find("btn_EliminarCustom");
        if (oldDelete != null)
            Destroy(oldDelete.gameObject);
    }

    private bool UsesHorizontalLayoutGroup() => GetComponent<HorizontalLayoutGroup>() != null;

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
    }

    private static string FormatDuration(float durationSeconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0f, durationSeconds));
        return duration.TotalHours >= 1d ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
    }

    private void CargarCancionEnJuego()
    {
        if (SelectedSongManager.Instance == null)
            return;

        SelectedSongManager.Instance.selectedSong = _myFullData;
        string targetScene = string.Equals(_myFullData.mode, "CANTO", StringComparison.OrdinalIgnoreCase) ? "SingGame"
            : string.Equals(_myFullData.mode, "PIANO", StringComparison.OrdinalIgnoreCase) ? "PianoGame"
            : null;

        if (targetScene == null)
            return;

        Time.timeScale = 1f;
        SelectedSongManager.Instance.BeginSongSelectionMeasurement(_myFullData, targetScene);
        EndGameManager.gameEnded = false;

        // Fade solo en Repertorio → PianoGame. Canto carga normal (flujo de tu compañero).
        if (IsPianoMode) SceneTransition.Load(targetScene, "Preparando práctica de piano...");
        else SceneManager.LoadScene(targetScene);
    }

    public void UpdateButtonState(bool isEnabled)
    {
        if (btnJugar == null) return;
        btnJugar.interactable = isEnabled;
    }

    private void VerificarCompatibilidadVocal(string tonalidad, string modo)
    {
        if (txtAdvertencia == null) return;
        txtAdvertencia.text = "";
        txtAdvertencia.gameObject.SetActive(false);
        if (modo != "CANTO" || UserSession.Instance == null || string.IsNullOrEmpty(UserSession.Instance.tessitura)) return;

        string tessitura = UserSession.Instance.tessitura.ToUpper();
        tonalidad = tonalidad.ToUpper();
        bool esAguda = tonalidad.Contains("G") || tonalidad.Contains("A") || tonalidad.Contains("B");
        bool esGrave = tonalidad.Contains("C") || tonalidad.Contains("D") || tonalidad.Contains("E");

        string mensaje = tessitura switch
        {
            "BASS" when esAguda => "<color=orange>Advertencia: Muy alta para tu registro de Bajo.</color>",
            "BARITONE" when tonalidad.Contains("A") || tonalidad.Contains("B") => "<color=orange>Advertencia: Esta cancion suele ser alta para un Baritono.</color>",
            "TENOR" when esGrave => "<color=yellow>Advertencia: Puede quedarte algo grave para tu voz de Tenor.</color>",
            "CONTRALTO" when esAguda => "<color=orange>Advertencia: Tonalidad alta para una voz Contralto.</color>",
            "MEZZO_SOPRANO" when tonalidad.Contains("B") => "<color=orange>Advertencia: El tono Si (B) puede ser muy exigente para Mezzos.</color>",
            "SOPRANO" when esGrave => "<color=yellow>Advertencia: Tonalidad baja para tu registro de Soprano.</color>",
            _ => null
        };

        if (string.IsNullOrEmpty(mensaje)) return;
        txtAdvertencia.text = mensaje;
        txtAdvertencia.gameObject.SetActive(true);
    }
}
