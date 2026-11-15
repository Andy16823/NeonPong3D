# Neon Pong 3D

A small 3D Pong tech demo built with the GFX engine and Bullet physics.

## Features

- 3D playfield with a neon-inspired look and HUD
- Dynamic ball rigid body with impulse serves and collisions
- Kinematic player and CPU paddles
- CPU opponent with configurable aim error
- First to seven points wins
- IBM Plex Mono HUD font, distributed with the SIL Open Font License 1.1

## Requirements

- Windows
- .NET 8 SDK
- Access to a NuGet feed containing GFX `1.1.4` and GFX.BulletSharp `1.0.1`

## Build and run

```powershell
dotnet restore
dotnet run --project .\NeonPong3D.csproj
```

## Controls

- `W` / `S`: move the left paddle
- `Space`: reset the ball and serve again; the score is preserved
- `R`: start a new match after the game ends
- `Esc`: quit the game

## Project layout

- `PongGame.cs`: scene setup, game loop, HUD, and scoring
- `PongBehaviors.cs`: paddle and ball behaviors, plus shared gameplay tuning values
- `Resources/Fonts`: IBM Plex Mono Regular and its license
- `TODO.md`: planned engine and demo improvements

The font license is included at `Resources/Fonts/OFL.txt` and is copied to the build output.
