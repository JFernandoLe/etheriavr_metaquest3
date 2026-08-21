using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.VRTemplate;

/// <summary>
/// Trazas de diagnóstico del rig XR. Los snapshots de rayo se comparan con el anterior
/// para no inundar el log: solo se emiten cuando algo cambia de verdad.
/// </summary>
public partial class QuestXRInteractionController
{
    private static readonly List<XRHandSubsystem> CachedHandSubsystems = new List<XRHandSubsystem>();

    private string lastLeftControllerRaySnapshot;
    private string lastRightControllerRaySnapshot;

    private void LogControllerRayDiagnostics(string reason, bool force)
    {
        foreach (ControllerInputActionManager controllerManager in GetComponentsInChildren<ControllerInputActionManager>(true))
        {
            if (controllerManager == null) continue;

            Transform controllerRoot = controllerManager.transform;
            Transform rigRoot = controllerRoot.parent;
            if (rigRoot == null) continue;

            bool isLeftController = controllerRoot.name == LeftControllerName;
            Transform stabilizedOrigin = FindChildTransform(rigRoot, isLeftController ? LeftControllerStabilizedOriginName : RightControllerStabilizedOriginName);
            Transform stabilizedAttach = FindChildTransform(rigRoot, isLeftController ? LeftControllerStabilizedAttachName : RightControllerStabilizedAttachName);
            XRTransformStabilizer stabilizer = stabilizedOrigin != null ? stabilizedOrigin.GetComponent<XRTransformStabilizer>() : null;

            bool useDirectOrigin = UseDirectControllerRayOrigin;
            Transform desiredOrigin = useDirectOrigin ? controllerRoot : stabilizedOrigin;
            Transform desiredAttach = useDirectOrigin
                ? EnsureControllerUiAttach(controllerRoot, isLeftController)
                : stabilizedAttach;

            LogControllerRayDiagnostics(controllerManager, desiredOrigin, desiredAttach, stabilizer,
                $"{DescribeOriginMode(useDirectOrigin)}:{reason}", force);
        }
    }

    private void LogControllerRayDiagnostics(ControllerInputActionManager controllerManager, Transform desiredOrigin,
        Transform desiredAttach, XRTransformStabilizer stabilizer, string reason, bool force)
    {
        if (!enableControllerRayDiagnostics || controllerManager == null) return;

        Transform controllerRoot = controllerManager.transform;
        StringBuilder builder = new StringBuilder(1024);
        builder.Append("ControllerRayDiag reason=").Append(reason);
        builder.Append(" controller=").Append(controllerRoot.name);
        builder.Append(" controllerActive=").Append(controllerRoot.gameObject.activeInHierarchy);
        builder.Append(" desiredOrigin=").Append(DescribeTransform(desiredOrigin));
        builder.Append(" desiredAttach=").Append(DescribeTransform(desiredAttach));
        builder.Append(" controllerRootPose=").Append(DescribeTransform(controllerRoot));
        builder.Append(" controllerTracking=").Append(DescribeTrackingStatus(controllerRoot.name == LeftControllerName
            ? lastLeftControllerStatus
            : lastRightControllerStatus));

        if (stabilizer != null)
        {
            builder.Append(" stabilizerEnabled=").Append(stabilizer.enabled);
            builder.Append(" stabilizerTarget=").Append(DescribeTransform(TryGetTransformPropertyValue(stabilizer, "target")));
            builder.Append(" stabilizerAimTarget=").Append(DescribeObject(TryGetObjectPropertyValue(stabilizer, "aimTarget")));
        }

        AppendRelevantComponentFields(builder, controllerManager);

        string snapshot = builder.ToString();
        ref string lastSnapshot = ref GetControllerRaySnapshotStorage(controllerRoot.name);
        if (!force && lastSnapshot == snapshot) return;

        lastSnapshot = snapshot;
        LogHands(snapshot);
    }

    private static void AppendRelevantComponentFields(StringBuilder builder, ControllerInputActionManager controllerManager)
    {
        foreach (MonoBehaviour behaviour in controllerManager.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;

            bool hasRelevantField = false;
            StringBuilder componentBuilder = new StringBuilder();

            foreach (string fieldName in RayTransformFieldNames)
                AppendTransformField(componentBuilder, behaviour, fieldName, ref hasRelevantField);

            AppendObjectField(componentBuilder, behaviour, "m_AimTargetObject", ref hasRelevantField);
            AppendObjectField(componentBuilder, behaviour, "m_Target", ref hasRelevantField);

            if (!hasRelevantField) continue;

            builder.Append(" component[").Append(behaviour.GetType().Name).Append("]{");
            builder.Append(componentBuilder);
            builder.Append('}');
        }
    }

    private ref string GetControllerRaySnapshotStorage(string controllerName)
    {
        if (controllerName == LeftControllerName) return ref lastLeftControllerRaySnapshot;

        return ref lastRightControllerRaySnapshot;
    }

    private static void AppendTransformField(StringBuilder builder, object target, string fieldName, ref bool hasRelevantField)
    {
        if (!TryGetTransformFieldValue(target, fieldName, out Transform value)) return;

        hasRelevantField = true;
        if (builder.Length > 0) builder.Append(' ');

        builder.Append(fieldName).Append('=').Append(DescribeTransform(value));
    }

    private static void AppendObjectField(StringBuilder builder, object target, string fieldName, ref bool hasRelevantField)
    {
        if (!TryGetObjectFieldValue(target, fieldName, out Object value)) return;

        hasRelevantField = true;
        if (builder.Length > 0) builder.Append(' ');

        builder.Append(fieldName).Append('=').Append(DescribeObject(value));
    }

    private static string DescribeTrackingStatus(TrackingStatus status) =>
        $"tracked={status.isTracked},connected={status.isConnected},state={status.trackingState}";

    private static string DescribeTransform(Transform transform)
    {
        if (transform == null) return "<null>";

        Vector3 position = transform.position;
        Vector3 rotation = transform.rotation.eulerAngles;
        return $"{GetHierarchyPath(transform)}@p({position.x:F3},{position.y:F3},{position.z:F3}) r({rotation.x:F1},{rotation.y:F1},{rotation.z:F1})";
    }

    private static string DescribeObject(Object value) => value switch
    {
        null => "<null>",
        Component component => GetHierarchyPath(component.transform),
        GameObject gameObject => GetHierarchyPath(gameObject.transform),
        _ => value.name,
    };

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null) return "<null>";

        List<string> pathParts = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
            pathParts.Add(current.name);

        pathParts.Reverse();
        return string.Join("/", pathParts);
    }

    private static string DescribeHandSubsystems()
    {
        CachedHandSubsystems.Clear();
        SubsystemManager.GetSubsystems(CachedHandSubsystems);

        int runningCount = 0;
        foreach (XRHandSubsystem subsystem in CachedHandSubsystems)
        {
            if (subsystem != null && subsystem.running) runningCount++;
        }

        return $"handSubsystems={CachedHandSubsystems.Count} running={runningCount}";
    }

    private void LogHands(string message) => WriteDiagnostic(message, false);

    private void LogHandsWarning(string message) => WriteDiagnostic(message, true);

    private void WriteDiagnostic(string message, bool isWarning)
    {
        if (!enableXrHandsDebugLogs) return;

        string formattedMessage = $"[{DiagnosticTag}] {message}";

        if (isWarning) Debug.LogWarning(formattedMessage, this);
        else Debug.Log(formattedMessage, this);
    }
}
