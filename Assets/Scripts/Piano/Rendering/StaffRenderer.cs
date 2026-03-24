using UnityEngine;

/// <summary>
/// Dibuja un pentagrama musical con 5 líneas y su clave
/// Puede ser Clave de Sol (Treble) o Clave de Fa (Bass)
/// </summary>
public partial class StaffRenderer : MonoBehaviour
{
    [Header("Tipo de Pentagrama")]
    [SerializeField] private StaffType staffType = StaffType.Treble;
    
    [Header("Dimensiones")]
    [SerializeField] private float staffWidth = 3f; // Ancho del pentagrama (3 metros)
    [SerializeField] private float lineSpacing = 0.15f; // Espaciado entre líneas
    [SerializeField] private float lineThickness = 0.35f; // Grosor de línea (EXTRA EXTRA grueso para VR)
    
    [Header("Colores")]
    [SerializeField] private Color lineColor = Color.white; // Color blanco para mejor visibilidad
    
    [Header("Líneas Dinámicas")]
    [SerializeField] private float ledgerLineWidth = 0.4f; // Ancho de líneas auxiliares (más cortas)
    [SerializeField] private int maxLedgerLinesAbove = 10; // Máximo de líneas extras arriba (suficiente para notas altas)
    [SerializeField] private int maxLedgerLinesBelow = 10; // Máximo de líneas extras abajo (ej: MIDI 36 = C1 requiere ~7 líneas)

    private GameObject[] staffLines = new GameObject[5]; // 5 líneas del pentagrama
    private GameObject clefSymbol; // Símbolo de clave (Sol o Fa)
    private GameObject hitLine; // Línea amarilla de hit
    private GameObject ledgerLinesContainer; // Contenedor para líneas adicionales
    
    private float currentVerticalOffset = 0f; // Offset vertical para scroll

    public enum StaffType
    {
        Treble, // Clave de Sol (mano derecha)
        Bass    // Clave de Fa (mano izquierda)
    }

    void Awake()
    {
        // LIMPIEZA EN AWAKE: Eliminar TODOS los hijos antes de Start()
        CleanOldStaffLines();
    }

    void Start()
    {
        CreateStaff();
        CreateClefSymbol();
        CreateHitLine(); // Crear línea amarilla de hit
        
        // NO ROTAR - El pentagrama está en orientación normal
        // Las notas irán de DERECHA a IZQUIERDA usando spawn/hit points correctos
        Debug.Log($"[StaffRenderer] Pentagrama creado - Notas irán de DERECHA → IZQUIERDA");
    }
    
    /// <summary>
    /// Elimina TODAS las líneas y objetos hijos del pentagrama
    /// </summary>
    private void CleanOldStaffLines()
    {
        Debug.Log($"[StaffRenderer] 🧹 LIMPIANDO {transform.childCount} objetos hijos...");
        
        // ELIMINAR TODOS LOS HIJOS sin excepciones
        while (transform.childCount > 0)
        {
            Transform child = transform.GetChild(0);
            Debug.Log($"[StaffRenderer] 🗑️ Destruyendo: {child.name}");
            DestroyImmediate(child.gameObject);
        }
        
        Debug.Log($"[StaffRenderer] ✅ Limpieza completa. Hijos restantes: {transform.childCount}");
    }

    /// <summary>
    /// Crea las 5 líneas horizontales del pentagrama
    /// </summary>
    private void CreateStaff()
    {
        // INTENTAR MÚLTIPLES SHADERS como fallback
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogError("[StaffRenderer] ❌ Shader 'Unlit/Color' NO ENCONTRADO! Intentando Standard...");
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            Debug.LogError("[StaffRenderer] ❌ Shader 'Standard' NO ENCONTRADO! Usando shader por defecto...");
            shader = Shader.Find("Diffuse");
        }
        
        if (shader == null)
        {
            Debug.LogError("[StaffRenderer] 💀 NINGÚN SHADER DISPONIBLE! Las líneas serán ROSAS");
        }
        else
        {
            Debug.Log($"[StaffRenderer] ✅ Shader encontrado: {shader.name}");
        }
        
        Material unlitMaterial = new Material(shader != null ? shader : Shader.Find("UI/Default"));
        unlitMaterial.color = lineColor;
        
        Debug.Log($"[StaffRenderer] 🎵 Creando pentagrama {staffType} | Material: {unlitMaterial.shader.name} | Color: {lineColor}");
        
        for (int i = 0; i < 5; i++)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"StaffLine_{i}";
            line.transform.parent = transform;
            
            // Posición: de abajo hacia arriba
            float yPos = i * lineSpacing;
            line.transform.localPosition = new Vector3(0, yPos, 0);
            
            // Escala: ANCHO (X), GROSOR (Y), PROFUNDIDAD (Z)
            line.transform.localScale = new Vector3(staffWidth, lineThickness, 0.01f);
            
            // APLICAR MATERIAL - con validación exhaustiva
            Renderer renderer = line.GetComponent<Renderer>();
            
            if (renderer == null)
            {
                Debug.LogError($"[StaffRenderer] ❌ Renderer NULL en línea {i}!");
            }
            else
            {
                // FORZAR material nuevo
                renderer.material = unlitMaterial;
                
                // VERIFICAR que se aplicó correctamente
                if (renderer.material == null)
                {
                    Debug.LogError($"[StaffRenderer] ❌ Material NULL después de asignar en línea {i}!");
                }
                else if (renderer.material.shader == null)
                {
                    Debug.LogError($"[StaffRenderer] ❌ Shader NULL en material de línea {i}!");
                }
                else
                {
                    Debug.Log($"[StaffRenderer] ✅ Línea {i}: shader={renderer.material.shader.name}, color={renderer.material.color}");
                }
                
                // Desactivar sombras para VR
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            
            // Quitar collider
            Collider collider = line.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            
            staffLines[i] = line;
        }
        
        Debug.Log($"[StaffRenderer] 🎹 {staffType} COMPLETO: {staffLines.Length} líneas | {staffWidth}m ancho | {lineThickness}m grosor");
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
    /// Crea la línea amarilla vertical de hit (donde deben tocarse las notas)
    /// </summary>
    private void CreateHitLine()
    {
        hitLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hitLine.name = "HitLine_Yellow";
        hitLine.transform.parent = transform;
        
        // Posicionar en ESPACIO LOCAL a la IZQUIERDA (donde deben llegar las notas)
        float pentagramHeight = lineSpacing * 4f; // Distancia de línea 0 a línea 4
        float centerY = pentagramHeight / 2f;
        hitLine.transform.localPosition = new Vector3(staffWidth * -0.4f, centerY, 0);
        
        // Altura INICIAL (se actualizará dinámicamente con líneas ledger)
        float lineHeight = pentagramHeight + lineSpacing * 0.5f;
        hitLine.transform.localScale = new Vector3(0.03f, lineHeight, 0.001f);
        
        // Crear material amarillo brillante
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        
        Material hitMaterial = new Material(shader);
        hitMaterial.color = Color.yellow;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        renderer.material = hitMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Quitar collider
        Collider collider = hitLine.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        
        Debug.Log($"[StaffRenderer] 💛 Línea de hit amarilla creada (altura inicial: {lineHeight:F3}m)");
    }
    
    /// <summary>
    /// Ajusta dinámicamente la altura de la línea amarilla según las líneas ledger actuales
    /// Se llama cuando se crean nuevas líneas ledger
    /// </summary>
    public void UpdateHitLineHeight()
    {
        if (hitLine == null) return;
        
        // Calcular altura total incluyendo líneas ledger
        float pentagramHeight = lineSpacing * 4f; // Estándar
        float ledgerMargin = maxLedgerLinesAbove != 0 || maxLedgerLinesBelow != 0 ? lineSpacing * 1f : 0;
        float totalHeight = pentagramHeight + (maxLedgerLinesAbove * lineSpacing) + (maxLedgerLinesBelow * lineSpacing) + ledgerMargin;
        
        // Actualizar escala Y de la línea
        Vector3 scale = hitLine.transform.localScale;
        scale.y = totalHeight;
        hitLine.transform.localScale = scale;
        
        // Reposicionar el centro verticalmente
        float topY = (maxLedgerLinesAbove * lineSpacing) + (pentagramHeight / 2f);
        float bottomY = -(maxLedgerLinesBelow * lineSpacing) + (pentagramHeight / 2f);
        float centerY = (topY + bottomY) / 2f;
        
        Vector3 pos = hitLine.transform.localPosition;
        pos.y = centerY;
        hitLine.transform.localPosition = pos;
        
        Debug.Log($"[StaffRenderer] 📐 HitLine actualizada: altura={totalHeight:F3}m, center={centerY:F3}m (ledger: +{maxLedgerLinesAbove}/-{maxLedgerLinesBelow})");
    }
    
    /// <summary>
    /// Obtiene la posición Y local para una nota MIDI en este pentagrama
    /// Basado en la teoría musical: cada nota tiene su posición en línea o espacio
    /// </summary>
    public float GetNoteYPosition(int midiNote)
    {
        // Sistema de posición basado en mapeo cromático directo
        // Cada nota (incluyendo sostenidos/bemoles) tiene posición propia
        float halfSemitone = lineSpacing / 2f; // Separación por semitono
        
        if (staffType == StaffType.Treble)
        {
            // ========== CLAVE DE SOL (Treble Clef) ==========
            // Primera línea = E4 (MIDI 64)
            // Cada semitono = halfSemitone unidades de altura
            // Referencia: E4 = posición 0
            
            int positionFromE4 = GetStaffPositionFromMidi(midiNote, 64);
            return positionFromE4 * halfSemitone;
        }
        else // Bass Clef
        {
            // ========== CLAVE DE FA (Bass Clef) ==========
            // Primera línea = G2 (MIDI 43)
            // Cada semitono = halfSemitone unidades de altura
            // Referencia: G2 (posición 0)
            
            int positionFromG2 = GetStaffPositionFromMidi(midiNote, 43);
            return positionFromG2 * halfSemitone;
        }
    }
    
    /// <summary>
    /// Calcula la posición en el pentagrama desde una nota de referencia
    /// MAPEO CROMÁTICO: Cada nota tiene su propia posición, incluyendo sostenidos/bemoles
    /// C=0, C#=1, D=2, Eb=3, E=4, F=5, F#=6, G=7, G#=8, A=9, Bb=10, B=11 (por octava)
    /// </summary>
    private int GetStaffPositionFromMidi(int targetMidi, int referenceMidi)
    {
        int semitoneOffset = targetMidi - referenceMidi;
        
        // CORRECCIÓN: Mapeo cromático DIRECTO
        // Cada semitono = 1 posición visual
        // Esto asegura que C y C# estén en diferentes posiciones
        
        // Calcular octavas y posición dentro de la octava
        int octaveOffset = semitoneOffset / 12;
        int semitoneInOctave = semitoneOffset % 12;
        
        // Manejar negativos correctamente
        if (semitoneInOctave < 0)
        {
            semitoneInOctave += 12;
            octaveOffset -= 1;
        }
        
        // 12 posiciones por octava (una para cada nota cromática)
        int positionInOctave = semitoneInOctave;
        return octaveOffset * 12 + positionInOctave;
    }
    
    /// <summary>
    /// Calcula la posición diatónica de una nota relativa a C
    /// Maneja sostenidos y bemoles mapeándolos a la posición visual más cercana
    /// </summary>
    private int GetDiatonicPosition(int semitonesFromC)
    {
        // Cada octava = 7 posiciones diatónicas (C D E F G A B)
        // Patrones de semitonos en escala diatónica: 2 2 1 2 2 2 1
        
        // Tabla de conversión de semitonos a posiciones diatónicas
        // Para notas con alteraciones (sostenidos/bemoles), usar posición visual correspondiente
        int[] semitoneToPosition = { 
            0,  // C  (0 semitonos)
            0,  // C# (1 semitono) - visual como C
            1,  // D  (2 semitonos)
            1,  // Eb (3 semitonos) - visual como D
            2,  // E  (4 semitonos)
            3,  // F  (5 semitonos)
            3,  // F# (6 semitonos) - visual como F
            4,  // G  (7 semitonos)
            4,  // G# (8 semitonos) - visual como G
            5,  // A  (9 semitonos)
            5,  // Bb (10 semitonos) - visual como A
            6   // B  (11 semitonos)
        };
        
        int octaves = semitonesFromC / 12;
        int semitoneInOctave = semitonesFromC % 12;
        if (semitoneInOctave < 0) semitoneInOctave += 12;
        
        int basePosition = octaves * 7; // 7 posiciones por octava
        int positionInOctave = semitoneToPosition[semitoneInOctave];
        
        return basePosition + positionInOctave;
    }

    /// <summary>
    /// Calcula el spawn point (inicio del pentagrama) en world space
    /// Las notas aparecen en la DERECHA (+X) y viajan hacia la IZQUIERDA (-X)
    /// </summary>
    public Vector3 GetSpawnPoint()
    {
        return transform.position + transform.right * (staffWidth * 0.5f);
    }

    /// <summary>
    /// Calcula el hit point (línea de acierto) en world space
    /// La línea de acierto está a la IZQUIERDA (-X) del pentagrama
    /// </summary>
    public Vector3 GetHitPoint()
    {
        return transform.position - transform.right * (staffWidth * 0.4f);
    }
    
    /// <summary>
    /// Crea líneas auxiliares (ledger lines) dinámicamente para notas fuera del pentagrama
    /// </summary>
    public void CreateLedgerLinesForNote(float noteYPosition)
    {
        // Rango del pentagrama estándar: y=0 (línea 1) a y=0.6 (línea 5)
        float pentagramBottom = 0f;
        float pentagramTop = lineSpacing * 4f; // 0.60m
        
        // Si la nota está dentro del rango, no necesita ledger lines
        if (noteYPosition >= pentagramBottom && noteYPosition <= pentagramTop)
        {
            return;
        }
        
        // Crear contenedor si no existe
        if (ledgerLinesContainer == null)
        {
            ledgerLinesContainer = new GameObject("LedgerLines");
            ledgerLinesContainer.transform.parent = transform;
            ledgerLinesContainer.transform.localPosition = Vector3.zero;
            ledgerLinesContainer.transform.localRotation = Quaternion.identity;
        }
        
        // Determinar cuántas líneas ledger necesitamos y en qué dirección
        float halfSpace = lineSpacing / 2f;
        bool needsUpdate = false;
        
        if (noteYPosition < pentagramBottom)
        {
            // Notas DEBAJO del pentagrama
            int lineCount = Mathf.CeilToInt((pentagramBottom - noteYPosition) / lineSpacing);
            lineCount = Mathf.Min(lineCount, maxLedgerLinesBelow);
            
            for (int i = 1; i <= lineCount; i++)
            {
                float yPos = pentagramBottom - (i * lineSpacing);
                if (ledgerLinesContainer.transform.Find($"LedgerBelow_{i}") == null)
                {
                    CreateSingleLedgerLine(yPos, $"LedgerBelow_{i}");
                    needsUpdate = true;
                }
            }
        }
        else if (noteYPosition > pentagramTop)
        {
            // Notas ARRIBA del pentagrama
            int lineCount = Mathf.CeilToInt((noteYPosition - pentagramTop) / lineSpacing);
            lineCount = Mathf.Min(lineCount, maxLedgerLinesAbove);
            
            for (int i = 1; i <= lineCount; i++)
            {
                float yPos = pentagramTop + (i * lineSpacing);
                if (ledgerLinesContainer.transform.Find($"LedgerAbove_{i}") == null)
                {
                    CreateSingleLedgerLine(yPos, $"LedgerAbove_{i}");
                    needsUpdate = true;
                }
            }
        }
        
        // Actualizar altura de la línea amarilla si se crearon nuevas líneas ledger
        if (needsUpdate)
        {
            UpdateHitLineHeight();
        }
    }
    
    /// <summary>
    /// Crea una única línea ledger en la posición especificada
    /// </summary>
    private void CreateSingleLedgerLine(float yPosition, string lineName)
    {
        // Verificar si ya existe esta línea
        Transform existing = ledgerLinesContainer.transform.Find(lineName);
        if (existing != null) return; // Ya existe
        
        GameObject ledgerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ledgerLine.name = lineName;
        ledgerLine.transform.parent = ledgerLinesContainer.transform;
        ledgerLine.transform.localPosition = new Vector3(0, yPosition, 0);
        
        // Líneas ledger son MÁS CORTAS que las líneas del pentagrama
        ledgerLine.transform.localScale = new Vector3(ledgerLineWidth, lineThickness, 0.01f);
        
        // Aplicar material
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        
        Material ledgerMaterial = new Material(shader);
        ledgerMaterial.color = lineColor;
        
        Renderer renderer = ledgerLine.GetComponent<Renderer>();
        renderer.material = ledgerMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        // Quitar collider
        Collider collider = ledgerLine.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }
    
    /// <summary>
    /// Limpia todas las líneas ledger generadas dinámicamente
    /// </summary>
    public void ClearLedgerLines()
    {
        if (ledgerLinesContainer != null)
        {
            foreach (Transform child in ledgerLinesContainer.transform)
            {
                Destroy(child.gameObject);
            }
        }
    }
    
    /// <summary>
    /// Aplica scroll vertical al pentagrama completo
    /// Útil cuando hay muchas notas altas o bajas simultáneamente
    /// </summary>
    public void ApplyVerticalScroll(float targetYPosition)
    {
        // Calcular el centro del pentagrama
        float pentagramCenter = lineSpacing * 2f; // Línea 3 (centro)
        
        // Calcular offset necesario para centrar la nota target
        float desiredOffset = targetYPosition - pentagramCenter;
        
        // Suavizar el movimiento
        currentVerticalOffset = Mathf.Lerp(currentVerticalOffset, desiredOffset, Time.deltaTime * 3f);
        
        // Aplicar offset a todos los elementos visuales (excepto el transform raíz)
        // Esto mantiene las notas en posición correcta mientras el pentagrama se "desplaza"
        foreach (GameObject line in staffLines)
        {
            if (line != null)
            {
                Vector3 pos = line.transform.localPosition;
                pos.y = (System.Array.IndexOf(staffLines, line) * lineSpacing) - currentVerticalOffset;
                line.transform.localPosition = pos;
            }
        }
        
        // También ajustar la línea de hit
        if (hitLine != null)
        {
            Vector3 hitPos = hitLine.transform.localPosition;
            hitPos.y = (lineSpacing * 2f) - currentVerticalOffset; // Mantener en el centro
            hitLine.transform.localPosition = hitPos;
        }
    }
}

