"""Abnahme C5d: Erbauer ohne Sammelziel bauen an der naechsten unfertigen Baustelle weiter.

Das Verhalten prueft tools/spielablauf (Gruppe weiterbauen, eigener verify-Schritt).
Hier: die Suche beschraenkt sich auf eigene, unfertige Baustellen im Umkreis, und
Lager-Erbauer sammeln weiterhin zuerst.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

fertig = methode(text, "FinishConstruction") or ""
for name in ("NearestUnfinishedSite(", "AssignBuilder(", "new GatherJob("):
    if name not in fertig:
        fehler.append(f"FinishConstruction: {name[:-1]} fehlt")
i_sammeln, i_bauen = fertig.find("new GatherJob("), fertig.find("AssignBuilder(")
if i_sammeln >= 0 and i_bauen >= 0 and i_bauen < i_sammeln:
    fehler.append("FinishConstruction: Lager-Erbauer sollen zuerst sammeln, dann erst weiterbauen")

suche = methode(text, "NearestUnfinishedSite")
if suche is None:
    fehler.append("Methode NearestUnfinishedSite fehlt")
else:
    for muster, was in ((r"\.IsComplete\b", "nur unfertige Baustellen"),
                        (r"\.OwnerId\b", "nur eigene Baustellen"),
                        (r"SITE_SEARCH_RADIUS", "nur im Umkreis"),
                        (r"\bb\s*==\s*from\b|\bfrom\s*==\s*b\b", "nicht die eben fertig gewordene")):
        if not re.search(muster, suche):
            fehler.append(f"NearestUnfinishedSite: {was} ({muster}) fehlt")
if not re.search(r"const\s+int\s+SITE_SEARCH_RADIUS\s*=\s*(8|GatherJob\.SEARCH_RADIUS)\s*;", text):
    fehler.append("SITE_SEARCH_RADIUS ist nicht 8 Kacheln")

melde(fehler, "C5d erfuellt: Erbauer bauen an der naechsten unfertigen Baustelle weiter")
