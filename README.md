# Wulang — Elements of the Past

A single-player, first-person action-adventure prototype made for the German University in Cairo's Video Game Design course.

Wulang explores a world damaged by elemental corruption. Collectible books unlock Earth, Fire, Ice, and Lightning spells; the intended progression pairs regional book rewards with memories of her past. This assignment builds the exploration, interaction, spell, camera, and lighting foundation for that game.

**Author:** Youssef Yasser · **Student ID:** 58-3187  
**Repository:** https://github.com/Chainingrobin/Assignment-VGD  
**Gameplay video:** Link to be added after recording.

## Quick start

1. Install **Unity 6000.3.24f1** through Unity Hub. The exact version is recorded in `ProjectSettings/ProjectVersion.txt`.
2. Install Git and Git LFS, then clone the project:

   ```sh
   git lfs install
   git clone https://github.com/Chainingrobin/Assignment-VGD.git
   cd Assignment-VGD
   git lfs pull
   ```

3. In Unity Hub, choose **Add project from disk** and select the repository root containing `Assets`, `Packages`, and `ProjectSettings`.
4. Open it with the specified editor and allow the initial asset import and Package Manager resolution to finish.
5. Open **`Assets/Scenes/SampleScene.unity`** and press **Play**. Click the Game view to control the player.

The repository contains imported asset demonstration scenes. `Assets/Scenes/SampleScene.unity` is the game scene and the enabled scene in Build Settings.

## Controls

| Input | Current behavior |
|---|---|
| WASD | Move |
| Mouse | Look and aim |
| Left Shift | Run |
| Space | Jump |
| Q / E | Select the previous / next unlocked element |
| Left mouse | Cast the selected elemental projectile |
| Right mouse | Cast the selected elemental area spell on valid ground |
| F | Activate the outlined statue or request an eligible landmark preview |
| Touch a book | Collect it and unlock its element |
| Touch a coin | Collect it and increase the wallet |

`R` reload, potion use, and trading belong to the planned resource systems and currently have no gameplay binding. Casting is currently unrestricted by charges.

## Suggested demonstration

1. Explore the lowlands and armory. The prototype begins with **Fire** available.
2. Collect the books beside the armory. Watch the unlock notification, then use Q/E to change the active element shown on the HUD.
3. Collect the nearby Yeopjeon coin; the wallet increases by one.
4. Try both spell modes. Projectiles travel and produce impact effects; area spells create elemental effects and an impulse on eligible physical objects. Ice and Earth area effects follow uneven ground.
5. Approach the statue at the water crossing. Aim at it within interaction range until the **blue outline** and **Press F** prompt appear, then press F.
6. Watch the freeze-front camera sequence. After the six-second freeze completes and control returns, walk across the enabled ice surface.
7. Look toward the ice-region or volcanic landmark from a valid position. Press F when the area-preview prompt appears to play its Cinemachine/Timeline sequence.
8. Observe spell illumination, the moving freeze light, and the changing sun/moon lighting. The current scene's full day/night cycle lasts **three real minutes**.

The statue and each landmark preview are single-use per Play session. Stop and restart Play mode to repeat them and reset collected books and coins.

## Development status

| Area | Current prototype |
|---|---|
| World | Connected lowlands, armory, water crossing, ice region, and volcanic mountain/cavern |
| Movement | First-person walking, running, jumping, and traversal of reachable slopes |
| Books and HUD | Four book pickups, affinity unlocking/switching, unlock notification, and active-element display |
| Currency | Coin collection and wallet display |
| Spells | Four elemental projectile and area-effect variants, collision/impact effects, local illumination, and area physics impulse |
| Ice crossing | Statue targeting, outline/prompt, progressive freezing, particles, moving light, and collider activation |
| Cameras | First-person play, freeze-front tracking, and two regional preview sequences |
| Lighting | Day/night cycle, spell lighting, freeze lighting, and static volcanic accents |

Enemy encounters, spawning, elemental resistance/status effects, bosses, health, healing, spell charges, charge pickups, vendor services, and memory playback are planned development milestones. The vampire in the scene is an enemy visual reference. The spells provide damage hooks through `IDamageable`; they do not establish a complete enemy combat system.

The design provisionally starts Wulang with Earth, while this demonstration scene starts with Fire and places all four books beside the armory. Regional boss drops and final book placement come with the progression work. Water drowning and lava damage are design rules awaiting health/hazard integration.

## Project map

| Location | Purpose |
|---|---|
| `Assets/Scenes/SampleScene.unity` | Main playable scene |
| `Assets/Scripts/Player Scripts/` | Affinities, books, coins, spells, HUD, and notifications |
| `Assets/Scripts/Interaction System/` | Raycast targeting, interaction prompts, and outlines |
| `Assets/Scripts/Events/` | Water freezing, camera playback, regional previews, day/night lighting, and lava flow |
| `Assets/Starter Assets/Runtime/FirstPersonController/` | First-person controller |
| `Assets/Starter Assets/Runtime/ThirdPersonController/Scripts/CharacterAnimationController.cs` | Character animation support used by the project |
| `Assets/Scripts/PlayerControls.inputactions` | Element switching, spell casting, and interaction bindings |
| `Assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions` | Movement and look bindings |
| `Assets/Cutscenes/` | Cinematic assets |
| `Assets/Resources/` and `Assets/Shaders/` | Runtime-loaded resources and custom shaders, including interaction/freezing support |
| `Assets/Editor/` | Scene setup, preview, and targeted verification helpers |
| `Packages/manifest.json` | Unity package dependencies |
| `ProjectSettings/` | Editor, input, rendering, and build configuration |

### Main systems

- **Collection and affinity:** `BookCollect` unlocks elements through `PlayerMagicAffinity`. `ElementSwitchInput` selects an unlocked element; HUD/notification components respond to its events. `CoinCollect` updates `PlayerCoins`.
- **Interaction:** `PlayerInteract` checks the camera ray and manages the prompt. `Interactable` creates the blue outline and invokes the statue's configured event. `BiomePreview` checks landmark distance, viewing angle, and visibility.
- **Spells:** `SpellCastInput` calls `SpellCaster`. `SpellProjectile` handles projectile collision and expiry; `AoESpellHitbox` applies one hit per target and pushes eligible rigidbodies. `TerrainAoE` fits Ice/Earth visuals to ground; `SpellElementLight` supplies illumination.
- **Freeze reveal:** `WaterFreezer` advances the ice surface and enables its collider at completion. `FreezeRevealCutscene` coordinates the camera sequence, temporarily suspends controls/HUD, and restores them afterward.
- **Environment:** `DayNightCycle` updates sun, moon, ambient illumination, sky, fog color, and reflections. `LavaFlow` animates volcanic surfaces.

## Dependencies and builds

The project uses **URP 17.3.0**, **Cinemachine 3.1.7**, **Input System 1.20.0**, **Timeline 1.8.13**, and Starter Assets. Package Manager resolves the complete dependency list from `Packages/manifest.json` and `Packages/packages-lock.json`.

For a local build, open **File → Build Profiles**, select the target platform with its editor module installed, confirm that `Assets/Scenes/SampleScene.unity` is enabled, and build into `Builds/`. Keyboard and mouse are the documented controls; other control schemes and target platforms need their own validation.

Imported environment and effects include Idyllic Fantasy Nature, Stylized Fantasy Armory, Stylized Magic Books, PBR Statues, Yeopjeon, Stylized Rock Pack, Unique Projectiles Volume 1, AOE Magic Spells Vol.1, and Cartoon Sky. Third-party assets retain their respective licenses and attribution files; this README does not assign them a new license.

## Verification and troubleshooting

- **Missing models, textures, or tiny placeholder files:** run `git lfs pull`, then let Unity finish importing. Preserve `.meta` files so asset references remain intact.
- **Controls do not respond:** click the Game view. During a cinematic, controls and HUD are intentionally suspended until playback finishes.
- **No statue prompt:** aim at the unused statue within the configured 3 m raycast range. Restart Play mode if it has already been activated.
- **No landmark prompt:** move within range, face the landmark, and check that unrelated geometry is not blocking the view. Each preview plays once per session.
- **No visible time progression while working in another app:** focus the Game view; the project is not configured to run in the background.
- **Import or compilation errors:** check the Console, editor version, and Package Manager resolution before testing the scene.

`Assets/Editor/` contains targeted checks for area visuals, cinematic wiring/playback, terrain, volcanic traversal, and character animation. These are Editor helper methods with individual Edit/Play mode requirements, rather than a single automated acceptance suite. Read each method's preconditions before invoking it. A complete verification pass should also include the demonstration route above and a launch test of the selected build target.

Keep `Assets`, `Packages`, `ProjectSettings`, and their `.meta` files in source control. Generated folders such as `Library`, `Temp`, `Logs`, and `Builds` are ignored. Large binary asset types use Git LFS through `.gitattributes`.
