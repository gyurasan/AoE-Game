# Age of Evolutions – Copilot Instructions

- Projekt: Age-of-Empires-I-artiges RTS, C#, MonoGame, .NET 10.
- src/AoE.Core enthält die Spiellogik und darf KEINE MonoGame-Abhängigkeit bekommen – das ist die Bedingung dafür, dass sie testbar bleibt.
- Die Spielspezifikation mit den verbindlichen Regeln (vier Ressourcen, Schadensformel mit Angriffs- und Rüstungsklassen, Zeitalter, Fog of War) steht in docs/AgeOfEmpires.md.
- Vor dem Abschluss einer Änderung bauen und testen: `dotnet build` auf AgeOfEvolutions/AgeOfEvolutions.DesktopGL und `dotnet test` auf tests/AoE.Tests.
- Offene Punkte stehen in TODO.md.
