using UnityEngine;
using TMPro;

public class ShowUserInfoHome : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text user_text;

    private void Start()
    {
        if (UserSession.Instance != null && UserSession.Instance.IsLoggedIn)
        {
            string nombreUsuario = UserSession.Instance.username;

            user_text.text = $"Bienvenido, {nombreUsuario}";

            Debug.Log($"<color=green>[ShowUserInfoHome] Mostrando perfil de: {nombreUsuario}</color>");
        }
        else
        {

            Debug.LogWarning("[ShowUserInfoHome] No hay sesión activa. Regresando...");
            user_text.text = "Usuario no identificado";

        }
    }
}