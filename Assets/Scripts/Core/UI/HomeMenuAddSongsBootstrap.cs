using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Botón "Agregar canción" en HomeScene — mismo layout que Repertorio y Configuración.
/// </summary>
public class HomeMenuAddSongsBootstrap : MonoBehaviour
{
    private const string HomeSceneName = "HomeScene";
    private const string HomeCanvasName = "HomeCanvas";
    private const string AddSongsButtonName = "add_songs_button";

    private const float ButtonWidth = 960f;
    private const float ButtonHeight = 240f;
    private const float ButtonX = 1280f;
    private static readonly float[] ButtonYOrder = { -520f, -780f, -1040f };

    private static bool hookRegistered;
    private AddSongMenuController menuController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterHook()
    {
        if (hookRegistered) return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        hookRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeCurrentScene() =>
        AttachIfNeeded(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode) => AttachIfNeeded(scene);

    private static void AttachIfNeeded(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene.name != HomeSceneName) return;
        if (FindFirstObjectByType<HomeMenuAddSongsBootstrap>() != null) return;

        var go = new GameObject(nameof(HomeMenuAddSongsBootstrap));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<HomeMenuAddSongsBootstrap>();
    }

    private void Start() => StartCoroutine(SetupWhenReady());

    private IEnumerator SetupWhenReady()
    {
        Time.timeScale = 1f;
        yield return null;
        yield return null;

        Canvas canvas = GameObject.Find(HomeCanvasName)?.GetComponent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[HomeMenuAddSongsBootstrap] No se encontró HomeCanvas.");
            yield break;
        }

        menuController = AddSongMenuController.Ensure(canvas);
        SetupHomeButtons(canvas.transform);
    }

    private void SetupHomeButtons(Transform canvasRoot)
    {
        Transform panel = canvasRoot.Find("Panel");
        if (panel == null)
            return;

        Transform oldStack = panel.Find("home_menu_button_stack");
        if (oldStack != null)
            Destroy(oldStack.gameObject);

        Button presentation = panel.Find("presentation_button")?.GetComponent<Button>();
        Button configuration = panel.Find("configuration_button")?.GetComponent<Button>();
        if (presentation == null || configuration == null || menuController == null)
            return;

        SetButtonClick(presentation, () => UnityEngine.SceneManagement.SceneManager.LoadScene("RepertorioScene"));
        SetButtonClick(configuration, () => UnityEngine.SceneManagement.SceneManager.LoadScene("ConfigScene"));

        ConfigureHomeButton(presentation, "Repertorio", "Selecciona una cancion y practica", ButtonYOrder[0]);
        ConfigureHomeButton(configuration, "Configuracion", "Ajustes del sistema y publico virtual", ButtonYOrder[2]);

        Button addButton = panel.Find(AddSongsButtonName)?.GetComponent<Button>();
        if (addButton == null)
        {
            GameObject clone = Instantiate(presentation.gameObject, panel);
            clone.name = AddSongsButtonName;
            addButton = clone.GetComponent<Button>();
        }

        ConfigureHomeButton(addButton, "Agregar cancion", "Importa un MP3 para cantar", ButtonYOrder[1]);
        SetButtonClick(addButton, menuController.OpenAddSongMenu);
        addButton.gameObject.SetActive(true);
    }

    private static void ConfigureHomeButton(Button button, string title, string subtitle, float anchoredY)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(ButtonX, anchoredY);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rect.localScale = Vector3.one;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
        {
            texts[0].text = title;
            texts[0].fontSize = 54f;
        }

        if (texts.Length > 1)
        {
            texts[1].text = subtitle;
            texts[1].fontSize = 32f;
        }
    }

    public static void SetButtonClick(Button button, Action onClick)
    {
        if (button == null) return;
        button.onClick = new Button.ButtonClickedEvent();
        if (onClick != null)
            button.onClick.AddListener(() => onClick());
    }
}
