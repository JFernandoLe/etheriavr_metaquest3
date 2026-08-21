using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador de audiencia para MODO CANTO (SingPublicSystem).
/// </summary>
public class ControladorAudienciaSing : MonoBehaviour
{
    private class DatosAnimadorSing
    {
        public Animator animador;
        public float offsetCalidad;
        public float velocidadSuavizado;
        public float velocidadRotacion;
        public float lastCalidad;
    }

    private static readonly int CalidadHash = Animator.StringToHash("Calidad");

    [Header("Configuración de Desempeño")]
    [Range(0f, 100f)]
    public float puntajeCanto = 0f;
    public SingPublicSystem sistemaPublico;

    [Header("Respuesta Visual")]
    [SerializeField] private float velocidadSeguimientoPuntaje = 3.1f;
    [SerializeField] private Vector2 rangoOffsetCalidad = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 rangoSuavizadoAnimacion = new Vector2(0.7f, 1.9f);
    [SerializeField] private float animationUpdateHz = 12f;
    [SerializeField] private float lookUpdateHz = 8f;

    [Header("Rotación")]
    public Transform jugador;
    public float velocidadRotacion = 2f;

    private readonly List<DatosAnimadorSing> listaAnimadores = new List<DatosAnimadorSing>();
    private float nextAnimationUpdate;
    private float nextLookUpdate;
    private float animationStep;
    private float lookStep;

    void Start()
    {
        ResolveDependencies();
        CacheAudienceAnimators();
        animationStep = 1f / Mathf.Max(1f, animationUpdateHz);
        lookStep = 1f / Mathf.Max(1f, lookUpdateHz);
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;

        if (sistemaPublico == null) ResolveDependencies();

        if (sistemaPublico != null)
        {
            float objetivoPublico = sistemaPublico.GetCurrentAudienceAnimationScore();
            puntajeCanto = Mathf.Lerp(puntajeCanto, objetivoPublico, Time.deltaTime * velocidadSeguimientoPuntaje);
        }

        float now = Time.unscaledTime;
        bool updateAnim = now >= nextAnimationUpdate;
        bool updateLook = now >= nextLookUpdate && jugador != null;
        if (!updateAnim && !updateLook) return;

        if (updateAnim) nextAnimationUpdate = now + animationStep;
        if (updateLook) nextLookUpdate = now + lookStep;

        for (int i = listaAnimadores.Count - 1; i >= 0; i--)
        {
            DatosAnimadorSing datos = listaAnimadores[i];
            if (datos == null || datos.animador == null)
            {
                listaAnimadores.RemoveAt(i);
                continue;
            }

            if (updateAnim)
            {
                float calidadObjetivo = Mathf.Clamp(puntajeCanto + datos.offsetCalidad, 0f, 100f);
                float calidadNueva = Mathf.Lerp(datos.lastCalidad, calidadObjetivo, animationStep * datos.velocidadSuavizado);
                if (Mathf.Abs(calidadNueva - datos.lastCalidad) > 0.05f)
                {
                    datos.lastCalidad = calidadNueva;
                    datos.animador.SetFloat(CalidadHash, calidadNueva);
                }
            }

            if (!updateLook) continue;

            Vector3 direccion = jugador.position - datos.animador.transform.position;
            direccion.y = 0f;
            if (direccion.sqrMagnitude <= 0.0001f) continue;

            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion.normalized);
            datos.animador.transform.rotation = Quaternion.Slerp(
                datos.animador.transform.rotation,
                rotacionObjetivo,
                lookStep * velocidadRotacion * datos.velocidadRotacion);
        }
    }

    private void ResolveDependencies()
    {
        if (sistemaPublico == null) sistemaPublico = FindObjectOfType<SingPublicSystem>();
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;
    }

    private void CacheAudienceAnimators()
    {
        listaAnimadores.Clear();

        GameObject[] personajes = GameObject.FindGameObjectsWithTag("Publico");
        for (int i = 0; i < personajes.Length; i++)
        {
            GameObject personaje = personajes[i];
            if (personaje == null) continue;

            Animator animador = personaje.GetComponent<Animator>()
                                ?? personaje.GetComponentInChildren<Animator>(true);
            if (animador == null) continue;

            listaAnimadores.Add(new DatosAnimadorSing
            {
                animador = animador,
                offsetCalidad = Random.Range(rangoOffsetCalidad.x, rangoOffsetCalidad.y),
                velocidadSuavizado = Random.Range(rangoSuavizadoAnimacion.x, rangoSuavizadoAnimacion.y),
                velocidadRotacion = Random.Range(0.75f, 1.35f),
                lastCalidad = animador.GetFloat(CalidadHash)
            });
        }
    }
}
