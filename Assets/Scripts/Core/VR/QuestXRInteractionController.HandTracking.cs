using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using Unity.VRTemplate;

/// <summary>
/// Instalación y gestión del rig de manos: permisos de hand tracking, clonado del
/// template oficial cuando la escena no lo trae, y enlace de las mallas al visualizador.
/// </summary>
public partial class QuestXRInteractionController
{
    private const string AndroidHandTrackingPermission = "android.permission.HAND_TRACKING";
    private const string OculusHandTrackingPermission = "com.oculus.permission.HAND_TRACKING";
    private const string HorizonOsHandTrackingPermission = "horizonos.permission.HAND_TRACKING";
    private const string MetaQuestLeftHandVisualName = "Left Hand Quest Visual";
    private const string MetaQuestRightHandVisualName = "Right Hand Quest Visual";
    private const string AndroidXRLeftHandVisualName = "Left Hand Android XR Visual";
    private const string AndroidXRRightHandVisualName = "Right Hand Android XR Visual";
    private const string AimPoseName = "Aim Pose";
    private const string HandPointerDotName = "Hand Tracking Pointer Dot";
    private const float HandPointerDotScale = 0.0125f;

    private static bool isInstallingHandsTemplate;
    private static bool isHandTrackingPermissionRequestPending;
    private static Material cachedHandPointerDotMaterial;

    private HandSubsystemManager cachedHandSubsystemManager;
    private string metaQuestLeftHandVisualPath;
    private string metaQuestRightHandVisualPath;
    private string androidXRLeftHandVisualPath;
    private string androidXRRightHandVisualPath;

    private void EnsureHandTrackingSupport()
    {
        if (!IsHandTrackingEnabledForActiveScene())
        {
            DisableHandTrackingSceneObjects();
            LogHands($"Hand tracking support disabled on QuestXRInteractionController scene={SceneManager.GetActiveScene().name}");
            return;
        }

        if (IsActiveScene(PianoGameSceneName))
            TryEnableSimultaneousHandsAndControllers();

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

        LogHands($"HandsBeforeInstall templateAssigned={(handsRigTemplate != null)} permissionsPrefabAssigned={(handsPermissionsManagerPrefab != null)} " +
                 $"leftHand={(existingLeftHand != null)} rightHand={(existingRightHand != null)} handVisualizer={(existingHandVisualizer != null)}");

        bool isMissingAnyHandObject = existingLeftHand == null || existingRightHand == null || existingHandVisualizer == null;
        if (isMissingAnyHandObject && handsRigTemplate != null)
        {
            InstallHandsFromTemplate(handsRigTemplate, cameraOffset,
                existingHandVisualizer == null, existingLeftHand == null, existingRightHand == null);

            existingHandVisualizer = FindChildObject(cameraOffset, handVisualizerName);
            existingLeftHand = FindChildObject(cameraOffset, leftHandName);
            existingRightHand = FindChildObject(cameraOffset, rightHandName);
        }

        LogHands($"HandsAfterInstall leftHand={(existingLeftHand != null)} rightHand={(existingRightHand != null)} handVisualizer={(existingHandVisualizer != null)}");

        AssignHandsToModalityManager(existingLeftHand, existingRightHand);
        AssignHandVisualizerMeshes(existingHandVisualizer, existingLeftHand, existingRightHand);
        EnsureHandVisualizerDrawMeshes(existingHandVisualizer);
        ForceActivatePianoHandVisuals(existingLeftHand, existingRightHand, existingHandVisualizer);
        SyncHandPointerDots(existingLeftHand, existingRightHand);
        RefreshTrackedInteractionState(forceLog: true);
    }

    /// <summary>
    /// En PianoGame las raíces de mano y el visualizador deben estar activos siempre.
    /// Si arrancan desactivados, el HandVisualizer no puede dibujar aunque el tracking exista.
    /// </summary>
    private void ForceActivatePianoHandVisuals(GameObject leftHand, GameObject rightHand, GameObject handVisualizer)
    {
        if (!ShouldPreferHandsInPianoGame()) return;

        SetActiveIfNeeded(leftHand, true);
        SetActiveIfNeeded(rightHand, true);
        SetActiveIfNeeded(handVisualizer, true);

        // También fuerza activos los meshes Meta Quest dentro de cada mano.
        ForceActivateNamedChild(leftHand, MetaQuestLeftHandVisualName);
        ForceActivateNamedChild(rightHand, MetaQuestRightHandVisualName);
        ForceActivateNamedChild(leftHand, AndroidXRLeftHandVisualName);
        ForceActivateNamedChild(rightHand, AndroidXRRightHandVisualName);

        LogHands($"ForceActivatePianoHandVisuals left={(leftHand != null && leftHand.activeSelf)} right={(rightHand != null && rightHand.activeSelf)} visualizer={(handVisualizer != null && handVisualizer.activeSelf)}");
    }

    private static void ForceActivateNamedChild(GameObject handRoot, string childName)
    {
        if (handRoot == null || string.IsNullOrEmpty(childName)) return;

        GameObject child = FindChildObject(handRoot.transform, childName);
        SetActiveIfNeeded(child, true);
    }

    private void EnsureHandVisualizerDrawMeshes(GameObject handVisualizer)
    {
        if (handVisualizer == null) return;

        foreach (Component component in handVisualizer.GetComponents<Component>())
        {
            if (component == null) continue;

            // HandVisualizer.drawMeshes (paquete XR Hands) — se setea por reflexión por si el tipo cambia de nombre.
            System.Type type = component.GetType();
            if (type.Name.IndexOf("HandVisualizer", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            System.Reflection.PropertyInfo drawMeshesProperty = type.GetProperty("drawMeshes");
            if (drawMeshesProperty != null && drawMeshesProperty.CanWrite && drawMeshesProperty.PropertyType == typeof(bool))
            {
                drawMeshesProperty.SetValue(component, true);
                LogHands($"Forced {type.Name}.drawMeshes=true");
            }

            TrySetBoolField(component, "m_DrawMeshes", true);
        }
    }

    private void DisableHandTrackingSceneObjects()
    {
        HandSubsystemManager handSubsystemManager = EnsureHandSubsystemManager();
        if (handSubsystemManager != null)
        {
            handSubsystemManager.DisableHandTracking();
            LogHands($"Stopped hand tracking subsystem for scene={SceneManager.GetActiveScene().name} {DescribeHandSubsystems()}");
        }

        Transform cameraOffset = transform.Find(cameraOffsetName);
        if (cameraOffset == null) return;

        GameObject leftHand = FindChildObject(cameraOffset, leftHandName);
        GameObject rightHand = FindChildObject(cameraOffset, rightHandName);

        RemoveHandPointerDot(leftHand);
        RemoveHandPointerDot(rightHand);

        SetActiveIfNeeded(leftHand, false);
        SetActiveIfNeeded(rightHand, false);
        SetActiveIfNeeded(FindChildObject(cameraOffset, handVisualizerName), false);

        XRInputModalityManager inputModalityManager = ResolveInputModalityManager();
        if (inputModalityManager == null) return;

        inputModalityManager.leftHand = null;
        inputModalityManager.rightHand = null;
    }

    /// <summary>
    /// En PianoGame los mandos suelen seguir trackeados (sobre la mesa). Sin modo
    /// simultáneo, Meta apaga el tracking de manos y las mallas nunca aparecen.
    /// Hay que llamar a OVRInput.EnableSimultaneousHandsAndControllers(), no solo
    /// marcar el checkbox de OVRManager.
    /// </summary>
    private void TryEnableSimultaneousHandsAndControllers()
    {
        try
        {
            if (cachedOvrManager == null) cachedOvrManager = FindObjectOfType<OVRManager>(true);
            if (cachedOvrManager != null)
            {
                cachedOvrManager.SimultaneousHandsAndControllersEnabled = true;
                cachedOvrManager.launchSimultaneousHandsControllersOnStartup = true;
            }

            bool enabled = OVRInput.EnableSimultaneousHandsAndControllers();
            LogHands($"OVRInput.EnableSimultaneousHandsAndControllers result={enabled}");
        }
        catch (System.Exception e)
        {
            LogHandsWarning($"Could not enable SimultaneousHandsAndControllers: {e.Message}");
        }
    }

    private void MaintainPianoHandTracking()
    {
        nextPianoHandsMaintainTime = Time.unscaledTime + 2.5f;
        TryEnableSimultaneousHandsAndControllers();
        EnsureHandSubsystemManager()?.EnableHandTracking();

        Transform cameraOffset = transform.Find(cameraOffsetName);
        if (cameraOffset == null) return;

        ForceActivatePianoHandVisuals(
            FindChildObject(cameraOffset, leftHandName),
            FindChildObject(cameraOffset, rightHandName),
            FindChildObject(cameraOffset, handVisualizerName));
    }

    private HandSubsystemManager EnsureHandSubsystemManager()
    {
        if (cachedHandSubsystemManager != null) return cachedHandSubsystemManager;

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
        if (handSubsystemManager == null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Cada versión de Horizon OS ha usado un nombre de permiso distinto; basta con uno.
        bool hasAnyPermission = true;
        try
        {
            hasAnyPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(AndroidHandTrackingPermission)
                               || UnityEngine.Android.Permission.HasUserAuthorizedPermission(OculusHandTrackingPermission)
                               || UnityEngine.Android.Permission.HasUserAuthorizedPermission(HorizonOsHandTrackingPermission);
        }
        catch
        {
        }

        LogHands($"PermissionCheck granted={hasAnyPermission} requestPending={isHandTrackingPermissionRequestPending}");

        if (hasAnyPermission)
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

    /// <summary>
    /// Clona del template oficial solo las piezas que falten. El template se instancia
    /// desactivado y se destruye siempre, incluso si el clonado falla a medias.
    /// </summary>
    private void InstallHandsFromTemplate(GameObject handsTemplatePrefab, Transform cameraOffset,
        bool needsHandVisualizer, bool needsLeftHand, bool needsRightHand)
    {
        if (!needsHandVisualizer && !needsLeftHand && !needsRightHand) return;

        if (handsTemplatePrefab == null)
        {
            LogHandsWarning("Hands template prefab is null; cannot install hand rig");
            return;
        }

        LogHands($"InstallHandsFromTemplate template={handsTemplatePrefab.name} needsVisualizer={needsHandVisualizer} needsLeft={needsLeftHand} needsRight={needsRightHand}");

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

            CacheHandVisualizerReferencePaths(
                FindChildObject(templateCameraOffset, handVisualizerName),
                FindChildObject(templateCameraOffset, leftHandName),
                FindChildObject(templateCameraOffset, rightHandName));

            if (needsHandVisualizer) CloneChildToTarget(FindChildTransform(templateCameraOffset, handVisualizerName), cameraOffset);
            if (needsLeftHand) CloneChildToTarget(FindChildTransform(templateCameraOffset, leftHandName), cameraOffset);
            if (needsRightHand) CloneChildToTarget(FindChildTransform(templateCameraOffset, rightHandName), cameraOffset);

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

        XRInputModalityManager inputModalityManager = ResolveInputModalityManager();
        if (inputModalityManager == null)
        {
            LogHandsWarning("XRInputModalityManager not found on XR rig root");
            return;
        }

        inputModalityManager.leftHand = leftHand;
        inputModalityManager.rightHand = rightHand;
        LogHands($"Assigned hands to XRInputModalityManager leftActiveSelf={leftHand.activeSelf} rightActiveSelf={rightHand.activeSelf} currentInputMode={XRInputModalityManager.currentInputMode.Value}");
    }

    /// <summary>
    /// El visualizador de manos referencia las mallas por campos privados, y el nombre del
    /// componente cambia entre versiones del paquete: se recorren todos sus componentes.
    /// </summary>
    private void AssignHandVisualizerMeshes(GameObject handVisualizer, GameObject leftHand, GameObject rightHand)
    {
        if (handVisualizer == null || leftHand == null || rightHand == null)
        {
            LogHandsWarning($"AssignHandVisualizerMeshes skipped handVisualizer={(handVisualizer != null)} left={(leftHand != null)} right={(rightHand != null)}");
            return;
        }

        GameObject metaQuestLeftHandVisual = FindHandVisualObject(leftHand.transform, metaQuestLeftHandVisualPath, MetaQuestLeftHandVisualName);
        GameObject metaQuestRightHandVisual = FindHandVisualObject(rightHand.transform, metaQuestRightHandVisualPath, MetaQuestRightHandVisualName);
        GameObject androidXRLeftHandVisual = FindHandVisualObject(leftHand.transform, androidXRLeftHandVisualPath, AndroidXRLeftHandVisualName);
        GameObject androidXRRightHandVisual = FindHandVisualObject(rightHand.transform, androidXRRightHandVisualPath, AndroidXRRightHandVisualName);

        foreach (Component component in handVisualizer.GetComponents<Component>())
        {
            if (component == null) continue;

            if (metaQuestLeftHandVisual != null) TrySetObjectField(component, "m_MetaQuestLeftHandMesh", metaQuestLeftHandVisual);
            if (metaQuestRightHandVisual != null) TrySetObjectField(component, "m_MetaQuestRightHandMesh", metaQuestRightHandVisual);
            if (androidXRLeftHandVisual != null) TrySetObjectField(component, "m_AndroidXRLeftHandMesh", androidXRLeftHandVisual);
            if (androidXRRightHandVisual != null) TrySetObjectField(component, "m_AndroidXRRightHandMesh", androidXRRightHandVisual);
        }

        LogHands($"VisualizerMeshes questLeft={(metaQuestLeftHandVisual != null)} questRight={(metaQuestRightHandVisual != null)} androidLeft={(androidXRLeftHandVisual != null)} androidRight={(androidXRRightHandVisual != null)} leftPaths=({metaQuestLeftHandVisualPath ?? "<null>"}|{androidXRLeftHandVisualPath ?? "<null>"}) rightPaths=({metaQuestRightHandVisualPath ?? "<null>"}|{androidXRRightHandVisualPath ?? "<null>"})");
    }

    private static void CloneChildToTarget(Transform source, Transform targetParent)
    {
        if (source == null || targetParent == null) return;

        GameObject clone = Instantiate(source.gameObject, targetParent, false);
        clone.name = source.name;

        // Las manos arrancan ocultas: el XRInputModalityManager decide cuándo mostrarlas.
        bool shouldStartActive = source.gameObject.activeSelf && source.name != "Left Hand" && source.name != "Right Hand";
        clone.SetActive(shouldStartActive);
    }

    private void SyncHandPointerDots(GameObject leftHand, GameObject rightHand)
    {
        if (!showHandPointerDots)
        {
            RemoveHandPointerDot(leftHand);
            RemoveHandPointerDot(rightHand);
            return;
        }

        EnsureHandPointerDot(leftHand, "left");
        EnsureHandPointerDot(rightHand, "right");
    }

    private void RemoveHandPointerDot(GameObject handRoot)
    {
        if (handRoot == null) return;

        Transform aimPose = FindChildTransform(handRoot.transform, AimPoseName);
        Transform existingDot = aimPose != null ? aimPose.Find(HandPointerDotName) : null;

        if (existingDot != null) Destroy(existingDot.gameObject);
    }

    private void EnsureHandPointerDot(GameObject handRoot, string handednessLabel)
    {
        if (handRoot == null) return;

        Transform aimPose = FindChildTransform(handRoot.transform, AimPoseName);
        if (aimPose == null)
        {
            LogHandsWarning($"Aim pose not found for hand pointer dot handedness={handednessLabel}");
            return;
        }

        if (aimPose.Find(HandPointerDotName) != null) return;

        GameObject pointerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pointerDot.name = HandPointerDotName;
        pointerDot.layer = handRoot.layer;
        pointerDot.transform.SetParent(aimPose, false);
        pointerDot.transform.localPosition = Vector3.zero;
        pointerDot.transform.localRotation = Quaternion.identity;
        pointerDot.transform.localScale = Vector3.one * HandPointerDotScale;

        Collider pointerCollider = pointerDot.GetComponent<Collider>();
        if (pointerCollider != null) Destroy(pointerCollider);

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
        if (cachedHandPointerDotMaterial != null) return cachedHandPointerDotMaterial;

        // Se prueban en orden de preferencia porque depende del render pipeline del proyecto.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color")
                        ?? Shader.Find("Sprites/Default")
                        ?? Shader.Find("Standard");

        if (shader == null) return null;

        cachedHandPointerDotMaterial = new Material(shader) { name = "XR Hands Pointer Dot Material" };

        Color pointerColor = new Color(1f, 0.55f, 0.08f, 1f);
        if (cachedHandPointerDotMaterial.HasProperty("_BaseColor")) cachedHandPointerDotMaterial.SetColor("_BaseColor", pointerColor);
        if (cachedHandPointerDotMaterial.HasProperty("_Color")) cachedHandPointerDotMaterial.SetColor("_Color", pointerColor);
        if (cachedHandPointerDotMaterial.HasProperty("_EmissionColor")) cachedHandPointerDotMaterial.SetColor("_EmissionColor", pointerColor * 1.5f);

        return cachedHandPointerDotMaterial;
    }

    /// <summary>
    /// Memoriza dónde estaba cada malla dentro del template, para poder reencontrarla
    /// en el rig instalado aunque el nombre del objeto no coincida.
    /// </summary>
    private void CacheHandVisualizerReferencePaths(GameObject templateHandVisualizer, GameObject templateLeftHand, GameObject templateRightHand)
    {
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_MetaQuestLeftHandMesh", templateLeftHand, ref metaQuestLeftHandVisualPath);
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_MetaQuestRightHandMesh", templateRightHand, ref metaQuestRightHandVisualPath);
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_AndroidXRLeftHandMesh", templateLeftHand, ref androidXRLeftHandVisualPath);
        CaptureHandVisualizerReferencePath(templateHandVisualizer, "m_AndroidXRRightHandMesh", templateRightHand, ref androidXRRightHandVisualPath);
    }

    private void CaptureHandVisualizerReferencePath(GameObject templateHandVisualizer, string fieldName, GameObject templateHandRoot, ref string cachedPath)
    {
        if (!string.IsNullOrEmpty(cachedPath) || templateHandVisualizer == null || templateHandRoot == null) return;

        foreach (Component component in templateHandVisualizer.GetComponents<Component>())
        {
            if (component == null || !TryGetObjectFieldValue(component, fieldName, out Object value)) continue;

            Transform visualTransform = value switch
            {
                GameObject gameObject => gameObject.transform,
                Component sourceComponent => sourceComponent.transform,
                _ => null,
            };

            if (visualTransform == null || !visualTransform.IsChildOf(templateHandRoot.transform)) continue;

            cachedPath = GetRelativeTransformPath(templateHandRoot.transform, visualTransform);
            return;
        }
    }

    private static GameObject FindHandVisualObject(Transform handRoot, string relativePath, string fallbackName)
    {
        if (handRoot == null) return null;

        Transform relativeTransform = FindRelativeTransform(handRoot, relativePath);
        return relativeTransform != null ? relativeTransform.gameObject : FindChildObject(handRoot, fallbackName);
    }

    private static Transform FindRelativeTransform(Transform root, string relativePath)
    {
        if (root == null || string.IsNullOrEmpty(relativePath)) return null;

        Transform current = root;
        foreach (string pathPart in relativePath.Split('/'))
        {
            current = current.Find(pathPart);
            if (current == null) return null;
        }

        return current;
    }

    private static string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null) return null;

        List<string> pathParts = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        if (current != root) return null;

        pathParts.Reverse();
        return string.Join("/", pathParts);
    }
}
