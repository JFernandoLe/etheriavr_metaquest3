using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 🔧 INICIALIZADOR MIDI AUTOMÁTICO
/// Garantiza que el runtime MIDI global existe en cualquier escena.
/// </summary>
public class MidiInitializer : MonoBehaviour
{
    private static bool initialized = false;
    private static bool sceneHookRegistered = false;

    private const string RuntimeContainerName = "MIDI Runtime";
    private const string ConnectionManagerName = "MIDI Connection Manager";
    private const string StatusWidgetName = "MIDI Status Widget";
    private const string LoginSceneName = "LoginScene";
    private const string RegisterSceneName = "RegisterScene";
    private const string SingGameSceneName = "SingGame";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        Debug.Log("<color=magenta>[MIDI INITIALIZER]</color> 🎯 RuntimeInitializeOnLoadMethod EJECUTADO");

        if (!sceneHookRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            sceneHookRegistered = true;
        }

        if (initialized)
        {
            return;
        }

        initialized = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeCurrentScene()
    {
        HandleScene(SceneManager.GetActiveScene());
    }

    public static bool ShouldEnableMidiForScene(string sceneName)
    {
        return !string.Equals(sceneName, LoginSceneName, System.StringComparison.Ordinal) &&
               !string.Equals(sceneName, RegisterSceneName, System.StringComparison.Ordinal) &&
               !string.Equals(sceneName, SingGameSceneName, System.StringComparison.Ordinal);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleScene(scene);
    }

    private static void HandleScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        bool shouldEnableMidi = ShouldEnableMidiForScene(scene.name);
        if (shouldEnableMidi)
        {
            EnsureRuntimeSystems();
        }

        SetRuntimeState(shouldEnableMidi);
    }

    private static void EnsureRuntimeSystems()
    {
        Debug.Log("<color=magenta>[MIDI INITIALIZER]</color> ⏭️  Verificando runtime MIDI global...");

        try
        {
            GameObject midiContainer = FindOrCreatePersistentObject(RuntimeContainerName);

            DirectMidiReceiver directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
            if (directMidiReceiver == null)
            {
                directMidiReceiver = midiContainer.GetComponent<DirectMidiReceiver>();
                if (directMidiReceiver == null)
                {
                    directMidiReceiver = midiContainer.AddComponent<DirectMidiReceiver>();
                    Debug.Log("<color=yellow>[MIDI INIT]</color> 🎹 DirectMidiReceiver agregado al runtime global");
                }
            }

            MIDIConnectionManager midiConnManager = FindObjectOfType<MIDIConnectionManager>();
            if (midiConnManager == null)
            {
                GameObject connMgrObj = FindOrCreatePersistentObject(ConnectionManagerName);
                connMgrObj.AddComponent<MIDIConnectionManager>();
                Debug.Log("<color=yellow>[MIDI INIT]</color> 🔗 MIDIConnectionManager agregado");
            }

            MidiStatusWidgetController statusWidget = FindObjectOfType<MidiStatusWidgetController>();
            if (statusWidget == null)
            {
                GameObject statusWidgetObject = FindOrCreatePersistentObject(StatusWidgetName);
                statusWidgetObject.AddComponent<MidiStatusWidgetController>();
                Debug.Log("<color=yellow>[MIDI INIT]</color> 🟢 Widget global de estado MIDI agregado");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<color=red>[MIDI INITIALIZER]</color> ❌ ERROR en Initialize: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void SetRuntimeState(bool active)
    {
        DirectMidiReceiver directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
        if (directMidiReceiver != null)
        {
            directMidiReceiver.SetValidationActive(active);
        }

        MidiStatusWidgetController statusWidget = FindObjectOfType<MidiStatusWidgetController>();
        if (statusWidget != null)
        {
            statusWidget.SetWidgetVisible(active);
            if (!active)
            {
                statusWidget.ClearGameplayPrompt();
            }
        }
    }

    private static GameObject FindOrCreatePersistentObject(string objectName)
    {
        GameObject existingObject = GameObject.Find(objectName);
        if (existingObject != null)
        {
            DontDestroyOnLoad(existingObject);
            return existingObject;
        }

        GameObject createdObject = new GameObject(objectName);
        DontDestroyOnLoad(createdObject);
        return createdObject;
    }
}
