using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garantiza que el runtime MIDI global (receptor, manager de conexión y widget de estado)
/// exista y esté activo en las escenas que lo necesitan, sin depender del cableado de cada escena.
/// </summary>
public class MidiInitializer : MonoBehaviour
{
    private static bool sceneHookRegistered;

    private const string RuntimeContainerName = "MIDI Runtime";
    private const string ConnectionManagerName = "MIDI Connection Manager";
    private const string StatusWidgetName = "MIDI Status Widget";

    // Escenas sin piano: login, registro y el modo canto.
    private static readonly string[] MidiDisabledScenes = { "LoginScene", "RegisterScene", "SingGame" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (sceneHookRegistered) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeCurrentScene() => HandleScene(SceneManager.GetActiveScene());

    public static bool ShouldEnableMidiForScene(string sceneName) =>
        Array.IndexOf(MidiDisabledScenes, sceneName) < 0;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HandleScene(scene);

    private static void HandleScene(Scene scene)
    {
        if (!scene.IsValid()) return;

        bool shouldEnableMidi = ShouldEnableMidiForScene(scene.name);
        if (shouldEnableMidi) EnsureRuntimeSystems();

        SetRuntimeState(shouldEnableMidi);
    }

    private static void EnsureRuntimeSystems()
    {
        try
        {
            GameObject midiContainer = FindOrCreatePersistentObject(RuntimeContainerName);

            if (FindObjectOfType<DirectMidiReceiver>() == null
                && midiContainer.GetComponent<DirectMidiReceiver>() == null)
            {
                midiContainer.AddComponent<DirectMidiReceiver>();
            }

            if (FindObjectOfType<MIDIConnectionManager>() == null)
                FindOrCreatePersistentObject(ConnectionManagerName).AddComponent<MIDIConnectionManager>();

            if (FindObjectOfType<MidiStatusWidgetController>() == null)
                FindOrCreatePersistentObject(StatusWidgetName).AddComponent<MidiStatusWidgetController>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MIDI Initializer] Error preparando el runtime MIDI: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void SetRuntimeState(bool active)
    {
        DirectMidiReceiver directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
        if (directMidiReceiver != null) directMidiReceiver.SetValidationActive(active);

        MidiStatusWidgetController statusWidget = FindObjectOfType<MidiStatusWidgetController>();
        if (statusWidget == null) return;

        statusWidget.SetWidgetVisible(active);
        if (!active) statusWidget.ClearGameplayPrompt();
    }

    private static GameObject FindOrCreatePersistentObject(string objectName)
    {
        GameObject persistentObject = GameObject.Find(objectName) ?? new GameObject(objectName);
        DontDestroyOnLoad(persistentObject);
        return persistentObject;
    }
}
