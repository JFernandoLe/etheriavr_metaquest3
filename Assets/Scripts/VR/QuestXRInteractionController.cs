using System.Reflection;
using UnityEngine;
using Unity.VRTemplate;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DefaultExecutionOrder(-1000)]
public class QuestXRInteractionController : MonoBehaviour
{
    [SerializeField] private bool disableLocomotionRoot = true;
    [SerializeField] private bool disableLocomotionAndTeleportOnControllers = true;
    [SerializeField] private bool disableJoystickUiFallback = true;
    [SerializeField] private bool disableGamepadUiFallback = true;
    [SerializeField] private bool disableBuiltInUiFallback = true;
    [SerializeField] private string locomotionRootName = "Locomotion";
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
    private bool hasLogged;

    private void Awake()
    {
        ApplyInteractionMode();
    }

    private void OnEnable()
    {
        ApplyInteractionMode();
    }

    private void Start()
    {
        ApplyInteractionMode();
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
            return;

        EnsureHandsPermissionManager();

        Transform cameraOffset = transform.Find(cameraOffsetName);
        if (cameraOffset == null)
            return;

        GameObject existingLeftHand = FindChildObject(cameraOffset, leftHandName);
        GameObject existingRightHand = FindChildObject(cameraOffset, rightHandName);
        GameObject existingHandVisualizer = FindChildObject(cameraOffset, handVisualizerName);

        if ((existingLeftHand == null || existingRightHand == null || existingHandVisualizer == null) && handsRigTemplate != null)
        {
            InstallHandsFromTemplate(cameraOffset, existingHandVisualizer == null, existingLeftHand == null, existingRightHand == null);
            existingLeftHand = FindChildObject(cameraOffset, leftHandName);
            existingRightHand = FindChildObject(cameraOffset, rightHandName);
        }

        AssignHandsToModalityManager(existingLeftHand, existingRightHand);
    }

    private void EnsureHandsPermissionManager()
    {
        if (handsPermissionsManagerPrefab == null)
            return;

        HandSubsystemManager existingManager = FindLoadedObjectOfType<HandSubsystemManager>();
        if (existingManager != null)
            return;

        Instantiate(handsPermissionsManagerPrefab, transform);
    }

    private void InstallHandsFromTemplate(Transform cameraOffset, bool needsHandVisualizer, bool needsLeftHand, bool needsRightHand)
    {
        if (!needsHandVisualizer && !needsLeftHand && !needsRightHand)
            return;

        isInstallingHandsTemplate = true;
        GameObject templateInstance = Instantiate(handsRigTemplate);
        isInstallingHandsTemplate = false;

        if (templateInstance == null)
            return;

        templateInstance.SetActive(false);

        try
        {
            Transform templateCameraOffset = templateInstance.transform.Find(cameraOffsetName);
            if (templateCameraOffset == null)
                return;

            if (needsHandVisualizer)
                MoveChildToTarget(FindChildTransform(templateCameraOffset, handVisualizerName), cameraOffset);

            if (needsLeftHand)
                MoveChildToTarget(FindChildTransform(templateCameraOffset, leftHandName), cameraOffset);

            if (needsRightHand)
                MoveChildToTarget(FindChildTransform(templateCameraOffset, rightHandName), cameraOffset);

            if (FindChildObject(transform, handsSmoothingPostProcessorName) == null)
                MoveChildToTarget(FindChildTransform(templateInstance.transform, handsSmoothingPostProcessorName), transform);
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
            return;

        Component[] components = GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
                continue;

            TrySetObjectField(component, "m_LeftHand", leftHand);
            TrySetObjectField(component, "m_RightHand", rightHand);
        }
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

            if (disableJoystickUiFallback)
                TrySetBoolField(inputModule, "m_EnableJoystickInput", false);

            if (disableGamepadUiFallback)
                TrySetBoolField(inputModule, "m_EnableGamepadInput", false);

            if (disableBuiltInUiFallback)
                TrySetBoolField(inputModule, "m_EnableBuiltinActionsAsFallback", false);
        }
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

    private static void MoveChildToTarget(Transform source, Transform targetParent)
    {
        if (source == null || targetParent == null)
            return;

        source.SetParent(targetParent, false);
        source.gameObject.SetActive(true);
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

    private static bool TrySetObjectField(object target, string fieldName, Object value)
    {
        for (System.Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || !field.FieldType.IsAssignableFrom(value.GetType()))
                continue;

            field.SetValue(target, value);
            return true;
        }

        return false;
    }
}