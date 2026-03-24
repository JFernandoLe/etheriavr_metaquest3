using UnityEngine;

public class PianoCalibrator : MonoBehaviour
{
    public GameObject confirmUI; 
    private bool isLocked = false;
    public float moveSpeed = 0.5f;
    public float scaleSpeed = 0.3f;
    
    // Evento que se dispara cuando el usuario confirma la configuración
    public static event System.Action OnPianoConfigured;

    void Update()
    {
        if (isLocked) return;

        // --- 1. MOVER POSICIÓN (Joysticks Libres) ---
        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

        if (!OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)) 
        {
            transform.Translate(leftStick.x * moveSpeed * Time.deltaTime, 0, leftStick.y * moveSpeed * Time.deltaTime, Space.Self);
            transform.Translate(0, rightStick.y * moveSpeed * Time.deltaTime, 0, Space.World);
        }
        if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch)) 
        {
            Vector3 currentScale = transform.localScale;
            currentScale.y += rightStick.y * scaleSpeed * Time.deltaTime;
            currentScale.x += rightStick.x * scaleSpeed * Time.deltaTime;
            currentScale.z += leftStick.y * scaleSpeed * Time.deltaTime;
            transform.localScale = currentScale;
        }
        if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch)) {
            transform.Rotate(0, leftStick.x * 60f * Time.deltaTime, 0);
        }
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) {
            ToggleLock();
        }
    }

    public void ToggleLock() {
        if (!isLocked)  // Solo permitir LOCK, no unlock
        {
            isLocked = true;
            if (confirmUI != null) confirmUI.SetActive(false);
            
            Debug.Log("<color=green>[PianoCalibrator]</color> ✅ Piano BLOQUEADO - ¡Iniciando juego!");
            OnPianoConfigured?.Invoke();  // Disparar evento UNA SOLA VEZ
        }
        else
        {
            Debug.Log("<color=yellow>[PianoCalibrator]</color> ⚠️  Piano ya está BLOQUEADO. Presiona otra tecla para desbloquear.");
        }
    }
}