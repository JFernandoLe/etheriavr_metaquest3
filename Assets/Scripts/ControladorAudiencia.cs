using UnityEngine;
using System.Collections.Generic;

public class ControladorAudiencia : MonoBehaviour
{
    [Header("Configuracion de Desempeño")]
    [Range(0f, 100f)]
    public float puntajeCanto = 0f;
    public AudioSource fuenteAplausos;

    [Header("Dificultad (normalizacion)")]
    [Tooltip("Valor máximo esperado del accuracy")]
    public float dificultad = 40f;

    [Header("Comportamiento del puntaje")]
    public float velocidadSubida = 5f;
    public float velocidadBajada = 10f;
    public float inercia = 2f;

    [Header("Rotación")]
    public Transform jugador;
    public float velocidadRotacionBase = 2f;

    private float puntajeSuavizado = 0f;
    private bool yaEstaAplaudiendo = false;

    class DatosAnimador
    {
        public Animator anim;
        public float offset;
        public float velocidad;
        public float velocidadRotacion;
    }

    private List<DatosAnimador> animadores = new List<DatosAnimador>();

    void Start()
    {
        GameObject[] personajes = GameObject.FindGameObjectsWithTag("Publico");

        foreach (GameObject personaje in personajes)
        {
            Animator anim = personaje.GetComponentInChildren<Animator>();

            if (anim != null)
            {
                DatosAnimador datos = new DatosAnimador();
                datos.anim = anim;

                // Variación natural
                datos.offset = Random.Range(-10f, 10f);
                datos.velocidad = Random.Range(0.5f, 2f);
                datos.velocidadRotacion = Random.Range(0.5f, 1.5f);

                animadores.Add(datos);
            }
        }

        Debug.Log("Animadores encontrados: " + animadores.Count);
    }

    void Update()
    {
        if (ScoreManager.Instance != null)
        {
            float raw = ScoreManager.Instance.accuracyPercent;

            // 🔥 Normalización con dificultad
            float target = Mathf.InverseLerp(0f, dificultad, raw) * 100f;

            // Subida / bajada controlada
            if (target > puntajeCanto)
            {
                puntajeCanto = Mathf.Lerp(puntajeCanto, target, Time.deltaTime * velocidadSubida);
            }
            else
            {
                puntajeCanto = Mathf.Lerp(puntajeCanto, target, Time.deltaTime * velocidadBajada);
            }

            puntajeCanto = Mathf.Clamp(puntajeCanto, 0f, 100f);

            // Inercia (tendencia)
            puntajeSuavizado = Mathf.Lerp(puntajeSuavizado, puntajeCanto, Time.deltaTime * inercia);

            // Debug ligero
            if (Time.frameCount % 120 == 0)
            {
                Debug.Log("Raw: " + raw + " | Puntaje: " + puntajeCanto);
            }
        }

        // ROTACIÓN NATURAL
        if (jugador != null)
        {
            foreach (var datos in animadores)
            {
                if (datos.anim == null) continue;

                Vector3 direccion = jugador.position - datos.anim.transform.position;
                direccion.y = 0;

                if (direccion.sqrMagnitude > 0.01f)
                {
                    Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);

                    datos.anim.transform.rotation = Quaternion.Slerp(
                        datos.anim.transform.rotation,
                        rotacionObjetivo,
                        Time.deltaTime * 5f * velocidadRotacionBase * datos.velocidadRotacion
                    );
                }
            }
        }

        // ANIMACIÓN NATURAL
        foreach (var datos in animadores)
        {
            if (datos.anim == null) continue;

            float calidadFinal = puntajeSuavizado + datos.offset;
            calidadFinal = Mathf.Clamp(calidadFinal, 0f, 100f);

            float suavizado = Mathf.Lerp(
                datos.anim.GetFloat("Calidad"),
                calidadFinal,
                Time.deltaTime * datos.velocidad
            );

            datos.anim.SetFloat("Calidad", suavizado);
        }

        ManejarAudio();
    }

    void ManejarAudio()
    {
        if (fuenteAplausos == null) return;

        if (puntajeCanto > 70 && !yaEstaAplaudiendo)
        {
            fuenteAplausos.Play();
            yaEstaAplaudiendo = true;
        }
        else if (puntajeCanto <= 70 && yaEstaAplaudiendo)
        {
            fuenteAplausos.Stop();
            yaEstaAplaudiendo = false;
        }
    }
}