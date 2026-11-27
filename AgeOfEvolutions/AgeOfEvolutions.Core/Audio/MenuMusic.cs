using System;
using AgeOfEvolutions.Core.Diagnostics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace AgeOfEvolutions.Core.Audio;

/// <summary>
/// Hintergrundmusik der Menüs außerhalb des Spiels (Nutzer 2026-11-27: „sollte im Menü als
/// hintergrund musik verwendet werden“): das Haupt-Thema <see cref="ASSET"/> in Dauerschleife
/// über den MediaPlayer. Quelle ist tools/klang/Age of Evolutions - Main Theme.mp3; im Content
/// liegt nur die Tonspur, ohne Titelbild und Liedtext:
/// <code>
/// ffmpeg -i "tools/klang/Age of Evolutions - Main Theme.mp3" -map 0:a -c:a copy -map_metadata -1
///        AgeOfEvolutions/AgeOfEvolutions.Core/Content/Sounds/MainTheme.mp3
/// </code>
/// Der BackgroundScreen - er liegt unter Hauptmenü, „Einstellungen“ und „Spiel laden“ und geht
/// erst mit dem Spielstart - startet sie, blendet sie mit seiner Übergangsblende ein und aus und
/// hält sie beim Entladen an; im Spiel bleibt es still. Fehlt die Datei oder das Audiogerät,
/// bleibt es ebenfalls still - die Musik darf das Menü nicht umbringen.
/// </summary>
public sealed class MenuMusic
{
    /// <summary>Das Lied im Content (Content/Sounds/MainTheme.mp3, SongProcessor).</summary>
    public const string ASSET = "Sounds/MainTheme";

    /// <summary>
    /// Pegel bei voller Menü-Lautstärke (Option „Lautstärke“ 100 %). Das Stück ist laut
    /// gemastert (im Mittel −17,5 dB, Spitzen −2,3 dB) und soll unter dem Klick der
    /// Menüeinträge bleiben; beim Standard 60 % ergibt das 0,36.
    /// </summary>
    public const float LEVEL = 0.6f;

    private Song _song;
    private bool _playing;
    // Zuletzt an den MediaPlayer gegebene Lautstärke; -1 = keine
    private float _volume = -1f;

    /// <summary>Läuft die Musik? (Start war erfolgreich, Stop noch nicht gerufen.)</summary>
    public bool Playing => _playing;

    /// <summary>
    /// Lautstärke der Musik für den MediaPlayer. <paramref name="soundVolumePercent"/> ist die
    /// Option „Lautstärke“ (AgeOfEvolutionsSettings.SoundVolume), <paramref name="fade"/> die
    /// TransitionAlpha des BackgroundScreen (0 unsichtbar, 1 ganz da).
    /// VERTRAG: return Math.Clamp(soundVolumePercent, 0, 100) / 100f * LEVEL * Math.Clamp(fade, 0f, 1f);
    /// </summary>
    public static float Volume(int soundVolumePercent, float fade)
    {
        return Math.Clamp(soundVolumePercent, 0, 100) / 100f * LEVEL * Math.Clamp(fade, 0f, 1f);
    }

    /// <summary>
    /// Lädt das Lied und spielt es in Dauerschleife mit der Lautstärke <paramref name="volume"/>.
    /// VERTRAG: ist _playing schon true, sofort return. Sonst in einem try-Block der Reihe nach:
    /// _song ??= content.Load&lt;Song&gt;(ASSET); MediaPlayer.IsRepeating = true;
    /// MediaPlayer.Volume = volume; MediaPlayer.Play(_song); _volume = volume; _playing = true;
    /// GameLog.Info($"Menümusik: {ASSET} läuft ({(int)_song.Duration.TotalSeconds} s)");
    /// im catch (Exception e): _playing = false;
    /// GameLog.Warn($"Menümusik: {ASSET} nicht abspielbar - {e.Message}");
    /// Die Methode wirft nie.
    /// </summary>
    public void Start(ContentManager content, float volume)
    {
        if (_playing)
            return;
        try
        {
            _song ??= content.Load<Song>(ASSET);
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = volume;
            MediaPlayer.Play(_song);
            _volume = volume;
            _playing = true;
            GameLog.Info($"Menümusik: {ASSET} läuft ({(int)_song.Duration.TotalSeconds} s)");
        }
        catch (Exception e)
        {
            _playing = false;
            GameLog.Warn($"Menümusik: {ASSET} nicht abspielbar - {e.Message}");
        }
    }

    /// <summary>
    /// Neue Lautstärke; der BackgroundScreen ruft das in jedem Bild.
    /// VERTRAG: return, wenn !_playing oder volume == _volume (der MediaPlayer bekommt nur
    /// Änderungen). Sonst try { MediaPlayer.Volume = volume; _volume = volume; } catch (Exception) { }
    /// Die Methode wirft nie.
    /// </summary>
    public void SetVolume(float volume)
    {
        if (!_playing || volume == _volume)
            return;
        try
        {
            MediaPlayer.Volume = volume;
            _volume = volume;
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Hält die Musik an; danach darf der ContentManager das Lied entladen.
    /// VERTRAG: return, wenn !_playing. Sonst _playing = false; _volume = -1f;
    /// try { MediaPlayer.Stop(); } catch (Exception) { } und danach GameLog.Info("Menümusik angehalten");
    /// Die Methode wirft nie.
    /// </summary>
    public void Stop()
    {
        if (!_playing)
            return;
        _playing = false;
        _volume = -1f;
        try
        {
            MediaPlayer.Stop();
        }
        catch (Exception)
        {
        }
        GameLog.Info("Menümusik angehalten");
    }
}
