using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PianoCalibrator : MonoBehaviour
{
    private enum CalibrationFlowState
    {
        AwaitingDecision,
        Editing,
        Locked
    }

    private enum EditStep
    {
        BasePlacement,
        Size,
        Translation
    }

    [Header("Panel de decisión")]
    public GameObject confirmUI;
    [SerializeField] private TextMeshProUGUI decisionTitleText;
    [SerializeField] private TextMeshProUGUI decisionBodyText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button modifyButton;

    [Header("Hint contextual")]
    [SerializeField] private GameObject controllerHintUI;
    [SerializeField] private TMP_Text controllerHintText;
    [SerializeField] private Transform rightControllerAnchor;
    [SerializeField] private Vector3 hintLocalOffset = new Vector3(0.06f, 0.03f, 0.1f);
    [SerializeField] private Vector3 cameraFallbackOffset = new Vector3(0.25f, -0.12f, 0.65f);

    [Header("Modo mando")]
    [SerializeField] private bool useControllerPromptForDecision = true;
    [SerializeField] private Transform continueButtonAnchor;
    [SerializeField] private Transform modifyButtonAnchor;
    [SerializeField] private float highlightedButtonScale = 1.18f;
    [SerializeField] private float highlightedButtonPulseSpeed = 6f;

    [Header("Etiquetas A/B")]
    [SerializeField] private TMP_Text continueButtonLabelText;
    [SerializeField] private TMP_Text modifyButtonLabelText;
    [SerializeField] private Vector3 buttonLabelLocalOffset = new Vector3(0f, 0.012f, 0f);

    [Header("Velocidades")]
    public float moveSpeed = 0.5f;
    public float scaleSpeed = 0.3f;
    [SerializeField] private float rotateSpeed = 60f;

    private bool isLocked = false;
    private CalibrationFlowState flowState = CalibrationFlowState.AwaitingDecision;
    private EditStep currentEditStep = EditStep.BasePlacement;
    private Vector3 continueButtonBaseScale = Vector3.one;
    private Vector3 modifyButtonBaseScale = Vector3.one;

    // Evento que se dispara cuando el usuario confirma la configuración
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
        ShowAwaitingDecision(false);
    }

    void Update()
    {
        if (isLocked)
        {
            UpdateButtonHighlightVisuals(false, false);
            return;
        }

        if (flowState == CalibrationFlowState.AwaitingDecision)
        {
            HandleDecisionInput();
        }
        else if (flowState == CalibrationFlowState.Editing)
        {
            HandleEditingInput();
        }

        UpdateControllerHintTransform();
        UpdateButtonLabelTransforms();
        UpdateButtonHighlightVisuals(
            flowState == CalibrationFlowState.AwaitingDecision || flowState == CalibrationFlowState.Editing,
            flowState == CalibrationFlowState.AwaitingDecision);
    }

    private void HandleDecisionInput()
    {
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            ContinueWithAssignedArea();
            return;
        }

        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            BeginModification();
        }
    }

    private void HandleEditingInput()
    {
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        switch (currentEditStep)
        {
            case EditStep.BasePlacement:
                AdjustBasePlacement(rightStick);
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                {
                    currentEditStep = EditStep.Size;
                }
                else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                {
                    ReturnToDecisionFromEditing();
                    return;
                }
                break;

            case EditStep.Size:
                AdjustSize(rightStick);
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                {
                    currentEditStep = EditStep.Translation;
                }
                else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                {
                    currentEditStep = EditStep.BasePlacement;
                }
                break;

            case EditStep.Translation:
                AdjustTranslation(rightStick);
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                {
                    ContinueWithAssignedArea();
                    return;
                }
                else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                {
                    currentEditStep = EditStep.Size;
                }
                break;
        }

        UpdateEditingHint();
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

        isLocked = true;
        flowState = CalibrationFlowState.Locked;
        ShowDecisionUI(false);
        ShowControllerHint(false);
        UpdateButtonHighlightVisuals(false, false);

        Debug.Log("<color=green>[PianoCalibrator]</color> ✅ Área del piano confirmada - Iniciando juego");
        OnPianoConfigured?.Invoke();
    }

    public void BeginModification()
    {
        if (isLocked)
        {
            return;
        }

        flowState = CalibrationFlowState.Editing;
        currentEditStep = EditStep.BasePlacement;
        ShowDecisionUI(false);
        ShowControllerHint(true);
        UpdateEditingHint();
        Debug.Log("<color=yellow>[PianoCalibrator]</color> ✏️ Modo edición del passthrough activado");
    }

    private void ReturnToDecisionFromEditing()
    {
        flowState = CalibrationFlowState.AwaitingDecision;
        ShowAwaitingDecision(true);
        Debug.Log("<color=cyan>[PianoCalibrator]</color> ↩️ Volviendo a la decisión principal del calibrador.");
    }

    private void ShowAwaitingDecision(bool afterEdit)
    {
        UpdateDecisionTexts(afterEdit);
        UpdateButtonLabelTexts("A", "B");

        bool useControllerPrompt = useControllerPromptForDecision && controllerHintUI != null && controllerHintText != null;
        ShowDecisionUI(!useControllerPrompt);

        if (useControllerPrompt)
        {
            ShowControllerHint(true);
            controllerHintText.text = afterEdit
                ? "Área ajustada.\nPulsa A para continuar.\nPulsa B para modificar otra vez."
                : "Área del teclado lista.\nPulsa A para continuar.\nPulsa B para modificar.";
            return;
        }

        ShowControllerHint(false);
    }

    private void AdjustBasePlacement(Vector2 rightStick)
    {
        Vector3 currentPosition = transform.position;
        if (Mathf.Abs(rightStick.y) > 0.05f)
        {
            currentPosition.y += rightStick.y * moveSpeed * Time.deltaTime;
        }

        transform.position = currentPosition;

        if (Mathf.Abs(rightStick.x) > 0.05f)
        {
            transform.Rotate(0f, rightStick.x * rotateSpeed * Time.deltaTime, 0f, Space.World);
        }
    }

    private void AdjustSize(Vector2 rightStick)
    {
        Vector3 currentScale = transform.localScale;
        if (Mathf.Abs(rightStick.x) > 0.05f)
        {
            currentScale.x = Mathf.Max(0.05f, currentScale.x + rightStick.x * scaleSpeed * Time.deltaTime);
        }

        if (Mathf.Abs(rightStick.y) > 0.05f)
        {
            currentScale.z = Mathf.Max(0.05f, currentScale.z + rightStick.y * scaleSpeed * Time.deltaTime);
        }

        transform.localScale = currentScale;
    }

    private void AdjustTranslation(Vector2 rightStick)
    {
        if (rightStick.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Vector3 rightMovement = transform.right * (rightStick.x * moveSpeed * Time.deltaTime);
        Vector3 forwardMovement = transform.forward * (rightStick.y * moveSpeed * Time.deltaTime);
        transform.position += rightMovement + forwardMovement;
    }

    private void UpdateEditingHint()
    {
        switch (currentEditStep)
        {
            case EditStep.BasePlacement:
                UpdateButtonLabelTexts("A", "B");
                SetControllerHint($"Paso 1 de 3: base.\nJoystick derecho arriba/abajo sube o baja.\nJoystick derecha/izquierda gira.\nAltura Y: {transform.position.y:F2}  Giro: {transform.eulerAngles.y:F0}°\nA siguiente. B volver.");
                break;

            case EditStep.Size:
                UpdateButtonLabelTexts("A", "B");
                SetControllerHint($"Paso 2 de 3: tamaño.\nJoystick derecha/izquierda cambia ancho.\nJoystick arriba/abajo cambia largo.\nAncho: {transform.localScale.x:F2}  Largo: {transform.localScale.z:F2}\nA siguiente. B regresar.");
                break;

            case EditStep.Translation:
                UpdateButtonLabelTexts("A", "B");
                SetControllerHint($"Paso 3 de 3: mover.\nJoystick derecho mueve el piano en el piso.\nPosición X: {transform.position.x:F2} Z: {transform.position.z:F2}\nA confirmar e iniciar. B regresar.");
                break;
        }
    }

    private void UpdateDecisionTexts(bool afterEdit)
    {
        if (decisionTitleText != null)
        {
            decisionTitleText.text = afterEdit
                ? "¿Continuar con el área ajustada?"
                : "¿Continuar con el área del teclado asignada?";
        }

        if (decisionBodyText != null)
        {
            decisionBodyText.text = afterEdit
                ? "Si el passthrough ya quedó bien, pulsa Continuar. Si todavía quieres corregirlo, pulsa Modificar."
                : "Puedes continuar con el área actual del teclado MIDI o entrar a modificarla antes de empezar la canción.";
        }
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

        Transform anchor = rightControllerAnchor;
        if (anchor != null)
        {
            controllerHintUI.transform.position = anchor.TransformPoint(hintLocalOffset);
            controllerHintUI.transform.rotation = Quaternion.LookRotation(controllerHintUI.transform.position - GetLookTargetPosition());
            return;
        }

        if (Camera.main != null)
        {
            Transform cameraTransform = Camera.main.transform;
            controllerHintUI.transform.position = cameraTransform.TransformPoint(cameraFallbackOffset);
            controllerHintUI.transform.rotation = Quaternion.LookRotation(controllerHintUI.transform.position - cameraTransform.position);
        }
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

        label.transform.position = buttonAnchor.TransformPoint(buttonLabelLocalOffset);

        Vector3 lookTarget = GetLookTargetPosition();
        Vector3 directionToTarget = label.transform.position - lookTarget;
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

        return controllerHintUI.transform.position - controllerHintUI.transform.forward;
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
}