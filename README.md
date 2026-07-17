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

## Project structure

- `Assets/Scenes/IslandMap.unity`: main gameplay scene.
- `Assets/Scripts/Player`: player driving, progression, skills and HUD.
- `Assets/Scripts/Enemy`: enemy spawning, navigation driving and explosions.
- `Assets/Editor/EnemyNavigationBaker.cs`: AI Navigation configuration and bake command.
- `Packages/manifest.json`: Unity package dependencies, including AI Navigation.

