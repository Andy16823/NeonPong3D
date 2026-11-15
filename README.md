# Neon Pong 3D

Kleine 3D-Pong-Tech-Demo auf Basis von GFX 1.1.3.6 und Bullet-Physik.

## Starten

```powershell
dotnet restore
dotnet run --project .\NeonPong3D.csproj
```

Das Paket `GFX 1.1.3.6` wird aus dem lokalen Feed `C:\Users\andy1\Documents\NugetLocal` bezogen. Dieser Feed ist in der NuGet-Konfiguration der Entwicklungsumgebung registriert.

## Steuerung

- `W` / `S`: linker Spieler-Schläger
- `R`: Match nach Spielende neu starten
- `Esc`: Spiel schließen

## Aufbau

- `PongGame.cs`: Szene, Spielfeld, HUD, Spielzustand und Punktestand
- `PongBehaviors.cs`: Paddle- und Ball-Behaviors sowie zentrale Tuningwerte
- `TODO.md`: offene Engine- und Demo-Punkte
- `Resources/Fonts`: IBM Plex Mono Regular plus OFL-Lizenz


## Schriftart

Das HUD verwendet IBM Plex Mono Regular. Fontdatei und SIL Open Font License 1.1 liegen in `Resources/Fonts`; die Lizenz wird beim Build ebenfalls in den Ausgabeordner kopiert.

