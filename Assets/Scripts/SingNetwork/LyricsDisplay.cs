using UnityEngine;
using TMPro;

public class LyricsDisplay : MonoBehaviour
{
    public SongLoader songLoader;
    public TextMeshPro lyricText;

    private int currentIndex = 0;

    void Update()
    {
        if (songLoader == null || lyricText == null)
            return;

        if (songLoader.loadedSong == null || songLoader.loadedSong.lyrics == null)
            return;

        float time = songLoader.GetSongTime() + songLoader.songOffset;
        var lyrics = songLoader.loadedSong.lyrics;

        // avanzar a la siguiente línea cuando toca
        if (currentIndex < lyrics.Length - 1 &&
            time >= lyrics[currentIndex + 1].time)
        {
            currentIndex++;
        }

        // mostrar texto actual
        lyricText.text = lyrics[currentIndex].text;
    }
}