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
    }

    private static readonly int CalidadHash = Animator.StringToHash("Calidad");

    [Header("Configuración de Desempeño")]
    [Range(0f, 100f)] public float puntajePiano = 0f;
    public PianoPublicSystem sistemaPublico;

    [Header("Respuesta Visual")]
    [SerializeField] private float velocidadSeguimientoPuntaje = 3.1f;
    [SerializeField] private Vector2 rangoOffsetCalidad = new Vector2(-10f, 10f);
    [SerializeField] private Vector2 rangoSuavizadoAnimacion = new Vector2(0.7f, 1.9f);
    [SerializeField] private float intervaloRecacheo = 2f;

    [Header("Rotación")]
    public Transform jugador;
    public float velocidadRotacion = 2f;

    private readonly List<DatosAnimadorPiano> listaAnimadores = new List<DatosAnimadorPiano>();
    private float siguienteRecacheo = 0f;

    void Start()
    {
        ResolveDependencies();
        CacheAudienceAnimators();
    }

    void Update()
    {
        ResolveDependencies();

        if (listaAnimadores.Count == 0 || Time.unscaledTime >= siguienteRecacheo)
            CacheAudienceAnimators();

        if (sistemaPublico != null)
        {
            puntajePiano = Mathf.Lerp(puntajePiano, sistemaPublico.GetCurrentAudienceAnimationScore(),
                Time.deltaTime * velocidadSeguimientoPuntaje);
        }

        if (jugador == null) return;

        for (int i = listaAnimadores.Count - 1; i >= 0; i--)
        {
            DatosAnimadorPiano datos = listaAnimadores[i];
            if (datos?.animador == null)
            {
                listaAnimadores.RemoveAt(i);
                continue;
            }

            float calidadObjetivo = Mathf.Clamp(puntajePiano + datos.offsetCalidad, 0f, 100f);
            float calidadActual = datos.animador.GetFloat(CalidadHash);
            datos.animador.SetFloat(CalidadHash,
                Mathf.Lerp(calidadActual, calidadObjetivo, Time.deltaTime * datos.velocidadSuavizado));

            Vector3 direccion = jugador.position - datos.animador.transform.position;
            direccion.y = 0f;
            if (direccion.sqrMagnitude <= 0.0001f) continue;

            Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion.normalized);
            datos.animador.transform.rotation = Quaternion.Slerp(
                datos.animador.transform.rotation,
                rotacionObjetivo,
                Time.deltaTime * velocidadRotacion * datos.velocidadRotacion);
        }
    }

    private void ResolveDependencies()
    {
        if (sistemaPublico == null) sistemaPublico = FindObjectOfType<PianoPublicSystem>();
        if (jugador == null && Camera.main != null) jugador = Camera.main.transform;
    }

    private void CacheAudienceAnimators()
    {
        siguienteRecacheo = Time.unscaledTime + Mathf.Max(0.5f, intervaloRecacheo);
        listaAnimadores.Clear();

        foreach (GameObject personaje in GameObject.FindGameObjectsWithTag("Publico"))
        {
            if (personaje == null) continue;

            Animator animador = personaje.GetComponent<Animator>() ?? personaje.GetComponentInChildren<Animator>(true);
            if (animador == null) continue;

            listaAnimadores.Add(new DatosAnimadorPiano
            {
                animador = animador,
                offsetCalidad = Random.Range(rangoOffsetCalidad.x, rangoOffsetCalidad.y),
                velocidadSuavizado = Random.Range(rangoSuavizadoAnimacion.x, rangoSuavizadoAnimacion.y),
                velocidadRotacion = Random.Range(0.75f, 1.35f)
            });
        }
    }
}
