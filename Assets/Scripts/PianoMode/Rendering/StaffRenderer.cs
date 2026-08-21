using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dibuja un pentagrama musical de 5 líneas con su clave (Sol o Fa),
/// la línea de hit y las líneas auxiliares dinámicas.
/// </summary>
public partial class StaffRenderer : MonoBehaviour
{
    public enum StaffType
    {
        Treble, // Clave de Sol (mano derecha)
        Bass    // Clave de Fa (mano izquierda)
    }

    [Header("Tipo de Pentagrama")]
    [SerializeField] private StaffType staffType = StaffType.Treble;

    [Header("Dimensiones")]
    [Tooltip("Ancho del pentagrama en metros")]
    [SerializeField] private float staffWidth = 3f;
    [SerializeField] private float lineSpacing = 0.13f;
    [Tooltip("Grosor de línea, exagerado a propósito para legibilidad en VR")]
    [SerializeField] private float lineThickness = 0.35f;

    [Header("Colores")]
    [SerializeField] private Color lineColor = Color.white;

    [Header("Líneas Dinámicas")]
    [Tooltip("Las líneas auxiliares son más cortas que las del pentagrama")]
    [SerializeField] private float ledgerLineWidth = 0.4f;
    [SerializeField] private int maxLedgerLinesAbove = 10;
    [SerializeField] private int maxLedgerLinesBelow = 10;

    private GameObject[] staffLines = new GameObject[5];
    private GameObject clefSymbol;
    private GameObject hitLine;
    private GameObject ledgerLinesContainer;
    private float currentVerticalOffset = 0f;

    public StaffType Type => staffType;

    private float PentagramHeight => lineSpacing * 4f;

    void Awake() => CleanOldStaffLines();

    void Start()
    {
        CreateStaff();
        CreateClefSymbol();
        CreateHitLine();
    }

    /// <summary>Elimina todos los hijos antes de reconstruir el pentagrama.</summary>
    private void CleanOldStaffLines()
    {
        while (transform.childCount > 0)
            DestroyImmediate(transform.GetChild(0).gameObject);
    }

    private static Shader ResolveLineShader()
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader != null) return shader;

        shader = Shader.Find("Standard");
        if (shader != null) return shader;

        shader = Shader.Find("Diffuse");
        return shader != null ? shader : Shader.Find("UI/Default");
    }

    /// <summary>Crea un cubo sin collider ni sombras usando el material indicado.</summary>
    private GameObject CreateLineCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.parent = parent;
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;

        Renderer cubeRenderer = cube.GetComponent<Renderer>();
        cubeRenderer.material = material;
        cubeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        cubeRenderer.receiveShadows = false;

        Collider cubeCollider = cube.GetComponent<Collider>();
        if (cubeCollider != null) Destroy(cubeCollider);

        return cube;
    }

    /// <summary>
    /// Crea las 5 líneas horizontales, de abajo hacia arriba.
    /// Comparten un único material a propósito.
    /// </summary>
    private void CreateStaff()
    {
        Material unlitMaterial = new Material(ResolveLineShader()) { color = lineColor };

        for (int i = 0; i < 5; i++)
        {
            staffLines[i] = CreateLineCube(
                $"StaffLine_{i}",
                transform,
                new Vector3(0, i * lineSpacing, 0),
                new Vector3(staffWidth, lineThickness, 0.01f),
                unlitMaterial);
        }
    }

    /// <summary>Crea el símbolo de clave con el carácter Unicode musical correspondiente.</summary>
    private void CreateClefSymbol()
    {
        GameObject symbolObj = new GameObject("ClefSymbol");
        symbolObj.transform.parent = transform;

        TextMesh textMesh = symbolObj.AddComponent<TextMesh>();
        bool isTreble = staffType == StaffType.Treble;

        // 𝄞 (U+1D11E) clave de Sol, sobre la segunda línea; 𝄢 (U+1D122) clave de Fa, sobre la cuarta.
        textMesh.text = isTreble ? "𝄞" : "𝄢";
        textMesh.fontSize = isTreble ? 80 : 60;
        symbolObj.transform.localPosition = new Vector3(
            -staffWidth * 0.4f, lineSpacing * (isTreble ? 2 : 3), -0.01f);

        textMesh.characterSize = 0.05f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = lineColor;

        clefSymbol = symbolObj;
    }

    /// <summary>Crea la línea vertical amarilla donde deben tocarse las notas.</summary>
    private void CreateHitLine()
    {
        Material hitMaterial = new Material(ResolveLineShader()) { color = Color.yellow };

        hitLine = CreateLineCube(
            "HitLine_Yellow",
            transform,
            new Vector3(staffWidth * -0.4f, PentagramHeight / 2f, 0),
            new Vector3(0.03f, PentagramHeight + lineSpacing * 0.5f, 0.001f),
            hitMaterial);
    }

    /// <summary>
    /// Ajusta la altura de la línea de hit para abarcar también las líneas auxiliares.
    /// </summary>
    public void UpdateHitLineHeight()
    {
        if (hitLine == null) return;

        float ledgerMargin = maxLedgerLinesAbove != 0 || maxLedgerLinesBelow != 0 ? lineSpacing : 0;
        float totalHeight = PentagramHeight
                            + (maxLedgerLinesAbove * lineSpacing)
                            + (maxLedgerLinesBelow * lineSpacing)
                            + ledgerMargin;

        Vector3 scale = hitLine.transform.localScale;
        scale.y = totalHeight;
        hitLine.transform.localScale = scale;

        float topY = (maxLedgerLinesAbove * lineSpacing) + (PentagramHeight / 2f);
        float bottomY = -(maxLedgerLinesBelow * lineSpacing) + (PentagramHeight / 2f);

        Vector3 pos = hitLine.transform.localPosition;
        pos.y = (topY + bottomY) / 2f;
        hitLine.transform.localPosition = pos;
    }

    /// <summary>
    /// Posición Y local de una nota MIDI en este pentagrama (posiciones diatónicas reales).
    /// Clave de Sol: línea inferior = E4. Clave de Fa: línea inferior = G2.
    /// Las alteraciones (sostenidos/bemoles) comparten la misma línea/espacio que su nota natural.
    /// </summary>
    public float GetNoteYPosition(int midiNote)
    {
        int referenceMidi = staffType == StaffType.Treble ? 64 : 43; // E4 / G2
        int stepsFromReference = GetDiatonicStaffPosition(midiNote) - GetDiatonicStaffPosition(referenceMidi);
        return stepsFromReference * (lineSpacing * 0.5f);
    }

    /// <summary>
    /// Índice diatónico en el pentagrama: 7 grados por octava (C D E F G A B).
    /// Los sostenidos/bemoles usan el mismo escalón que su nota natural.
    /// </summary>
    private static int GetDiatonicStaffPosition(int midiNote)
    {
        // C C# D D# E F F# G G# A A# B
        int[] semitoneToDegree = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
        int safeMidi = Mathf.Clamp(midiNote, 0, 127);
        int octave = safeMidi / 12;
        int semitone = safeMidi % 12;
        return (octave * 7) + semitoneToDegree[semitone];
    }

    /// <summary>Las notas aparecen a la derecha (+X) y viajan hacia la izquierda (-X).</summary>
    public Vector3 GetSpawnPoint() => transform.position + transform.right * (staffWidth * 0.5f);

    /// <summary>La línea de acierto está a la izquierda del pentagrama.</summary>
    public Vector3 GetHitPoint() => transform.position - transform.right * (staffWidth * 0.4f);

    /// <summary>Crea las líneas auxiliares necesarias para una nota fuera del pentagrama.</summary>
    public void CreateLedgerLinesForNote(float noteYPosition)
    {
        const float pentagramBottom = 0f;
        float pentagramTop = PentagramHeight;

        if (noteYPosition >= pentagramBottom && noteYPosition <= pentagramTop) return;

        if (ledgerLinesContainer == null)
        {
            ledgerLinesContainer = new GameObject("LedgerLines");
            ledgerLinesContainer.transform.parent = transform;
            ledgerLinesContainer.transform.localPosition = Vector3.zero;
            ledgerLinesContainer.transform.localRotation = Quaternion.identity;
        }

        bool below = noteYPosition < pentagramBottom;
        float edge = below ? pentagramBottom : pentagramTop;
        float distance = below ? pentagramBottom - noteYPosition : noteYPosition - pentagramTop;
        int lineCount = Mathf.Min(
            Mathf.FloorToInt(distance / lineSpacing),
            below ? maxLedgerLinesBelow : maxLedgerLinesAbove);
        string prefix = below ? "LedgerBelow_" : "LedgerAbove_";
        float direction = below ? -1f : 1f;

        bool createdAny = false;
        for (int i = 1; i <= lineCount; i++)
        {
            string lineName = $"{prefix}{i}";
            if (ledgerLinesContainer.transform.Find(lineName) != null) continue;

            CreateSingleLedgerLine(edge + (direction * i * lineSpacing), lineName);
            createdAny = true;
        }

        if (createdAny) UpdateHitLineHeight();
    }

    private void CreateSingleLedgerLine(float yPosition, string lineName)
    {
        if (ledgerLinesContainer.transform.Find(lineName) != null) return;

        CreateLineCube(
            lineName,
            ledgerLinesContainer.transform,
            new Vector3(0, yPosition, 0),
            new Vector3(ledgerLineWidth, lineThickness, 0.01f),
            new Material(ResolveLineShader()) { color = lineColor });
    }

    public void ClearLedgerLines()
    {
        if (ledgerLinesContainer == null) return;

        foreach (Transform child in ledgerLinesContainer.transform)
            Destroy(child.gameObject);
    }

    /// <summary>
    /// Desplaza verticalmente el pentagrama para centrar una nota concreta,
    /// útil cuando hay muchas notas muy agudas o muy graves seguidas.
    /// </summary>
    public void ApplyVerticalScroll(float targetYPosition)
    {
        float desiredOffset = targetYPosition - (lineSpacing * 2f);
        currentVerticalOffset = Mathf.Lerp(currentVerticalOffset, desiredOffset, Time.deltaTime * 3f);

        for (int i = 0; i < staffLines.Length; i++)
        {
            if (staffLines[i] == null) continue;

            Vector3 pos = staffLines[i].transform.localPosition;
            pos.y = (i * lineSpacing) - currentVerticalOffset;
            staffLines[i].transform.localPosition = pos;
        }

        if (hitLine != null)
        {
            Vector3 hitPos = hitLine.transform.localPosition;
            hitPos.y = (lineSpacing * 2f) - currentVerticalOffset;
            hitLine.transform.localPosition = hitPos;
        }
    }
}
