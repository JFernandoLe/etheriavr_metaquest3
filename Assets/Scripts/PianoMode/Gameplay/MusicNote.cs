using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Representa una nota musical (línea de duración) que se mueve hacia la derecha.
/// Se comporta como Piano Tiles: la zona de hit es una trituradora fija.
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

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    [Header("Datos de la Nota")]
    public int midiNote;
    public float duration;
    public float spawnTime;
    public string hand;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 1.0f;
    [SerializeField] private float destroyAfterEndDelay = 1.0f;

    [Header("Visual")]
    [SerializeField] private Color rightHandColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color leftHandColor = new Color(1f, 0.4f, 0.2f);
    [SerializeField] private float noteLabelFontSize = 100;
    [SerializeField] private Color unburnedColor = new Color(1f, 1f, 0.3f);
    [SerializeField] private Color burnedColor = new Color(1f, 0.35f, 0.1f);

    private Rigidbody rb;
    private bool isActive = true;
    private Vector3 targetDirection;
    private GameObject durationLine;
    private MidiAudioManager midiAudioManager;
    private GameplayScoring gameplayScoring;
    private float originalLineLength = 0f;
    private Material durationLineMaterial;
    private float fallbackStartTime = 0f;
    private Vector3 localHitPosition;
    private readonly List<BurnSegmentVisual> burnSegments = new List<BurnSegmentVisual>();
    private BurnSegmentVisual activeBurnSegment;
    private bool isPreviewMode = false;

    public static List<MusicNote> GetActiveNotes() => new List<MusicNote>(FindObjectsOfType<MusicNote>(true));

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

    public void Initialize(PianoNoteData noteData, Vector3 startPosition, Vector3 hitPosition, float speedOverride = -1)
    {
        midiNote = noteData.midi;
        duration = noteData.duration;
        spawnTime = noteData.time;
        hand = noteData.hand;
        localHitPosition = hitPosition;

        midiAudioManager = FindObjectOfType<MidiAudioManager>();
        gameplayScoring = FindObjectOfType<GameplayScoring>();

        targetDirection = (hitPosition - startPosition).normalized;
        if (speedOverride > 0) moveSpeed = speedOverride;

        fallbackStartTime = Time.time;
        transform.localPosition = CalculateHeadPosition(GetCurrentSongTime());

        CreateDurationLine(speedOverride);
    }

    void Update()
    {
        if (!isActive || isPreviewMode) return;

        float songTime = GetCurrentSongTime();
        transform.localPosition = CalculateHeadPosition(songTime);

        bool isPlayableWindow = songTime >= spawnTime && songTime <= (spawnTime + duration);
        bool isPressedNow = midiAudioManager != null && midiAudioManager.IsNotePressedNow(midiNote);

        if (isPlayableWindow && isPressedNow) ExtendBurnSegment(songTime);
        else activeBurnSegment = null;

        if (songTime > spawnTime + duration + destroyAfterEndDelay)
        {
            isActive = false;
            Destroy(gameObject);
        }
    }

    private float GetCurrentSongTime()
    {
        PianoGameManager gameManager = PianoGameManager.Instance;
        if (gameManager != null) return gameManager.GetSongPlaybackTime();
        return Time.time - fallbackStartTime;
    }

    private Vector3 CalculateHeadPosition(float songTime) =>
        localHitPosition - (targetDirection * moveSpeed * (spawnTime - songTime));

    /// <summary>Marca la nota como fallada.</summary>
    public void OnNoteMissed()
    {
        isActive = false;
        Destroy(gameObject);
    }

    /// <summary>Invocado al soltar la tecla MIDI.</summary>
    public void OnNoteRelease()
    {
    }

    public void SetPreviewPose(float songTime)
    {
        isPreviewMode = true;
        transform.localPosition = CalculateHeadPosition(songTime);
    }

    public void ExitPreviewMode() => isPreviewMode = false;

    private static Shader ResolveLineShader()
    {
        Shader shader = Shader.Find("Unlit/Color");
        return shader != null ? shader : Shader.Find("Standard");
    }

    /// <summary>Crea un cubo visual sin collider, hijo de esta nota.</summary>
    private GameObject CreateLineCube(string name, Color color, out Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(transform, false);

        material = new Material(ResolveLineShader()) { color = color };

        Renderer cubeRenderer = cube.GetComponent<Renderer>();
        cubeRenderer.material = material;
        cubeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cubeRenderer.receiveShadows = false;

        Collider cubeCollider = cube.GetComponent<Collider>();
        if (cubeCollider != null) DestroyImmediate(cubeCollider);

        return cube;
    }

    /// <summary>
    /// Crea la línea que representa la duración de la nota, con su nombre en la cabeza.
    /// </summary>
    private void CreateDurationLine(float noteSpeed)
    {
        if (duration <= 0) return;

        float speed = noteSpeed > 0 ? noteSpeed : moveSpeed;
        // Se limita la longitud para evitar escalas exageradas en notas muy largas.
        originalLineLength = Mathf.Min(speed * duration, 8f);

        durationLine = CreateLineCube($"DurationLine_MIDI{midiNote}", unburnedColor, out durationLineMaterial);
        durationLine.transform.localPosition = new Vector3(originalLineLength / 2f, 0, 0);
        durationLine.transform.localScale = new Vector3(originalLineLength, 0.08f, 0.08f);

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
        if (durationLine == null || originalLineLength <= 0f || duration <= 0.0001f) return;

        float currentOffset = Mathf.Clamp(songTime - spawnTime, 0f, duration);
        float segmentStep = Mathf.Max(Time.deltaTime, 0.01f);

        if (activeBurnSegment == null)
        {
            activeBurnSegment = CreateBurnSegment(currentOffset);
            burnSegments.Add(activeBurnSegment);
        }

        activeBurnSegment.endOffset = Mathf.Min(
            Mathf.Max(activeBurnSegment.endOffset, currentOffset + segmentStep), duration);
        UpdateBurnSegmentVisual(activeBurnSegment);
    }

    private BurnSegmentVisual CreateBurnSegment(float startOffset)
    {
        GameObject segmentObject = CreateLineCube($"BurnedSegment_MIDI{midiNote}", burnedColor, out _);

        BurnSegmentVisual segment = new BurnSegmentVisual
        {
            segmentObject = segmentObject,
            startOffset = startOffset,
            endOffset = startOffset
        };

        gameplayScoring?.ReportVisualFeedbackLatency(midiNote, segmentObject.name);

        UpdateBurnSegmentVisual(segment);
        return segment;
    }

    private void UpdateBurnSegmentVisual(BurnSegmentVisual segment)
    {
        if (segment?.segmentObject == null) return;

        float normalizedStart = Mathf.Clamp01(segment.startOffset / duration);
        float normalizedEnd = Mathf.Clamp01(segment.endOffset / duration);
        float segmentLength = Mathf.Max((normalizedEnd - normalizedStart) * originalLineLength, 0.001f);
        float segmentStartX = normalizedStart * originalLineLength;

        segment.segmentObject.transform.localPosition = new Vector3(segmentStartX + (segmentLength * 0.5f), 0f, 0f);
        segment.segmentObject.transform.localScale = new Vector3(segmentLength, 0.1f, 0.1f);
    }

    /// <summary>Convierte un número MIDI a nombre de nota (60 -> "C4", 61 -> "C#4").</summary>
    public static string MidiToNoteName(int midiNumber) => NoteNames[midiNumber % 12] + ((midiNumber / 12) - 1);

    private void OnTriggerExit(Collider other)
    {
        if (isActive && other.CompareTag("PlayArea")) OnNoteMissed();
    }
}
