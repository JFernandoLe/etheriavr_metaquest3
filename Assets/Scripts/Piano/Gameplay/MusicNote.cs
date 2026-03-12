using UnityEngine;

/// <summary>
/// Representa una nota musical que se mueve por el pentagrama hacia la línea de hit
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class MusicNote : MonoBehaviour
{
    [Header("Datos de la Nota")]
    public int midiNote; // Número MIDI (60 = C4)
    public float duration; // Duración en segundos
    public float spawnTime; // Tiempo en el que debe aparecer
    public string hand; // "right" o "left"
    
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 1.0f; // Velocidad inicial (se sobrescribe por NoteSpawner)
    
    [Header("Visual")]
    [SerializeField] private Color rightHandColor = new Color(0.2f, 0.6f, 1f); // Azul
    [SerializeField] private Color leftHandColor = new Color(1f, 0.4f, 0.2f);  // Naranja
    [SerializeField] private float noteLabelFontSize = 100; // Tamaño del texto de la nota
    
    private Renderer noteRenderer;
    private Rigidbody rb;
    private bool isActive = true;
    private Vector3 targetDirection;
    private GameObject noteLabel; // TextMesh con el nombre de la nota
    private GameObject durationLine; // Línea amarilla que indica la duración

    void Awake()
    {
        noteRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();
        
        // Configurar Rigidbody
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // No rotar
        
        // Tamaño GIGANTE para VR - MUY visible
        transform.localScale = Vector3.one * 0.8f; // 80cm de diámetro - GIGANTESCAS
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
        
        // La posición ya se configuró como localPosition desde el spawner
        // transform.localPosition ya está configurado
        
        // Calcular dirección hacia el hit point EN ESPACIO LOCAL
        targetDirection = (hitPosition - startPosition).normalized;
        
        // Aplicar velocidad override si se especifica
        if (speedOverride > 0)
        {
            moveSpeed = speedOverride;
        }
        
        // Color según mano
        if (noteRenderer != null)
        {
            Material mat = noteRenderer.material;
            mat.color = (hand == "right") ? rightHandColor : leftHandColor;
        }
        
        // Crear label con el nombre de la nota
        CreateNoteLabel();
        
        // Crear línea de duración (hold indicator)
        CreateDurationLine(speedOverride);
        
        Debug.Log($"[MusicNote] Nota creada: MIDI {midiNote} ({hand}) a {moveSpeed}m/s");
    }
    
    void FixedUpdate()
    {
        if (!isActive) return;
        
        // Mover EN ESPACIO LOCAL del pentagrama (se mueve con el parent)
        // Convertir dirección local a dirección mundial
        Vector3 worldDirection = transform.parent != null 
            ? transform.parent.TransformDirection(targetDirection) 
            : targetDirection;
        
        rb.linearVelocity = worldDirection * moveSpeed;
    }
    
    void Start()
    {
        // Destruir automáticamente después de 10 segundos si no fue destruida antes
        Destroy(gameObject, 10f);
    }

    /// <summary>
    /// Marca la nota como tocada correctamente
    /// </summary>
    public void OnNoteHit()
    {
        isActive = false;
        
        // Efecto visual de acierto (cambiar color, escala, etc.)
        if (noteRenderer != null)
        {
            noteRenderer.material.color = Color.green;
        }
        
        // Destruir después de un momento
        Destroy(gameObject, 0.2f);
        
        Debug.Log($"[MusicNote] ¡Nota {midiNote} tocada correctamente!");
    }

    /// <summary>
    /// Marca la nota como fallada (pasó la línea de hit sin ser tocada)
    /// </summary>
    public void OnNoteMissed()
    {
        isActive = false;
        
        // Efecto visual de fallo
        if (noteRenderer != null)
        {
            noteRenderer.material.color = Color.red;
        }
        
        // Destruir después de un momento
        Destroy(gameObject, 0.2f);
        
        Debug.Log($"[MusicNote] Nota {midiNote} fallada.");
    }
    
    /// <summary>
    /// Crea un TextMesh con el nombre de la nota
    /// </summary>
    private void CreateNoteLabel()
    {
        noteLabel = new GameObject("NoteLabel");
        noteLabel.transform.SetParent(transform, false);
        noteLabel.transform.localPosition = Vector3.zero; // Centrado en la esfera
        
        // El pentagrama NO está rotado, así que el texto tampoco necesita rotación
        noteLabel.transform.localRotation = Quaternion.identity;
        
        TextMesh textMesh = noteLabel.AddComponent<TextMesh>();
        textMesh.text = MidiToNoteName(midiNote);
        textMesh.fontSize = (int)noteLabelFontSize;
        textMesh.characterSize = 0.01f; // Tamaño del texto en el mundo 3D
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        
        // Hacer que el texto siempre mire hacia la cámara (billboard)
        // Este componente se puede agregar si quieres que el texto siempre sea legible
        // Por ahora lo dejamos fijo
        
        Debug.Log($"[MusicNote] Label creado: {textMesh.text}");
    }
    
    /// <summary>
    /// Crea una línea amarilla que indica la duración que debe sostener la nota
    /// La línea se extiende en la dirección del movimiento
    /// </summary>
    private void CreateDurationLine(float noteSpeed)
    {
        if (duration <= 0) return; // No crear línea para notas sin duración
        
        // Calcular la longitud de la línea basada en duración y velocidad
        // longitud = velocidad * duración (distancia que recorre en el tiempo de duración)
        float lineLength = noteSpeed * duration;
        
        // Limitar longitud máxima para que no sea demasiado larga
        lineLength = Mathf.Min(lineLength, 2.0f); // Máximo 2 metros
        
        durationLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        durationLine.name = "DurationLine";
        durationLine.transform.SetParent(transform, false);
        
        // Posicionar la línea DETRÁS de la nota (en dirección opuesta al movimiento)
        // En espacio local, si la nota se mueve en +X, la línea va hacia -X
        // La línea se extiende DESDE la nota hacia atrás
        durationLine.transform.localPosition = new Vector3(-lineLength / 2f, 0, 0);
        
        // Escala: longitud en X, grosor pequeño en Y y Z
        durationLine.transform.localScale = new Vector3(lineLength, 0.08f, 0.08f);
        
        // Material amarillo brillante
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        
        Material durationMaterial = new Material(shader);
        durationMaterial.color = new Color(1f, 0.9f, 0f); // Amarillo brillante
        
        Renderer renderer = durationLine.GetComponent<Renderer>();
        renderer.material = durationMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Quitar collider para que no interfiera
        Collider collider = durationLine.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        
        Debug.Log($"[MusicNote] Línea de duración creada: {lineLength:F2}m para {duration:F2}s");
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
            // La nota salió del área sin ser tocada
            if (isActive)
            {
                OnNoteMissed();
            }
        }
    }
}
