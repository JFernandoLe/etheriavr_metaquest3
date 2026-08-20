using System;
using UnityEngine;

/// <summary>
/// Obsoleto: EtheriaVR ya no requiere etheria_desktop para descubrimiento UDP.
/// </summary>
[Obsolete("UDPBeacon ya no es necesario. El procesamiento de voz es local en Quest.")]
public class UDPBeacon : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[UDPBeacon] Desactivado — ya no se requiere conexión con etheria_desktop.");
        enabled = false;
    }
}
