using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Representa una nota musical (línea de duración) que se mueve hacia la derecha
/// Se comporta como Piano Tiles: zona de hit es una trituradora fija
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MusicNote : MonoBehaviour
{
    private class BurnSegmentVisual
    {
        public GameObject segmentObject;
        public float startOffset;
        public float endOffset;
    }

    [Header("Datos de la Nota")]
    public int midiNote; // Número MIDI (60 = C4)
    public float duration; // Duración en segundos (longitud total inicial)
    public float spawnTime; // Tiempo en el que debe aparecer
    public string hand; // "right" o "left"
    
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 1.0f; // Velocidad de avance hacia adelante
    [SerializeField] private float destroyAfterEndDelay = 1.0f;
    
    [Header("Visual")]
    [SerializeField] private Color rightHandColor = new Color(0.2f, 0.6f, 1f); // Azul
    [SerializeField] private Color leftHandColor = new Color(1f, 0.4f, 0.2f);  // Naranja
    [SerializeField] private float noteLabelFontSize = 100; // Tamaño del texto de la nota
    [SerializeField] private Color unburnedColor = new Color(1f, 1f, 0.3f);
    [SerializeField] private Color burnedColor = new Color(1f, 0.35f, 0.1f);
    
    private Rigidbody rb;
    private bool isActive = true;
    private Vector3 targetDirection;
    private GameObject durationLine; // Línea (cuerpo de la nota)
    private GameObject burnedLine;
    
    // Referencias para detección de hit
    private StaffRenderer staffRenderer; // Parent - el pentagrama
    private MidiAudioManager midiAudioManager;
    private float originalLineLength = 0f; // Longitud original de la línea (mapea de duration)
    private Material durationLineMaterial; // Material de la línea
    private float fallbackStartTime = 0f;
    private Vector3 localSpawnPosition;
    private Vector3 localHitPosition;
    private readonly List<BurnSegmentVisual> burnSegments = new List<BurnSegmentVisual>();
    private BurnSegmentVisual activeBurnSegment;
    private bool isPreviewMode = false;
    
    /// <summary>
    /// Obtener todas las notas activas en pantalla
    /// </summary>
    public static List<MusicNote> GetActiveNotes()
    {
        return FindObjectsOfType<MusicNote>(true).ToList();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        Renderer parentRenderer = GetComponent<Renderer>();
        if (parentRenderer != null) parentRenderer.enabled = false;
        
        Collider parentCollider = GetComponent<Collider>();
        if (parentCollider != null) Destroy(parentCollider);
    }
    
    void OnDestroy()
    {
        // Limpiar referencias si es necesario
    }

    /// <summary>
    /// Inicializa la nota con sus datos y dirección de movimiento
    /// </summary>
    public void Initialize(PianoNoteData noteData, Vector3 startPosition, Vector3 hitPosition, float speedOverride = -1)
    {
        midiNote = noteData.midi;
        duration = noteData.duration;
        spawnTime = noteData.time;
        hand = noteData.hand;
        localSpawnPosition = startPosition;
        localHitPosition = hitPosition;
        
        staffRenderer = transform.parent?.GetComponent<StaffRenderer>();
        midiAudioManager = FindObjectOfType<MidiAudioManager>();
        
        targetDirection = (hitPosition - startPosition).normalized;
        
        if (speedOverride > 0)
        {
            moveSpeed = speedOverride;
        }

        fallbackStartTime = Time.time;
        transform.localPosition = CalculateHeadPosition(GetCurrentSongTime());
        
        CreateDurationLine(speedOverride);
    }
    
    void Update()
    {
        if (!isActive) return;

        if (isPreviewMode)
        {
            return;
        }

        float songTime = GetCurrentSongTime();
        transform.localPosition = CalculateHeadPosition(songTime);

        bool isPlayableWindow = songTime >= spawnTime && songTime <= (spawnTime + duration);
        bool isPressedNow = midiAudioManager != null && midiAudioManager.IsNotePressedNow(midiNote);

        if (isPlayableWindow && isPressedNow)
        {
            ExtendBurnSegment(songTime);
        }
        else
        {
            activeBurnSegment = null;
        }

        if (songTime > spawnTime + duration + destroyAfterEndDelay)
        {
            isActive = false;
            Destroy(gameObject);
        }
    }

    private float GetCurrentSongTime()
    {
        PianoGameManager gameManager = PianoGameManager.Instance;
        if (gameManager != null && gameManager.BackgroundMusicSource != null && gameManager.BackgroundMusicSource.isPlaying)
        {
            return gameManager.BackgroundMusicSource.time;
        }

        return Time.time - fallbackStartTime;
    }

    private Vector3 CalculateHeadPosition(float songTime)
    {
        float remainingUntilHit = spawnTime - songTime;
        return localHitPosition - (targetDirection * moveSpeed * remainingUntilHit);
    }
    
    void Start()
    {
        // Las notas NO se destruyen jamás - solo avanzan infinitamente
        // Comentar el Destroy automático
        // Destroy(gameObject, 10f);
    }

    /// <summary>
    /// Marks note as failed
    /// </summary>
    public void OnNoteMissed()
    {
        isActive = false;
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Called when MIDI key is released
    /// </summary>
    public void OnNoteRelease()
    {
    }

    public void SetPreviewPose(float songTime)
    {
        isPreviewMode = true;
        transform.localPosition = CalculateHeadPosition(songTime);
    }

    public void ExitPreviewMode()
    {
        isPreviewMode = false;
    }
    
    /// <summary>
    /// Crea una línea que representa la duración de la nota CON EL NOMBRE incluido
    /// </summary>
    private void CreateDurationLine(float noteSpeed)
    {
        if (duration <= 0) return; // No crear línea para notas sin duración
        
        // Calcular la longitud basada en duración y velocidad (usa moveSpeed si noteSpeed < 0)
        float speed = noteSpeed > 0 ? noteSpeed : moveSpeed;
        float lineLength = speed * duration;
        
        // Evitar escalas exageradas sin aplastar notas largas de forma agresiva
        lineLength = Mathf.Min(lineLength, 8f);
        
        // GUARDAR LA LONGITUD FINAL
        originalLineLength = lineLength;
        
        durationLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        durationLine.name = $"DurationLine_MIDI{midiNote}";
        durationLine.transform.SetParent(transform, false);
        
        durationLine.transform.localPosition = new Vector3(lineLength / 2f, 0, 0);
        durationLine.transform.localScale = new Vector3(lineLength, 0.08f, 0.08f);
        
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        
        durationLineMaterial = new Material(shader);
        durationLineMaterial.color = unburnedColor;
        
        Renderer renderer = durationLine.GetComponent<Renderer>();
        renderer.material = durationLineMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Quitar collider (es solo visual)
        Collider collider = durationLine.GetComponent<Collider>();
        if (collider != null) DestroyImmediate(collider);

        burnedLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(burnedLine);

        GameObject labelObj = new GameObject("HeadNoteLabel");
        labelObj.transform.SetParent(transform, false);
        labelObj.transform.localPosition = new Vector3(0f, 0f, -0.055f);
        labelObj.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = MidiToNoteName(midiNote);
        textMesh.fontSize = (int)noteLabelFontSize;
        textMesh.characterSize = 0.0125f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        textMesh.richText = false;
    }

    private void ExtendBurnSegment(float songTime)
    {
        if (durationLine == null || originalLineLength <= 0f || duration <= 0.0001f)
        {
            return;
        }

        float currentOffset = Mathf.Clamp(songTime - spawnTime, 0f, duration);
        float segmentStep = Mathf.Max(Time.deltaTime, 0.01f);

        if (activeBurnSegment == null)
        {
            activeBurnSegment = CreateBurnSegment(currentOffset);
            burnSegments.Add(activeBurnSegment);
        }

        activeBurnSegment.endOffset = Mathf.Max(activeBurnSegment.endOffset, currentOffset + segmentStep);
        activeBurnSegment.endOffset = Mathf.Min(activeBurnSegment.endOffset, duration);
        UpdateBurnSegmentVisual(activeBurnSegment);
    }

    private BurnSegmentVisual CreateBurnSegment(float startOffset)
    {
        GameObject segmentObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segmentObject.name = $"BurnedSegment_MIDI{midiNote}";
        segmentObject.transform.SetParent(transform, false);

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        Material segmentMaterial = new Material(shader);
        segmentMaterial.color = burnedColor;

        Renderer segmentRenderer = segmentObject.GetComponent<Renderer>();
        segmentRenderer.material = segmentMaterial;
        segmentRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        segmentRenderer.receiveShadows = false;

        Collider segmentCollider = segmentObject.GetComponent<Collider>();
        if (segmentCollider != null) DestroyImmediate(segmentCollider);

        BurnSegmentVisual segment = new BurnSegmentVisual
        {
            segmentObject = segmentObject,
            startOffset = startOffset,
            endOffset = startOffset
        };

        UpdateBurnSegmentVisual(segment);
        return segment;
    }

    private void UpdateBurnSegmentVisual(BurnSegmentVisual segment)
    {
        if (segment == null || segment.segmentObject == null)
        {
            return;
        }

        float normalizedStart = Mathf.Clamp01(segment.startOffset / duration);
        float normalizedEnd = Mathf.Clamp01(segment.endOffset / duration);
        float segmentLength = Mathf.Max((normalizedEnd - normalizedStart) * originalLineLength, 0.001f);
        float segmentStartX = normalizedStart * originalLineLength;

        segment.segmentObject.transform.localPosition = new Vector3(segmentStartX + (segmentLength * 0.5f), 0f, 0f);
        segment.segmentObject.transform.localScale = new Vector3(segmentLength, 0.1f, 0.1f);
    }
    
    /// <summary>
    /// Convierte un número MIDI a nombre de nota musical
    /// Ejemplo: 60 -> "C4", 61 -> "C#4", 72 -> "C5"
    /// </summary>
    public static string MidiToNoteName(int midiNumber)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        
        int octave = (midiNumber / 12) - 1; // MIDI 60 = C4
        int noteIndex = midiNumber % 12;
        
        return noteNames[noteIndex] + octave;
    }

    /// <summary>
    /// Se llama cuando la nota sale del área de juego
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("PlayArea"))
        {
            if (isActive)
            {
                OnNoteMissed();
            }
        }
    }
}
