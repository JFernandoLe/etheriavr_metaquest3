using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sintetiza el piano a partir de los paquetes MIDI del DirectMidiReceiver,
/// usando un pool de AudioSource con pitch-shift sobre los samples más cercanos.
/// También gestiona el loop de aplausos del público.
/// </summary>
[DefaultExecutionOrder(-900)]
public class MidiAudioManager : MonoBehaviour
{
    private const int MsgNoteOff = 0;
    private const int MsgNoteOn = 1;
    private const int MsgControlChange = 2;

    private static readonly Dictionary<string, int> NoteOffsets = new Dictionary<string, int>
    {
        { "c", 0 }, { "c#", 1 }, { "d", 2 }, { "d#", 3 }, { "e", 4 }, { "f", 5 },
        { "f#", 6 }, { "g", 7 }, { "g#", 8 }, { "a", 9 }, { "a#", 10 }, { "b", 11 }
    };

    [Header("Conexiones")]
    public DirectMidiReceiver directMidiReceiver;

    public delegate void OnMidiNoteDelegate(int midiNote, int velocity);
    public event OnMidiNoteDelegate OnMidiNoteOn;
    public event OnMidiNoteDelegate OnMidiNoteOff;

    [Header("Ajustes de Sonido")]
    [Range(0.5f, 20f)] public float volumeBoost = 1.0f;
    [Range(1f, 4f)] public float velocityCurve = 2.2f;
    public int poolSize = 40;
    [SerializeField] private bool optimizeLowLatency = true;
    [SerializeField] private int targetDspBufferSize = 256;
    [SerializeField] private int targetRealVoices = 64;
    [SerializeField] private bool verboseMidiLogging = false;

    [Header("Aplausos")]
    [SerializeField] private AudioClip applauseClip;
    [SerializeField] private AudioClip applauseClipLow;
    [SerializeField] private AudioClip applauseClipMedium;
    [SerializeField] private AudioClip applauseClipHigh;
    [SerializeField] private float applauseMaxVolume = 1.0f;
    [SerializeField] private float applauseMinAudibleVolume = 0.0f;

    private AudioSource applauseSource;
    private readonly Dictionary<int, AudioClip> pianoSamples = new Dictionary<int, AudioClip>();
    private readonly List<int> availableMidiNotes = new List<int>();
    private readonly List<AudioSource> audioPool = new List<AudioSource>();
    private readonly Dictionary<int, AudioSource> activeNotes = new Dictionary<int, AudioSource>();
    private readonly HashSet<int> sustainedNotes = new HashSet<int>();
    private readonly HashSet<int> currentlyPressedNotes = new HashSet<int>();
    private readonly List<int> sustainReleaseBuffer = new List<int>();
    private bool isPedalDown = false;
    private int packetsReceived = 0;

    /// <summary>True mientras la tecla siga pulsada (sin note off).</summary>
    public bool IsNotePressedNow(int midiNote) => currentlyPressedNotes.Contains(midiNote);

    public void SetPianoVolume(float volume)
    {
        volumeBoost = Mathf.Clamp(volume * 1.75f, 0.85f, 2.5f);
        Debug.Log($"<color=cyan>[MIDI Audio]</color> Piano volume set to {volumeBoost:F3}");
    }

    void Awake()
    {
        if (optimizeLowLatency) ApplyLowLatencyAudioConfiguration();
    }

    void Start()
    {
        poolSize = Mathf.Max(poolSize, 128);
        targetRealVoices = Mathf.Max(targetRealVoices, 128);

        if (directMidiReceiver == null)
        {
            directMidiReceiver = FindObjectOfType<DirectMidiReceiver>();
            if (directMidiReceiver == null)
            {
                Debug.LogError("<color=red>[MidiAudio]</color> No se encontró DirectMidiReceiver!");
                return;
            }
        }

        LoadPianoSamples();
        BuildVoicePool();

        if (pianoSamples.Count == 0)
        {
            Debug.LogError("<color=red>[MIDI ERROR]</color> No se cargaron samples! Verifica que Resources/notes/ contenga audio.");
            return;
        }

        Debug.Log($"<color=green>[MIDI INIT]</color> {pianoSamples.Count} samples (MIDI {availableMidiNotes[0]}-" +
                  $"{availableMidiNotes[availableMidiNotes.Count - 1]}) | pool={poolSize} voces | " +
                  $"volumeBoost={volumeBoost:F2}x | velocityCurve={velocityCurve:F2}");
    }

    /// <summary>Mapea los samples "c2", "c#2", ... a su número MIDI.</summary>
    private void LoadPianoSamples()
    {
        foreach (AudioClip clip in Resources.LoadAll<AudioClip>("notes"))
        {
            string name = clip.name.ToLower().Trim();

            char octaveChar = name[name.Length - 1];
            if (!char.IsDigit(octaveChar)) continue;

            string noteName = name.Substring(0, name.Length - 1);
            if (!NoteOffsets.TryGetValue(noteName, out int offset)) continue;

            int octave = (int)char.GetNumericValue(octaveChar);
            int midiNum = (octave + 1) * 12 + offset;

            pianoSamples[midiNum] = clip;
            availableMidiNotes.Add(midiNum);
        }

        availableMidiNotes.Sort();
    }

    private void BuildVoicePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.priority = 0;
            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            audioPool.Add(source);
        }
    }

    void Update()
    {
        if (directMidiReceiver == null) return;

        int dequeueCount = 0;
        while (directMidiReceiver.messageQueue.TryDequeue(out byte[] data))
        {
            packetsReceived++;
            dequeueCount++;
            ProcessMidi(data);
        }

        if (verboseMidiLogging && dequeueCount > 0)
            Debug.Log($"<color=green>[MidiAudioManager]</color> Dequeued {dequeueCount} evento(s) en este frame");
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

        if (!changed) return;

        bool resetOk = AudioSettings.Reset(config);
        Debug.Log($"<color=cyan>[MIDI Audio]</color> Low latency audio config | dspBuffer={config.dspBufferSize} | " +
                  $"realVoices={config.numRealVoices} | reset={resetOk}");
    }

    /// <summary>Parsea un paquete MIDI binario de 3 bytes (status, nota, velocidad).</summary>
    void ProcessMidi(byte[] data)
    {
        if (data.Length != 3)
        {
            Debug.LogWarning($"<color=yellow>[MIDI]</color> Paquete incorrecto: {data.Length} bytes (esperaba 3)");
            return;
        }

        byte status = data[0];
        byte note = data[1];
        byte vel = data[2];

        int msgType = (status & 0xF0) switch
        {
            0x90 => vel > 0 ? MsgNoteOn : MsgNoteOff, // Note On con velocidad 0 equivale a Note Off
            0x80 => MsgNoteOff,
            0xB0 => MsgControlChange,
            _ => -1
        };

        if (msgType == -1) return;

        if (verboseMidiLogging)
        {
            string typeStr = msgType switch
            {
                MsgNoteOff => "NOTE OFF",
                MsgNoteOn => "NOTE ON ",
                _ => "CC (Pedal)"
            };
            Debug.Log($"<color=cyan>[MIDI RX]</color> #{packetsReceived:D5} | 0x{status:X2} | {typeStr} | Nota: {note} | Vel: {vel}");
        }

        if (msgType == MsgControlChange)
        {
            isPedalDown = vel >= 64;
            if (!isPedalDown) ReleaseSustain();
            return;
        }

        if (msgType == MsgNoteOn)
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

    void PlayNote(int targetNote, int vel)
    {
        if (availableMidiNotes.Count == 0)
        {
            Debug.LogError("<color=red>[MIDI]</color> No hay samples cargados!");
            return;
        }

        // Se usa el sample más cercano y se ajusta el pitch por semitonos.
        int bestBaseNote = availableMidiNotes[0];
        float minDiff = float.MaxValue;
        foreach (int n in availableMidiNotes)
        {
            float diff = Mathf.Abs(targetNote - n);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestBaseNote = n;
            }
        }

        if (activeNotes.TryGetValue(targetNote, out AudioSource previousSource))
        {
            previousSource.Stop();
            activeNotes.Remove(targetNote);
        }

        AudioSource foundSource = null;
        int sourceIndex = -1;
        for (int i = 0; i < audioPool.Count; i++)
        {
            if (audioPool[i].isPlaying) continue;

            foundSource = audioPool[i];
            sourceIndex = i;
            break;
        }

        if (foundSource == null)
        {
            Debug.LogWarning($"<color=yellow>[MIDI]</color> Pool de voces lleno (necesita {audioPool.Count + 1})");
            return;
        }

        foundSource.clip = pianoSamples[bestBaseNote];
        if (foundSource.clip != null && foundSource.clip.loadState == AudioDataLoadState.Unloaded)
            foundSource.clip.LoadAudioData();

        foundSource.pitch = Mathf.Pow(2.0f, (targetNote - bestBaseNote) / 12.0f);
        foundSource.volume = Mathf.Clamp01(Mathf.Pow(vel / 127f, velocityCurve) * volumeBoost);
        foundSource.Play();

        activeNotes[targetNote] = foundSource;
        sustainedNotes.Remove(targetNote);

        if (verboseMidiLogging)
        {
            string sampleName = foundSource.clip != null ? foundSource.clip.name : "NULL";
            Debug.Log($"<color=green>[MIDI PLAY]</color> MIDI{targetNote} | Vel{vel}/127 | Pitch{foundSource.pitch:F2}x | " +
                      $"Vol{foundSource.volume:F3} | Src{sourceIndex}/{audioPool.Count} | Sample:{sampleName}");
        }
    }

    void StopNote(int note)
    {
        if (isPedalDown)
        {
            sustainedNotes.Add(note);
        }
        else if (activeNotes.TryGetValue(note, out AudioSource source))
        {
            source.Stop();
            activeNotes.Remove(note);
        }
    }

    /// <summary>Al soltar el pedal, corta las notas sostenidas que ya no estén pulsadas.</summary>
    void ReleaseSustain()
    {
        sustainReleaseBuffer.Clear();
        foreach (int n in sustainedNotes)
        {
            if (!currentlyPressedNotes.Contains(n)) sustainReleaseBuffer.Add(n);
        }

        foreach (int midiNote in sustainReleaseBuffer)
        {
            if (activeNotes.TryGetValue(midiNote, out AudioSource source))
            {
                source.Stop();
                activeNotes.Remove(midiNote);
            }

            sustainedNotes.Remove(midiNote);
        }
    }

    public void InitializeApplauseSystem()
    {
        if (applauseSource == null)
        {
            applauseSource = gameObject.AddComponent<AudioSource>();
            applauseSource.playOnAwake = false;
            applauseSource.spatialBlend = 0;
            applauseSource.volume = 0f;
            applauseSource.priority = 0;
            applauseSource.bypassEffects = true;
            applauseSource.bypassListenerEffects = true;
            applauseSource.bypassReverbZones = true;
            applauseSource.ignoreListenerPause = true;
        }

        RefreshApplauseClipForCurrentIntensity();
    }

    /// <summary>
    /// Ajusta el volumen de los aplausos al score del público (0-100):
    /// silencio por debajo del 35% y volumen pleno a partir del 80%.
    /// </summary>
    public void SetApplauseVolume(float publicScore)
    {
        if (applauseSource == null || applauseSource.clip == null) return;

        const float audibleThreshold = 0.35f;
        const float fullVolumeThreshold = 0.8f;
        float normalizedScore = Mathf.Clamp01(publicScore / 100f);

        if (normalizedScore <= audibleThreshold)
        {
            applauseSource.volume = applauseMinAudibleVolume;
            return;
        }

        float t = Mathf.InverseLerp(audibleThreshold, fullVolumeThreshold, normalizedScore);
        applauseSource.volume = Mathf.Lerp(applauseMinAudibleVolume, Mathf.Clamp(applauseMaxVolume, 0f, 1f), t);
    }

    public void StartApplauseLoop()
    {
        if (applauseSource != null && applauseSource.clip != null && !applauseSource.isPlaying)
            applauseSource.Play();
    }

    public void StopApplauseLoop()
    {
        if (applauseSource != null && applauseSource.isPlaying) applauseSource.Stop();
    }

    /// <summary>Selecciona el clip de aplausos acorde a la intensidad configurada por el usuario.</summary>
    public void RefreshApplauseClipForCurrentIntensity()
    {
        if (applauseSource == null) return;

        AudioClip resolvedApplauseClip = ResolveApplauseClip();
        if (resolvedApplauseClip == null)
        {
            Debug.LogWarning("<color=yellow>[MIDI Audio]</color> No se encontró un clip de aplausos utilizable");
            return;
        }

        bool clipChanged = applauseSource.clip != resolvedApplauseClip;
        bool wasPlaying = applauseSource.isPlaying;
        float cachedVolume = applauseSource.volume;

        if (clipChanged && wasPlaying) applauseSource.Stop();

        applauseSource.clip = resolvedApplauseClip;
        applauseSource.loop = true;
        applauseSource.volume = cachedVolume;

        if (applauseSource.clip.loadState == AudioDataLoadState.Unloaded) applauseSource.clip.LoadAudioData();

        if (clipChanged)
            Debug.Log($"<color=cyan>[MIDI Audio]</color> Clip de aplausos seleccionado: {resolvedApplauseClip.name}");

        if (wasPlaying && !applauseSource.isPlaying) applauseSource.Play();
    }

    /// <summary>
    /// Busca un clip de aplausos por orden de preferencia: el de la intensidad actual,
    /// el genérico de este componente, el de otro MidiAudioManager, Resources, el
    /// controlador de audiencia y por último cualquier AudioSource de la escena.
    /// </summary>
    private AudioClip ResolveApplauseClip()
    {
        AudioClip intensityClip = ResolveIntensityApplauseClip();
        if (intensityClip != null) return intensityClip;
        if (applauseClip != null) return applauseClip;

        foreach (MidiAudioManager otherManager in FindObjectsOfType<MidiAudioManager>(true))
        {
            if (otherManager == null || otherManager == this) continue;
            if (otherManager.applauseClip != null) return otherManager.applauseClip;
            if (otherManager.applauseSource != null && otherManager.applauseSource.clip != null)
                return otherManager.applauseSource.clip;
        }

        AudioClip resourceClip = Resources.Load<AudioClip>("Sounds/aplause");
        if (resourceClip != null) return resourceClip;

        ControladorAudiencia audienceController = FindObjectOfType<ControladorAudiencia>(true);
        if (audienceController != null && audienceController.fuenteAplausos != null)
        {
            AudioClip audienceClip = audienceController.fuenteAplausos.clip;
            if (audienceClip != null) return audienceClip;
        }

        foreach (AudioSource sceneAudioSource in FindObjectsOfType<AudioSource>(true))
        {
            if (sceneAudioSource == null || sceneAudioSource.clip == null) continue;

            string clipName = sceneAudioSource.clip.name.ToLowerInvariant();
            if (clipName.Contains("aplause") || clipName.Contains("applause")) return sceneAudioSource.clip;
        }

        return null;
    }

    private AudioClip ResolveIntensityApplauseClip()
    {
        AudioClip intensityClip = PianoAudienceIntensityProfile.ResolveCurrentProfile().NormalizedIntensity switch
        {
            PianoAudienceIntensityProfile.Low => applauseClipLow,
            PianoAudienceIntensityProfile.High => applauseClipHigh,
            _ => applauseClipMedium
        };

        return intensityClip != null ? intensityClip : applauseClip;
    }
}
