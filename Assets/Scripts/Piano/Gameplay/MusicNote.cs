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
    [SerializeField] private float moveSpeed = 2f; // Velocidad de movimiento (m/s)
    
    [Header("Visual")]
    [SerializeField] private Color rightHandColor = new Color(0.2f, 0.6f, 1f); // Azul
    [SerializeField] private Color leftHandColor = new Color(1f, 0.4f, 0.2f);  // Naranja
    
    private Renderer noteRenderer;
    private Rigidbody rb;
    private bool isActive = true;
    private Vector3 targetDirection;

    void Awake()
    {
        noteRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();
        
        // Configurar Rigidbody
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // No rotar
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
        
        transform.position = startPosition;
        
        // Calcular dirección hacia el hit point
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
        
        Debug.Log($"[MusicNote] Nota creada: MIDI {midiNote} ({hand}) a {moveSpeed}m/s");
    }

    void FixedUpdate()
    {
        if (!isActive) return;
        
        // Mover hacia la línea de hit
        rb.linearVelocity = targetDirection * moveSpeed;
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
