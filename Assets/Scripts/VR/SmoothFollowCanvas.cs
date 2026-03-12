using UnityEngine;

public class SmoothFollowCanvas : MonoBehaviour
{
    [Header("Control")]
    [SerializeField] private bool enableFollowing = false; // DESACTIVADO por defecto - objetos FIJOS
    
    public Transform cameraTransform;
    public float distance = 0.7f;
    public float damping = 0.8f;
    public float followSpeed = 10f;
    public float timeScale = 0.25f;

    Vector3 velocity = Vector3.zero;
    private bool firstFrame = true; // Control para el salto inicial

    // Cada vez que el Canvas se activa (al iniciar la app o cambiar escena)
    void OnEnable()
    {
        firstFrame = true;
        velocity = Vector3.zero; // Reseteamos la inercia
    }

    void LateUpdate()
    {
        // SI ESTÁ DESACTIVADO, NO HACER NADA - objeto permanece FIJO
        if (!enableFollowing) return;
        
        if (cameraTransform == null) return;

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 targetPosition = cameraTransform.position + forward * distance;

        if (firstFrame)
        {
            // Salto instantáneo la primera vez para que no "viaje" desde lejos
            transform.position = targetPosition;
            transform.LookAt(cameraTransform);
            transform.Rotate(0, 180f, 0);
            firstFrame = false;
            return;
        }

        // Movimiento suave para el resto del tiempo
        float scaledDamping = damping / timeScale;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            scaledDamping,
            followSpeed
        );

        transform.LookAt(cameraTransform);
        transform.Rotate(0, 180f, 0);
    }
}