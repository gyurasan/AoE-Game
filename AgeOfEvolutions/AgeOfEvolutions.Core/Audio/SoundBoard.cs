using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace AgeOfEvolutions.Core.Audio;

/// <summary>
/// Die Geräusche der Spielwelt aus tools/klang (Arbeit und Kampf): je Geräusch
/// <see cref="VARIANTS"/> Varianten Sounds/name_1 bis name_3, zufällig gewählt und
/// leicht verstimmt, damit dreißig Holzfäller nicht wie einer im Gleichschritt
/// klingen. Fehlt eine Datei oder das Audiogerät, bleibt es still - ein fehlendes
/// Geräusch darf das Spiel nicht umbringen.
/// </summary>
public sealed class SoundBoard
{
    /// <summary>Varianten je Geräusch, wie tools/klang/klang.py sie schreibt.</summary>
    public const int VARIANTS = 3;

    /// <summary>
    /// Kürzester Abstand zweier gleicher Geräusche in Sekunden: schlagen zwanzig
    /// Holzfäller im selben Augenblick zu, hört man ohnehin nur einen Schlag - es
    /// würde bloß lauter und matschiger.
    /// </summary>
    public const double MIN_GAP = 0.07;

    /// <summary>Lautstärke der Weltgeräusche; die Angriffswarnung bleibt voll.</summary>
    public const float VOLUME = 0.6f;

    /// <summary>Zufällige Verstimmung je Abspielen, in Oktaven (plus oder minus).</summary>
    public const float PITCH_SPREAD = 0.08f;

    /// <summary>Die Geräusche in der Benennung von klang.py.</summary>
    public static readonly string[] Names =
    {
        "axt", "spitzhacke", "hammer", "erde", "rascheln",
        "schwert", "treffer", "gebaeude", "bogen", "tod", "einsturz",
    };

    private readonly Dictionary<string, List<SoundEffect>> _effects = new();
    private readonly Dictionary<string, double> _lastAllowed = new();
    private readonly Random _random;

    public SoundBoard(Random random = null)
    {
        _random = random ?? new Random();
    }

    /// <summary>
    /// Lädt alle Geräusche.
    /// VERTRAG: für jeden Namen aus Names und n = 1 bis VARIANTS
    /// content.Load&lt;SoundEffect&gt;($"Sounds/{name}_{n}"); jede geladene Variante kommt in
    /// _effects[name] (die Liste anlegen, wenn sie fehlt). Wirft das Laden eine Exception
    /// (Datei fehlt, kein Audiogerät), fehlt nur diese Variante - weiter mit der nächsten.
    /// Rückgabe: Anzahl aller geladenen Varianten.
    /// </summary>
    public int Load(ContentManager content)
    {
        int count = 0;
        foreach (string name in Names)
        {
            for (int n = 1; n <= VARIANTS; n++)
            {
                try
                {
                    SoundEffect effect = content.Load<SoundEffect>($"Sounds/{name}_{n}");
                    if (!_effects.TryGetValue(name, out List<SoundEffect> variants))
                    {
                        variants = new List<SoundEffect>();
                        _effects[name] = variants;
                    }
                    variants.Add(effect);
                    count++;
                }
                catch (Exception)
                {
                    // Datei fehlt oder kein Audiogerät: diese Variante bleibt stumm.
                }
            }
        }
        return count;
    }

    /// <summary>
    /// VERTRAG: Anzahl der geladenen Varianten von name, 0 für einen unbekannten Namen.
    /// </summary>
    public int Count(string name)
        => _effects.TryGetValue(name, out List<SoundEffect> variants) ? variants.Count : 0;

    /// <summary>
    /// Die Drossel: darf name zur Spielzeit now (Sekunden) erklingen?
    /// VERTRAG: false, wenn name zuletzt vor weniger als MIN_GAP Sekunden erlaubt wurde
    /// (now - letzte Zeit &lt; MIN_GAP); sonst true, und now wird als letzte Zeit von name
    /// gemerkt (_lastAllowed). Jeder Name hat seine eigene Drossel; ein abgelehnter Aufruf
    /// ändert nichts.
    /// </summary>
    public bool Allow(string name, double now)
    {
        if (_lastAllowed.TryGetValue(name, out double last) && now - last < MIN_GAP)
        {
            // Zu früh: abgelehnt, die letzte Zeit bleibt unverändert.
            return false;
        }
        _lastAllowed[name] = now;
        return true;
    }

    /// <summary>
    /// Spielt name einmal ab.
    /// VERTRAG: false ohne Ton, wenn volume &lt;= 0 ist, keine Variante von name geladen ist
    /// oder Allow(name, now) false liefert - in dieser Reihenfolge, damit ein stummes
    /// Geräusch die Drossel nicht belegt. Sonst eine zufällige Variante (_random) per
    /// SoundEffect.Play(Lautstärke, Tonhöhe, Lage): Lautstärke = volume * VOLUME, auf 0 bis 1
    /// begrenzt; Tonhöhe zufällig zwischen -PITCH_SPREAD und PITCH_SPREAD; Lage = pan, auf
    /// -1 bis 1 begrenzt. Wirft das Abspielen (kein Audiogerät), false. Rückgabe: true, wenn
    /// abgespielt (das Ergebnis von SoundEffect.Play).
    /// </summary>
    public bool Play(string name, float volume, float pan, double now)
    {
        if (volume <= 0f)
        {
            return false;
        }
        if (!_effects.TryGetValue(name, out List<SoundEffect> variants) || variants.Count == 0)
        {
            return false;
        }
        if (!Allow(name, now))
        {
            return false;
        }

        float loudness = Math.Clamp(volume * VOLUME, 0f, 1f);
        float pitch = ((float)_random.NextDouble() * 2f - 1f) * PITCH_SPREAD;
        float position = Math.Clamp(pan, -1f, 1f);

        try
        {
            return variants[_random.Next(variants.Count)].Play(loudness, pitch, position);
        }
        catch (Exception)
        {
            // Kein Audiogerät: bleibt still.
            return false;
        }
    }
}
