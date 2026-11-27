"""Abnahme AM1c/AM1e: der Angriffskreis auf der Minikarte (Nutzer 2026-11-27: „Wird man angegriffen
sollte auf der Minikarte angezeigt werden wo der Angriff stattfindet. Es muss nicht genau sein
und kann mit einem kleinen roten leicht durchsichtigem Kreis dargestellt werden“, AM1e: „darf etwas dicker sein der rote kreis und leicht pulsierend“).

Ob jeder Treffer seinen Ort meldet, prüft `tools/spielablauf -- angriffsort`; zeichnen kann der
Spielablauf nicht. Hier wird nur der Aufbau geprüft: beide Rümpfe sind gefüllt und nutzen,
was der VERTRAG nennt, und DrawMinimap ruft DrawAttackAlerts genau einmal - nach den
Einheitenpunkten, vor dem Kameraausschnitt. Wie es aussieht, zeigt das Foto.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

scheibe = methode(text, "AlertDiscTexture")
if scheibe is None:
    fehler.append("Methode AlertDiscTexture fehlt")
else:
    if "NotImplementedException" in scheibe:
        fehler.append("AlertDiscTexture: Rumpf ist noch leer (NotImplementedException)")
    for teil in ("_alertDisc", "ALERT_RADIUS", "ALERT_SUPERSAMPLE", "ALERT_RIM", "new Texture2D(graphicsDevice",
                 "SetData(", "Color.White", "0.7f"):
        if teil not in scheibe:
            fehler.append(f"AlertDiscTexture: „{teil}“ fehlt im Rumpf")

kreise = methode(text, "DrawAttackAlerts")
if kreise is None:
    fehler.append("Methode DrawAttackAlerts fehlt")
else:
    if "NotImplementedException" in kreise:
        fehler.append("DrawAttackAlerts: Rumpf ist noch leer (NotImplementedException)")
    for teil in ("_attackAlerts.Spots", "AttackAlerts.Alpha(", "AttackAlerts.Pulse(", "_gameSeconds", "MinimapPoint(",
                 "AlertDiscTexture()", "ALERT_RADIUS", "AlertColor", "ALERT_OPACITY", "SpriteEffects.None",
                 "continue"):
        if teil not in kreise:
            fehler.append(f"DrawAttackAlerts: „{teil}“ fehlt im Rumpf")

karte = methode(text, "DrawMinimap")
if karte is None:
    fehler.append("Methode DrawMinimap fehlt")
else:
    n = karte.count("DrawAttackAlerts(sb, r);")
    if n != 1:
        fehler.append(f"DrawMinimap ruft DrawAttackAlerts(sb, r) {n}-mal, erwartet einmal")
    else:
        i = karte.index("DrawAttackAlerts(sb, r);")
        einheiten = karte.find("foreach (var u in units)")
        rahmen = karte.find("DrawMinimapFrame(")
        if einheiten < 0 or i < einheiten:
            fehler.append("DrawAttackAlerts steht vor den Einheitenpunkten - der Kreis soll über ihnen liegen")
        if rahmen < 0 or i > rahmen:
            fehler.append("DrawAttackAlerts steht nach dem Kameraausschnitt - der Rahmen soll obenauf bleiben")

melde(fehler, "OK: AlertDiscTexture und DrawAttackAlerts gefüllt, DrawMinimap zeichnet die Angriffsorte "
              "nach den Einheiten und vor dem Kameraausschnitt")
