using System.Collections.Generic;
using UnityEngine;

public class ControladorAudienciaPiano : MonoBehaviour
{
    private class DatosAnimadorPiano
    {
        public Animator animador;
        public float offsetCalidad;
        public float velocidadSuavizado;
        public float velocidadRotacion;
        public float lastCalidad;
    }

    private static readonly int CalidadHash = Animator.StringToHash("Calidad");

    [Header("Configuración de Desempeño")]
    [Range(0f, 100f)] public float puntajePiano = 0f;
    public PianoPublicSystem sistemaPublico;

    [Header("Respuesta Visual")]
    [SerializeField] private float velocidadSeguimientoPuntaje = 3.1f;
    [SerializeField] private Vector2 rangoOffsetCalidad = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 rangoSuavizadoAnimacion = new Vector2(0.7f, 1.9f);
    [SerializeField] private float animationUpdateHz = 12f;
    [SerializeField] private float lookUpdateHz = 8f;

    [Header("Rotación")]
    public Transform jugador;
    public float velocidadRotacion = 2f;

    private readonly List<DatosAnimadorPiano> listaAnimadores = new List<DatosAnimadorPiano>();
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
            puntajePiano = Mathf.Lerp(puntajePiano, sistemaPublico.GetCurrentAudienceAnimationScore(),
                Time.deltaTime * velocidadSeguimientoPuntaje);
        }

        float now = Time.unscaledTime;
        bool updateAnim = now >= nextAnimationUpdate;
        bool updateLook = now >= nextLookUpdate && jugador != null;

        if (!updateAnim && !updateLook) return;
        if (updateAnim) nextAnimationUpdate = now + animationStep;
        if (updateLook) nextLookUpdate = now + lookStep;

        for (int i = listaAnimadores.Count - 1; i >= 0; i--)
        {
            DatosAnimadorPiano datos = listaAnimadores[i];
            if (datos?.animador == null)
            {
                listaAnimadores.RemoveAt(i);
                continue;
            }

            if (updateAnim)
            {
                float calidadObjetivo = Mathf.Clamp(puntajePiano + datos.offsetCalidad, 0f, 100f);
                float calidadActual = datos.lastCalidad;
                float calidadNueva = Mathf.Lerp(calidadActual, calidadObjetivo, animationStep * datos.velocidadSuavizado);
                if (Mathf.Abs(calidadNueva - calidadActual) > 0.05f)
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
        if (sistemaPublico == null) sistemaPublico = FindObjectOfType<PianoPublicSystem>();
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;
    }

    private void CacheAudienceAnimators()
    {
        listaAnimadores.Clear();

        foreach (GameObject personaje in GameObject.FindGameObjectsWithTag("Publico"))
        {
            if (personaje == null) continue;

            Animator animador = personaje.GetComponent<Animator>() ?? personaje.GetComponentInChildren<Animator>(true);
            if (animador == null) continue;

            float offset = Random.Range(rangoOffsetCalidad.x, rangoOffsetCalidad.y);
            listaAnimadores.Add(new DatosAnimadorPiano
            {
                animador = animador,
                offsetCalidad = offset,
                velocidadSuavizado = Random.Range(rangoSuavizadoAnimacion.x, rangoSuavizadoAnimacion.y),
                velocidadRotacion = Random.Range(0.75f, 1.35f),
                lastCalidad = animador.GetFloat(CalidadHash)
            });
        }
    }
}
