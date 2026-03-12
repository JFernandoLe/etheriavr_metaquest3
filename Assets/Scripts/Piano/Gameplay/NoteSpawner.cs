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
    [SerializeField] private float spawnAheadTime = 6f; // Cuántos segundos antes spawear (MÁS TIEMPO = notas más lentas)
    [SerializeField] private float noteSpeed = 1.0f; // Velocidad base (se calcula dinámicamente)
    
    [Header("Modo Continuo")]
    [SerializeField] private bool useContinuousMode = true; // Generar notas automáticamente por 1 minuto
    [SerializeField] private float continuousDuration = 60f; // Duración en segundos (1 minuto)
    [SerializeField] private float noteInterval = 0.5f; // Intervalo entre notas/acordes (segundos)
    
    private PianoSongData currentSong;
    private List<PianoNoteData> allNotes = new List<PianoNoteData>();
    private int nextNoteIndex = 0;
    private bool isSpawning = false;
    private float songStartTime;
    private float currentSongTime = 0f;

    /// <summary>
    /// Carga la canción y prepara las notas para spawn
    /// </summary>
    public void LoadSong(PianoSongData songData)
    {
        currentSong = songData;
        allNotes.Clear();
        nextNoteIndex = 0;
        
        // MODO CONTINUO: Generar notas automáticamente por 1 minuto
        if (useContinuousMode)
        {
            GenerateContinuousNotes();
            Debug.Log($"[NoteSpawner] MODO CONTINUO: {allNotes.Count} notas generadas para {continuousDuration}s");
            return;
        }
        
        // Modo normal: usar datos de la canción
        if (songData.melody != null)
        {
            allNotes.AddRange(songData.melody);
        }
        
        // Ordenar por tiempo de aparición
        allNotes.Sort((a, b) => a.time.CompareTo(b.time));
        
        Debug.Log($"[NoteSpawner] Canción cargada: {allNotes.Count} notas preparadas");
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
        if (currentSong == null)
        {
            Debug.LogError("[NoteSpawner] No hay canción cargada!");
            return;
        }
        
        isSpawning = true;
        songStartTime = Time.time;
        currentSongTime = 0f;
        nextNoteIndex = 0;
        
        Debug.Log("[NoteSpawner] Spawn iniciado");
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
        
        // Actualizar tiempo de la canción
        currentSongTime = Time.time - songStartTime;
        
        // Spawear notas que deben aparecer ahora
        while (nextNoteIndex < allNotes.Count)
        {
            PianoNoteData note = allNotes[nextNoteIndex];
            
            // Calcular cuándo debe aparecer la nota (antes del tiempo real para dar tiempo de reacción)
            float spawnTime = note.time - spawnAheadTime;
            
            if (currentSongTime >= spawnTime)
            {
                SpawnNote(note);
                nextNoteIndex++;
            }
            else
            {
                // Ya no hay más notas listas para spawear
                break;
            }
        }
        
        // Verificar si terminamos de spawear todas las notas
        if (nextNoteIndex >= allNotes.Count)
        {
            Debug.Log("[NoteSpawner] Todas las notas spawneadas");
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
            Debug.LogWarning($"[NoteSpawner] No hay pentagrama para mano {noteData.hand}");
            return;
        }
        
        // Crear instancia de la nota
        GameObject noteObj = Instantiate(notePrefab);
        MusicNote note = noteObj.GetComponent<MusicNote>();
        
        if (note == null)
        {
            Debug.LogError("[NoteSpawner] El prefab no tiene componente MusicNote!");
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
        
        // Calcular velocidad necesaria para llegar al hit point EXACTAMENTE en el tiempo correcto
        // La distancia es entre spawn point (izquierda) y hit point (derecha)
        float distance = Vector3.Distance(spawnPos, hitPos);
        
        // El tiempo de viaje debe ser igual a spawnAheadTime para sincronización perfecta
        // Velocidad = Distancia / Tiempo
        float calculatedSpeed = distance / spawnAheadTime;
        
        // Inicializar la nota con velocidad calculada precisa
        note.Initialize(noteData, spawnPos, hitPos, calculatedSpeed);
        
        Debug.Log($"[NoteSpawner] ✅ Nota: MIDI {noteData.midi} ({noteData.hand}) | Y={noteY:F3}m | Tiempo: {noteData.time}s | Vel: {calculatedSpeed:F2}m/s");
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
