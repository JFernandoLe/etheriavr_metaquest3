using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Unity.VRTemplate;

/// <summary>
/// Fuerza un modo de interacción VR coherente sobre el rig del XR Interaction Toolkit:
/// desactiva locomoción y teleport, deja el puntero de UI como única interacción de mando
/// y reconcilia manos y mandos según lo que esté realmente trackeado.
/// Se reparte en archivos parciales: HandTracking, ControllerRays, Diagnostics y Reflection.
/// </summary>
[DefaultExecutionOrder(-1000)]
public partial class QuestXRInteractionController : MonoBehaviour
{
    private const string DiagnosticTag = "XRHandsDebug";
    private const string PianoGameSceneName = "PianoGame";
    private const string LeftControllerName = "Left Controller";
    private const string RightControllerName = "Right Controller";
    private const string LeftControllerStabilizedOriginName = "Left Controller Teleport Stabilized Origin";
    private const string RightControllerStabilizedOriginName = "Right Controller Teleport Stabilized Origin";
    private const string LeftControllerStabilizedAttachName = "Left Controller Stabilized Attach";
    private const string RightControllerStabilizedAttachName = "Right Controller Stabilized Attach";
    private const string LeftControllerUiAttachName = "Left Controller UI Attach";
    private const string RightControllerUiAttachName = "Right Controller UI Attach";
    private const float MinimumVisibleControllerRayWidth = 0.01f;

    [SerializeField] private bool disableLocomotionRoot = true;
    [SerializeField] private bool disableLocomotionAndTeleportOnControllers = true;
    [SerializeField] private bool disableJoystickUiFallback = true;
    [SerializeField] private bool disableGamepadUiFallback = true;
    [SerializeField] private bool disableBuiltInUiFallback = true;
    [SerializeField] private string locomotionRootName = "Locomotion";
    [SerializeField] private bool useDirectControllerRayOriginWhenTeleportDisabled = true;
    [SerializeField] private bool enableControllerRayDiagnostics = false;
    [SerializeField] private int controllerRayDiagnosticIntervalFrames = 120;
    [SerializeField] private bool enableHandTrackingSupport = true;
    [SerializeField] private bool disableHandTrackingInPianoGameScene = false;
    [SerializeField] private bool forceControllerOnlyModeInPianoGameScene = false;
    [SerializeField] private bool enableXrHandsDebugLogs = false;
    [Tooltip("Tras confirmar el área del piano, oculta los modelos de mando para que Meta active hand tracking.")]
    [SerializeField] private bool hideControllersAfterPianoCalibration = true;
    [SerializeField] private float trackedInteractionRefreshHz = 8f;
    [SerializeField] private bool showHandPointerDots = false;
    [SerializeField] private GameObject handsRigTemplate;
    [SerializeField] private GameObject handsPermissionsManagerPrefab;
    [SerializeField] private string cameraOffsetName = "Camera Offset";
    [SerializeField] private string leftHandName = "Left Hand";
    [SerializeField] private string rightHandName = "Right Hand";
    [SerializeField] private string handVisualizerName = "Hand Visualizer";
    [SerializeField] private string handsSmoothingPostProcessorName = "Hands Smoothing Post Processor";
    [SerializeField] private bool logConfigurationOnce;

    private static readonly List<InputDevice> CachedXRInputDevices = new List<InputDevice>();

    private bool hasLogged;
    private XRInputModalityManager cachedInputModalityManager;
    private XRInputModalityManager.InputMode lastLoggedLeftResolvedMode = XRInputModalityManager.InputMode.None;
    private XRInputModalityManager.InputMode lastLoggedRightResolvedMode = XRInputModalityManager.InputMode.None;
    private bool hasLoggedResolvedModes;
    private TrackingStatus lastLeftHandStatus;
    private TrackingStatus lastRightHandStatus;
    private TrackingStatus lastLeftControllerStatus;
    private TrackingStatus lastRightControllerStatus;
    private bool hasTrackingSnapshot;
    private int controllerRayDiagnosticFrameCounter;
    private bool pianoGameplayHandsMode;
    private float nextTrackedInteractionRefreshTime;
    private float nextPianoHandsMaintainTime;
    private OVRManager cachedOvrManager;

    private void Awake()
    {
        LogHands($"Awake root={gameObject.name}");
        ApplyInteractionMode();
    }

    private void OnEnable()
    {
        LogHands($"OnEnable root={gameObject.name}");
        PianoCalibrator.OnPianoConfigured += EnterPianoGameplayHandsMode;
        ApplyInteractionMode();
    }

    private void OnDisable()
    {
        PianoCalibrator.OnPianoConfigured -= EnterPianoGameplayHandsMode;
    }

    private void Start()
    {
        LogHands($"Start root={gameObject.name}");
        ApplyInteractionMode();
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (ShouldPreferHandsInPianoGame() && now >= nextPianoHandsMaintainTime)
            MaintainPianoHandTracking();

        if (now >= nextTrackedInteractionRefreshTime)
        {
            nextTrackedInteractionRefreshTime = now + (1f / Mathf.Max(1f, trackedInteractionRefreshHz));
            RefreshTrackedInteractionState();
        }

        if (!enableControllerRayDiagnostics) return;

        controllerRayDiagnosticFrameCounter++;
        if (controllerRayDiagnosticFrameCounter < Mathf.Max(1, controllerRayDiagnosticIntervalFrames)) return;

        controllerRayDiagnosticFrameCounter = 0;
        LogControllerRayDiagnostics("periodic", false);
    }

    /// <summary>
    /// Tras calibrar el piano: oculta mandos visuales y fuerza hand tracking.
    /// Meta no dibuja manos virtuales mientras prioriza Touch controllers.
    /// </summary>
    public void EnterPianoGameplayHandsMode()
    {
        if (!ShouldPreferHandsInPianoGame()) return;

        pianoGameplayHandsMode = hideControllersAfterPianoCalibration;
        TryEnableSimultaneousHandsAndControllers();
        EnsureHandSubsystemManager()?.EnableHandTracking();

        Transform cameraOffset = transform.Find(cameraOffsetName);
        if (cameraOffset != null)
        {
            ForceActivatePianoHandVisuals(
                FindChildObject(cameraOffset, leftHandName),
                FindChildObject(cameraOffset, rightHandName),
                FindChildObject(cameraOffset, handVisualizerName));
        }

        RefreshTrackedInteractionState(forceLog: true);
        LogHands($"EnterPianoGameplayHandsMode hideControllers={pianoGameplayHandsMode}");
    }

    public void ExitPianoGameplayHandsMode()
    {
        pianoGameplayHandsMode = false;
        RefreshTrackedInteractionState(forceLog: true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        LogHands($"ApplicationFocus hasFocus={hasFocus}");
        if (hasFocus) HandleApplicationResume("focus");
    }

    private void OnApplicationPause(bool isPaused)
    {
        LogHands($"ApplicationPause isPaused={isPaused}");
        if (!isPaused) HandleApplicationResume("pause");
    }

    /// <summary>Al volver del suspendido el rig puede quedar desincronizado, así que se reaplica todo.</summary>
    private void HandleApplicationResume(string reason)
    {
        LogHands($"Resuming interaction sync reason={reason}");
        ApplyInteractionMode();
        RefreshTrackedInteractionState(forceLog: true);
        LogControllerRayDiagnostics($"resume:{reason}", true);
    }

    private void ApplyInteractionMode()
    {
        // Instanciar el template dispara OnEnable de este componente; evita la reentrada.
        if (isInstallingHandsTemplate) return;

        if (disableLocomotionRoot)
        {
            Transform locomotionRoot = transform.Find(locomotionRootName);
            if (locomotionRoot != null && locomotionRoot.gameObject.activeSelf)
                locomotionRoot.gameObject.SetActive(false);
        }

        if (disableLocomotionAndTeleportOnControllers)
        {
            foreach (ControllerInputActionManager controllerManager in GetComponentsInChildren<ControllerInputActionManager>(true))
                controllerManager.enableLocomotionAndTeleport = false;
        }

        ConfigureControllerRayOrigins();
        EnsureHandTrackingSupport();
        ConfigureUiInputModules();

        if (!logConfigurationOnce || hasLogged) return;

        Debug.Log("[XRInteraction] Controller UI-only mode enabled. Native locomotion and teleport are disabled.", this);
        hasLogged = true;
    }

    private bool IsHandTrackingEnabledForActiveScene() =>
        enableHandTrackingSupport && !IsPianoGameHandTrackingOverrideEnabled();

    private bool IsControllerOnlyModeEnabledForActiveScene() =>
        forceControllerOnlyModeInPianoGameScene && IsActiveScene(PianoGameSceneName);

    private bool IsPianoGameHandTrackingOverrideEnabled() =>
        disableHandTrackingInPianoGameScene && IsActiveScene(PianoGameSceneName);

    private static bool IsActiveScene(string sceneName) =>
        string.Equals(SceneManager.GetActiveScene().name, sceneName, System.StringComparison.Ordinal);

    private XRInputModalityManager ResolveInputModalityManager()
    {
        if (cachedInputModalityManager == null) cachedInputModalityManager = GetComponent<XRInputModalityManager>();

        return cachedInputModalityManager;
    }

    /// <summary>
    /// Detecta cambios de tracking, pide al XRInputModalityManager que recalcule su modo
    /// y reconcilia qué visuales deben estar activas.
    /// </summary>
    private void RefreshTrackedInteractionState(bool forceLog = false)
    {
        XRInputModalityManager inputModalityManager = ResolveInputModalityManager();
        if (inputModalityManager == null) return;

        TrackingStatus leftHandStatus = XRInputTrackingAggregator.GetLeftTrackedHandStatus();
        TrackingStatus rightHandStatus = XRInputTrackingAggregator.GetRightTrackedHandStatus();
        TrackingStatus leftControllerStatus = XRInputTrackingAggregator.GetLeftControllerStatus();
        TrackingStatus rightControllerStatus = XRInputTrackingAggregator.GetRightControllerStatus();

        bool isFirstSnapshot = forceLog || !hasTrackingSnapshot;
        bool leftTrackingChanged = isFirstSnapshot
                                   || DidTrackingStatusChange(lastLeftHandStatus, leftHandStatus)
                                   || DidTrackingStatusChange(lastLeftControllerStatus, leftControllerStatus);
        bool rightTrackingChanged = isFirstSnapshot
                                    || DidTrackingStatusChange(lastRightHandStatus, rightHandStatus)
                                    || DidTrackingStatusChange(lastRightControllerStatus, rightControllerStatus);

        lastLeftHandStatus = leftHandStatus;
        lastRightHandStatus = rightHandStatus;
        lastLeftControllerStatus = leftControllerStatus;
        lastRightControllerStatus = rightControllerStatus;
        hasTrackingSnapshot = true;

        if (leftTrackingChanged) TryInvokeNoArgumentMethod(inputModalityManager, "UpdateLeftMode");
        if (rightTrackingChanged) TryInvokeNoArgumentMethod(inputModalityManager, "UpdateRightMode");
        if (leftTrackingChanged || rightTrackingChanged) ConfigureControllerRayOrigins();

        XRInputModalityManager.InputMode leftMode = GetInputModeField(inputModalityManager, "m_LeftInputMode");
        XRInputModalityManager.InputMode rightMode = GetInputModeField(inputModalityManager, "m_RightInputMode");

        SyncTrackedInteractionVisibility(inputModalityManager, leftMode, rightMode,
            leftHandStatus, rightHandStatus, leftControllerStatus, rightControllerStatus);

        bool shouldLog = forceLog || !hasLoggedResolvedModes
                                  || leftMode != lastLoggedLeftResolvedMode
                                  || rightMode != lastLoggedRightResolvedMode;
        if (!shouldLog) return;

        hasLoggedResolvedModes = true;
        lastLoggedLeftResolvedMode = leftMode;
        lastLoggedRightResolvedMode = rightMode;

        LogHands($"Resolved modality leftMode={leftMode} rightMode={rightMode} leftHandTracked={leftHandStatus.isTracked} rightHandTracked={rightHandStatus.isTracked} leftControllerTracked={leftControllerStatus.isTracked} rightControllerTracked={rightControllerStatus.isTracked} leftHandActive={(inputModalityManager.leftHand != null && inputModalityManager.leftHand.activeSelf)} rightHandActive={(inputModalityManager.rightHand != null && inputModalityManager.rightHand.activeSelf)} leftControllerActive={(inputModalityManager.leftController != null && inputModalityManager.leftController.activeSelf)} rightControllerActive={(inputModalityManager.rightController != null && inputModalityManager.rightController.activeSelf)}");

        LogControllerRayDiagnostics("modality-change", forceLog);
    }

    private static bool DidTrackingStatusChange(TrackingStatus previousStatus, TrackingStatus currentStatus) =>
        previousStatus.isConnected != currentStatus.isConnected
        || previousStatus.isTracked != currentStatus.isTracked
        || previousStatus.trackingState != currentStatus.trackingState;

    private void SyncTrackedInteractionVisibility(
        XRInputModalityManager inputModalityManager,
        XRInputModalityManager.InputMode leftMode,
        XRInputModalityManager.InputMode rightMode,
        TrackingStatus leftHandStatus,
        TrackingStatus rightHandStatus,
        TrackingStatus leftControllerStatus,
        TrackingStatus rightControllerStatus)
    {
        if (inputModalityManager == null) return;

        bool leftShouldShowHand;
        bool rightShouldShowHand;
        bool leftShouldShowController;
        bool rightShouldShowController;

        if (IsControllerOnlyModeEnabledForActiveScene())
        {
            // Modo legacy opcional: solo mandos en piano.
            leftShouldShowHand = false;
            rightShouldShowHand = false;
            leftShouldShowController = IsExactPhysicalControllerTracked(InputDeviceCharacteristics.Left);
            rightShouldShowController = IsExactPhysicalControllerTracked(InputDeviceCharacteristics.Right);
        }
        else if (ShouldPreferHandsInPianoGame())
        {
            // Piano: raíces de mano siempre activas. Tras calibrar, oculta mandos para
            // que Meta deje de priorizar Touch y active hand tracking de verdad.
            leftShouldShowHand = true;
            rightShouldShowHand = true;

            if (pianoGameplayHandsMode)
            {
                leftShouldShowController = false;
                rightShouldShowController = false;
            }
            else
            {
                leftShouldShowController = leftControllerStatus.isTracked
                                           || IsExactPhysicalControllerTracked(InputDeviceCharacteristics.Left);
                rightShouldShowController = rightControllerStatus.isTracked
                                            || IsExactPhysicalControllerTracked(InputDeviceCharacteristics.Right);
            }
        }
        else
        {
            leftShouldShowHand = leftHandStatus.isTracked && (IsHandInputMode(leftMode) || !leftControllerStatus.isTracked);
            rightShouldShowHand = rightHandStatus.isTracked && (IsHandInputMode(rightMode) || !rightControllerStatus.isTracked);
            leftShouldShowController = leftControllerStatus.isTracked && !leftShouldShowHand;
            rightShouldShowController = rightControllerStatus.isTracked && !rightShouldShowHand;
        }

        SetActiveIfNeeded(inputModalityManager.leftHand, leftShouldShowHand);
        SetActiveIfNeeded(inputModalityManager.rightHand, rightShouldShowHand);
        SetActiveIfNeeded(inputModalityManager.leftController, leftShouldShowController);
        SetActiveIfNeeded(inputModalityManager.rightController, rightShouldShowController);

        // El visualizador de mallas debe seguir activo si hay al menos una mano visible.
        Transform cameraOffset = transform.Find(cameraOffsetName);
        if (cameraOffset != null)
            SetActiveIfNeeded(FindChildObject(cameraOffset, handVisualizerName), leftShouldShowHand || rightShouldShowHand);
    }

    private bool ShouldPreferHandsInPianoGame() =>
        enableHandTrackingSupport
        && IsActiveScene(PianoGameSceneName)
        && !disableHandTrackingInPianoGameScene;

    private static bool IsHandInputMode(XRInputModalityManager.InputMode inputMode) =>
        inputMode.ToString().IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    /// <summary>
    /// Exige coincidencia exacta de características para no confundir un mando real
    /// con una mano emulada que también se anuncia como controller.
    /// </summary>
    private static bool IsExactPhysicalControllerTracked(InputDeviceCharacteristics handedness)
    {
        InputDeviceCharacteristics desiredCharacteristics = InputDeviceCharacteristics.HeldInHand
                                                            | InputDeviceCharacteristics.TrackedDevice
                                                            | InputDeviceCharacteristics.Controller
                                                            | handedness;

        if (!TryGetDeviceWithExactCharacteristics(desiredCharacteristics, out InputDevice inputDevice)) return false;

        if (inputDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && !isTracked) return false;

        if (inputDevice.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState trackingState))
        {
            const InputTrackingState requiredFlags = InputTrackingState.Position | InputTrackingState.Rotation;
            if ((trackingState & requiredFlags) == 0) return false;
        }

        return true;
    }

    private static bool TryGetDeviceWithExactCharacteristics(InputDeviceCharacteristics desiredCharacteristics, out InputDevice inputDevice)
    {
        CachedXRInputDevices.Clear();
        InputDevices.GetDevices(CachedXRInputDevices);

        foreach (InputDevice device in CachedXRInputDevices)
        {
            if (device.characteristics != desiredCharacteristics) continue;

            inputDevice = device;
            return true;
        }

        inputDevice = default;
        return false;
    }

    private static T FindLoadedObjectOfType<T>() where T : Object
    {
        foreach (T loadedObject in Resources.FindObjectsOfTypeAll<T>())
        {
            if (loadedObject == null) continue;

            GameObject owner = loadedObject switch
            {
                Component component => component.gameObject,
                GameObject gameObject => gameObject,
                _ => null,
            };

            if (owner == null) continue;
            if (!owner.scene.IsValid() || !owner.scene.isLoaded) continue;

            return loadedObject;
        }

        return null;
    }

    private static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == childName) return child;
        }

        return null;
    }

    private static GameObject FindChildObject(Transform parent, string childName) =>
        FindChildTransform(parent, childName)?.gameObject;
}
