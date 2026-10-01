"""Abnahme C2c: Haeuser lassen sich setzen - abgeloest durch c5b_bauen.py und c5c_grafik.py.

C2c fuehrte einen Sonderweg nur fuer Haeuser ein (Taste H, sofort fertig). Seit C5b
ist das Haus ein Eintrag im allgemeinen Baumenue und entsteht als Baustelle. Die
Eigenschaften von C2c pruefen jetzt andere: Taste H, Vorschau und Platzregeln
c5b_bauen.py, die eigene Grafik c5c_grafik.py, Kosten (25 Holz) und Groesse (2x2)
die xUnit-Tests in ConstructionTests.cs. Die erste Fassung pruefte die Bezeichner
des Sonderwegs (placingHouse, HOUSE_SIZE ...) und schlug deshalb seit C5b an.
"""

import subprocess
import sys
from pathlib import Path

hier = Path(__file__).resolve().parent
codes = [subprocess.call([sys.executable, str(hier / name)]) for name in ("c5b_bauen.py", "c5c_grafik.py")]
raise SystemExit(max(codes))
