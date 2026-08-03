# IslandMapPrototype

Unity 2022.3.62f3 top-down vehicle survival prototype set on a tropical island.

## Open the project

1. Install Git LFS and run `git lfs install`.
2. Clone the repository and run `git lfs pull`.
3. Open the project with Unity `2022.3.62f3`.
4. Open `Assets/Scenes/IslandMap.unity`.

## Gameplay

- Survive while an automatically moving car is chased by enemy vehicles.
- Choose two active skills before starting the run.
- Enemy cars use Unity AI Navigation to calculate routes while Rigidbody physics drives them.
- Enemy collisions trigger explosions and chain reactions.
- Destroyed enemies drop experience gears and may drop health pickups.
- Leveling pauses the game and offers a one-time upgrade for an active skill.

## Controls

| Input | Action |
| --- | --- |
| `A` / `D` or arrow keys | Steer left or right |
| `Q` | Use the first active skill |
| `E` | Use the second active skill |
| Pause button | Pause or resume |
| `Esc` | Stop Play Mode in the Unity editor |

## Navigation

The baked vehicle NavMesh is stored beside the island scene. Roads, grass and beach are walkable; buildings, palm trees, street lights and barricades create exclusion areas with additional high-speed turning clearance.

After changing the environment layout, rebuild navigation from:

`Tools > Island Map > Bake Enemy Navigation`

Enemy cars do not use `NavMeshAgent` for movement. Navigation supplies route corners, and `NavMeshEnemyCarChaser` follows visible look-ahead points using Rigidbody velocity and rotation.

## AI sound effects

The editor can generate sound effects through the 302.AI ElevenLabs endpoint without storing an API key in the project.

1. Create a fresh 302.AI API key and store it in the Windows user environment:

   ```powershell
   [Environment]::SetEnvironmentVariable("AI302_API_KEY", "YOUR_NEW_KEY", "User")
   ```

2. Restart Unity so the editor can read the environment variable.
3. Open `Tools > Island Map > Generate Sound Effect (302.AI)`.
4. Generate the sound. The default output directory is `Assets/Audio/Generated`.

The generator calls `POST https://api.302.ai/elevenlabs/sound-generation`. It is editor-only so the API key is not included in player builds.

## Project structure

- `Assets/Scenes/IslandMap.unity`: main gameplay scene.
- `Assets/Scripts/Player`: player driving, progression, skills and HUD.
- `Assets/Scripts/Enemy`: enemy spawning, navigation driving and explosions.
- `Assets/Editor/EnemyNavigationBaker.cs`: AI Navigation configuration and bake command.
- `Packages/manifest.json`: Unity package dependencies, including AI Navigation.

