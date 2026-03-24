using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Genera notas musicales en el momento correcto según los datos de la canción
/// </summary>
public class NoteSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject notePrefab; // Prefab de la nota (esfera)
    [SerializeField] private StaffRenderer trebleStaff; // Pentagrama de clave de Sol
    [SerializeField] private StaffRenderer bassStaff; // Pentagrama de clave de Fa
    
    [Header("Configuración")]
    [SerializeField] private float noteTravelTime = 4.5f; // Tiempo visible antes de tocar la hit line
    
    [Header("Modo Continuo")]
    private bool useContinuousMode = false; // ❌ SIEMPRE DESHABILITADO - Cargar del JSON en su lugar (no editable en Inspector)
    [SerializeField] private float continuousDuration = 60f; // Duración en segundos (1 minuto)
    [SerializeField] private float noteInterval = 0.5f; // Intervalo entre notas/acordes (segundos)
    
    private PianoSongData currentSong;
    private List<PianoNoteData> allNotes = new List<PianoNoteData>();
    private int nextNoteIndex = 0;
    private bool isSpawning = false;
    private float songStartTime;
    private float currentSongTime = 0f;
    private float currentScrollSpeed = 0.4f;

    /// <summary>
    /// Carga la canción y prepara las notas para spawn
    /// Soporta AMBOS formatos: melody (antiguo) y all_notes (nuevo)
    /// </summary>
    public void LoadSong(PianoSongData songData)
    {
        currentSong = songData;
        allNotes.Clear();
        nextNoteIndex = 0;
        
        // DEBUG EXHAUSTIVO
        Debug.Log($"\n=== [NoteSpawner] INICIANDO CARGA DE CANCIÓN ===");
        Debug.Log($"[NoteSpawner] useContinuousMode = {useContinuousMode} (DEBE SER FALSE)");
        Debug.Log($"[NoteSpawner] songData es null? {songData == null}");
        
        if (songData != null)
        {
            Debug.Log($"[NoteSpawner] songData.all_notes = {(songData.all_notes == null ? "NULL" : songData.all_notes.Count + " elementos")}");
            Debug.Log($"[NoteSpawner] songData.melody = {(songData.melody == null ? "NULL" : songData.melody.Count + " elementos")}");
            Debug.Log($"[NoteSpawner] songData.song_name = {songData.song_name}");
        }
        
        // MODO CONTINUO: Generar notas automáticamente por 1 minuto
        if (useContinuousMode)
        {
            Debug.LogError("[NoteSpawner] ⚠️ MODO CONTINUO ACTIVO - ESTO NO DEBERÍA SUCEDER!");
            GenerateContinuousNotes();
            Debug.Log($"[NoteSpawner] ⚠️ MODO CONTINUO: {allNotes.Count} notas generadas (DESHABILITADO - usar JSON)");
            return;
        }
        
        // FORMATO NUEVO: all_notes (contiene GameNoteData con midi_notes[])
        if (songData.all_notes != null && songData.all_notes.Count > 0)
        {
            Debug.Log($"[NoteSpawner] 🟢 USANDO FORMATO NUEVO: {songData.all_notes.Count} notas en all_notes[]");
            
            // Convertir GameNoteData a PianoNoteData
            foreach (var gameNote in songData.all_notes)
            {
                // Cada GameNoteData puede tener múltiples MIDI notes (es un acorde)
                if (gameNote.midi_notes != null && gameNote.midi_notes.Length > 0)
                {
                    foreach (int midiNote in gameNote.midi_notes)
                    {
                        // Determinar mano según clef
                        string hand = (gameNote.clef == "bass") ? "left" : "right";
                        
                        PianoNoteData pianoNote = new PianoNoteData
                        {
                            midi = midiNote,
                            time = gameNote.time,
                            duration = gameNote.duration,
                            hand = hand
                        };
                        
                        allNotes.Add(pianoNote);
                    }
                }
            }
            
            Debug.Log($"[NoteSpawner] ✅ Convertidas {allNotes.Count} notas individuales desde all_notes");
        }
        // FORMATO ANTIGUO: melody + chords
        else if (songData.melody != null && songData.melody.Count > 0)
        {
            Debug.LogWarning($"[NoteSpawner] 🟡 USANDO FORMATO ANTIGUO: melody con {songData.melody.Count} notas");
            allNotes.AddRange(songData.melody);
            Debug.Log($"[NoteSpawner] Notas agregadas desde melody: {allNotes.Count}");
        }
        else
        {
            Debug.LogError("[NoteSpawner] 🔴 ¡¡¡NO HAY NOTAS PARA CARGAR!!! all_notes = null/vacío Y melody = null/vacío");
            return;
        }
        
        // Ordenar por tiempo de aparición
        allNotes.Sort((a, b) => a.time.CompareTo(b.time));

        RecalculateScrollSpeed();
        
        Debug.Log($"\n[NoteSpawner] ✅✅✅ CANCIÓN CARGADA CORRECTAMENTE ✅✅✅");
        Debug.Log($"[NoteSpawner] TOTAL: {allNotes.Count} notas");
        if (allNotes.Count > 0)
        {
            Debug.Log($"[NoteSpawner] ━━━━ PRIMERA NOTA ━━━━");
            Debug.Log($"[NoteSpawner]   MIDI: {allNotes[0].midi}");
            Debug.Log($"[NoteSpawner]   time en JSON: {allNotes[0].time:F3}s (audio-synced)");
            Debug.Log($"[NoteSpawner] ━━━━ ÚLTIMA NOTA ━━━━");
            Debug.Log($"[NoteSpawner]   MIDI: {allNotes[allNotes.Count-1].midi}");
            Debug.Log($"[NoteSpawner]   time: {allNotes[allNotes.Count-1].time:F3}s");
        }
        Debug.Log($"[NoteSpawner] ⏱️  Duración juego: {songData.GetGameDuration()}s | Audio: {(songData.backgroundAudioClip != null ? songData.backgroundAudioClip.length : 0):F1}s");
        Debug.Log($"[NoteSpawner] Scroll lead time: {noteTravelTime:F2}s | Scroll speed: {currentScrollSpeed:F2}m/s");
        Debug.Log($"=== [NoteSpawner] FIN CARGA ===\n");
    }

    private void RecalculateScrollSpeed()
    {
        float safeTravelTime = Mathf.Max(noteTravelTime, 0.1f);
        float trebleDistance = trebleStaff != null ? Vector3.Distance(trebleStaff.GetSpawnPoint(), trebleStaff.GetHitPoint()) : 0f;
        float bassDistance = bassStaff != null ? Vector3.Distance(bassStaff.GetSpawnPoint(), bassStaff.GetHitPoint()) : 0f;
        float referenceDistance = Mathf.Max(trebleDistance, bassDistance, 0.01f);
        currentScrollSpeed = referenceDistance / safeTravelTime;

        Debug.Log($"[NoteSpawner] 🎼 Scroll recalculado | Distancia ref={referenceDistance:F2}m | Lead={safeTravelTime:F2}s | Speed={currentScrollSpeed:F3}m/s");
    }
    
    /// <summary>
    /// Genera notas musicales continuas por 1 minuto
    /// Alterna entre notas individuales y acordes simples
    /// </summary>
    private void GenerateContinuousNotes()
    {
        // Escala de Do mayor (C major): C, D, E, F, G, A, B
        int[] majorScale = { 60, 62, 64, 65, 67, 69, 71, 72 }; // MIDI notes
        
        // Acordes simples en Do mayor
        int[][] chords = new int[][]
        {
            new int[] { 60, 64, 67 }, // C major (Do-Mi-Sol)
            new int[] { 62, 65, 69 }, // D minor (Re-Fa-La)
            new int[] { 64, 67, 71 }, // E minor (Mi-Sol-Si)
            new int[] { 65, 69, 72 }, // F major (Fa-La-Do)
            new int[] { 67, 71, 62 }, // G major (Sol-Si-Re)
        };
        
        float currentTime = 0f;
        int patternIndex = 0;
        
        while (currentTime < continuousDuration)
        {
            // Alternar entre nota individual (índice par) y acorde (índice impar)
            if (patternIndex % 2 == 0)
            {
                // Nota individual (mano derecha)
                int noteIndex = Random.Range(0, majorScale.Length);
                PianoNoteData note = new PianoNoteData
                {
                    midi = majorScale[noteIndex],
                    time = currentTime,
                    duration = noteInterval * 0.8f,
                    hand = "right"
                };
                allNotes.Add(note);
            }
            else
            {
                // Acorde (mano izquierda)
                int chordIndex = Random.Range(0, chords.Length);
                int[] chord = chords[chordIndex];
                
                foreach (int midi in chord)
                {
                    PianoNoteData note = new PianoNoteData
                    {
                        midi = midi - 12, // Una octava más grave para mano izquierda
                        time = currentTime,
                        duration = noteInterval * 1.2f,
                        hand = "left"
                    };
                    allNotes.Add(note);
                }
            }
            
            currentTime += noteInterval;
            patternIndex++;
        }
        
        // Ordenar por tiempo
        allNotes.Sort((a, b) => a.time.CompareTo(b.time));
    }

    /// <summary>
    /// Inicia el spawn de notas
    /// </summary>
    public void StartSpawning()
    {
        StartSpawningInternal(true);
    }

    public void ResumeSpawning()
    {
        StartSpawningInternal(false);
    }

    private void StartSpawningInternal(bool resetProgress)
    {
        if (currentSong == null)
        {
            Debug.LogError("[NoteSpawner] No hay canción cargada!");
            return;
        }
        
        isSpawning = true;
        if (resetProgress)
        {
            songStartTime = Time.time;
            currentSongTime = 0f;
            nextNoteIndex = 0;
        }
        
        Debug.Log(resetProgress ? "[NoteSpawner] Spawn iniciado" : "[NoteSpawner] Spawn reanudado");
    }

    /// <summary>
    /// Detiene el spawn de notas
    /// </summary>
    public void StopSpawning()
    {
        isSpawning = false;
        Debug.Log("[NoteSpawner] Spawn detenido");
    }

    void Update()
    {
        if (!isSpawning) return;
        
        // 🎵 Usar AUDIO TIME como fuente de verdad (perfectamente sincronizado)
        // Si no hay audio, fallback a local time
        PianoGameManager gameManager = PianoGameManager.Instance;
        if (gameManager != null && gameManager.BackgroundMusicSource != null && gameManager.BackgroundMusicSource.isPlaying)
        {
            currentSongTime = gameManager.BackgroundMusicSource.time;
        }
        else
        {
            // Fallback: usar relativo a start time local
            currentSongTime = Time.time - songStartTime;
        }
        
        // Spawear notas que deben aparecer ahora
        while (nextNoteIndex < allNotes.Count)
        {
            PianoNoteData note = allNotes[nextNoteIndex];
            
            // Calcular dinámicamente el spawnAheadTime basado en velocidad deseada
            StaffRenderer targetStaff = (note.hand == "right") ? trebleStaff : bassStaff;
            if (targetStaff != null)
            {
                float spawnTime = note.time - Mathf.Max(noteTravelTime, 0.1f);
                
                if (currentSongTime >= spawnTime)
                {
                    SpawnNote(note);
                    nextNoteIndex++;
                }
                else
                {
                    break;
                }
            }
            else
            {
                nextNoteIndex++; // Saltar si no hay staff
            }
        }
        
        // Verificar si terminamos de spawear todas las notas
        if (nextNoteIndex >= allNotes.Count && isSpawning)
        {
            Debug.Log($"[NoteSpawner] ✅ Todas las {allNotes.Count} notas spawneadas. Esperando finalización...");
            StopSpawning();
        }
    }

    /// <summary>
    /// Instancia una nota en el pentagrama correcto
    /// </summary>
    private void SpawnNote(PianoNoteData noteData)
    {
        // Determinar qué pentagrama usar
        StaffRenderer targetStaff = (noteData.hand == "right") ? trebleStaff : bassStaff;
        
        if (targetStaff == null)
        {
            Debug.LogError($"[NoteSpawner] ❌ No hay pentagrama para mano {noteData.hand}");
            return;
        }
        
        // Crear instancia de la nota
        GameObject noteObj = Instantiate(notePrefab);
        MusicNote note = noteObj.GetComponent<MusicNote>();
        
        if (note == null)
        {
            Debug.LogError("[NoteSpawner] ❌ El prefab no tiene componente MusicNote!");
            Destroy(noteObj);
            return;
        }
        
        // IMPORTANTE: Hacer que la nota sea hija del pentagrama para que se mueva con él
        noteObj.transform.SetParent(targetStaff.transform, false);
        
        // Calcular posición de spawn (extremo izquierdo del pentagrama) EN ESPACIO LOCAL
        Vector3 spawnPos = targetStaff.transform.InverseTransformPoint(targetStaff.GetSpawnPoint());
        
        // Ajustar altura según la nota MIDI - ESTO POSICIONA LA NOTA EN LA LÍNEA CORRECTA
        float noteY = targetStaff.GetNoteYPosition(noteData.midi);
        spawnPos.y = noteY; // Establecer Y directamente (no sumar)
        
        // Crear líneas ledger si la nota está fuera del pentagrama estándar
        targetStaff.CreateLedgerLinesForNote(noteY);
        
        // Calcular posición de hit (línea de acierto) EN ESPACIO LOCAL
        Vector3 hitPos = targetStaff.transform.InverseTransformPoint(targetStaff.GetHitPoint());
        hitPos.y = spawnPos.y; // Misma altura
        
        // Posición local
        noteObj.transform.localPosition = spawnPos;
        
        // Calcular velocidad y tiempo de spawn DINÁMICAMENTE basados en distancia y velocidad deseada
        Vector3 worldSpawnPos = targetStaff.GetSpawnPoint();
        Vector3 worldHitPos = targetStaff.GetHitPoint();
        float distance = Vector3.Distance(worldSpawnPos, worldHitPos);

        float safeTravelTime = Mathf.Max(noteTravelTime, 0.1f);
        float calculatedSpeed = distance / safeTravelTime;

        note.Initialize(noteData, spawnPos, hitPos, calculatedSpeed);
        
        // DEBUG: Información detallada de sincronización (comentado para reducir spam)
        // Debug.Log($"[NoteSpawner] 🎵 SPAWN Nota MIDI{noteData.midi} ({noteData.hand})\n" +
        //     $"  📍 YPos={noteY:F4}m | Duration={noteData.duration:F3}s\n" +
        //     $"  ⏱️  JSONtime={noteData.time:F3}s (audio-synced)\n" +
        //     $"  🚀 SpawnTime={(noteData.time - spawnAheadTime):F3}s | ArrivalTime={arrivalTime:F3}s\n" +
        //     $"  📏 Distance={distance:F3}m | TravelTime={spawnAheadTime:F2}s | Speed={calculatedSpeed:F2}m/s");
    }

    /// <summary>
    /// Limpia todas las notas activas (para reset o salida)
    /// </summary>
    public void ClearAllNotes()
    {
        MusicNote[] activeNotes = FindObjectsOfType<MusicNote>();
        foreach (MusicNote note in activeNotes)
        {
            Destroy(note.gameObject);
        }
        
        Debug.Log("[NoteSpawner] Todas las notas limpiadas");
    }

    void OnDisable()
    {
        StopSpawning();
    }
}
