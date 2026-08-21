using System;
using UnityEngine;

/// <summary>
/// Obsoleto: la tesitura ya no depende de un servidor Flask en el PC.
/// </summary>
[Obsolete("AIServerFinder ya no se utiliza. La clasificación de tesitura es local.")]
public class AIServerFinder : MonoBehaviour
{
    public static string ServerURL;
}
