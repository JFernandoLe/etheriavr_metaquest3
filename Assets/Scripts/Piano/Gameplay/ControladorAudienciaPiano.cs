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
    [Range(0f, 100f)]
    public float puntajePiano = 0f;
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
        CacheAudienceAnimators(true);
    }

    void Update()
    {
        ResolveDependencies();

        if (listaAnimadores.Count == 0 || Time.unscaledTime >= siguienteRecacheo)
        {
            CacheAudienceAnimators(false);
        }

        if (sistemaPublico != null)
        {
            float objetivoPublico = sistemaPublico.GetCurrentAudienceAnimationScore();
            puntajePiano = Mathf.Lerp(puntajePiano, objetivoPublico, Time.deltaTime * velocidadSeguimientoPuntaje);
        }

        if (jugador == null)
        {
            return;
        }

        for (int i = listaAnimadores.Count - 1; i >= 0; i--)
        {
            DatosAnimadorPiano datos = listaAnimadores[i];
            if (datos == null || datos.animador == null)
            {
                listaAnimadores.RemoveAt(i);
                continue;
            }

            float calidadObjetivo = Mathf.Clamp(puntajePiano + datos.offsetCalidad, 0f, 100f);
            float calidadActual = datos.animador.GetFloat(CalidadHash);
            float calidadSuavizada = Mathf.Lerp(calidadActual, calidadObjetivo, Time.deltaTime * datos.velocidadSuavizado);

            datos.animador.SetFloat(CalidadHash, calidadSuavizada);

            Vector3 direccion = jugador.position - datos.animador.transform.position;
            direccion.y = 0f;

            if (direccion.sqrMagnitude > 0.0001f)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion.normalized);
                datos.animador.transform.rotation = Quaternion.Slerp(
                    datos.animador.transform.rotation,
                    rotacionObjetivo,
                    Time.deltaTime * velocidadRotacion * datos.velocidadRotacion);
            }
        }
    }

    private void ResolveDependencies()
    {
        if (sistemaPublico == null)
        {
            sistemaPublico = FindObjectOfType<PianoPublicSystem>();
        }

        if (jugador == null && Camera.main != null)
        {
            jugador = Camera.main.transform;
        }
    }

    private void CacheAudienceAnimators(bool forceLog)
    {
        siguienteRecacheo = Time.unscaledTime + Mathf.Max(0.5f, intervaloRecacheo);
        listaAnimadores.Clear();

        GameObject[] personajes = GameObject.FindGameObjectsWithTag("Publico");
        for (int i = 0; i < personajes.Length; i++)
        {
            GameObject personaje = personajes[i];
            if (personaje == null)
            {
                continue;
            }

            Animator animador = personaje.GetComponent<Animator>();
            if (animador == null)
            {
                animador = personaje.GetComponentInChildren<Animator>(true);
            }

            if (animador == null)
            {
                continue;
            }

            DatosAnimadorPiano datos = new DatosAnimadorPiano
            {
                animador = animador,
                offsetCalidad = Random.Range(rangoOffsetCalidad.x, rangoOffsetCalidad.y),
                velocidadSuavizado = Random.Range(rangoSuavizadoAnimacion.x, rangoSuavizadoAnimacion.y),
                velocidadRotacion = Random.Range(0.75f, 1.35f)
            };

            listaAnimadores.Add(datos);
        }

        if (forceLog || listaAnimadores.Count == 0)
        {
            Debug.Log($"[ControladorAudienciaPiano] Animadores de publico detectados: {listaAnimadores.Count}");
        }
    }
}