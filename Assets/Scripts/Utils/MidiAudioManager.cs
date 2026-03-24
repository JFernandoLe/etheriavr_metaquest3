using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-900)]
public class MidiAudioManager : MonoBehaviour
{
    [Header("Conexiones")]
    public DirectMidiReceiver directMidiReceiver;
    
    // 🎵 EVENTOS para comunicar con GameplayScoring
    public delegate void OnMidiNoteDelegate(int midiNote, int velocity);
    public event OnMidiNoteDelegate OnMidiNoteOn;
    public event OnMidiNoteDelegate OnMidiNoteOff;
    
    [Header("Ajustes de Sonido")]
    [Range(0.5f, 20f)] public float volumeBoost = 1.0f;  // REDUCIDO de 10.0f a 1.0f para evitar clipping
    [Range(1f, 4f)] public float velocityCurve = 2.2f; 
    public int poolSize = 40;
    [SerializeField] private bool optimizeLowLatency = true;
    [SerializeField] private int targetDspBufferSize = 256;
    [SerializeField] private int targetRealVoices = 64;
    [SerializeField] private bool verboseMidiLogging = false;
    
    [Header("Aplausos")]
    private AudioSource applauseSource;
    [SerializeField] private float applauseMaxVolume = 1.0f;
    [SerializeField] private float applauseMinAudibleVolume = 0.35f;

    private Dictionary<int, AudioClip> pianoSamples = new Dictionary<int, AudioClip>();
    private List<int> availableMidiNotes = new List<int>();
    private List<AudioSource> audioPool = new List<AudioSource>();
    private Dictionary<int, AudioSource> activeNotes = new Dictionary<int, AudioSource>();
    private HashSet<int> sustainedNotes = new HashSet<int>();
    private HashSet<int> currentlyPressedNotes = new HashSet<int>(); // 🎹 Notas siendo presionadas AHORA
    private bool isPedalDown = false;

    private Dictionary<string, int> noteOffsets = new Dictionary<string, int>() {
        {"c", 0}, {"c#", 1}, {"d", 2}, {"d#", 3}, {"e", 4}, {"f", 5},
        {"f#", 6}, {"g", 7}, {"g#", 8}, {"a", 9}, {"a#", 10}, {"b", 11}
    };

    /// <summary>
    /// Consultar si un MIDI note está siendo presionado AHORA (sin soltarse)
    /// </summary>
    public bool IsNotePressedNow(int midiNote)
    {
        return currentlyPressedNotes.Contains(midiNote);
    }

    /// <summary>
    /// Cambiar el volumen del piano dinámicamente (0.0-1.0)
    /// </summary>
    public void SetPianoVolume(float volume)
    {
        volumeBoost = Mathf.Clamp(volume * 1.75f, 0.85f, 2.5f);
        Debug.Log($"<color=cyan>[MIDI Audio]</color> 🎚️ Piano volume set to {volumeBoost:F3}");
    }

    void Awake()
    {
        if (optimizeLowLatency)
        {
            ApplyLowLatencyAudioConfiguration();
        }
    }

    void Start()
    {
        // AUTO-DETECTAR DirectMidiReceiver si no está asignado
        if (directMidiReceiver == null)
        {
            directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
            if (directMidiReceiver == null)
            {
                Debug.LogError("<color=red>[MidiAudio]</color> ❌ NO SE ENCONTRÓ DirectMidiReceiver!");
                return;
            }
            Debug.Log("<color=cyan>[MidiAudio]</color> ✅ DirectMidiReceiver detectado automáticamente");
        }
        
        // 1. Carga Inteligente de Samples (c2, c#2, etc.)
        AudioClip[] loadedClips = Resources.LoadAll<AudioClip>("notes");

        foreach (var clip in loadedClips)
        {
            string name = clip.name.ToLower().Trim();
            
            char octaveChar = name[name.Length - 1];
            if (char.IsDigit(octaveChar))
            {
                int octave = (int)char.GetNumericValue(octaveChar);
                string noteName = name.Substring(0, name.Length - 1);

                if (noteOffsets.ContainsKey(noteName))
                {
                    int midiNum = (octave + 1) * 12 + noteOffsets[noteName];
                    pianoSamples[midiNum] = clip;
                    availableMidiNotes.Add(midiNum);
                    Debug.Log($"<color=green>[ETHERIA]</color> Mapeado: {name} -> MIDI {midiNum}");
                }
            }
        }
        availableMidiNotes.Sort();

        // 2. Pool de voces
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            s.priority = 0;
            s.bypassEffects = true;
            s.bypassListenerEffects = true;
            s.bypassReverbZones = true;
            audioPool.Add(s);
        }
        
        Debug.Log($"<color=green>[MIDI INIT]</color> ✅ Cargados {pianoSamples.Count} samples de piano");
        Debug.Log($"<color=green>[MIDI INIT]</color> ✅ Pool de {poolSize} voces creado");
        Debug.Log($"<color=cyan>[MIDI INIT]</color> 🎚️  volumeBoost={volumeBoost:F2}x, velocityCurve={velocityCurve:F2}");
        Debug.Log($"<color=yellow>[MIDI INIT]</color> 📡 Esperando paquetes MIDI binarios (30 Hz)...");
        
        if (pianoSamples.Count == 0)
        {
            Debug.LogError("<color=red>[MIDI ERROR]</color> ❌ NO SE CARGARON SAMPLES! Verifica Resources/notes/ contiene archivos de audio");
        }
        else
        {
            Debug.Log($"<color=green>[MIDI INIT]</color> ✅ Rango MIDI: {availableMidiNotes[0]} a {availableMidiNotes[availableMidiNotes.Count-1]}");
        }
    }

    private int packetsReceived = 0;
    
    void Update()
    {
        if (directMidiReceiver == null)
        {
            Debug.LogError("<color=red>[MidiAudioManager UPDATE]</color> ❌ directMidiReceiver es NULL!");
            return;
        }
        
        // ✅ OPTIMIZADO: Procesa bytes directamente sin bloqueos
        int dequeueCount = 0;
        while (directMidiReceiver.messageQueue.TryDequeue(out byte[] data))
        {
            packetsReceived++;
            dequeueCount++;
            ProcessMidi(data);
        }

        if (verboseMidiLogging && dequeueCount > 0)
        {
            Debug.Log($"<color=green>[MidiAudioManager UPDATE]</color> ✅ Dequeued {dequeueCount} evento(s) en este frame");
        }
    }

    private void ApplyLowLatencyAudioConfiguration()
    {
        AudioConfiguration config = AudioSettings.GetConfiguration();
        bool changed = false;

        if (targetDspBufferSize > 0 && config.dspBufferSize > targetDspBufferSize)
        {
            config.dspBufferSize = targetDspBufferSize;
            changed = true;
        }

        if (targetRealVoices > 0 && config.numRealVoices < targetRealVoices)
        {
            config.numRealVoices = targetRealVoices;
            changed = true;
        }

        if (changed)
        {
            bool resetOk = AudioSettings.Reset(config);
            Debug.Log($"<color=cyan>[MIDI Audio]</color> Low latency audio config | dspBuffer={config.dspBufferSize} | realVoices={config.numRealVoices} | reset={resetOk}");
        }
    }

    // ✅ OPTIMIZADO: Parsea datos binarios (3 bytes) del DirectMidiReceiver
    void ProcessMidi(byte[] data)
    {
        // Validar que sea el tamaño correcto (directamente de DirectMidiReceiver)
        if (data.Length != 3) 
        {
            Debug.LogWarning($"<color=yellow>[MIDI DEBUG]</color> ⚠️ Paquete incorrecto: {data.Length} bytes (esperaba 3)");
            return;
        }

        byte status = data[0];
        byte note = data[1];
        byte vel = data[2];
        
        // Convertir status MIDI estándar a tipo de mensaje
        int msgType = (status & 0xF0) switch
        {
            0x90 => vel > 0 ? 1 : 0,  // Note On si vel > 0, sino Note Off
            0x80 => 0,                 // Note Off
            0xB0 => 2,                 // Control Change (pedal)
            _ => -1
        };
        
        if (msgType == -1) return; // Ignorar mensajes desconocidos
        
        if (verboseMidiLogging)
        {
            string typeStr = msgType switch
            {
                0 => "NOTE OFF",
                1 => "NOTE ON ",
                2 => "CC (Pedal)",
                _ => "DESCONOCIDO"
            };

            Debug.Log($"<color=cyan>[MIDI RX]</color> #{packetsReceived:D5} | 0x{status:X2} | {typeStr} | Nota: {note} | Vel: {vel}");
        }
        
        // Procesar según el tipo de mensaje
        if (msgType == 0 || msgType == 1) // note_off (0) o note_on (1)
        {
            if (msgType == 1 && vel > 0) 
            {
                PlayNote(note, vel);
                currentlyPressedNotes.Add(note);
                OnMidiNoteOn?.Invoke(note, vel);
            }
            else 
            {
                currentlyPressedNotes.Remove(note);
                StopNote(note);
                OnMidiNoteOff?.Invoke(note, 0);
            }
        }
        else if (msgType == 2) // control_change (pedal)
        {
            isPedalDown = vel >= 64;
            if (!isPedalDown) 
            {
                ReleaseSustain();
                if (currentlyPressedNotes.Count > 0)
                {
                    currentlyPressedNotes.Clear();
                }
            }
        }
    }

    void PlayNote(int targetNote, int vel)
    {
        if (availableMidiNotes.Count == 0) 
        {
            Debug.LogError("<color=red>[MIDI]</color> ❌ NO hay samples cargados!");
            return;
        }

        // Encontrar sample más cercano
        int bestBaseNote = availableMidiNotes[0];
        float minDiff = float.MaxValue;
        foreach (int n in availableMidiNotes)
        {
            float diff = Mathf.Abs(targetNote - n);
            if (diff < minDiff) { minDiff = diff; bestBaseNote = n; }
        }

        float semitoneOffset = targetNote - bestBaseNote;
        float pitch = Mathf.Pow(2.0f, semitoneOffset / 12.0f);

        float normalizedVel = vel / 127f;
        float curvedVolume = Mathf.Pow(normalizedVel, velocityCurve) * volumeBoost;

        if (activeNotes.ContainsKey(targetNote)) 
        {
            activeNotes[targetNote].Stop();
            activeNotes.Remove(targetNote);
        }

        // Buscar AudioSource disponible
        AudioSource foundSource = null;
        int sourceIndex = -1;
        for (int i = 0; i < audioPool.Count; i++)
        {
            if (!audioPool[i].isPlaying)
            {
                foundSource = audioPool[i];
                sourceIndex = i;
                break;
            }
        }
        
        if (foundSource == null)
        {
            Debug.LogWarning($"<color=yellow>[MIDI]</color> ⚠️ Pool de voces LLENO (necesita {audioPool.Count + 1})");
            return;
        }

        // Reproducir nota
        foundSource.clip = pianoSamples[bestBaseNote];
        if (foundSource.clip != null && foundSource.clip.loadState == AudioDataLoadState.Unloaded)
        {
            foundSource.clip.LoadAudioData();
        }
        foundSource.pitch = pitch;
        foundSource.volume = Mathf.Clamp01(curvedVolume);  // Clamped 0-1 para evitar distorsión
        foundSource.Play();
        
        activeNotes[targetNote] = foundSource;
        sustainedNotes.Remove(targetNote);
        
        if (verboseMidiLogging)
        {
            string sampleName = pianoSamples[bestBaseNote] != null ? pianoSamples[bestBaseNote].name : "NULL";
            Debug.Log($"<color=green>[MIDI PLAY]</color> 🎹 MIDI{targetNote} | Vel{vel}/127 | Pitch{pitch:F2}x | Vol{foundSource.volume:F3} | Src{sourceIndex}/{audioPool.Count} | Sample:{sampleName}");
        }
    }

    void StopNote(int note)
    {
        if (isPedalDown) 
        {
            sustainedNotes.Add(note);
        }
        else if (activeNotes.ContainsKey(note))
        {
            activeNotes[note].Stop();
            activeNotes.Remove(note);
        }
    }

    void ReleaseSustain()
    {
        foreach (int n in sustainedNotes)
        {
            if (activeNotes.ContainsKey(n))
            {
                activeNotes[n].Stop();
                activeNotes.Remove(n);
            }
        }
        sustainedNotes.Clear();
    }
    
    /// <summary>
    /// Inicializar el AudioSource de aplausos
    /// Se llama al iniciar en la escena del piano
    /// </summary>
    public void InitializeApplauseSystem()
    {
        if (applauseSource == null)
        {
            applauseSource = gameObject.AddComponent<AudioSource>();
            applauseSource.playOnAwake = false;
            applauseSource.spatialBlend = 0; // 2D
            applauseSource.volume = 0f;
            
            AudioClip applauseClip = Resources.Load<AudioClip>("Sounds/aplause");
            if (applauseClip != null)
            {
                applauseSource.clip = applauseClip;
                applauseSource.loop = true;
                Debug.Log("<color=cyan>[MIDI Audio]</color> ✅ Sistema de aplausos inicializado");
            }
            else
            {
                Debug.LogWarning("<color=yellow>[MIDI Audio]</color> ⚠️ No se encontró Assets/Sounds/aplause.mp3");
            }
        }
    }
    
    /// <summary>
    /// Actualizar volumen de aplausos según score público (0-100)
    /// </summary>
    public void SetApplauseVolume(float publicScore)
    {
        if (applauseSource == null) return;
        
        float normalizedScore = Mathf.Pow(Mathf.Clamp01(publicScore / 100f), 0.75f); // 0-1
        applauseSource.volume = Mathf.Lerp(applauseMinAudibleVolume, applauseMaxVolume, normalizedScore);
    }
    
    /// <summary>
    /// Iniciar aplausos
    /// </summary>
    public void StartApplauseLoop()
    {
        if (applauseSource != null && !applauseSource.isPlaying)
        {
            applauseSource.Play();
            Debug.Log("<color=cyan>[MIDI Audio]</color> 🎵 Aplausos iniciados");
        }
    }
    
    /// <summary>
    /// Detener aplausos
    /// </summary>
    public void StopApplauseLoop()
    {
        if (applauseSource != null && applauseSource.isPlaying)
        {
            applauseSource.Stop();
            Debug.Log("<color=cyan>[MIDI Audio]</color> 🔇 Aplausos detenidos");
        }
    }
}
