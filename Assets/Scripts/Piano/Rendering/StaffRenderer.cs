using UnityEngine;

/// <summary>
/// Dibuja un pentagrama musical con 5 líneas y su clave
/// Puede ser Clave de Sol (Treble) o Clave de Fa (Bass)
/// </summary>
public class StaffRenderer : MonoBehaviour
{
    [Header("Tipo de Pentagrama")]
    [SerializeField] private StaffType staffType = StaffType.Treble;
    
    [Header("Dimensiones")]
    [SerializeField] private float staffWidth = 3f; // Ancho del pentagrama
    [SerializeField] private float lineSpacing = 0.15f; // Espaciado entre líneas
    [SerializeField] private float lineThickness = 0.01f; // Grosor de línea
    
    [Header("Materiales")]
    [SerializeField] private Material lineMaterial; // Material para las líneas
    
    [Header("Colores")]
    [SerializeField] private Color lineColor = new Color(0.3f, 0.2f, 0.15f); // Color café oscuro

    private GameObject[] staffLines = new GameObject[5]; // 5 líneas del pentagrama
    private GameObject clefSymbol; // Símbolo de clave (Sol o Fa)

    public enum StaffType
    {
        Treble, // Clave de Sol (mano derecha)
        Bass    // Clave de Fa (mano izquierda)
    }

    void Start()
    {
        CreateStaff();
        CreateClefSymbol();
    }

    /// <summary>
    /// Crea las 5 líneas horizontales del pentagrama
    /// </summary>
    private void CreateStaff()
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"StaffLine_{i}";
            line.transform.parent = transform;
            
            // Posición: de abajo hacia arriba
            float yPos = i * lineSpacing;
            line.transform.localPosition = new Vector3(0, yPos, 0);
            
            // Escala: largo, delgado, plano
            line.transform.localScale = new Vector3(staffWidth, lineThickness, 0.001f);
            
            // Material
            Renderer renderer = line.GetComponent<Renderer>();
            if (lineMaterial != null)
            {
                renderer.material = lineMaterial;
            }
            renderer.material.color = lineColor;
            
            // Quitar collider (no lo necesitamos)
            Destroy(line.GetComponent<Collider>());
            
            staffLines[i] = line;
        }
        
        Debug.Log($"[StaffRenderer] Pentagrama creado: {staffType}");
    }

    /// <summary>
    /// Crea el símbolo de clave (Sol o Fa) usando un TextMesh con Unicode
    /// </summary>
    private void CreateClefSymbol()
    {
        GameObject symbolObj = new GameObject("ClefSymbol");
        symbolObj.transform.parent = transform;
        
        TextMesh textMesh = symbolObj.AddComponent<TextMesh>();
        
        // Símbolos Unicode musicales:
        // 𝄞 (U+1D11E) = Clave de Sol
        // 𝄢 (U+1D122) = Clave de Fa
        // Nota: Unity puede no soportar algunos caracteres Unicode avanzados
        // Alternativas: usar sprites o modelos 3D
        
        if (staffType == StaffType.Treble)
        {
            // Clave de Sol - posición en la segunda línea (g)
            textMesh.text = "𝄞"; // Puede no renderizar bien, usar "G" de respaldo
            symbolObj.transform.localPosition = new Vector3(-staffWidth * 0.4f, lineSpacing * 2, -0.01f);
            textMesh.fontSize = 80;
        }
        else // Bass
        {
            // Clave de Fa - posición en la cuarta línea (f)
            textMesh.text = "𝄢"; // Puede no renderizar bien, usar "F" de respaldo
            symbolObj.transform.localPosition = new Vector3(-staffWidth * 0.4f, lineSpacing * 3, -0.01f);
            textMesh.fontSize = 60;
        }
        
        textMesh.characterSize = 0.05f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = lineColor;
        
        // Si el símbolo Unicode no se ve, usar letra simple como respaldo
        if (!IsFontSupportingSymbol(textMesh.text))
        {
            textMesh.text = (staffType == StaffType.Treble) ? "𝄞" : "𝄢";
            textMesh.fontSize = 100;
            Debug.LogWarning($"[StaffRenderer] Símbolo Unicode no soportado, usando respaldo: {textMesh.text}");
        }
        
        clefSymbol = symbolObj;
    }

    /// <summary>
    /// Verifica si la fuente soporta el símbolo (método básico)
    /// </summary>
    private bool IsFontSupportingSymbol(string symbol)
    {
        // Método simple: Unity suele no soportar símbolos musicales avanzados
        // En producción, usarías sprites o modelos 3D personalizados
        return true; // Asumir que sí por ahora
    }

    /// <summary>
    /// Obtiene la posición Y local para una nota MIDI en este pentagrama
    /// </summary>
    public float GetNoteYPosition(int midiNote)
    {
        // Sistema de posición basado en la escala musical
        // C4 (Do central) = 60
        
        if (staffType == StaffType.Treble)
        {
            // Clave de Sol: E4 (64) está en la primera línea
            // Cada semitono = lineSpacing / 4 (aproximado)
            int e4 = 64; // Nota E4 en primera línea
            float semitonesFromE4 = midiNote - e4;
            return (lineSpacing / 4f) * semitonesFromE4;
        }
        else // Bass
        {
            // Clave de Fa: G2 (43) está en la primera línea
            int g2 = 43;
            float semitonesFromG2 = midiNote - g2;
            return (lineSpacing / 4f) * semitonesFromG2;
        }
    }

    /// <summary>
    /// Calcula el spawn point (inicio del pentagrama) en world space
    /// </summary>
    public Vector3 GetSpawnPoint()
    {
        return transform.position + transform.right * (staffWidth * 0.5f);
    }

    /// <summary>
    /// Calcula el hit point (línea de acierto) en world space
    /// </summary>
    public Vector3 GetHitPoint()
    {
        return transform.position - transform.right * (staffWidth * 0.3f);
    }
}
