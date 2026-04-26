using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.VRTemplate;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DefaultExecutionOrder(-1000)]
public class QuestXRInteractionController : MonoBehaviour
{
    private const string DiagnosticTag = "XRHandsDebug";
    private const string AndroidHandTrackingPermission = "android.permission.HAND_TRACKING";
    private const string OculusHandTrackingPermission = "com.oculus.permission.HAND_TRACKING";
    private const string HorizonOsHandTrackingPermission = "horizonos.permission.HAND_TRACKING";
    private const string MetaQuestLeftHandVisualName = "Left Hand Quest Visual";
    private const string MetaQuestRightHandVisualName = "Right Hand Quest Visual";
    private const string AndroidXRLeftHandVisualName = "Left Hand Android XR Visual";
    private const string AndroidXRRightHandVisualName = "Right Hand Android XR Visual";
    private const string AimPoseName = "Aim Pose";
    private const string HandPointerDotName = "Hand Tracking Pointer Dot";
    private const string LeftControllerName = "Left Controller";
    private const string RightControllerName = "Right Controller";
    private const string LeftControllerStabilizedOriginName = "Left Controller Teleport Stabilized Origin";
    private const string RightControllerStabilizedOriginName = "Right Controller Teleport Stabilized Origin";
    private const string LeftControllerStabilizedAttachName = "Left Controller Stabilized Attach";
    private const string RightControllerStabilizedAttachName = "Right Controller Stabilized Attach";
    private const string LeftControllerUiAttachName = "Left Controller UI Attach";
    private const string RightControllerUiAttachName = "Right Controller UI Attach";
    private const float HandPointerDotScale = 0.0125f;
    private const float MinimumVisibleControllerRayWidth = 0.01f;

    [SerializeField] private bool disableLocomotionRoot = true;
    [SerializeField] private bool disableLocomotionAndTeleportOnControllers = true;
    [SerializeField] private bool disableJoystickUiFallback = true;
    [SerializeField] private bool disableGamepadUiFallback = true;
    [SerializeField] private bool disableBuiltInUiFallback = true;
    [SerializeField] private string locomotionRootName = "Locomotion";
    [SerializeField] private bool useDirectControllerRayOriginWhenTeleportDisabled = true;
    [SerializeField] private bool enableControllerRayDiagnostics = true;
    [SerializeField] private int controllerRayDiagnosticIntervalFrames = 120;
    [SerializeField] private bool enableHandTrackingSupport = true;
    [SerializeField] private GameObject handsRigTemplate;
    [SerializeField] private GameObject handsPermissionsManagerPrefab;
    [SerializeField] private string cameraOffsetName = "Camera Offset";
    [SerializeField] private string leftHandName = "Left Hand";
    [SerializeField] private string rightHandName = "Right Hand";
    [SerializeField] private string handVisualizerName = "Hand Visualizer";
    [SerializeField] private string handsSmoothingPostProcessorName = "Hands Smoothing Post Processor";
    [SerializeField] private bool logConfigurationOnce;

    private static bool isInstallingHandsTemplate;
    private static bool isHandTrackingPermissionRequestPending;
    private bool hasLogged;
    private HandSubsystemManager cachedHandSubsystemManager;
    private XRInputModalityManager cachedInputModalityManager;
    private static readonly List<XRHandSubsystem> CachedHandSubsystems = new List<XRHandSubsystem>();
    private string metaQuestLeftHandVisualPath;
    private string metaQuestRightHandVisualPath;
    private string androidXRLeftHandVisualPath;
    private string androidXRRightHandVisualPath;
    private XRInputModalityManager.InputMode lastLoggedLeftResolvedMode = XRInputModalityManager.InputMode.None;
    private XRInputModalityManager.InputMode lastLoggedRightResolvedMode = XRInputModalityManager.InputMode.None;
    private bool hasLoggedResolvedModes;
    private TrackingStatus lastLeftHandStatus;
    private TrackingStatus lastRightHandStatus;
    private TrackingStatus lastLeftControllerStatus;
    private TrackingStatus lastRightControllerStatus;
    private bool hasTrackingSnapshot;
    private static Material cachedHandPointerDotMaterial;
    private int controllerRayDiagnosticFrameCounter;
    private string lastLeftControllerRaySnapshot;
    private string lastRightControllerRaySnapshot;

    private void Awake()
    {
        LogHands($"Awake root={gameObject.name}");
        ApplyInteractionMode();
    }

    private void OnEnable()
    {
        LogHands($"OnEnable root={gameObject.name}");
        ApplyInteractionMode();
    }

    private void Start()
    {
        LogHands($"Start root={gameObject.name}");
        ApplyInteractionMode();
    }

    private void Update()
    {
        RefreshTrackedInteractionState();

        if (!enableControllerRayDiagnostics)
            return;

        controllerRayDiagnosticFrameCounter++;
        if (controllerRayDiagnosticFrameCounter >= Mathf.Max(1, controllerRayDiagnosticIntervalFrames))
        {
            controllerRayDiagnosticFrameCounter = 0;
            LogControllerRayDiagnostics("periodic", false);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        LogHands($"ApplicationFocus hasFocus={hasFocus}");
        if (hasFocus)
            HandleApplicationResume("focus");
    }

    private void OnApplicationPause(bool isPaused)
    {
        LogHands($"ApplicationPause isPaused={isPaused}");
        if (!isPaused)
            HandleApplicationResume("pause");
    }

    private void ApplyInteractionMode()
    {
        if (isInstallingHandsTemplate)
            return;

        if (disableLocomotionRoot)
        {
            Transform locomotionRoot = transform.Find(locomotionRootName);
            if (locomotionRoot != null && locomotionRoot.gameObject.activeSelf)
                locomotionRoot.gameObject.SetActive(false);
        }

        if (disableLocomotionAndTeleportOnControllers)
        {
            ControllerInputActionManager[] controllerManagers = GetComponentsInChildren<ControllerInputActionManager>(true);
            foreach (ControllerInputActionManager controllerManager in controllerManagers)
            {
                controllerManager.enableLocomotionAndTeleport = false;
            }
        }

        ConfigureControllerRayOrigins();
        EnsureHandTrackingSupport();
        ConfigureUiInputModules();

        if (logConfigurationOnce && !hasLogged)
        {
            Debug.Log("[XRInteraction] Controller UI-only mode enabled. Native locomotion and teleport are disabled.", this);
            hasLogged = true;
        }
    }

    private void EnsureHandTrackingSupport()
    {
        if (!enableHandTrackingSupport)
        {
            LogHands("Hand tracking support disabled on QuestXRInteractionController");
            return;
        }

        HandSubsystemManager handSubsystemManager = EnsureHandSubsystemManager();
        LogHands($"EnsureHandTrackingSupport managerFound={(handSubsystemManager != null)} {DescribeHandSubsystems()}");
        EnsureHandTrackingPermission(handSubsystemManager);

        Transform cameraOffset = transform.Find(cameraOffsetName);
        if (cameraOffset == null)
        {
            LogHandsWarning($"Camera offset not found. expected={cameraOffsetName}");
            return;
        }

        GameObject existingLeftHand = FindChildObject(cameraOffset, leftHandName);
        GameObject existingRightHand = FindChildObject(cameraOffset, rightHandName);
        GameObject existingHandVisualizer = FindChildObject(cameraOffset, handVisualizerName);

        LogHands(
            $"HandsBeforeInstall templateAssigned={(handsRigTemplate != null)} permissionsPrefabAssigned={(handsPermissionsManagerPrefab != null)} " +
            $"leftHand={(existingLeftHand != null)} rightHand={(existingRightHand != null)} handVisualizer={(existingHandVisualizer != null)}");

        if ((existingLeftHand == null || existingRightHand == null || existingHandVisualizer == null) && handsRigTemplate != null)
        {
            InstallHandsFromTemplate(handsRigTemplate, cameraOffset, existingHandVisualizer == null, existingLeftHand == null, existingRightHand == null);
            existingHandVisualizer = FindChildObject(cameraOffset, handVisualizerName);
            existingLeftHand = FindChildObject(cameraOffset, leftHandName);
            existingRightHand = FindChildObject(cameraOffset, rightHandName);
        }

        LogHands(
            $"HandsAfterInstall leftHand={(existingLeftHand != null)} rightHand={(existingRightHand != null)} handVisualizer={(existingHandVisualizer != null)}");

        AssignHandsToModalityManager(existingLeftHand, existingRightHand);
        AssignHandVisualizerMeshes(existingHandVisualizer, existingLeftHand, existingRightHand);
        EnsureHandPointerDots(existingLeftHand, existingRightHand);
        RefreshTrackedInteractionState(forceLog: true);
    }

    private void HandleApplicationResume(string reason)
    {
        LogHands($"Resuming interaction sync reason={reason}");
        ApplyInteractionMode();
        RefreshTrackedInteractionState(forceLog: true);
        LogControllerRayDiagnostics($"resume:{reason}", true);
    }

    private HandSubsystemManager EnsureHandSubsystemManager()
    {
        if (cachedHandSubsystemManager != null)
        {
            LogHands("Using cached HandSubsystemManager");
            return cachedHandSubsystemManager;
        }

        cachedHandSubsystemManager = GetComponentInChildren<HandSubsystemManager>(true);
        if (cachedHandSubsystemManager != null)
        {
            LogHands("Found HandSubsystemManager in XR rig hierarchy");
            return cachedHandSubsystemManager;
        }

        cachedHandSubsystemManager = FindLoadedObjectOfType<HandSubsystemManager>();
        if (cachedHandSubsystemManager != null)
        {
            LogHands("Found HandSubsystemManager in loaded scene objects");
            return cachedHandSubsystemManager;
        }

        GameObject runtimeManagerObject = new GameObject("Runtime Hand Subsystem Manager");
        runtimeManagerObject.transform.SetParent(transform, false);
        cachedHandSubsystemManager = runtimeManagerObject.AddComponent<HandSubsystemManager>();
        LogHands("Created fallback Runtime Hand Subsystem Manager");
        return cachedHandSubsystemManager;
    }

    private void EnsureHandTrackingPermission(HandSubsystemManager handSubsystemManager)
    {
        if (handSubsystemManager == null)
            return;

#if UNITY_ANDROID && !UNITY_EDITOR
        bool hasAndroidPermission = true;
        bool hasOculusPermission = true;
        bool hasHorizonOsPermission = true;
        try
        {
            hasAndroidPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(AndroidHandTrackingPermission);
            hasOculusPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(OculusHandTrackingPermission);
            hasHorizonOsPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(HorizonOsHandTrackingPermission);
        }
        catch
        {
        }

        LogHands(
            $"PermissionCheck android={hasAndroidPermission} oculus={hasOculusPermission} horizonos={hasHorizonOsPermission} requestPending={isHandTrackingPermissionRequestPending}");

        if (hasAndroidPermission || hasOculusPermission || hasHorizonOsPermission)
        {
            LogHands($"PermissionReady enabling hand tracking {DescribeHandSubsystems()}");
            handSubsystemManager.EnableHandTracking();
            return;
        }

        if (isHandTrackingPermissionRequestPending)
        {
            LogHands("Permission request already pending");
            return;
        }

        UnityEngine.Android.PermissionCallbacks callbacks = new UnityEngine.Android.PermissionCallbacks();
        callbacks.PermissionGranted += _ =>
        {
            isHandTrackingPermissionRequestPending = false;
            LogHands($"PermissionGranted enabling hand tracking {DescribeHandSubsystems()}");
            handSubsystemManager.EnableHandTracking();
        };
        callbacks.PermissionDenied += _ =>
        {
            isHandTrackingPermissionRequestPending = false;
            LogHandsWarning("PermissionDenied disabling hand tracking");
            handSubsystemManager.DisableHandTracking();
        };
        callbacks.PermissionDeniedAndDontAskAgain += _ =>
        {
            isHandTrackingPermissionRequestPending = false;
            LogHandsWarning("PermissionDeniedDontAskAgain disabling hand tracking");
            handSubsystemManager.DisableHandTracking();
        };

        isHandTrackingPermissionRequestPending = true;
        LogHands("Requesting hand tracking permission via horizonos.permission.HAND_TRACKING");
        UnityEngine.Android.Permission.RequestUserPermission(HorizonOsHandTrackingPermission, callbacks);
#else
        LogHands($"Non-Android environment enabling hand tracking {DescribeHandSubsystems()}");
        handSubsystemManager.EnableHandTracking();
#endif
    }

    private void InstallHandsFromTemplate(GameObject handsTemplatePrefab, Transform cameraOffset, bool needsHandVisualizer, bool needsLeftHand, bool needsRightHand)
    {
        if (!needsHandVisualizer && !needsLeftHand && !needsRightHand)
            return;

        if (handsTemplatePrefab == null)
        {
            LogHandsWarning("Hands template prefab is null; cannot install hand rig");
            return;
        }

        LogHands(
            $"InstallHandsFromTemplate template={handsTemplatePrefab.name} needsVisualizer={needsHandVisualizer} needsLeft={needsLeftHand} needsRight={needsRightHand}");

        isInstallingHandsTemplate = true;
        GameObject templateInstance = Instantiate(handsTemplatePrefab);
        isInstallingHandsTemplate = false;

        if (templateInstance == null)
        {
            LogHandsWarning("Failed to instantiate hands template prefab");
            return;
        }

        templateInstance.SetActive(false);

        try
        {
            Transform templateCameraOffset = FindChildTransform(templateInstance.transform, cameraOffsetName);
            if (templateCameraOffset == null)
            {
                LogHandsWarning($"Hands template camera offset not found. expected={cameraOffsetName}");
                return;
            }

            GameObject templateHandVisualizer = FindChildObject(templateCameraOffset, handVisualizerName);
            GameObject templateLeftHand = FindChildObject(templateCameraOffset, leftHandName);
            GameObject templateRightHand = FindChildObject(templateCameraOffset, rightHandName);
            CacheHandVisualizerReferencePaths(templateHandVisualizer, templateLeftHand, templateRightHand);

            if (needsHandVisualizer)
                CloneChildToTarget(FindChildTransform(templateCameraOffset, handVisualizerName), cameraOffset);

            if (needsLeftHand)
                CloneChildToTarget(FindChildTransform(templateCameraOffset, leftHandName), cameraOffset);

            if (needsRightHand)
                CloneChildToTarget(FindChildTransform(templateCameraOffset, rightHandName), cameraOffset);

            if (FindChildObject(transform, handsSmoothingPostProcessorName) == null)
                CloneChildToTarget(FindChildTransform(templateInstance.transform, handsSmoothingPostProcessorName), transform);

            LogHands("Installed missing hand objects from official hands template");
        }
        finally
        {
            Destroy(templateInstance);
            isInstallingHandsTemplate = false;
        }
    }

    private void AssignHandsToModalityManager(GameObject leftHand, GameObject rightHand)
    {
        if (leftHand == null || rightHand == null)
        {
            LogHandsWarning($"AssignHandsToModalityManager skipped left={(leftHand != null)} right={(rightHand != null)}");
            return;
        }

        XRInputModalityManager inputModalityManager = cachedInputModalityManager != null
            ? cachedInputModalityManager
            : GetComponent<XRInputModalityManager>();

        if (inputModalityManager == null)
        {
            LogHandsWarning("XRInputModalityManager not found on XR rig root");
            return;
        }

        cachedInputModalityManager = inputModalityManager;
        inputModalityManager.leftHand = leftHand;
        inputModalityManager.rightHand = rightHand;
        LogHands(
            $"Assigned hands to XRInputModalityManager leftActiveSelf={leftHand.activeSelf} rightActiveSelf={rightHand.activeSelf} currentInputMode={XRInputModalityManager.currentInputMode.Value}");
    }

    private void RefreshTrackedInteractionState(bool forceLog = false)
    {
        XRInputModalityManager inputModalityManager = cachedInputModalityManager != null
            ? cachedInputModalityManager
            : GetComponent<XRInputModalityManager>();

        if (inputModalityManager == null)
            return;

        TrackingStatus leftHandStatus = XRInputTrackingAggregator.GetLeftTrackedHandStatus();
        TrackingStatus rightHandStatus = XRInputTrackingAggregator.GetRightTrackedHandStatus();
        TrackingStatus leftControllerStatus = XRInputTrackingAggregator.GetLeftControllerStatus();
        TrackingStatus rightControllerStatus = XRInputTrackingAggregator.GetRightControllerStatus();

        bool leftTrackingChanged = forceLog || !hasTrackingSnapshot || DidTrackingStatusChange(lastLeftHandStatus, leftHandStatus) || DidTrackingStatusChange(lastLeftControllerStatus, leftControllerStatus);
        bool rightTrackingChanged = forceLog || !hasTrackingSnapshot || DidTrackingStatusChange(lastRightHandStatus, rightHandStatus) || DidTrackingStatusChange(lastRightControllerStatus, rightControllerStatus);

        lastLeftHandStatus = leftHandStatus;
        lastRightHandStatus = rightHandStatus;
        lastLeftControllerStatus = leftControllerStatus;
        lastRightControllerStatus = rightControllerStatus;
        hasTrackingSnapshot = true;

        if (leftTrackingChanged)
            TryInvokeNoArgumentMethod(inputModalityManager, "UpdateLeftMode");

        if (rightTrackingChanged)
            TryInvokeNoArgumentMethod(inputModalityManager, "UpdateRightMode");

        if (leftTrackingChanged || rightTrackingChanged)
            ConfigureControllerRayOrigins();

        XRInputModalityManager.InputMode leftMode = GetInputModeField(inputModalityManager, "m_LeftInputMode");
        XRInputModalityManager.InputMode rightMode = GetInputModeField(inputModalityManager, "m_RightInputMode");

        SyncTrackedInteractionVisibility(inputModalityManager, leftMode, rightMode, leftHandStatus, rightHandStatus, leftControllerStatus, rightControllerStatus);

        bool shouldLog = forceLog || !hasLoggedResolvedModes || leftMode != lastLoggedLeftResolvedMode || rightMode != lastLoggedRightResolvedMode;
        if (!shouldLog)
            return;

        hasLoggedResolvedModes = true;
        lastLoggedLeftResolvedMode = leftMode;
        lastLoggedRightResolvedMode = rightMode;

        LogHands(
            $"Resolved modality leftMode={leftMode} rightMode={rightMode} leftHandTracked={leftHandStatus.isTracked} rightHandTracked={rightHandStatus.isTracked} leftControllerTracked={leftControllerStatus.isTracked} rightControllerTracked={rightControllerStatus.isTracked} leftHandActive={(inputModalityManager.leftHand != null && inputModalityManager.leftHand.activeSelf)} rightHandActive={(inputModalityManager.rightHand != null && inputModalityManager.rightHand.activeSelf)} leftControllerActive={(inputModalityManager.leftController != null && inputModalityManager.leftController.activeSelf)} rightControllerActive={(inputModalityManager.rightController != null && inputModalityManager.rightController.activeSelf)}");

        LogControllerRayDiagnostics("modality-change", forceLog);
    }

    private void AssignHandVisualizerMeshes(GameObject handVisualizer, GameObject leftHand, GameObject rightHand)
    {
        if (handVisualizer == null || leftHand == null || rightHand == null)
        {
            LogHandsWarning(
                $"AssignHandVisualizerMeshes skipped handVisualizer={(handVisualizer != null)} left={(leftHand != null)} right={(rightHand != null)}");
            return;
        }

        GameObject metaQuestLeftHandVisual = FindHandVisualObject(leftHand.transform, metaQuestLeftHandVisualPath, MetaQuestLeftHandVisualName);
        GameObject metaQuestRightHandVisual = FindHandVisualObject(rightHand.transform, metaQuestRightHandVisualPath, MetaQuestRightHandVisualName);
        GameObject androidXRLeftHandVisual = FindHandVisualObject(leftHand.transform, androidXRLeftHandVisualPath, AndroidXRLeftHandVisualName);
        GameObject androidXRRightHandVisual = FindHandVisualObject(rightHand.transform, androidXRRightHandVisualPath, AndroidXRRightHandVisualName);

        Component[] components = handVisualizer.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
                continue;

            if (metaQuestLeftHandVisual != null)
                TrySetObjectField(component, "m_MetaQuestLeftHandMesh", metaQuestLeftHandVisual);

            if (metaQuestRightHandVisual != null)
                TrySetObjectField(component, "m_MetaQuestRightHandMesh", metaQuestRightHandVisual);

            if (androidXRLeftHandVisual != null)
                TrySetObjectField(component, "m_AndroidXRLeftHandMesh", androidXRLeftHandVisual);

            if (androidXRRightHandVisual != null)
                TrySetObjectField(component, "m_AndroidXRRightHandMesh", androidXRRightHandVisual);
        }

        LogHands(
            $"VisualizerMeshes questLeft={(metaQuestLeftHandVisual != null)} questRight={(metaQuestRightHandVisual != null)} androidLeft={(androidXRLeftHandVisual != null)} androidRight={(androidXRRightHandVisual != null)} leftPaths=({metaQuestLeftHandVisualPath ?? "<null>"}|{androidXRLeftHandVisualPath ?? "<null>"}) rightPaths=({metaQuestRightHandVisualPath ?? "<null>"}|{androidXRRightHandVisualPath ?? "<null>"})");
    }

    private void ConfigureUiInputModules()
    {
        if (!disableJoystickUiFallback && !disableGamepadUiFallback && !disableBuiltInUiFallback)
            return;

        XRUIInputModule[] inputModules = Resources.FindObjectsOfTypeAll<XRUIInputModule>();
        foreach (XRUIInputModule inputModule in inputModules)
        {
            if (inputModule == null)
                continue;

            if (!inputModule.gameObject.scene.IsValid() || !inputModule.gameObject.scene.isLoaded)
                continue;

            if (!HasExplicitUiActions(inputModule))
                continue;

            if (disableJoystickUiFallback)
                TrySetBoolField(inputModule, "m_EnableJoystickInput", false);

            if (disableGamepadUiFallback)
                TrySetBoolField(inputModule, "m_EnableGamepadInput", false);

            if (disableBuiltInUiFallback)
                TrySetBoolField(inputModule, "m_EnableBuiltinActionsAsFallback", false);
        }
    }

    private void ConfigureControllerRayOrigins()
    {
        ControllerInputActionManager[] controllerManagers = GetComponentsInChildren<ControllerInputActionManager>(true);
        foreach (ControllerInputActionManager controllerManager in controllerManagers)
        {
            ConfigureControllerRayOrigin(controllerManager);
        }
    }

    private void ConfigureControllerRayOrigin(ControllerInputActionManager controllerManager)
    {
        if (controllerManager == null)
            return;

        Transform controllerRoot = controllerManager.transform;
        Transform rigRoot = controllerRoot.parent;
        if (rigRoot == null)
            return;

        bool isLeftController = controllerRoot.name == LeftControllerName;
        string stabilizedOriginName = isLeftController ? LeftControllerStabilizedOriginName : RightControllerStabilizedOriginName;
        string stabilizedAttachName = isLeftController ? LeftControllerStabilizedAttachName : RightControllerStabilizedAttachName;

        Transform stabilizedOrigin = FindChildTransform(rigRoot, stabilizedOriginName);
        Transform stabilizedAttach = FindChildTransform(rigRoot, stabilizedAttachName);
        if (stabilizedOrigin == null || stabilizedAttach == null)
        {
            LogHandsWarning(
                $"Controller stabilized transforms missing controller={controllerRoot.name} originFound={(stabilizedOrigin != null)} attachFound={(stabilizedAttach != null)}");
            return;
        }

        XRTransformStabilizer stabilizer = stabilizedOrigin.GetComponent<XRTransformStabilizer>();
        if (disableLocomotionAndTeleportOnControllers && stabilizer != null && stabilizer.aimTarget != null)
        {
            stabilizer.aimTarget = null;
            LogHands($"Cleared teleport aim target from controller stabilizer controller={controllerRoot.name}");
        }

        Transform desiredOrigin = stabilizedOrigin;
        Transform desiredAttach = stabilizedAttach;
        string originMode = "stabilized";

        if (disableLocomotionAndTeleportOnControllers && useDirectControllerRayOriginWhenTeleportDisabled)
        {
            desiredOrigin = controllerRoot;
            desiredAttach = EnsureControllerUiAttach(controllerRoot, isLeftController);
            originMode = "direct-controller";
        }

        if (stabilizer != null)
            stabilizer.enabled = originMode == "stabilized";

        MonoBehaviour[] behaviours = controllerManager.GetComponentsInChildren<MonoBehaviour>(true);
        int configuredProviders = 0;
        foreach (MonoBehaviour behaviour in behaviours)
        {
            bool changed = false;

            if (behaviour is IXRRayProvider rayProvider)
            {
                if (rayProvider.GetOrCreateRayOrigin() != desiredOrigin)
                {
                    rayProvider.SetRayOrigin(desiredOrigin);
                    changed = true;
                }

                if (rayProvider.GetOrCreateAttachTransform() != desiredAttach)
                {
                    rayProvider.SetAttachTransform(desiredAttach);
                    changed = true;
                }
            }

            changed |= TrySetTransformField(behaviour, "m_RayOriginTransform", desiredOrigin);
            changed |= TrySetTransformField(behaviour, "m_AttachTransform", desiredAttach);
            changed |= TrySetTransformField(behaviour, "m_LineOriginTransform", desiredOrigin);
            changed |= TrySetTransformField(behaviour, "m_CastOrigin", desiredOrigin);
            changed |= TrySetTransformField(behaviour, "m_TransformToFollow", desiredOrigin);

            if (originMode != "stabilized")
            {
                changed |= TrySetBoolField(behaviour, "m_EnableStabilization", false);
                changed |= TrySetObjectField(behaviour, "m_AimTargetObject", null);
                changed |= TrySetBoolField(behaviour, "m_DisableVisualsWhenBlockedInGroup", false);
                changed |= TrySetBoolField(behaviour, "m_ExtendLineToEmptyHit", true);
            }

            if (changed)
                configuredProviders++;
        }

        int adjustedLineRenderers = EnsureVisibleControllerRayLineWidths(controllerManager);

        if (configuredProviders > 0 || enableControllerRayDiagnostics)
        {
            LogHands(
                $"Configured controller ray origin controller={controllerRoot.name} providers={configuredProviders} adjustedLineRenderers={adjustedLineRenderers} mode={originMode} origin={desiredOrigin.name} attach={desiredAttach.name} stabilizerEnabled={(stabilizer != null && stabilizer.enabled)}");

            LogControllerRayDiagnostics(controllerManager, desiredOrigin, desiredAttach, stabilizer, originMode, configuredProviders > 0);
        }
    }

    private int EnsureVisibleControllerRayLineWidths(ControllerInputActionManager controllerManager)
    {
        if (controllerManager == null)
            return 0;

        int adjustedCount = 0;
        LineRenderer[] lineRenderers = controllerManager.GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            if (lineRenderer == null)
                continue;

            if (!lineRenderer.gameObject.activeSelf)
            {
                lineRenderer.gameObject.SetActive(true);
                adjustedCount++;
            }

            if (!lineRenderer.enabled)
            {
                lineRenderer.enabled = true;
                adjustedCount++;
            }

            AnimationCurve widthCurve = lineRenderer.widthCurve;
            if (widthCurve == null || widthCurve.length == 0)
                continue;

            Keyframe firstKey = widthCurve[0];
            if (firstKey.value >= MinimumVisibleControllerRayWidth)
                continue;

            firstKey.value = MinimumVisibleControllerRayWidth;
            firstKey.inTangent = 0f;
            firstKey.outTangent = 0f;
            widthCurve.MoveKey(0, firstKey);
            lineRenderer.widthCurve = widthCurve;
            adjustedCount++;
        }

        return adjustedCount;
    }

    private Transform EnsureControllerUiAttach(Transform originRoot, bool isLeftController)
    {
        if (originRoot == null)
            return null;

        string attachName = isLeftController ? LeftControllerUiAttachName : RightControllerUiAttachName;
        Transform existingAttach = originRoot.Find(attachName);
        if (existingAttach != null)
            return existingAttach;

        GameObject attachObject = new GameObject(attachName);
        Transform attachTransform = attachObject.transform;
        attachTransform.SetParent(originRoot, false);
        attachTransform.localPosition = Vector3.zero;
        attachTransform.localRotation = Quaternion.identity;
        attachTransform.localScale = Vector3.one;
        return attachTransform;
    }

    private void LogControllerRayDiagnostics(string reason, bool force)
    {
        ControllerInputActionManager[] controllerManagers = GetComponentsInChildren<ControllerInputActionManager>(true);
        foreach (ControllerInputActionManager controllerManager in controllerManagers)
        {
            if (controllerManager == null)
                continue;

            Transform controllerRoot = controllerManager.transform;
            bool isLeftController = controllerRoot.name == LeftControllerName;
            Transform rigRoot = controllerRoot.parent;
            if (rigRoot == null)
                continue;

            string stabilizedOriginName = isLeftController ? LeftControllerStabilizedOriginName : RightControllerStabilizedOriginName;
            string stabilizedAttachName = isLeftController ? LeftControllerStabilizedAttachName : RightControllerStabilizedAttachName;
            Transform stabilizedOrigin = FindChildTransform(rigRoot, stabilizedOriginName);
            Transform stabilizedAttach = FindChildTransform(rigRoot, stabilizedAttachName);
            XRTransformStabilizer stabilizer = stabilizedOrigin != null ? stabilizedOrigin.GetComponent<XRTransformStabilizer>() : null;
            string originMode = disableLocomotionAndTeleportOnControllers && useDirectControllerRayOriginWhenTeleportDisabled
                ? "direct-controller"
                : "stabilized";
            Transform desiredOrigin = originMode == "stabilized" ? stabilizedOrigin : controllerRoot;
            Transform desiredAttach = originMode == "stabilized"
                ? stabilizedAttach
                : EnsureControllerUiAttach(controllerRoot, isLeftController);

            LogControllerRayDiagnostics(controllerManager, desiredOrigin, desiredAttach, stabilizer, $"{originMode}:{reason}", force);
        }
    }

    private void LogControllerRayDiagnostics(ControllerInputActionManager controllerManager, Transform desiredOrigin, Transform desiredAttach, XRTransformStabilizer stabilizer, string reason, bool force)
    {
        if (!enableControllerRayDiagnostics || controllerManager == null)
            return;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(1024);
        Transform controllerRoot = controllerManager.transform;
        builder.Append("ControllerRayDiag reason=").Append(reason);
        builder.Append(" controller=").Append(controllerRoot.name);
        builder.Append(" controllerActive=").Append(controllerRoot.gameObject.activeInHierarchy);
        builder.Append(" desiredOrigin=").Append(DescribeTransform(desiredOrigin));
        builder.Append(" desiredAttach=").Append(DescribeTransform(desiredAttach));
        builder.Append(" controllerRootPose=").Append(DescribeTransform(controllerRoot));

        string controllerState = controllerRoot.name == LeftControllerName
            ? DescribeTrackingStatus(lastLeftControllerStatus)
            : DescribeTrackingStatus(lastRightControllerStatus);
        builder.Append(" controllerTracking=").Append(controllerState);

        if (stabilizer != null)
        {
            builder.Append(" stabilizerEnabled=").Append(stabilizer.enabled);
            builder.Append(" stabilizerTarget=").Append(DescribeTransform(TryGetTransformPropertyValue(stabilizer, "target")));
            builder.Append(" stabilizerAimTarget=").Append(DescribeObject(TryGetObjectPropertyValue(stabilizer, "aimTarget")));
        }

        MonoBehaviour[] behaviours = controllerManager.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            bool hasRelevantField = false;
            System.Text.StringBuilder componentBuilder = new System.Text.StringBuilder();
            AppendTransformField(componentBuilder, behaviour, "m_RayOriginTransform", ref hasRelevantField);
            AppendTransformField(componentBuilder, behaviour, "m_AttachTransform", ref hasRelevantField);
            AppendTransformField(componentBuilder, behaviour, "m_LineOriginTransform", ref hasRelevantField);
            AppendTransformField(componentBuilder, behaviour, "m_CastOrigin", ref hasRelevantField);
            AppendTransformField(componentBuilder, behaviour, "m_TransformToFollow", ref hasRelevantField);
            AppendObjectField(componentBuilder, behaviour, "m_AimTargetObject", ref hasRelevantField);
            AppendObjectField(componentBuilder, behaviour, "m_Target", ref hasRelevantField);

            if (!hasRelevantField)
                continue;

            builder.Append(" component[").Append(behaviour.GetType().Name).Append("]{");
            builder.Append(componentBuilder);
            builder.Append('}');
        }

        string snapshot = builder.ToString();
        ref string lastSnapshot = ref GetControllerRaySnapshotStorage(controllerRoot.name);
        if (!force && lastSnapshot == snapshot)
            return;

        lastSnapshot = snapshot;
        LogHands(snapshot);
    }

    private static bool HasExplicitUiActions(XRUIInputModule inputModule)
    {
        return HasObjectFieldValue(inputModule, "m_PointAction") ||
            HasObjectFieldValue(inputModule, "m_LeftClickAction") ||
            HasObjectFieldValue(inputModule, "m_MiddleClickAction") ||
            HasObjectFieldValue(inputModule, "m_RightClickAction") ||
            HasObjectFieldValue(inputModule, "m_ScrollWheelAction") ||
            HasObjectFieldValue(inputModule, "m_NavigateAction") ||
            HasObjectFieldValue(inputModule, "m_SubmitAction") ||
            HasObjectFieldValue(inputModule, "m_CancelAction");
    }

    private static T FindLoadedObjectOfType<T>() where T : Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        foreach (T loadedObject in objects)
        {
            if (loadedObject == null)
                continue;

            GameObject owner = loadedObject switch
            {
                Component component => component.gameObject,
                GameObject gameObject => gameObject,
                _ => null,
            };

            if (owner == null)
                continue;

            if (!owner.scene.IsValid() || !owner.scene.isLoaded)
                continue;

            return loadedObject;
        }

        return null;
    }

    private static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return null;

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private static GameObject FindChildObject(Transform parent, string childName)
    {
        Transform child = FindChildTransform(parent, childName);
        return child != null ? child.gameObject : null;
    }

    private static void CloneChildToTarget(Transform source, Transform targetParent)
    {
        if (source == null || targetParent == null)
            return;

        GameObject clone = Instantiate(source.gameObject, targetParent, false);
        clone.name = source.name;
        bool shouldStartActive = source.gameObject.activeSelf && source.name != "Left Hand" && source.name != "Right Hand";
        clone.SetActive(shouldStartActive);
    }

    private void EnsureHandPointerDots(GameObject leftHand, GameObject rightHand)
    {
        EnsureHandPointerDot(leftHand, "left");
        EnsureHandPointerDot(rightHand, "right");
    }

    private void EnsureHandPointerDot(GameObject handRoot, string handednessLabel)
    {
        if (handRoot == null)
            return;

        Transform aimPose = FindChildTransform(handRoot.transform, AimPoseName);
        if (aimPose == null)
        {
            LogHandsWarning($"Aim pose not found for hand pointer dot handedness={handednessLabel}");
            return;
        }

        Transform existingDot = aimPose.Find(HandPointerDotName);
        if (existingDot != null)
            return;

        GameObject pointerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pointerDot.name = HandPointerDotName;
        pointerDot.layer = handRoot.layer;
        pointerDot.transform.SetParent(aimPose, false);
        pointerDot.transform.localPosition = Vector3.zero;
        pointerDot.transform.localRotation = Quaternion.identity;
        pointerDot.transform.localScale = Vector3.one * HandPointerDotScale;

        Collider pointerCollider = pointerDot.GetComponent<Collider>();
        if (pointerCollider != null)
            Destroy(pointerCollider);

        MeshRenderer pointerRenderer = pointerDot.GetComponent<MeshRenderer>();
        if (pointerRenderer != null)
        {
            pointerRenderer.sharedMaterial = GetOrCreateHandPointerDotMaterial();
            pointerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pointerRenderer.receiveShadows = false;
        }

        LogHands($"Created hand pointer dot handedness={handednessLabel}");
    }

    private static Material GetOrCreateHandPointerDotMaterial()
    {
        if (cachedHandPointerDotMaterial != null)
            return cachedHandPointerDotMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        cachedHandPointerDotMaterial = shader != null ? new Material(shader) : null;
        if (cachedHandPointerDotMaterial == null)
            return null;

        cachedHandPointerDotMaterial.name = "XR Hands Pointer Dot Material";
        Color pointerColor = new Color(1f, 0.55f, 0.08f, 1f);
        if (cachedHandPointerDotMaterial.HasProperty("_BaseColor"))
            cachedHandPointerDotMaterial.SetColor("_BaseColor", pointerColor);
        if (cachedHandPointerDotMaterial.HasProperty("_Color"))
            cachedHandPointerDotMaterial.SetColor("_Color", pointerColor);
        if (cachedHandPointerDotMaterial.HasProperty("_EmissionColor"))
            cachedHandPointerDotMaterial.SetColor("_EmissionColor", pointerColor * 1.5f);

        return cachedHandPointerDotMaterial;
    }

    private void CacheHandVisualizerReferencePaths(GameObject templateHandVisualizer, GameObject templateLeftHand, GameObject templateRightHand)
    {
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_MetaQuestLeftHandMesh", templateLeftHand, ref metaQuestLeftHandVisualPath);
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_MetaQuestRightHandMesh", templateRightHand, ref metaQuestRightHandVisualPath);
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_AndroidXRLeftHandMesh", templateLeftHand, ref androidXRLeftHandVisualPath);
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_AndroidXRRightHandMesh", templateRightHand, ref androidXRRightHandVisualPath);
    }

    private void CaptureHandVisualizerReferencePath(GameObject templateHandVisualizer, string fieldName, GameObject templateHandRoot, ref string cachedPath)
    {
        if (!string.IsNullOrEmpty(cachedPath) || templateHandVisualizer == null || templateHandRoot == null)
            return;

        Component[] components = templateHandVisualizer.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null || !TryGetObjectFieldValue(component, fieldName, out Object value))
                continue;

            Transform visualTransform = value switch
            {
                GameObject gameObject => gameObject.transform,
                Component sourceComponent => sourceComponent.transform,
                _ => null,
            };

            if (visualTransform == null || !visualTransform.IsChildOf(templateHandRoot.transform))
                continue;

            cachedPath = GetRelativeTransformPath(templateHandRoot.transform, visualTransform);
            return;
        }
    }

    private static GameObject FindHandVisualObject(Transform handRoot, string relativePath, string fallbackName)
    {
        if (handRoot == null)
            return null;

        if (!string.IsNullOrEmpty(relativePath))
        {
            Transform relativeTransform = FindRelativeTransform(handRoot, relativePath);
            if (relativeTransform != null)
                return relativeTransform.gameObject;
        }

        return FindChildObject(handRoot, fallbackName);
    }

    private static Transform FindRelativeTransform(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrEmpty(relativePath))
            return null;

        string[] pathParts = relativePath.Split('/');
        Transform current = root;
        for (int index = 0; index < pathParts.Length; index++)
        {
            current = current.Find(pathParts[index]);
            if (current == null)
                return null;
        }

        return current;
    }

    private static string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null)
            return null;

        List<string> pathParts = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        if (current != root)
            return null;

        pathParts.Reverse();
        return string.Join("/", pathParts);
    }

    private static XRInputModalityManager.InputMode GetInputModeField(XRInputModalityManager inputModalityManager, string fieldName)
    {
        for (System.Type type = inputModalityManager.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(XRInputModalityManager.InputMode))
                continue;

            return (XRInputModalityManager.InputMode)field.GetValue(inputModalityManager);
        }

        return XRInputModalityManager.InputMode.None;
    }

    private static bool TryInvokeNoArgumentMethod(object target, string methodName)
    {
        if (target == null)
            return false;

        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                continue;

            method.Invoke(target, null);
            return true;
        }

        return false;
    }

    private static bool DidTrackingStatusChange(TrackingStatus previousStatus, TrackingStatus currentStatus)
    {
        return previousStatus.isConnected != currentStatus.isConnected ||
            previousStatus.isTracked != currentStatus.isTracked ||
            previousStatus.trackingState != currentStatus.trackingState;
    }

    private void SyncTrackedInteractionVisibility(
        XRInputModalityManager inputModalityManager,
        XRInputModalityManager.InputMode leftMode,
        XRInputModalityManager.InputMode rightMode,
        TrackingStatus leftHandStatus,
        TrackingStatus rightHandStatus,
        TrackingStatus leftControllerStatus,
        TrackingStatus rightControllerStatus)
    {
        if (inputModalityManager == null)
            return;

        bool leftShouldShowHand = leftHandStatus.isTracked && (IsHandInputMode(leftMode) || !leftControllerStatus.isTracked);
        bool rightShouldShowHand = rightHandStatus.isTracked && (IsHandInputMode(rightMode) || !rightControllerStatus.isTracked);
        bool leftShouldShowController = leftControllerStatus.isTracked && !leftShouldShowHand;
        bool rightShouldShowController = rightControllerStatus.isTracked && !rightShouldShowHand;

        SetActiveIfNeeded(inputModalityManager.leftHand, leftShouldShowHand);
        SetActiveIfNeeded(inputModalityManager.rightHand, rightShouldShowHand);
        SetActiveIfNeeded(inputModalityManager.leftController, leftShouldShowController);
        SetActiveIfNeeded(inputModalityManager.rightController, rightShouldShowController);
    }

    private static bool IsHandInputMode(XRInputModalityManager.InputMode inputMode)
    {
        return inputMode.ToString().IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SetActiveIfNeeded(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static bool TrySetBoolField(object target, string fieldName, bool value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(target, value);
                return true;
            }
        }

        return false;
    }

    private static bool TrySetTransformField(object target, string fieldName, Transform value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(Transform))
                continue;

            if (ReferenceEquals(field.GetValue(target), value))
                return false;

            field.SetValue(target, value);
            return true;
        }

        return false;
    }

    private static bool TryGetTransformFieldValue(object target, string fieldName, out Transform value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(Transform))
                continue;

            value = field.GetValue(target) as Transform;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetObjectFieldValue(object target, string fieldName, out Object value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !typeof(Object).IsAssignableFrom(field.FieldType))
                continue;

            value = field.GetValue(target) as Object;
            return value != null;
        }

        value = null;
        return false;
    }

    private static bool HasObjectFieldValue(object target, string fieldName)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !typeof(Object).IsAssignableFrom(field.FieldType))
                continue;

            if (field.GetValue(target) is Object objectValue && objectValue != null)
                return true;
        }

        return false;
    }

    private static Transform TryGetTransformPropertyValue(object target, string propertyName)
    {
        if (target == null)
            return null;

        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.PropertyType != typeof(Transform) || !property.CanRead)
                continue;

            return property.GetValue(target) as Transform;
        }

        return null;
    }

    private static Object TryGetObjectPropertyValue(object target, string propertyName)
    {
        if (target == null)
            return null;

        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !typeof(Object).IsAssignableFrom(property.PropertyType) || !property.CanRead)
                continue;

            return property.GetValue(target) as Object;
        }

        return null;
    }

    private ref string GetControllerRaySnapshotStorage(string controllerName)
    {
        if (controllerName == LeftControllerName)
            return ref lastLeftControllerRaySnapshot;

        return ref lastRightControllerRaySnapshot;
    }

    private static void AppendTransformField(System.Text.StringBuilder builder, object target, string fieldName, ref bool hasRelevantField)
    {
        if (!TryGetTransformFieldValue(target, fieldName, out Transform value))
            return;

        hasRelevantField = true;
        if (builder.Length > 0)
            builder.Append(' ');

        builder.Append(fieldName).Append('=').Append(DescribeTransform(value));
    }

    private static void AppendObjectField(System.Text.StringBuilder builder, object target, string fieldName, ref bool hasRelevantField)
    {
        if (!TryGetObjectFieldValue(target, fieldName, out Object value))
            return;

        hasRelevantField = true;
        if (builder.Length > 0)
            builder.Append(' ');

        builder.Append(fieldName).Append('=').Append(DescribeObject(value));
    }

    private static string DescribeTrackingStatus(TrackingStatus status)
    {
        return $"tracked={status.isTracked},connected={status.isConnected},state={status.trackingState}";
    }

    private static string DescribeTransform(Transform transform)
    {
        if (transform == null)
            return "<null>";

        Vector3 position = transform.position;
        Vector3 rotation = transform.rotation.eulerAngles;
        return $"{GetHierarchyPath(transform)}@p({position.x:F3},{position.y:F3},{position.z:F3}) r({rotation.x:F1},{rotation.y:F1},{rotation.z:F1})";
    }

    private static string DescribeObject(Object value)
    {
        if (value == null)
            return "<null>";

        return value switch
        {
            Component component => GetHierarchyPath(component.transform),
            GameObject gameObject => GetHierarchyPath(gameObject.transform),
            _ => value.name,
        };
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        List<string> pathParts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        pathParts.Reverse();
        return string.Join("/", pathParts);
    }

    private static string DescribeHandSubsystems()
    {
        CachedHandSubsystems.Clear();
        SubsystemManager.GetSubsystems(CachedHandSubsystems);

        int runningCount = 0;
        for (int index = 0; index < CachedHandSubsystems.Count; index++)
        {
            if (CachedHandSubsystems[index] != null && CachedHandSubsystems[index].running)
                runningCount++;
        }

        return $"handSubsystems={CachedHandSubsystems.Count} running={runningCount}";
    }

    private void LogHands(string message)
    {
        string formattedMessage = $"[{DiagnosticTag}] {message}";
        Debug.Log(formattedMessage, this);

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass logClass = new AndroidJavaClass("android.util.Log"))
            {
                logClass.CallStatic<int>("i", DiagnosticTag, message);
            }
        }
        catch
        {
        }
#endif
    }

    private void LogHandsWarning(string message)
    {
        string formattedMessage = $"[{DiagnosticTag}] {message}";
        Debug.LogWarning(formattedMessage, this);

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass logClass = new AndroidJavaClass("android.util.Log"))
            {
                logClass.CallStatic<int>("w", DiagnosticTag, message);
            }
        }
        catch
        {
        }
#endif
    }

    private static bool TrySetObjectField(object target, string fieldName, Object value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !typeof(Object).IsAssignableFrom(field.FieldType))
                continue;

            if (value != null && !field.FieldType.IsAssignableFrom(value.GetType()))
                continue;

            if (ReferenceEquals(field.GetValue(target), value))
                return false;

            field.SetValue(target, value);
            return true;
        }

        return false;
    }
}