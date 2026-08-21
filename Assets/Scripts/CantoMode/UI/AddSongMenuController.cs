using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Flujo "Agregar canción" como overlay sobre Home/Repertorio (no oculta botones del menú).
/// </summary>
public class AddSongMenuController : MonoBehaviour
{
    private GameObject overlayRoot;
    private GameObject modeSelectPanel;
    private GameObject cantoPanel;
    private GameObject processingPanel;
    private GameObject errorPanel;

    private TMP_Text cantoStatusText;
    private TMP_Text processingStatusText;
    private TMP_Text errorDetailText;
    private Button btnSelectSong;

    private CustomSongManager songManager;
    private bool isImporting;
    private string lastPickedPath;
    private string lastPickedTitle;
    private float importUiStartTime;

    public static AddSongMenuController Ensure(Canvas canvas)
    {
        AddSongMenuController existing = canvas.GetComponentInChildren<AddSongMenuController>(true);
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(AddSongMenuController), typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        StretchFull(go.GetComponent<RectTransform>());
        return go.AddComponent<AddSongMenuController>();
    }

    void Awake()
    {
        BuildPanels();
        CloseAll();
    }

    void Start()
    {
        EnsureSongManager();
        songManager.OnImportProgress += HandleImportProgress;
        songManager.OnSongImported += HandleSongImported;
        songManager.OnImportFailed += HandleImportFailed;
    }

    void OnDestroy()
    {
        if (songManager == null) return;
        songManager.OnImportProgress -= HandleImportProgress;
        songManager.OnSongImported -= HandleSongImported;
        songManager.OnImportFailed -= HandleImportFailed;
    }

    public void OpenAddSongMenu()
    {
        CloseAll();
        overlayRoot.transform.SetAsLastSibling();
        overlayRoot.SetActive(true);
        modeSelectPanel.SetActive(true);
    }

    public void CloseAll()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        if (modeSelectPanel != null) modeSelectPanel.SetActive(false);
        if (cantoPanel != null) cantoPanel.SetActive(false);
        if (processingPanel != null) processingPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
    }

    private void ShowCantoPanel()
    {
        CloseAll();
        overlayRoot.SetActive(true);
        cantoPanel.SetActive(true);
        cantoStatusText.text = "Selecciona un MP3 del dispositivo.";
        btnSelectSong.interactable = !isImporting;
    }

    private void ShowProcessingPanel(string message)
    {
        CloseAll();
        overlayRoot.SetActive(true);
        processingPanel.SetActive(true);
        processingStatusText.text = message;
    }

    private void ShowErrorPanel(string title, string detail)
    {
        CloseAll();
        overlayRoot.SetActive(true);
        errorPanel.SetActive(true);
        errorDetailText.text = $"{title}\n\n{detail}";
    }

    private void BeginSelectSong()
    {
        if (isImporting) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        cantoStatusText.text = "Abriendo selector de archivos...";
        QuestSongPicker.PickAudioFile(OnAudioPicked);
#else
        string sampleFolder = Path.Combine(Application.streamingAssetsPath, "SingSongs", "Songs");
        if (Directory.Exists(sampleFolder))
        {
            string[] mp3Files = Directory.GetFiles(sampleFolder, "*.mp3");
            if (mp3Files.Length > 0)
            {
                OnAudioPicked(mp3Files[0], Path.GetFileNameWithoutExtension(mp3Files[0]));
                return;
            }
        }
        cantoStatusText.text = "En editor: coloca un MP3 en StreamingAssets/SingSongs/Songs.";
#endif
    }

    private void OnAudioPicked(string path, string originalFileName)
    {
        if (string.IsNullOrEmpty(path))
        {
            cantoStatusText.text = "Selección cancelada.";
            return;
        }

        lastPickedPath = path;
        string songTitle = !string.IsNullOrWhiteSpace(originalFileName)
            ? originalFileName.Trim()
            : Path.GetFileNameWithoutExtension(path);
        lastPickedTitle = songTitle;
        isImporting = true;
        importUiStartTime = Time.unscaledTime;
        btnSelectSong.interactable = false;

        ShowProcessingPanel($"Importando \"{songTitle}\"...");
        EnsureSongManager();
        StartCoroutine(songManager.ImportFromFile(path, songTitle));
    }

    private void HandleImportProgress(CustomSongImportStage stage, string message)
    {
        if (processingPanel == null || processingStatusText == null)
            return;

        if (!overlayRoot.activeSelf || !processingPanel.activeSelf)
            ShowProcessingPanel(message);

        float elapsed = Time.unscaledTime - importUiStartTime;
        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);
        string stageLabel = stage switch
        {
            CustomSongImportStage.Loading => "Cargando audio",
            CustomSongImportStage.Analyzing => "Leyendo audio",
            CustomSongImportStage.GeneratingNotes => "Extrayendo notas",
            CustomSongImportStage.Saving => "Guardando",
            CustomSongImportStage.Ready => "Listo",
            _ => "Procesando"
        };

        processingStatusText.text =
            $"{message}\n\nEtapa: {stageLabel}\nTiempo: {minutes:00}:{seconds:00}\n\nNo cierres la app.";
        overlayRoot.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    private void HandleSongImported(CustomSongEntry entry)
    {
        isImporting = false;
        btnSelectSong.interactable = true;
        CloseAll();
        SceneManager.LoadScene("RepertorioScene");
    }

    private void HandleImportFailed(string error)
    {
        isImporting = false;
        btnSelectSong.interactable = true;
        ShowErrorPanel("No se pudo procesar la canción.", error);
    }

    private void RetryImport()
    {
        if (string.IsNullOrEmpty(lastPickedPath))
        {
            ShowCantoPanel();
            return;
        }
        OnAudioPicked(lastPickedPath, lastPickedTitle);
    }

    private void CancelImport()
    {
        if (!isImporting)
        {
            CloseAll();
            return;
        }

        isImporting = false;
        if (songManager != null)
            songManager.CancelCurrentImport();
        btnSelectSong.interactable = true;
        ShowCantoPanel();
    }

    private void EnsureSongManager()
    {
        if (CustomSongManager.Instance == null)
        {
            var managerObject = new GameObject("CustomSongManager");
            managerObject.AddComponent<CustomSongManager>();
        }

        songManager = CustomSongManager.Instance;
        songManager.OnImportProgress -= HandleImportProgress;
        songManager.OnSongImported -= HandleSongImported;
        songManager.OnImportFailed -= HandleImportFailed;
        songManager.OnImportProgress += HandleImportProgress;
        songManager.OnSongImported += HandleSongImported;
        songManager.OnImportFailed += HandleImportFailed;
    }

    private void BuildPanels()
    {
        Button template = GameObject.Find("presentation_button")?.GetComponent<Button>()
            ?? GameObject.Find("botonVolver")?.GetComponent<Button>();

        overlayRoot = CreatePanel("AddSongOverlay", transform, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        StretchFull(overlayRoot.GetComponent<RectTransform>());
        overlayRoot.SetActive(false);

        modeSelectPanel = BuildPanel("AddSong_ModeSelect", overlayRoot.transform, template, new[]
        {
            MenuButtonSpec.Header("AGREGAR CANCIÓN"),
            MenuButtonSpec.Disabled("Piano\n(Próximamente)"),
            MenuButtonSpec.Clickable("Canto", ShowCantoPanel),
            MenuButtonSpec.Clickable("Cerrar", CloseAll)
        });

        cantoPanel = BuildPanel("AddSong_Canto", overlayRoot.transform, template, new[]
        {
            MenuButtonSpec.Header("CANTO — IMPORTAR MP3"),
            MenuButtonSpec.Caption("Selecciona un MP3 del dispositivo."),
            MenuButtonSpec.Clickable("Seleccionar canción", BeginSelectSong),
            MenuButtonSpec.Clickable("Volver", OpenAddSongMenu)
        }, out cantoStatusText, out btnSelectSong);

        processingPanel = BuildPanel("AddSong_Processing", overlayRoot.transform, template, new[]
        {
            MenuButtonSpec.Header("Procesando canción"),
            MenuButtonSpec.Caption("Preparando importación..."),
            MenuButtonSpec.Clickable("Cancelar importación", CancelImport)
        }, out processingStatusText, out _);

        errorPanel = BuildPanel("AddSong_Error", overlayRoot.transform, template, new[]
        {
            MenuButtonSpec.Header("Error"),
            MenuButtonSpec.Caption(""),
            MenuButtonSpec.Clickable("Reintentar", RetryImport),
            MenuButtonSpec.Clickable("Volver", ShowCantoPanel)
        }, out errorDetailText, out _);
    }

    private GameObject BuildPanel(string name, Transform parent, Button template, MenuButtonSpec[] specs,
        out TMP_Text statusText, out Button actionButton)
    {
        statusText = null;
        actionButton = null;

        GameObject panel = CreatePanel(name, parent, new Color(0, 0, 0, 0.01f));
        StretchFull(panel.GetComponent<RectTransform>());

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20f;
        layout.padding = new RectOffset(60, 60, 60, 60);
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        foreach (MenuButtonSpec spec in specs)
        {
            if (spec.IsLabel)
            {
                float labelSize = name.Contains("Processing") ? 30f : 24f;
                statusText = CreateLabel(panel.transform, template, spec.Label, labelSize);
                if (name.Contains("Error"))
                    errorDetailText = statusText;
            }
            else if (spec.IsHeader)
            {
                CreateLabel(panel.transform, template, spec.Label, 36f);
            }
            else
            {
                Button btn = CreateButton(panel.transform, template, spec.Label, spec.OnClick, spec.Interactable);
                if (spec.Label.Contains("Seleccionar"))
                    actionButton = btn;
            }
        }

        panel.SetActive(false);
        return panel;
    }

    private GameObject BuildPanel(string name, Transform parent, Button template, MenuButtonSpec[] specs) =>
        BuildPanel(name, parent, template, specs, out _, out _);

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static TMP_Text CreateLabel(Transform parent, Button template, string text, float size = 24f)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (template != null)
        {
            TMP_Text sample = template.GetComponentInChildren<TMP_Text>();
            if (sample != null && sample.font != null)
                tmp.font = sample.font;
        }
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 20f;
        return tmp;
    }

    private static Button CreateButton(Transform parent, Button template, string label, Action onClick, bool interactable)
    {
        Button button;
        if (template != null)
        {
            GameObject clone = Instantiate(template.gameObject, parent);
            clone.name = label.Replace("\n", "_");
            button = clone.GetComponent<Button>();
            TMP_Text[] texts = clone.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0) texts[0].text = label;
            for (int i = 1; i < texts.Length; i++) texts[i].text = string.Empty;
        }
        else
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            button = go.GetComponent<Button>();
            go.GetComponent<Image>().color = new Color(0.38f, 0.27f, 0.19f, 1f);
            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        LayoutElement le = button.gameObject.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 120f;
        le.preferredHeight = 140f;

        HomeMenuAddSongsBootstrap.SetButtonClick(button, interactable ? onClick : null);
        button.interactable = interactable;
        return button;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private readonly struct MenuButtonSpec
    {
        public string Label { get; }
        public Action OnClick { get; }
        public bool Interactable { get; }
        public bool IsHeader { get; }
        public bool IsLabel { get; }

        private MenuButtonSpec(string label, Action onClick, bool interactable, bool isHeader, bool isLabel)
        {
            Label = label;
            OnClick = onClick;
            Interactable = interactable;
            IsHeader = isHeader;
            IsLabel = isLabel;
        }

        public static MenuButtonSpec Header(string label) => new MenuButtonSpec(label, null, false, true, false);
        public static MenuButtonSpec Caption(string label) => new MenuButtonSpec(label, null, false, false, true);
        public static MenuButtonSpec Clickable(string label, Action onClick) => new MenuButtonSpec(label, onClick, true, false, false);
        public static MenuButtonSpec Disabled(string label) => new MenuButtonSpec(label, null, false, false, false);
    }
}
