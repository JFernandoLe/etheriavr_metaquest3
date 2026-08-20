using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Configura el origen del rayo de los mandos y desactiva los fallbacks de UI
/// (joystick, gamepad, acciones integradas) para dejar solo el puntero VR.
/// </summary>
public partial class QuestXRInteractionController
{
    private static readonly string[] RayTransformFieldNames =
    {
        "m_RayOriginTransform", "m_AttachTransform", "m_LineOriginTransform", "m_CastOrigin", "m_TransformToFollow"
    };

    private static readonly string[] ExplicitUiActionFieldNames =
    {
        "m_PointAction", "m_LeftClickAction", "m_MiddleClickAction", "m_RightClickAction",
        "m_ScrollWheelAction", "m_NavigateAction", "m_SubmitAction", "m_CancelAction"
    };

    /// <summary>
    /// Sin teleport, el rayo sale directamente del mando en lugar del transform estabilizado:
    /// el estabilizador está pensado para apuntar teleports y añade retardo en la UI.
    /// </summary>
    private bool UseDirectControllerRayOrigin =>
        disableLocomotionAndTeleportOnControllers && useDirectControllerRayOriginWhenTeleportDisabled;

    private static string DescribeOriginMode(bool useDirectOrigin) => useDirectOrigin ? "direct-controller" : "stabilized";

    private void ConfigureControllerRayOrigins()
    {
        foreach (ControllerInputActionManager controllerManager in GetComponentsInChildren<ControllerInputActionManager>(true))
            ConfigureControllerRayOrigin(controllerManager);
    }

    private void ConfigureControllerRayOrigin(ControllerInputActionManager controllerManager)
    {
        if (controllerManager == null) return;

        Transform controllerRoot = controllerManager.transform;
        Transform rigRoot = controllerRoot.parent;
        if (rigRoot == null) return;

        bool isLeftController = controllerRoot.name == LeftControllerName;
        Transform stabilizedOrigin = FindChildTransform(rigRoot, isLeftController ? LeftControllerStabilizedOriginName : RightControllerStabilizedOriginName);
        Transform stabilizedAttach = FindChildTransform(rigRoot, isLeftController ? LeftControllerStabilizedAttachName : RightControllerStabilizedAttachName);

        if (stabilizedOrigin == null || stabilizedAttach == null)
        {
            LogHandsWarning($"Controller stabilized transforms missing controller={controllerRoot.name} originFound={(stabilizedOrigin != null)} attachFound={(stabilizedAttach != null)}");
            return;
        }

        XRTransformStabilizer stabilizer = stabilizedOrigin.GetComponent<XRTransformStabilizer>();
        if (disableLocomotionAndTeleportOnControllers && stabilizer != null && stabilizer.aimTarget != null)
        {
            stabilizer.aimTarget = null;
            LogHands($"Cleared teleport aim target from controller stabilizer controller={controllerRoot.name}");
        }

        bool useDirectOrigin = UseDirectControllerRayOrigin;
        Transform desiredOrigin = useDirectOrigin ? controllerRoot : stabilizedOrigin;
        Transform desiredAttach = useDirectOrigin
            ? EnsureControllerUiAttach(controllerRoot, isLeftController)
            : stabilizedAttach;

        if (stabilizer != null) stabilizer.enabled = !useDirectOrigin;

        int configuredProviders = ApplyRayTransforms(controllerManager, desiredOrigin, desiredAttach, useDirectOrigin);
        int adjustedLineRenderers = EnsureVisibleControllerRayLineWidths(controllerManager);

        if (configuredProviders <= 0 && !enableControllerRayDiagnostics) return;

        LogHands($"Configured controller ray origin controller={controllerRoot.name} providers={configuredProviders} adjustedLineRenderers={adjustedLineRenderers} mode={DescribeOriginMode(useDirectOrigin)} origin={desiredOrigin.name} attach={desiredAttach.name} stabilizerEnabled={(stabilizer != null && stabilizer.enabled)}");
        LogControllerRayDiagnostics(controllerManager, desiredOrigin, desiredAttach, stabilizer, DescribeOriginMode(useDirectOrigin), configuredProviders > 0);
    }

    /// <summary>Devuelve cuántos componentes se modificaron realmente.</summary>
    private static int ApplyRayTransforms(ControllerInputActionManager controllerManager, Transform desiredOrigin,
        Transform desiredAttach, bool useDirectOrigin)
    {
        int configuredProviders = 0;

        foreach (MonoBehaviour behaviour in controllerManager.GetComponentsInChildren<MonoBehaviour>(true))
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

            if (useDirectOrigin)
            {
                changed |= TrySetBoolField(behaviour, "m_EnableStabilization", false);
                changed |= TrySetObjectField(behaviour, "m_AimTargetObject", null);
                changed |= TrySetBoolField(behaviour, "m_DisableVisualsWhenBlockedInGroup", false);
                changed |= TrySetBoolField(behaviour, "m_ExtendLineToEmptyHit", true);
            }

            if (changed) configuredProviders++;
        }

        return configuredProviders;
    }

    /// <summary>
    /// Fuerza un grosor mínimo del rayo: algunas configuraciones del template lo dejan
    /// tan fino que resulta invisible en el visor.
    /// </summary>
    private int EnsureVisibleControllerRayLineWidths(ControllerInputActionManager controllerManager)
    {
        if (controllerManager == null) return 0;

        int adjustedCount = 0;

        foreach (LineRenderer lineRenderer in controllerManager.GetComponentsInChildren<LineRenderer>(true))
        {
            if (lineRenderer == null) continue;

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
            if (widthCurve == null || widthCurve.length == 0) continue;

            Keyframe firstKey = widthCurve[0];
            if (firstKey.value >= MinimumVisibleControllerRayWidth) continue;

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
        if (originRoot == null) return null;

        string attachName = isLeftController ? LeftControllerUiAttachName : RightControllerUiAttachName;
        Transform existingAttach = originRoot.Find(attachName);
        if (existingAttach != null) return existingAttach;

        Transform attachTransform = new GameObject(attachName).transform;
        attachTransform.SetParent(originRoot, false);
        attachTransform.localPosition = Vector3.zero;
        attachTransform.localRotation = Quaternion.identity;
        attachTransform.localScale = Vector3.one;
        return attachTransform;
    }

    private void ConfigureUiInputModules()
    {
        if (!disableJoystickUiFallback && !disableGamepadUiFallback && !disableBuiltInUiFallback) return;

        foreach (XRUIInputModule inputModule in Resources.FindObjectsOfTypeAll<XRUIInputModule>())
        {
            if (inputModule == null) continue;
            if (!inputModule.gameObject.scene.IsValid() || !inputModule.gameObject.scene.isLoaded) continue;

            // Si el módulo no tiene acciones asignadas explícitamente, se deja intacto:
            // desactivarle los fallbacks lo dejaría sin ninguna entrada.
            if (!HasExplicitUiActions(inputModule)) continue;

            if (disableJoystickUiFallback) TrySetBoolField(inputModule, "m_EnableJoystickInput", false);
            if (disableGamepadUiFallback) TrySetBoolField(inputModule, "m_EnableGamepadInput", false);
            if (disableBuiltInUiFallback) TrySetBoolField(inputModule, "m_EnableBuiltinActionsAsFallback", false);
        }
    }

    private static bool HasExplicitUiActions(XRUIInputModule inputModule)
    {
        foreach (string fieldName in ExplicitUiActionFieldNames)
        {
            if (HasObjectFieldValue(inputModule, fieldName)) return true;
        }

        return false;
    }
}
