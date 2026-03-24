using UnityEngine;

/// <summary>
/// 🔧 INICIALIZADOR MIDI AUTOMÁTICO
/// Garantiza que MidiAudioManager existe en cualquier escena.
/// Se ejecuta automáticamente sin necesidad de estar en la escena.
/// </summary>
public class MidiInitializer : MonoBehaviour
{
    private static bool initialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        Debug.Log("<color=magenta>[MIDI INITIALIZER]</color> 🎯 RuntimeInitializeOnLoadMethod EJECUTADO");
        
        if (initialized) 
        {
            Debug.Log("<color=magenta>[MIDI INITIALIZER]</color> ⏭️  Ya inicializado previamente, saltando...");
            return;
        }
        initialized = true;

        try
        {
            // Verificar si MidiAudioManager ya existe
            MidiAudioManager existingMidi = FindObjectOfType<MidiAudioManager>();
            if (existingMidi != null)
            {
                Debug.Log("<color=cyan>[MIDI INIT]</color> ✅ MidiAudioManager ya existe en la escena");
                return;
            }

            // Crear GameObject padre para componentes MIDI
            GameObject midiContainer = new GameObject("🎹 MIDI System");
            DontDestroyOnLoad(midiContainer);
            Debug.Log("<color=yellow>[MIDI INIT]</color> 📦 Contenedor MIDI creado: " + midiContainer.name);

            // Agregar DirectMidiReceiver si no existe
            DirectMidiReceiver directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
            if (directMidiReceiver == null)
            {
                directMidiReceiver = midiContainer.AddComponent<DirectMidiReceiver>();
                Debug.Log("<color=yellow>[MIDI INIT]</color> 🎹 DirectMidiReceiver agregado (detección directa)");
            }
            else
            {
                Debug.Log("<color=yellow>[MIDI INIT]</color> 🎹 DirectMidiReceiver ya existe");
            }

            // Agregar MidiAudioManager
            MidiAudioManager midiAudioManager = midiContainer.AddComponent<MidiAudioManager>();
            midiAudioManager.directMidiReceiver = directMidiReceiver;
            Debug.Log("<color=green>[MIDI INIT]</color> ✅ MidiAudioManager LISTO para reproducir piano");

            // Agregar MIDIConnectionManager para detectar cambios de estado
            MIDIConnectionManager midiConnManager = FindObjectOfType<MIDIConnectionManager>();
            if (midiConnManager == null)
            {
                GameObject connMgrObj = new GameObject("MIDI Connection Manager");
                DontDestroyOnLoad(connMgrObj);
                connMgrObj.AddComponent<MIDIConnectionManager>();
                Debug.Log("<color=yellow>[MIDI INIT]</color> 🔗 MIDIConnectionManager agregado");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"<color=red>[MIDI INITIALIZER]</color> ❌ ERROR en Initialize: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
