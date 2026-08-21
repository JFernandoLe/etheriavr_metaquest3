using UnityEngine;
using System.IO;

/// <summary>
/// Resuelve el file_path homogéneo de la BD (solo nombre de archivo) a la carpeta
/// de StreamingAssets según el modo. Canto y piano deciden su raíz aquí.
/// </summary>
public static class SongAssetPaths
{
    public const string PianoSongsFolder = "PianoSongs/Songs";
    public const string SingSongsFolder = "SingSongs/Songs";

    /// <summary>Nombre de archivo limpio desde file_path de BD (ej: "furelise.mid", "rosa_pastel.json").</summary>
    public static string GetAssetFileName(string filePathFromDb) =>
        string.IsNullOrWhiteSpace(filePathFromDb) ? string.Empty : Path.GetFileName(filePathFromDb.Trim());

    /// <summary>Basename sin extensión (ej: "rosa_pastel").</summary>
    public static string GetAssetBaseName(string filePathFromDb) =>
        Path.GetFileNameWithoutExtension(GetAssetFileName(filePathFromDb));

    public static string GetPianoMidiPath(string filePathFromDb) =>
        Path.Combine(Application.streamingAssetsPath, PianoSongsFolder, GetAssetFileName(filePathFromDb));

    public static string GetSingSongBasePath() =>
        Path.Combine(Application.streamingAssetsPath, SingSongsFolder);
}
