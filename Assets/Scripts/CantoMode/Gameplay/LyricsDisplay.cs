using UnityEngine;
using TMPro;

public class LyricsDisplay : MonoBehaviour
{
    public SongLoader songLoader;
    public TextMeshPro lyricText;

    private int currentIndex = 0;
    private string lastText;

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (songLoader == null || lyricText == null) return;
        if (songLoader.loadedSong == null || songLoader.loadedSong.lyrics == null) return;

        float time = songLoader.GetSongTime() + songLoader.songOffset;
        var lyrics = songLoader.loadedSong.lyrics;

        if (currentIndex < lyrics.Length - 1 && time >= lyrics[currentIndex + 1].time)
            currentIndex++;

        string text = lyrics[currentIndex].text;
        if (text == lastText) return;

        lastText = text;
        lyricText.text = text;
    }
}
