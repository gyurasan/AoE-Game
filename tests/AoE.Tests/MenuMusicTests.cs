using System;
using System.IO;
using AgeOfEvolutions.Core.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Menümusik (Nutzer 2026-11-27): Lautstärke aus Option „Lautstärke“ und Übergangsblende des
/// BackgroundScreen, und ohne Datei oder Audiogerät bleibt es still, statt das Menü zu stürzen.
/// Ob das Lied im echten Spiel läuft und mit dem Spielstart aufhört, prüft
/// tools/ollama-agent/checks/mm_menumusik.py beim Start der Exe.
/// </summary>
public class MenuMusicTests
{
    [Theory]
    [InlineData(100, 1f, MenuMusic.LEVEL)]
    [InlineData(60, 1f, 0.6f * MenuMusic.LEVEL)]
    [InlineData(0, 1f, 0f)]
    [InlineData(100, 0.5f, 0.5f * MenuMusic.LEVEL)]
    [InlineData(50, 0.25f, 0.125f * MenuMusic.LEVEL)]
    [InlineData(100, 0f, 0f)]
    // Außerhalb der Bereiche wird begrenzt, nicht hochgerechnet
    [InlineData(150, 1f, MenuMusic.LEVEL)]
    [InlineData(-20, 1f, 0f)]
    [InlineData(100, 1.5f, MenuMusic.LEVEL)]
    [InlineData(100, -0.5f, 0f)]
    public void Volume_Ist_Option_Mal_Pegel_Mal_Blende(int percent, float fade, float expected)
    {
        Assert.Equal(expected, MenuMusic.Volume(percent, fade), 5);
    }

    [Fact]
    public void Pegel_Bleibt_Unter_Voll()
    {
        Assert.InRange(MenuMusic.LEVEL, 0.1f, 0.9f);
    }

    [Fact]
    public void Start_Ohne_Datei_Bleibt_Still()
    {
        var music = new MenuMusic();
        var content = new ContentManager(new GameServiceContainer(),
            Path.Combine(Path.GetTempPath(), "aoe-ohne-content-" + Guid.NewGuid().ToString("N")));

        var fehler = Record.Exception(() => music.Start(content, 0.5f));

        Assert.Null(fehler);
        Assert.False(music.Playing);
    }

    [Fact]
    public void Lautstaerke_Und_Stop_Ohne_Start_Werfen_Nicht()
    {
        var music = new MenuMusic();

        Assert.Null(Record.Exception(() => music.SetVolume(0.3f)));
        Assert.Null(Record.Exception(() => music.Stop()));
        Assert.False(music.Playing);
    }

    [Fact]
    public void Stop_Nach_Gescheitertem_Start_Wirft_Nicht()
    {
        var music = new MenuMusic();
        var content = new ContentManager(new GameServiceContainer(), "gibt-es-nicht");
        music.Start(content, 0.5f);

        Assert.Null(Record.Exception(() => music.Stop()));
        Assert.False(music.Playing);
    }
}
