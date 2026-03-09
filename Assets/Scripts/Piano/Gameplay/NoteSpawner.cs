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
    [SerializeField] private float spawnAheadTime = 3f; // Cuántos segundos antes spawear
    [SerializeField] private float noteSpeed = 1.5f; // Velocidad de las notas (m/s)
    
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
        
        // Combinar todas las notas de melodía en una lista ordenada por tiempo
        if (songData.melody != null)
        {
            allNotes.AddRange(songData.melody);
        }
        
        // Ordenar por tiempo de aparición
        allNotes.Sort((a, b) => a.time.CompareTo(b.time));
        
        Debug.Log($"[NoteSpawner] Canción cargada: {allNotes.Count} notas preparadas");
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
        
        // Calcular posición de spawn (extremo derecho del pentagrama)
        Vector3 spawnPos = targetStaff.GetSpawnPoint();
        
        // Ajustar altura según la nota MIDI
        float noteY = targetStaff.GetNoteYPosition(noteData.midi);
        spawnPos.y += noteY;
        
        // Calcular posición de hit (línea de acierto)
        Vector3 hitPos = targetStaff.GetHitPoint();
        hitPos.y = spawnPos.y; // Misma altura
        
        // Calcular velocidad necesaria para llegar al hit point en el tiempo correcto
        float distance = Vector3.Distance(spawnPos, hitPos);
        float travelTime = spawnAheadTime;
        float calculatedSpeed = distance / travelTime;
        
        // Inicializar la nota
        note.Initialize(noteData, spawnPos, hitPos, calculatedSpeed);
        
        Debug.Log($"[NoteSpawner] Nota spawneada: MIDI {noteData.midi} en {spawnPos}");
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
