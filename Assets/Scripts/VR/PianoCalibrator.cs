using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class PianoCalibrator : MonoBehaviour
{
    private enum CalibrationFlowState
    {
        Editing,
        Locked
    }

    [Header("Panel de decision")]
    public GameObject confirmUI;
    [SerializeField] private TextMeshProUGUI decisionTitleText;
    [SerializeField] private TextMeshProUGUI decisionBodyText;
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
    [SerializeField] private float triggerDragThreshold = 0.15f;
    [SerializeField] private float minCornerHeight = 0.62f;
    [SerializeField] private float maxCornerHeight = 1.18f;
    [SerializeField] private float minPreviewWidth = 0.18f;
    [SerializeField] private float minPreviewDepth = 0.08f;
    [SerializeField] private float gripRotationThreshold = 0.15f;

    private bool isLocked;
    private CalibrationFlowState flowState = CalibrationFlowState.Editing;
    private Vector3 continueButtonBaseScale = Vector3.one;
    private Vector3 modifyButtonBaseScale = Vector3.one;
    private Vector3 passthroughWindowBaseLocalPosition;
    private Vector3 passthroughWindowBaseLocalScale;
    private float rootScaleY = 1f;
    private bool leftCornerSet;
    private bool rightCornerSet;
    private bool leftTriggerHeld;
    private bool rightTriggerHeld;
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

    public void ToggleLock()
    {
        ContinueWithAssignedArea();
    }

    public void ContinueWithAssignedArea()
    {
        if (isLocked)
        {
            return;
        }

        if (!CanConfirmCalibration())
        {
            UpdateCornerCalibrationHint("Coloca primero las dos esquinas del piano con los gatillos.");
            return;
        }

        isLocked = true;
        flowState = CalibrationFlowState.Locked;
        ShowDecisionUI(false);
        ShowControllerHint(false);
        SetButtonLabelsVisible(false);
        UpdateButtonHighlightVisuals(false, false);

        Debug.Log("<color=green>[PianoCalibrator]</color> Area del piano confirmada con dos esquinas manuales.");
        OnPianoConfigured?.Invoke();
    }

    public void BeginModification()
    {
        if (isLocked)
        {
            return;
        }

        ResetCornerCalibration();
    }

    private void BeginCornerCalibration()
    {
        isLocked = false;
        flowState = CalibrationFlowState.Editing;
        ShowDecisionUI(false);
        ShowControllerHint(true);
        SetButtonLabelsVisible(true);
        UpdateButtonLabelTexts("A", "B");
        InitializeCornerPreviewFromCurrentBounds();
        UpdateCornerCalibrationHint();
    }

    private void ResetCornerCalibration()
    {
        BeginCornerCalibration();
        Debug.Log("<color=yellow>[PianoCalibrator]</color> Calibracion manual reiniciada.");
    }

    private void HandleCornerCalibrationInput()
    {
        if (flowState != CalibrationFlowState.Editing)
        {
            return;
        }

        bool leftCornerUpdated = false;
        bool rightCornerUpdated = false;

        if (leftTriggerHeld && leftControllerAnchor != null)
        {
            leftCornerRawPosition = GetControllerCornerPoint(leftControllerAnchor);
            leftCornerSet = true;
            leftCornerUpdated = true;
        }

        if (rightTriggerHeld && rightControllerAnchor != null)
        {
            rightCornerRawPosition = GetControllerCornerPoint(rightControllerAnchor);
            rightCornerSet = true;
            rightCornerUpdated = true;
        }

        if (leftCornerSet || rightCornerSet)
        {
            SynchronizeCornerHeights(leftCornerUpdated, rightCornerUpdated);
            UpdateCalibrationPreview();
        }

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

    private void UpdateCalibrationPreview()
    {
        if (passthroughWindow == null || !leftCornerSet || !rightCornerSet)
        {
            return;
        }

        float planeHeight = GetCornerPlaneHeight();
        editingPlaneHeight = planeHeight;

        Vector3 leftCorner = new Vector3(leftCornerRawPosition.x, planeHeight, leftCornerRawPosition.z);
        Vector3 rightCorner = new Vector3(rightCornerRawPosition.x, planeHeight, rightCornerRawPosition.z);
        ApplyCornerPairPreview(leftCorner, rightCorner);
    }

    private void ApplyCornerPairPreview(Vector3 firstCorner, Vector3 secondCorner)
    {
        Vector3 center = (firstCorner + secondCorner) * 0.5f;

        Vector3 widthAxis = Vector3.ProjectOnPlane(previewWidthAxis, Vector3.up);
        if (widthAxis.sqrMagnitude <= 0.0001f)
        {
            widthAxis = transform.right;
        }
        widthAxis.Normalize();

        Vector3 depthAxis = Vector3.ProjectOnPlane(previewDepthAxis, Vector3.up);
        if (depthAxis.sqrMagnitude <= 0.0001f)
        {
            depthAxis = Vector3.Cross(widthAxis, Vector3.up);
        }
        depthAxis.Normalize();

        Vector3 diagonal = secondCorner - firstCorner;
        float width = Mathf.Max(minPreviewWidth, Mathf.Abs(Vector3.Dot(diagonal, widthAxis)));
        float depth = Mathf.Max(minPreviewDepth, Mathf.Abs(Vector3.Dot(diagonal, depthAxis)));

        currentPreviewWidth = width;
        currentPreviewDepth = depth;

        ApplyPassthroughBounds(center, Quaternion.LookRotation(depthAxis, Vector3.up), width, depth);
    }

    private void ApplySingleCornerPreview(Vector3 corner)
    {
        currentPreviewWidth = minPreviewWidth;
        currentPreviewDepth = minPreviewDepth;
        ApplyPassthroughBounds(corner, Quaternion.LookRotation(GetHorizontalDirectionTowardsUser(corner), Vector3.up), minPreviewWidth, minPreviewDepth);
    }

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

    private bool CanConfirmCalibration()
    {
        return leftCornerSet && rightCornerSet && currentPreviewWidth >= minPreviewWidth && currentPreviewDepth >= minPreviewDepth;
    }

    private void UpdateCornerCalibrationHint(string overrideMessage = null)
    {
        if (!string.IsNullOrEmpty(overrideMessage))
        {
            SetControllerHint(overrideMessage);
            return;
        }

        SetControllerHint($"Gatillos de índice: arrastra las esquinas.\nGatillos de agarre: gira el rectángulo.\nAncho: {currentPreviewWidth:F2} m  Fondo: {currentPreviewDepth:F2} m\nA = confirmar · B = reiniciar");
    }

    private void UpdateControllerState()
    {
        leftTriggerHeld = OVRInput.Get(OVRInput.RawAxis1D.LIndexTrigger) >= triggerDragThreshold;
        rightTriggerHeld = OVRInput.Get(OVRInput.RawAxis1D.RIndexTrigger) >= triggerDragThreshold;
        leftGripHeld = OVRInput.Get(OVRInput.RawAxis1D.LHandTrigger) >= gripRotationThreshold;
        rightGripHeld = OVRInput.Get(OVRInput.RawAxis1D.RHandTrigger) >= gripRotationThreshold;
    }

    private Vector3 GetControllerCornerPoint(Transform controllerAnchor)
    {
        return controllerAnchor.TransformPoint(controllerCornerLocalOffset);
    }

    private float GetCornerPlaneHeight()
    {
        if (leftCornerSet && rightCornerSet)
        {
            return (leftCornerRawPosition.y + rightCornerRawPosition.y) * 0.5f;
        }

        if (leftCornerSet)
        {
            return leftCornerRawPosition.y;
        }

        if (rightCornerSet)
        {
            return rightCornerRawPosition.y;
        }

        return editingPlaneHeight;
    }

    private void SynchronizeCornerHeights(bool leftCornerUpdated, bool rightCornerUpdated)
    {
        if (!leftCornerSet && !rightCornerSet)
        {
            return;
        }

        float targetHeight;
        if (leftCornerUpdated && rightCornerUpdated)
        {
            targetHeight = (leftCornerRawPosition.y + rightCornerRawPosition.y) * 0.5f;
        }
        else if (leftCornerUpdated)
        {
            targetHeight = leftCornerRawPosition.y;
        }
        else if (rightCornerUpdated)
        {
            targetHeight = rightCornerRawPosition.y;
        }
        else
        {
            targetHeight = GetCornerPlaneHeight();
        }

        editingPlaneHeight = targetHeight;

        if (leftCornerSet)
        {
            leftCornerRawPosition.y = targetHeight;
        }

        if (rightCornerSet)
        {
            rightCornerRawPosition.y = targetHeight;
        }
    }

    private void HandleGripRotationInput()
    {
        if (flowState != CalibrationFlowState.Editing)
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
            if (!wasLeftGripHeld)
            {
                lastLeftControllerYaw = currentYaw;
            }
            totalYawDelta += Mathf.DeltaAngle(lastLeftControllerYaw, currentYaw);
            lastLeftControllerYaw = currentYaw;
            gripCount++;
        }

        if (rightGripHeld && rightControllerAnchor != null)
        {
            float currentYaw = GetControllerYaw(rightControllerAnchor);
            if (!wasRightGripHeld)
            {
                lastRightControllerYaw = currentYaw;
            }
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
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }
        return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
    }

    private void RotatePreviewAxes(float yawDegrees)
    {
        Quaternion rot = Quaternion.AngleAxis(yawDegrees, Vector3.up);
        previewWidthAxis = (rot * previewWidthAxis).normalized;
        previewDepthAxis = (rot * previewDepthAxis).normalized;

        if (leftCornerSet && rightCornerSet)
        {
            Vector3 center = (leftCornerRawPosition + rightCornerRawPosition) * 0.5f;
            center.y = editingPlaneHeight;
            float halfW = currentPreviewWidth * 0.5f;
            float halfD = currentPreviewDepth * 0.5f;
            leftCornerRawPosition = center - (previewWidthAxis * halfW) - (previewDepthAxis * halfD);
            rightCornerRawPosition = center + (previewWidthAxis * halfW) + (previewDepthAxis * halfD);
            leftCornerRawPosition.y = editingPlaneHeight;
            rightCornerRawPosition.y = editingPlaneHeight;
        }
    }

    private void ResolveCalibrationReferences()
    {
        if (pianoSpawnPoint == null)
        {
            pianoSpawnPoint = transform.Find("Piano_SpawnPoint");
        }

        if (passthroughWindow == null)
        {
            passthroughWindow = transform.Find("Passthrough_Window");
        }

        if (cachedXrOrigin == null)
        {
            cachedXrOrigin = FindObjectOfType<XROrigin>(true);
        }

        if (cachedXrOrigin != null)
        {
            if (leftControllerAnchor == null)
            {
                leftControllerAnchor = FindChildTransformByName(cachedXrOrigin.transform, "Left Controller");
            }

            if (rightControllerAnchor == null)
            {
                rightControllerAnchor = FindChildTransformByName(cachedXrOrigin.transform, "Right Controller");
            }
        }
    }

    private Transform FindChildTransformByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in transforms)
        {
            if (child.name == targetName)
            {
                return child;
            }
        }

        return null;
    }

    private void CapturePassthroughReferenceData()
    {
        if (passthroughWindow == null)
        {
            return;
        }

        passthroughWindowBaseLocalPosition = passthroughWindow.localPosition;
        passthroughWindowBaseLocalScale = passthroughWindow.localScale;
        rootScaleY = transform.localScale.y;
        editingPlaneHeight = passthroughWindow.position.y;
    }

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

        Vector3 widthAxis = Vector3.ProjectOnPlane(passthroughWindow.right, Vector3.up);
        if (widthAxis.sqrMagnitude <= 0.0001f)
        {
            widthAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        }
        widthAxis.Normalize();
        previewWidthAxis = widthAxis;

        Vector3 depthAxis = Vector3.ProjectOnPlane(passthroughWindow.up, Vector3.up);
        if (depthAxis.sqrMagnitude <= 0.0001f)
        {
            depthAxis = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        depthAxis.Normalize();
        previewDepthAxis = depthAxis;

        currentPreviewWidth = Mathf.Max(minPreviewWidth, passthroughWindow.lossyScale.x);
        currentPreviewDepth = Mathf.Max(minPreviewDepth, passthroughWindow.lossyScale.y);

        Vector3 center = passthroughWindow.position;
        center.y = editingPlaneHeight;

        leftCornerRawPosition = center - (widthAxis * (currentPreviewWidth * 0.5f)) - (depthAxis * (currentPreviewDepth * 0.5f));
        rightCornerRawPosition = center + (widthAxis * (currentPreviewWidth * 0.5f)) + (depthAxis * (currentPreviewDepth * 0.5f));
        leftCornerRawPosition.y = editingPlaneHeight;
        rightCornerRawPosition.y = editingPlaneHeight;
        leftCornerSet = true;
        rightCornerSet = true;
    }

    private Vector3 GetHorizontalDirectionTowardsUser(Vector3 fromPosition)
    {
        Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
        Vector3 direction = cameraTransform != null ? cameraTransform.position - fromPosition : -transform.forward;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
    }

    private void ShowDecisionUI(bool shouldShow)
    {
        if (confirmUI != null)
        {
            confirmUI.SetActive(shouldShow);
        }
    }

    private void ShowControllerHint(bool shouldShow)
    {
        if (controllerHintUI != null)
        {
            controllerHintUI.SetActive(shouldShow);
        }
    }

    private void SetControllerHint(string message)
    {
        ShowControllerHint(true);

        if (controllerHintText != null)
        {
            controllerHintText.text = message;
        }
    }

    private void UpdateControllerHintTransform()
    {
        if (controllerHintUI == null || !controllerHintUI.activeSelf)
        {
            return;
        }

        Transform anchor = GetPreferredHintAnchor();
        if (anchor != null)
        {
            controllerHintUI.transform.position = anchor.TransformPoint(hintLocalOffset);
            Vector3 lookDirection = controllerHintUI.transform.position - GetLookTargetPosition();
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                controllerHintUI.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
            return;
        }

        if (Camera.main != null)
        {
            Transform cameraTransform = Camera.main.transform;
            controllerHintUI.transform.position = cameraTransform.TransformPoint(cameraFallbackOffset);
            Vector3 lookDirection = controllerHintUI.transform.position - cameraTransform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                controllerHintUI.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }

    private Transform GetPreferredHintAnchor()
    {
        if (leftTriggerHeld && leftControllerAnchor != null)
        {
            return leftControllerAnchor;
        }

        if (rightTriggerHeld && rightControllerAnchor != null)
        {
            return rightControllerAnchor;
        }

        if (rightControllerAnchor != null)
        {
            return rightControllerAnchor;
        }

        return leftControllerAnchor;
    }

    private void UpdateButtonLabelTransforms()
    {
        UpdateButtonLabelTransform(continueButtonLabelText, continueButtonAnchor);
        UpdateButtonLabelTransform(modifyButtonLabelText, modifyButtonAnchor);
    }

    private void UpdateButtonLabelTransform(TMP_Text label, Transform buttonAnchor)
    {
        if (label == null || buttonAnchor == null)
        {
            return;
        }

        EnsureButtonLabelCanvasReady(label);
        label.transform.position = buttonAnchor.TransformPoint(buttonLabelLocalOffset);

        Vector3 lookTarget = GetLookTargetPosition();
        Vector3 directionToTarget = lookTarget - label.transform.position;
        if (directionToTarget.sqrMagnitude > 0.0001f)
        {
            label.transform.rotation = Quaternion.LookRotation(directionToTarget);
        }
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
        if (Camera.main != null)
        {
            return Camera.main.transform.position;
        }

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
        bool highlightA = highlightContinue;
        bool highlightB = highlightModify || flowState == CalibrationFlowState.Editing;
        UpdateHighlightedButtonScale(continueButtonAnchor, continueButtonBaseScale, highlightA, 0f);
        UpdateHighlightedButtonScale(modifyButtonAnchor, modifyButtonBaseScale, highlightB, 1.2f);
    }

    private void UpdateHighlightedButtonScale(Transform buttonTransform, Vector3 baseScale, bool isHighlighted, float phaseOffset)
    {
        if (buttonTransform == null)
        {
            return;
        }

        if (!isHighlighted)
        {
            buttonTransform.localScale = baseScale;
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime * highlightedButtonPulseSpeed) + phaseOffset);
        float scaleMultiplier = Mathf.Lerp(1f, highlightedButtonScale, pulse);
        buttonTransform.localScale = baseScale * scaleMultiplier;
    }

    private void ResetHighlightedButtonScale(Transform buttonTransform, Vector3 baseScale)
    {
        if (buttonTransform != null)
        {
            buttonTransform.localScale = baseScale;
        }
    }

    private void SetButtonLabelsVisible(bool visible)
    {
        if (continueButtonLabelText != null)
        {
            continueButtonLabelText.gameObject.SetActive(visible);
        }

        if (modifyButtonLabelText != null)
        {
            modifyButtonLabelText.gameObject.SetActive(visible);
        }
    }

    private void EnsureButtonLabelCanvases()
    {
        EnsureButtonLabelCanvasReady(continueButtonLabelText);
        EnsureButtonLabelCanvasReady(modifyButtonLabelText);
    }

    private void EnsureButtonLabelCanvasReady(TMP_Text label)
    {
        if (!(label is TextMeshProUGUI) || label == null)
        {
            return;
        }

        Canvas parentCanvas = label.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            return;
        }

        parentCanvas.renderMode = RenderMode.WorldSpace;
        parentCanvas.worldCamera = Camera.main;

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one * 0.0007f;
            if (canvasRect.sizeDelta.x <= 1f || canvasRect.sizeDelta.y <= 1f)
            {
                canvasRect.sizeDelta = new Vector2(220f, 72f);
            }
        }

        GraphicRaycaster raycaster = parentCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            raycaster.enabled = false;
        }
    }

    void OnDestroy()
    {
        ResetHighlightedButtonScale(continueButtonAnchor, continueButtonBaseScale);
        ResetHighlightedButtonScale(modifyButtonAnchor, modifyButtonBaseScale);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueWithAssignedArea);
        }

        if (modifyButton != null)
        {
            modifyButton.onClick.RemoveListener(BeginModification);
        }
    }
}