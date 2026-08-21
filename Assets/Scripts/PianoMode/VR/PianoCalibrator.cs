using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/// <summary>
/// Calibración manual del área del piano en pasarela (passthrough): el jugador arrastra
/// dos esquinas opuestas con los gatillos de índice y gira el rectángulo con los de agarre.
/// A confirma, B reinicia.
/// </summary>
public class PianoCalibrator : MonoBehaviour
{
    [Header("Panel de decision")]
    public GameObject confirmUI;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button modifyButton;

    [Header("Hint contextual")]
    [SerializeField] private GameObject controllerHintUI;
    [SerializeField] private TMP_Text controllerHintText;
    [SerializeField] private Transform leftControllerAnchor;
    [SerializeField] private Transform rightControllerAnchor;
    [SerializeField] private Vector3 hintLocalOffset = new Vector3(0.06f, 0.03f, 0.1f);
    [SerializeField] private Vector3 cameraFallbackOffset = new Vector3(0.25f, -0.12f, 0.65f);

    [Header("Modo mando")]
    [SerializeField] private Transform continueButtonAnchor;
    [SerializeField] private Transform modifyButtonAnchor;
    [SerializeField] private float highlightedButtonScale = 1.18f;
    [SerializeField] private float highlightedButtonPulseSpeed = 6f;

    [Header("Etiquetas A/B")]
    [SerializeField] private TMP_Text continueButtonLabelText;
    [SerializeField] private TMP_Text modifyButtonLabelText;
    [SerializeField] private Vector3 buttonLabelLocalOffset = new Vector3(0f, 0.012f, 0f);

    [Header("Calibracion manual por esquinas")]
    [SerializeField] private Transform pianoSpawnPoint;
    [SerializeField] private Transform passthroughWindow;
    [SerializeField] private Vector3 controllerCornerLocalOffset = new Vector3(0f, -0.01f, 0.06f);
    [Tooltip("Gatillo debe superar este valor para EMPEZAR a arrastrar")]
    [SerializeField] private float triggerPressThreshold = 0.55f;
    [Tooltip("Gatillo debe bajar de este valor para SOLTAR (histéresis anti-microcorte)")]
    [SerializeField] private float triggerReleaseThreshold = 0.22f;
    [Tooltip("Segundos sosteniendo el gatillo antes de mover la esquina (evita toques accidentales)")]
    [SerializeField] private float dragArmDelay = 0.1f;
    [SerializeField] private float minPreviewWidth = 0.25f;
    [SerializeField] private float minPreviewDepth = 0.12f;
    [SerializeField] private float gripRotationThreshold = 0.15f;
    [SerializeField] private bool showCornerMarkers = true;
    [SerializeField] private float cornerMarkerScale = 0.035f;

    private bool isLocked;
    private Vector3 continueButtonBaseScale = Vector3.one;
    private Vector3 modifyButtonBaseScale = Vector3.one;
    private Vector3 passthroughWindowBaseLocalPosition;
    private Vector3 passthroughWindowBaseLocalScale;
    private float rootScaleY = 1f;
    private bool leftCornerSet;
    private bool rightCornerSet;
    private bool leftTriggerHeld;
    private bool rightTriggerHeld;
    private bool leftDragging;
    private bool rightDragging;
    private bool leftPressArmed;
    private bool rightPressArmed;
    private float leftPressStartTime;
    private float rightPressStartTime;
    private bool leftGripHeld;
    private bool rightGripHeld;
    private bool wasLeftGripHeld;
    private bool wasRightGripHeld;
    private float lastLeftControllerYaw;
    private float lastRightControllerYaw;
    private Vector3 leftCornerRawPosition;
    private Vector3 rightCornerRawPosition;
    private float currentPreviewWidth;
    private float currentPreviewDepth;
    private float editingPlaneHeight;
    private Vector3 previewWidthAxis = Vector3.right;
    private Vector3 previewDepthAxis = Vector3.forward;
    private XROrigin cachedXrOrigin;
    private Transform leftCornerMarker;
    private Transform rightCornerMarker;

    public static event System.Action OnPianoConfigured;

    void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueWithAssignedArea);
            continueButton.onClick.AddListener(ContinueWithAssignedArea);
        }

        if (modifyButton != null)
        {
            modifyButton.onClick.RemoveListener(BeginModification);
            modifyButton.onClick.AddListener(BeginModification);
        }

        CacheButtonBaseScales();
        ResolveCalibrationReferences();
        CapturePassthroughReferenceData();
        EnsureButtonLabelCanvases();
        BeginCornerCalibration();
    }

    void Update()
    {
        UpdateControllerState();

        if (isLocked)
        {
            UpdateButtonHighlightVisuals(false, false);
            return;
        }

        HandleCornerCalibrationInput();
        UpdateControllerHintTransform();
        UpdateButtonLabelTransforms();
        UpdateButtonHighlightVisuals(CanConfirmCalibration(), true);
    }

    public void ToggleLock() => ContinueWithAssignedArea();

    public void ContinueWithAssignedArea()
    {
        if (isLocked) return;

        if (!CanConfirmCalibration())
        {
            UpdateCornerCalibrationHint("Coloca primero las dos esquinas del piano con los gatillos.");
            return;
        }

        isLocked = true;
        leftDragging = false;
        rightDragging = false;
        ShowDecisionUI(false);
        ShowControllerHint(false);
        SetButtonLabelsVisible(false);
        SetCornerMarkersVisible(false);
        UpdateButtonHighlightVisuals(false, false);

        Debug.Log("<color=green>[PianoCalibrator]</color> Area del piano confirmada con dos esquinas manuales.");
        OnPianoConfigured?.Invoke();
    }

    public void BeginModification()
    {
        if (!isLocked) ResetCornerCalibration();
    }

    private void BeginCornerCalibration()
    {
        isLocked = false;
        leftDragging = false;
        rightDragging = false;
        leftPressArmed = false;
        rightPressArmed = false;
        ShowDecisionUI(false);
        ShowControllerHint(true);
        SetButtonLabelsVisible(true);
        UpdateButtonLabelTexts("A", "B");
        InitializeCornerPreviewFromCurrentBounds();
        EnsureCornerMarkers();
        UpdateCornerMarkers();
        UpdateCornerCalibrationHint();
    }

    private void ResetCornerCalibration()
    {
        BeginCornerCalibration();
        Debug.Log("<color=yellow>[PianoCalibrator]</color> Calibracion manual reiniciada.");
    }

    private void HandleCornerCalibrationInput()
    {
        if (isLocked) return;

        UpdateStickyCornerDrag(ref leftDragging, ref leftPressArmed, ref leftPressStartTime, leftTriggerHeld,
            leftControllerAnchor, ref leftCornerRawPosition, ref leftCornerSet, "izquierda");

        UpdateStickyCornerDrag(ref rightDragging, ref rightPressArmed, ref rightPressStartTime, rightTriggerHeld,
            rightControllerAnchor, ref rightCornerRawPosition, ref rightCornerSet, "derecha");

        if (leftCornerSet || rightCornerSet)
        {
            SynchronizeCornerHeights(leftDragging, rightDragging);
            UpdateCalibrationPreview();
        }

        UpdateCornerMarkers();
        HandleGripRotationInput();

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            ResetCornerCalibration();
            return;
        }

        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            ContinueWithAssignedArea();
            return;
        }

        UpdateCornerCalibrationHint();
    }

    /// <summary>
    /// Esquinas "pegajosas": solo se mueven mientras arrastras a propósito.
    /// Al soltar, quedan ancladas. Un toque breve no las teletransporta.
    /// </summary>
    private void UpdateStickyCornerDrag(
        ref bool isDragging,
        ref bool pressArmed,
        ref float pressStartTime,
        bool triggerHeld,
        Transform controllerAnchor,
        ref Vector3 cornerPosition,
        ref bool cornerSet,
        string cornerLabel)
    {
        if (controllerAnchor == null)
        {
            isDragging = false;
            pressArmed = false;
            return;
        }

        if (triggerHeld)
        {
            if (!pressArmed && !isDragging)
            {
                pressArmed = true;
                pressStartTime = Time.unscaledTime;
            }

            if (!isDragging && pressArmed && (Time.unscaledTime - pressStartTime) >= dragArmDelay)
            {
                isDragging = true;
                pressArmed = false;
                // Arranca desde la posición actual del mando, pero la otra esquina no se toca.
                cornerPosition = GetControllerCornerPoint(controllerAnchor);
                cornerSet = true;
            }

            if (isDragging)
            {
                cornerPosition = GetControllerCornerPoint(controllerAnchor);
                cornerSet = true;
            }
        }
        else
        {
            // Soltar = anclar. No se resetea ni se encoje.
            if (isDragging)
                Debug.Log($"<color=cyan>[PianoCalibrator]</color> Esquina {cornerLabel} anclada.");

            isDragging = false;
            pressArmed = false;
        }
    }

    private void UpdateCalibrationPreview()
    {
        if (passthroughWindow == null || !leftCornerSet || !rightCornerSet) return;

        editingPlaneHeight = GetCornerPlaneHeight();

        ApplyCornerPairPreview(
            new Vector3(leftCornerRawPosition.x, editingPlaneHeight, leftCornerRawPosition.z),
            new Vector3(rightCornerRawPosition.x, editingPlaneHeight, rightCornerRawPosition.z));
    }

    /// <summary>Proyecta un eje al plano horizontal, con un eje de reserva si queda degenerado.</summary>
    private static Vector3 FlattenOrFallback(Vector3 axis, Vector3 fallback)
    {
        Vector3 flattened = Vector3.ProjectOnPlane(axis, Vector3.up);
        if (flattened.sqrMagnitude <= 0.0001f) flattened = fallback;
        return flattened.normalized;
    }

    /// <summary>Deriva el rectángulo a partir de la diagonal entre las dos esquinas.</summary>
    private void ApplyCornerPairPreview(Vector3 firstCorner, Vector3 secondCorner)
    {
        Vector3 widthAxis = FlattenOrFallback(previewWidthAxis, transform.right);
        Vector3 depthAxis = FlattenOrFallback(previewDepthAxis, Vector3.Cross(widthAxis, Vector3.up));

        Vector3 diagonal = secondCorner - firstCorner;
        currentPreviewWidth = Mathf.Max(minPreviewWidth, Mathf.Abs(Vector3.Dot(diagonal, widthAxis)));
        currentPreviewDepth = Mathf.Max(minPreviewDepth, Mathf.Abs(Vector3.Dot(diagonal, depthAxis)));

        ApplyPassthroughBounds(
            (firstCorner + secondCorner) * 0.5f,
            Quaternion.LookRotation(depthAxis, Vector3.up),
            currentPreviewWidth,
            currentPreviewDepth);
    }

    /// <summary>
    /// Escala y coloca la raíz para que la ventana de passthrough cubra el área pedida,
    /// compensando el offset local que la ventana tiene respecto a la raíz.
    /// </summary>
    private void ApplyPassthroughBounds(Vector3 worldCenter, Quaternion worldRotation, float targetWidth, float targetDepth)
    {
        Vector3 updatedScale = transform.localScale;
        updatedScale.x = Mathf.Max(0.01f, targetWidth / Mathf.Max(0.01f, passthroughWindowBaseLocalScale.x));
        updatedScale.y = rootScaleY;
        updatedScale.z = Mathf.Max(0.01f, targetDepth / Mathf.Max(0.01f, passthroughWindowBaseLocalScale.y));

        Vector3 scaledOffset = new Vector3(
            passthroughWindowBaseLocalPosition.x * updatedScale.x,
            passthroughWindowBaseLocalPosition.y * updatedScale.y,
            passthroughWindowBaseLocalPosition.z * updatedScale.z);

        transform.localScale = updatedScale;
        transform.rotation = worldRotation;
        transform.position = worldCenter - (worldRotation * scaledOffset);
    }

    private bool CanConfirmCalibration() =>
        leftCornerSet && rightCornerSet && currentPreviewWidth >= minPreviewWidth && currentPreviewDepth >= minPreviewDepth;

    private void UpdateCornerCalibrationHint(string overrideMessage = null)
    {
        if (!string.IsNullOrEmpty(overrideMessage))
        {
            SetControllerHint(overrideMessage);
            return;
        }

        string leftState = leftDragging ? "arrastrando" : (leftCornerSet ? "anclada" : "libre");
        string rightState = rightDragging ? "arrastrando" : (rightCornerSet ? "anclada" : "libre");

        SetControllerHint(
            $"Mantén gatillo (~0.1s) para mover UNA esquina.\n" +
            $"Al soltar, esa esquina se ancla (no se encoje).\n" +
            $"Izq: {leftState} · Der: {rightState}\n" +
            $"Ancho: {currentPreviewWidth:F2} m  Fondo: {currentPreviewDepth:F2} m\n" +
            $"Agarre: girar · A confirmar · B reiniciar");
    }

    private void UpdateControllerState()
    {
        float leftTrigger = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger);
        float rightTrigger = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger);

        // Histéresis: evita que un micro-suelte corte el drag y deforme el área.
        leftTriggerHeld = leftTriggerHeld
            ? leftTrigger >= triggerReleaseThreshold
            : leftTrigger >= triggerPressThreshold;

        rightTriggerHeld = rightTriggerHeld
            ? rightTrigger >= triggerReleaseThreshold
            : rightTrigger >= triggerPressThreshold;

        leftGripHeld = OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger) >= gripRotationThreshold;
        rightGripHeld = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger) >= gripRotationThreshold;
    }

    private void EnsureCornerMarkers()
    {
        if (!showCornerMarkers) return;

        leftCornerMarker = EnsureCornerMarker(leftCornerMarker, "LeftCornerMarker", new Color(0.2f, 0.75f, 1f, 0.9f));
        rightCornerMarker = EnsureCornerMarker(rightCornerMarker, "RightCornerMarker", new Color(1f, 0.55f, 0.15f, 0.9f));
    }

    private Transform EnsureCornerMarker(Transform marker, string markerName, Color color)
    {
        if (marker != null) return marker;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = markerName;
        sphere.transform.SetParent(transform, true);
        sphere.transform.localScale = Vector3.one * cornerMarkerScale;

        Collider markerCollider = sphere.GetComponent<Collider>();
        if (markerCollider != null) Destroy(markerCollider);

        Renderer markerRenderer = sphere.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"))
            {
                color = color
            };
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
        }

        return sphere.transform;
    }

    private void UpdateCornerMarkers()
    {
        if (!showCornerMarkers) return;

        EnsureCornerMarkers();
        UpdateCornerMarker(leftCornerMarker, leftCornerSet, leftCornerRawPosition, leftDragging);
        UpdateCornerMarker(rightCornerMarker, rightCornerSet, rightCornerRawPosition, rightDragging);
    }

    private void UpdateCornerMarker(Transform marker, bool isSet, Vector3 worldPosition, bool isDragging)
    {
        if (marker == null) return;

        bool visible = !isLocked && isSet;
        if (marker.gameObject.activeSelf != visible) marker.gameObject.SetActive(visible);
        if (!visible) return;

        marker.position = worldPosition;
        float scale = cornerMarkerScale * (isDragging ? 1.35f : 1f);
        marker.localScale = Vector3.one * scale;
    }

    private void SetCornerMarkersVisible(bool visible)
    {
        if (leftCornerMarker != null) leftCornerMarker.gameObject.SetActive(visible);
        if (rightCornerMarker != null) rightCornerMarker.gameObject.SetActive(visible);
    }

    private Vector3 GetControllerCornerPoint(Transform controllerAnchor) =>
        controllerAnchor.TransformPoint(controllerCornerLocalOffset);

    private float GetCornerPlaneHeight()
    {
        if (leftCornerSet && rightCornerSet) return (leftCornerRawPosition.y + rightCornerRawPosition.y) * 0.5f;
        if (leftCornerSet) return leftCornerRawPosition.y;
        if (rightCornerSet) return rightCornerRawPosition.y;
        return editingPlaneHeight;
    }

    /// <summary>Mantiene ambas esquinas a la misma altura para que el área quede horizontal.</summary>
    private void SynchronizeCornerHeights(bool leftCornerUpdated, bool rightCornerUpdated)
    {
        if (!leftCornerSet && !rightCornerSet) return;

        float targetHeight =
            leftCornerUpdated && rightCornerUpdated ? (leftCornerRawPosition.y + rightCornerRawPosition.y) * 0.5f :
            leftCornerUpdated ? leftCornerRawPosition.y :
            rightCornerUpdated ? rightCornerRawPosition.y :
            GetCornerPlaneHeight();

        editingPlaneHeight = targetHeight;

        if (leftCornerSet) leftCornerRawPosition.y = targetHeight;
        if (rightCornerSet) rightCornerRawPosition.y = targetHeight;
    }

    /// <summary>Gira el rectángulo según el yaw de los mandos con el gatillo de agarre pulsado.</summary>
    private void HandleGripRotationInput()
    {
        if (isLocked)
        {
            wasLeftGripHeld = leftGripHeld;
            wasRightGripHeld = rightGripHeld;
            return;
        }

        float totalYawDelta = 0f;
        int gripCount = 0;

        if (leftGripHeld && leftControllerAnchor != null)
        {
            float currentYaw = GetControllerYaw(leftControllerAnchor);
            if (!wasLeftGripHeld) lastLeftControllerYaw = currentYaw;

            totalYawDelta += Mathf.DeltaAngle(lastLeftControllerYaw, currentYaw);
            lastLeftControllerYaw = currentYaw;
            gripCount++;
        }

        if (rightGripHeld && rightControllerAnchor != null)
        {
            float currentYaw = GetControllerYaw(rightControllerAnchor);
            if (!wasRightGripHeld) lastRightControllerYaw = currentYaw;

            totalYawDelta += Mathf.DeltaAngle(lastRightControllerYaw, currentYaw);
            lastRightControllerYaw = currentYaw;
            gripCount++;
        }

        wasLeftGripHeld = leftGripHeld;
        wasRightGripHeld = rightGripHeld;

        if (gripCount > 0 && Mathf.Abs(totalYawDelta) > 0.01f)
        {
            RotatePreviewAxes(totalYawDelta / gripCount);
            UpdateCalibrationPreview();
        }
    }

    private float GetControllerYaw(Transform controllerAnchor)
    {
        Vector3 forward = Vector3.ProjectOnPlane(controllerAnchor.forward, Vector3.up);
        return forward.sqrMagnitude <= 0.0001f ? 0f : Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private void RotatePreviewAxes(float yawDegrees)
    {
        Quaternion rot = Quaternion.AngleAxis(yawDegrees, Vector3.up);
        previewWidthAxis = (rot * previewWidthAxis).normalized;
        previewDepthAxis = (rot * previewDepthAxis).normalized;

        if (!leftCornerSet || !rightCornerSet) return;

        // Se recolocan las esquinas sobre los ejes girados, conservando el tamaño.
        Vector3 center = (leftCornerRawPosition + rightCornerRawPosition) * 0.5f;
        center.y = editingPlaneHeight;

        Vector3 halfDiagonal = (previewWidthAxis * (currentPreviewWidth * 0.5f))
                               + (previewDepthAxis * (currentPreviewDepth * 0.5f));

        leftCornerRawPosition = center - halfDiagonal;
        rightCornerRawPosition = center + halfDiagonal;
        leftCornerRawPosition.y = editingPlaneHeight;
        rightCornerRawPosition.y = editingPlaneHeight;
    }

    private void ResolveCalibrationReferences()
    {
        if (pianoSpawnPoint == null) pianoSpawnPoint = transform.Find("Piano_SpawnPoint");
        if (passthroughWindow == null) passthroughWindow = transform.Find("Passthrough_Window");
        if (cachedXrOrigin == null) cachedXrOrigin = FindObjectOfType<XROrigin>(true);

        if (cachedXrOrigin == null) return;

        if (leftControllerAnchor == null)
            leftControllerAnchor = FindChildTransformByName(cachedXrOrigin.transform, "Left Controller");

        if (rightControllerAnchor == null)
            rightControllerAnchor = FindChildTransformByName(cachedXrOrigin.transform, "Right Controller");
    }

    private Transform FindChildTransformByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == targetName) return child;
        }

        return null;
    }

    private void CapturePassthroughReferenceData()
    {
        if (passthroughWindow == null) return;

        passthroughWindowBaseLocalPosition = passthroughWindow.localPosition;
        passthroughWindowBaseLocalScale = passthroughWindow.localScale;
        rootScaleY = transform.localScale.y;
        editingPlaneHeight = passthroughWindow.position.y;
    }

    /// <summary>Parte del área ya existente para que el jugador solo tenga que ajustarla.</summary>
    private void InitializeCornerPreviewFromCurrentBounds()
    {
        if (passthroughWindow == null)
        {
            leftCornerSet = false;
            rightCornerSet = false;
            currentPreviewWidth = 0f;
            currentPreviewDepth = 0f;
            return;
        }

        editingPlaneHeight = passthroughWindow.position.y;

        previewWidthAxis = FlattenOrFallback(passthroughWindow.right, Vector3.ProjectOnPlane(transform.right, Vector3.up));
        previewDepthAxis = FlattenOrFallback(passthroughWindow.up, Vector3.ProjectOnPlane(transform.forward, Vector3.up));

        currentPreviewWidth = Mathf.Max(minPreviewWidth, passthroughWindow.lossyScale.x);
        currentPreviewDepth = Mathf.Max(minPreviewDepth, passthroughWindow.lossyScale.y);

        Vector3 center = passthroughWindow.position;
        center.y = editingPlaneHeight;

        Vector3 halfDiagonal = (previewWidthAxis * (currentPreviewWidth * 0.5f))
                               + (previewDepthAxis * (currentPreviewDepth * 0.5f));

        leftCornerRawPosition = center - halfDiagonal;
        rightCornerRawPosition = center + halfDiagonal;
        leftCornerRawPosition.y = editingPlaneHeight;
        rightCornerRawPosition.y = editingPlaneHeight;
        leftCornerSet = true;
        rightCornerSet = true;
    }

    private void ShowDecisionUI(bool shouldShow)
    {
        if (confirmUI != null) confirmUI.SetActive(shouldShow);
    }

    private void ShowControllerHint(bool shouldShow)
    {
        if (controllerHintUI != null) controllerHintUI.SetActive(shouldShow);
    }

    private void SetControllerHint(string message)
    {
        ShowControllerHint(true);
        if (controllerHintText != null) controllerHintText.text = message;
    }

    /// <summary>Orienta un transform de espaldas al punto de referencia (para paneles que miran al usuario).</summary>
    private static void OrientAwayFrom(Transform target, Vector3 referencePoint)
    {
        Vector3 lookDirection = target.position - referencePoint;
        if (lookDirection.sqrMagnitude > 0.0001f) target.rotation = Quaternion.LookRotation(lookDirection);
    }

    private void UpdateControllerHintTransform()
    {
        if (controllerHintUI == null || !controllerHintUI.activeSelf) return;

        Transform anchor = GetPreferredHintAnchor();
        if (anchor != null)
        {
            controllerHintUI.transform.position = anchor.TransformPoint(hintLocalOffset);
            OrientAwayFrom(controllerHintUI.transform, GetLookTargetPosition());
            return;
        }

        if (Camera.main == null) return;

        Transform cameraTransform = Camera.main.transform;
        controllerHintUI.transform.position = cameraTransform.TransformPoint(cameraFallbackOffset);
        OrientAwayFrom(controllerHintUI.transform, cameraTransform.position);
    }

    /// <summary>Prefiere el mando que esté arrastrando; si ninguno, el derecho.</summary>
    private Transform GetPreferredHintAnchor()
    {
        if (leftTriggerHeld && leftControllerAnchor != null) return leftControllerAnchor;
        if (rightTriggerHeld && rightControllerAnchor != null) return rightControllerAnchor;
        return rightControllerAnchor != null ? rightControllerAnchor : leftControllerAnchor;
    }

    private void UpdateButtonLabelTransforms()
    {
        UpdateButtonLabelTransform(continueButtonLabelText, continueButtonAnchor);
        UpdateButtonLabelTransform(modifyButtonLabelText, modifyButtonAnchor);
    }

    private void UpdateButtonLabelTransform(TMP_Text label, Transform buttonAnchor)
    {
        if (label == null || buttonAnchor == null) return;

        EnsureButtonLabelCanvasReady(label);
        label.transform.position = buttonAnchor.TransformPoint(buttonLabelLocalOffset);

        Vector3 directionToTarget = GetLookTargetPosition() - label.transform.position;
        if (directionToTarget.sqrMagnitude > 0.0001f)
            label.transform.rotation = Quaternion.LookRotation(directionToTarget);
    }

    private void UpdateButtonLabelTexts(string continueLabel, string modifyLabel)
    {
        if (continueButtonLabelText != null)
        {
            continueButtonLabelText.text = continueLabel;
            continueButtonLabelText.gameObject.SetActive(true);
        }

        if (modifyButtonLabelText != null)
        {
            modifyButtonLabelText.text = modifyLabel;
            modifyButtonLabelText.gameObject.SetActive(true);
        }
    }

    private Vector3 GetLookTargetPosition()
    {
        if (Camera.main != null) return Camera.main.transform.position;

        return controllerHintUI != null
            ? controllerHintUI.transform.position - controllerHintUI.transform.forward
            : transform.position - transform.forward;
    }

    private void CacheButtonBaseScales()
    {
        continueButtonBaseScale = continueButtonAnchor != null ? continueButtonAnchor.localScale : Vector3.one;
        modifyButtonBaseScale = modifyButtonAnchor != null ? modifyButtonAnchor.localScale : Vector3.one;
    }

    private void UpdateButtonHighlightVisuals(bool highlightContinue, bool highlightModify)
    {
        UpdateHighlightedButtonScale(continueButtonAnchor, continueButtonBaseScale, highlightContinue, 0f);
        UpdateHighlightedButtonScale(modifyButtonAnchor, modifyButtonBaseScale, highlightModify || !isLocked, 1.2f);
    }

    private void UpdateHighlightedButtonScale(Transform buttonTransform, Vector3 baseScale, bool isHighlighted, float phaseOffset)
    {
        if (buttonTransform == null) return;

        if (!isHighlighted)
        {
            buttonTransform.localScale = baseScale;
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime * highlightedButtonPulseSpeed) + phaseOffset);
        buttonTransform.localScale = baseScale * Mathf.Lerp(1f, highlightedButtonScale, pulse);
    }

    private void ResetHighlightedButtonScale(Transform buttonTransform, Vector3 baseScale)
    {
        if (buttonTransform != null) buttonTransform.localScale = baseScale;
    }

    private void SetButtonLabelsVisible(bool visible)
    {
        if (continueButtonLabelText != null) continueButtonLabelText.gameObject.SetActive(visible);
        if (modifyButtonLabelText != null) modifyButtonLabelText.gameObject.SetActive(visible);
    }

    private void EnsureButtonLabelCanvases()
    {
        EnsureButtonLabelCanvasReady(continueButtonLabelText);
        EnsureButtonLabelCanvasReady(modifyButtonLabelText);
    }

    /// <summary>
    /// Fuerza el canvas de la etiqueta a world space y desactiva su raycaster,
    /// para que flote junto al mando sin capturar la mirada.
    /// </summary>
    private void EnsureButtonLabelCanvasReady(TMP_Text label)
    {
        if (label == null || label is not TextMeshProUGUI) return;

        Canvas parentCanvas = label.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        parentCanvas.renderMode = RenderMode.WorldSpace;
        parentCanvas.worldCamera = Camera.main;

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one * 0.0007f;
            if (canvasRect.sizeDelta.x <= 1f || canvasRect.sizeDelta.y <= 1f)
                canvasRect.sizeDelta = new Vector2(220f, 72f);
        }

        GraphicRaycaster raycaster = parentCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = false;
    }

    void OnDestroy()
    {
        ResetHighlightedButtonScale(continueButtonAnchor, continueButtonBaseScale);
        ResetHighlightedButtonScale(modifyButtonAnchor, modifyButtonBaseScale);

        if (continueButton != null) continueButton.onClick.RemoveListener(ContinueWithAssignedArea);
        if (modifyButton != null) modifyButton.onClick.RemoveListener(BeginModification);
    }
}
