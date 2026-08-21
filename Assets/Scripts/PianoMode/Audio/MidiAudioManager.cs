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
    private const int MidiNoteCount = 128;

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
    [Range(0.5f, 12f)] public float volumeBoost = 6.0f;
    [Range(0.8f, 2.5f)] public float velocityCurve = 1.15f;
    public int poolSize = 48;
    [SerializeField] private bool optimizeLowLatency = true;
    [SerializeField] private int targetDspBufferSize = 128;
    [SerializeField] private int targetRealVoices = 128;
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
    private readonly AudioClip[] nearestClipByMidi = new AudioClip[MidiNoteCount];
    private readonly float[] nearestPitchByMidi = new float[MidiNoteCount];
    private readonly Queue<AudioSource> freeVoices = new Queue<AudioSource>(64);
    private readonly Dictionary<int, AudioSource> activeNotes = new Dictionary<int, AudioSource>();
    private readonly HashSet<int> sustainedNotes = new HashSet<int>();
    private readonly HashSet<int> currentlyPressedNotes = new HashSet<int>();
    private readonly List<int> sustainReleaseBuffer = new List<int>();
    private bool isPedalDown = false;
    private int packetsReceived = 0;
    private bool subscribedToImmediateMidi;

    /// <summary>True mientras la tecla siga pulsada (sin note off).</summary>
    public bool IsNotePressedNow(int midiNote) => currentlyPressedNotes.Contains(midiNote);

    public void SetPianoVolume(float volume)
    {
        // Sube el techo: en Quest el piano se oía muy bajo.
        volumeBoost = Mathf.Clamp(volume * 4.5f, 3.5f, 12f);
        Debug.Log($"<color=cyan>[MIDI Audio]</color> Piano volume set to {volumeBoost:F3}");
    }

    void Awake()
    {
        if (optimizeLowLatency) ApplyLowLatencyAudioConfiguration();
    }

    void Start()
    {
        poolSize = Mathf.Max(poolSize, 48);
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
        BuildNearestSampleLookup();
        BuildVoicePool();
        SubscribeImmediateMidi();

        if (pianoSamples.Count == 0)
        {
            Debug.LogError("<color=red>[MIDI ERROR]</color> No se cargaron samples! Verifica que Resources/notes/ contenga audio.");
            return;
        }

        Debug.Log($"<color=green>[MIDI INIT]</color> {pianoSamples.Count} samples (MIDI {availableMidiNotes[0]}-" +
                  $"{availableMidiNotes[availableMidiNotes.Count - 1]}) | pool={poolSize} voces | " +
                  $"volumeBoost={volumeBoost:F2}x | velocityCurve={velocityCurve:F2} | dsp={AudioSettings.GetConfiguration().dspBufferSize}");
    }

    void OnEnable() => SubscribeImmediateMidi();

    void OnDisable() => UnsubscribeImmediateMidi();

    void OnDestroy() => UnsubscribeImmediateMidi();

    private void SubscribeImmediateMidi()
    {
        if (subscribedToImmediateMidi || directMidiReceiver == null) return;

        directMidiReceiver.OnRawMidiEvent += ProcessMidiBytes;
        subscribedToImmediateMidi = true;
    }

    private void UnsubscribeImmediateMidi()
    {
        if (!subscribedToImmediateMidi || directMidiReceiver == null) return;

        directMidiReceiver.OnRawMidiEvent -= ProcessMidiBytes;
        subscribedToImmediateMidi = false;
    }

    /// <summary>Mapea los samples "c2", "c#2", ... a su número MIDI y precarga el audio en RAM.</summary>
    private void LoadPianoSamples()
    {
        foreach (AudioClip clip in Resources.LoadAll<AudioClip>("notes"))
        {
            string name = clip.name.ToLower().Trim();
            if (string.IsNullOrEmpty(name)) continue;

            char octaveChar = name[name.Length - 1];
            if (!char.IsDigit(octaveChar)) continue;

            string noteName = name.Substring(0, name.Length - 1);
            if (!NoteOffsets.TryGetValue(noteName, out int offset)) continue;

            int octave = (int)char.GetNumericValue(octaveChar);
            int midiNum = (octave + 1) * 12 + offset;

            pianoSamples[midiNum] = clip;
            availableMidiNotes.Add(midiNum);

            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }

        availableMidiNotes.Sort();
    }

    /// <summary>O(1) en cada Note On: clip más cercano + pitch ya precalculados.</summary>
    private void BuildNearestSampleLookup()
    {
        if (availableMidiNotes.Count == 0) return;

        for (int targetNote = 0; targetNote < MidiNoteCount; targetNote++)
        {
            int bestBaseNote = availableMidiNotes[0];
            int minDiff = Mathf.Abs(targetNote - bestBaseNote);

            for (int i = 1; i < availableMidiNotes.Count; i++)
            {
                int candidate = availableMidiNotes[i];
                int diff = Mathf.Abs(targetNote - candidate);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestBaseNote = candidate;
                }
            }

            nearestClipByMidi[targetNote] = pianoSamples[bestBaseNote];
            nearestPitchByMidi[targetNote] = Mathf.Pow(2f, (targetNote - bestBaseNote) / 12f);
        }
    }

    private void BuildVoicePool()
    {
        freeVoices.Clear();

        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            source.dopplerLevel = 0f;
            source.mute = false;
            freeVoices.Enqueue(source);
        }
    }

    void Update()
    {
        // Fallback si nadie está suscrito al callback inmediato (p. ej. orden de init).
        if (directMidiReceiver == null || subscribedToImmediateMidi) return;

        while (directMidiReceiver.messageQueue.TryDequeue(out byte[] data))
        {
            if (data == null || data.Length < 3) continue;
            ProcessMidiBytes(data[0], data[1], data[2]);
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

        if (config.numVirtualVoices < Mathf.Max(config.numRealVoices, 256))
        {
            config.numVirtualVoices = Mathf.Max(config.numRealVoices, 256);
            changed = true;
        }

        if (!changed) return;

        bool resetOk = AudioSettings.Reset(config);
        Debug.Log($"<color=cyan>[MIDI Audio]</color> Low latency audio config | dspBuffer={config.dspBufferSize} | " +
                  $"realVoices={config.numRealVoices} | virtualVoices={config.numVirtualVoices} | reset={resetOk}");
    }

    /// <summary>Entrada MIDI inmediata (sin esperar otro Update).</summary>
    public void ProcessMidiBytes(byte status, byte note, byte vel)
    {
        packetsReceived++;

        int msgType = (status & 0xF0) switch
        {
            0x90 => vel > 0 ? MsgNoteOn : MsgNoteOff,
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
            if (note != 64) return; // solo sustain pedal

            isPedalDown = vel >= 64;
            if (!isPedalDown) ReleaseSustain();
            return;
        }

        if (msgType == MsgNoteOn)
        {
            // Audio primero; scoring/visual después.
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
        if ((uint)targetNote >= MidiNoteCount) return;

        AudioClip clip = nearestClipByMidi[targetNote];
        if (clip == null) return;

        if (activeNotes.TryGetValue(targetNote, out AudioSource previousSource))
        {
            previousSource.Stop();
            activeNotes.Remove(targetNote);
            freeVoices.Enqueue(previousSource);
        }

        if (!TryAcquireVoice(out AudioSource voice))
        {
            if (verboseMidiLogging)
                Debug.LogWarning($"<color=yellow>[MIDI]</color> Pool de voces lleno ({poolSize})");
            return;
        }

        float normalizedVelocity = Mathf.Clamp01(vel / 127f);
        // Curva suave + boost alto: teclas suaves siguen audibles.
        float gain = Mathf.Pow(normalizedVelocity, velocityCurve) * volumeBoost;

        voice.clip = clip;
        voice.pitch = nearestPitchByMidi[targetNote];
        voice.volume = Mathf.Clamp(gain, 0.05f, 1f);
        voice.Play();

        activeNotes[targetNote] = voice;
        sustainedNotes.Remove(targetNote);
    }

    private bool TryAcquireVoice(out AudioSource voice)
    {
        while (freeVoices.Count > 0)
        {
            voice = freeVoices.Dequeue();
            if (voice != null) return true;
        }

        // Pool agotado: reutiliza cualquier voz ya detenida, o la primera activa.
        int stealMidi = -1;
        AudioSource stealVoice = null;

        foreach (KeyValuePair<int, AudioSource> pair in activeNotes)
        {
            if (pair.Value == null) continue;

            if (!pair.Value.isPlaying)
            {
                stealMidi = pair.Key;
                stealVoice = pair.Value;
                break;
            }

            if (stealVoice == null)
            {
                stealMidi = pair.Key;
                stealVoice = pair.Value;
            }
        }

        if (stealVoice == null)
        {
            voice = null;
            return false;
        }

        stealVoice.Stop();
        activeNotes.Remove(stealMidi);
        voice = stealVoice;
        return true;
    }

    void StopNote(int note)
    {
        if (isPedalDown)
        {
            sustainedNotes.Add(note);
            return;
        }

        if (!activeNotes.TryGetValue(note, out AudioSource source)) return;

        source.Stop();
        activeNotes.Remove(note);
        freeVoices.Enqueue(source);
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
                freeVoices.Enqueue(source);
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
