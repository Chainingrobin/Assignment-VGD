# Wulang — Elements of the Past

A single-player, first-person action-adventure prototype made for the German University in Cairo's Video Game Design course.

Wulang explores a world damaged by elemental corruption. Collectible books unlock Earth, Fire, Ice, and Lightning spells; the intended progression pairs regional book rewards with memories of her past. This assignment builds the exploration, interaction, spell, camera, and lighting foundation for that game.

**Author:** Youssef Yasser · **Student ID:** 58-3187  
**Repository:** https://github.com/Chainingrobin/Assignment-VGD  
**Gameplay video:** Link to be added after recording.

This branch is the **Assignment 3** prototype: the open world is split into a persistent World scene plus streamed story regions, with boss fights, a dialogue/objective/memory story layer, a final choice and two endings. All story text is a `[placeholder]` for the narrative pass.

## Quick start

1. Install **Unity 6000.3.24f1** through Unity Hub. The exact version is recorded in `ProjectSettings/ProjectVersion.txt`.
2. Install Git and Git LFS, then clone the project:

   ```sh
   git lfs install
   git clone https://github.com/Chainingrobin/Assignment-VGD.git
   cd Assignment-VGD
   git lfs pull
   ```

3. In Unity Hub, choose **Add project from disk**, select the repository root, and open it with the specified editor.
4. Open **`Assets/Scenes/MainMenu.unity`** and press **Play**, then choose **New Game** (or **Chapters** to start at any region).

Any region scene in `Assets/Scenes/Regions/` can also be played directly: it loads the shared World scene underneath and grants the story progress that region assumes (elements, defeated guardians).

## Scenes and level streaming

| Build order | Scene | Contents |
|---|---|---|
| 0 | `Scenes/MainMenu.unity` | New Game, chapter select in story order, New Game+ (after an ending), Quit |
| 1 | `Scenes/World.unity` | The whole terrain, sky, day/night, player, HUD, statue/lake freeze and landmark cinematics |
| 2 | `Scenes/Regions/Forest.unity` | Chapter I: Abe's armory camp, training and field vampires, Earth Guardian (Maw J Laygo) |
| 3 | `Scenes/Regions/Ice.unity` | Chapter II: Ice Guardian (Mutant), Ice vampires, mountain-pass waystone |
| 4 | `Scenes/Regions/Volcano.unity` | Chapter III: Fire Guardian (Warrok) in the caldera, Fire vampires, rift |
| 5 | `Scenes/Regions/FinalArena.unity` | Chapter IV: floating sanctum, Abe/Ganfaul, the offer and both endings |

The World stays loaded, so every region remains visible on the horizon. `RegionManager` streams region scenes additively: walking into the next region (only once its story flag is set) plays a landmark flight with the chapter title and recap while the region really loads and the previous one unloads. Locked regions push the player back with an elemental seal instead of a UI border; the unfrozen lake drowns (damage, then back to shore) and lava burns.

## Story progression

1. **Forest:** wake with Lightning, meet Abe, defeat the **Earth Guardian** (weak to Lightning) and claim Earth. The ice statue then freezes the lake.
2. **Ice:** cross the frozen lake, defeat the **Ice Guardian** with the Earth + Lightning **Storm Quake** combo, claim Ice, take the mountain-pass waystone.
3. **Volcano:** enter the caldera through the lava tube, defeat the **Fire Guardian** (weak to Ice), claim Fire, step into the rift.
4. **Final arena:** Abe offers Wulang her past in exchange for the elements. **Kneel** for the Submission ending, or **refuse** to fight **Ganfaul**, whose element changes every quarter of his health (only the current weakness hurts him), for the Truth ending.

Each element claim replays a **memory echo** (the world desaturates and ghosts of Wulang and Abe act it out). Vampires drop Wubucks; Abe sells healing potions at a camp in every region.

## Controls

| Input | Behaviour |
|---|---|
| WASD / Mouse / Left Shift / Space | Move, look, run, jump |
| Q / E | Previous / next unlocked element |
| Left mouse / Right mouse | Element projectile / element area spell |
| R | Reload projectile charges |
| F | Interact: Abe's shop, statue, waystone, rift, lore notes, landmark previews |
| H | Drink a healing potion |
| Tab | Satchel and journal (items, memories and lore) |
| Esc | Pause: Resume, Restart Region, Main Menu, Quit |
| Space / Enter / F / click | Advance dialogue; 1-9 or click to pick a choice |

## Systems (Assignment 3)

| Area | Scripts |
|---|---|
| Story state | `Progress/StoryStateManager` (flags, persists across scenes), `FlagGate`, `RegionStart` |
| Level streaming | `Progress/RegionManager`, `RegionTravel`, `UI/TransitionOverlay` |
| Dialogue | `Story/DialogueData` (ScriptableObject per conversation), `DialogueManager` (logic, typewriter, choices), `DialogueUI` (view) |
| Objectives | `Story/ObjectiveManager` (HUD tracker), `RegionObjectives` (per-region list derived from flags) |
| Narrative beats | `Story/NarrativeTrigger` (zone / interact / start / flag, fires once), `MemoryEcho`, `EndingController` |
| Bosses | `Bosses/BossBrain` (attack lists: melee, element projectiles, element ground attacks with warnings), `BossEncounter`, `BossProjectile`, `ElementPhases`, `UI/BossHealthBar` |
| Combat additions | `Combat/StormQuake` (Earth + Lightning combo), `CombatTarget` death event, stagger, status scaling, resistance |
| Player | `PlayerHealth` (HP, death and respawn), `Items/PlayerInventory` (potions on H, tomes), `ItemData`, `LootDrop`, `Merchant`, `World/HazardZone` |
| UI | `UI/MainMenu`, `PauseMenu`, `InventoryUI`, `ShopUI`, `Subtitle`, `UIKit` |

### Rebuilding content (Editor menus)

| Menu | Builds |
|---|---|
| **Wulang > Scenes > Split world into regions** | One-off: turned `SampleScene` into `World` + region scenes + `MainMenu` and set the build order |
| **Wulang > Setup > Items, loot and hazards** | Item data, player inventory, vampire Wubucks drops, lake drowning |
| **Wulang > Bosses > Build bosses** | Imports Maw, Mutant, Warrok, Ganfaul and Abe as Humanoid, builds their animator controllers and boss prefabs in `Assets/Bosses` |
| **Wulang > Scenes > Populate regions** | Guardian arenas, Abe camps, field vampires, waystone and rift, lava hazards |
| **Wulang > Scenes > Build final arena** | The floating sanctum, element pillars and final encounter |
| **Wulang > Story > Lay placeholder story onto regions** | Dialogue, portraits, objectives, triggers, lore notes, memory echoes, endings (run after the arena builder) |

Boss animations come from the Mixamo Creature Pack (via the Paramnesia project) and the Mixamo Pro Magic Pack; Abe's talk clip is Kevin Iglesias' Human Basic Motions.

## Volcano biome

The volcano east of the armory is baked editable scene content under `Redwood Lava Cavern`, built by **Wulang > Volcano > Rebuild volcano biome** (`VolcanoBiomeBuilder`).

- **Lava fields:** a heat field of lava-flow lobes spreads from the cone, so the biome has an organic edge rather than a ring. Flat-topped, dark basalt flow tongues with walkable fronts cross lighter ash. Two lava rivers run in carved channels into a crusted lava lake. Three cinder cones stand in the field, the largest with a lava-filled breach, alongside a basalt-columned mesa, glowing fissures, basalt spires, steaming fumaroles, drifting ash and embers.
- **Burnt forest edge:** the forest thins toward the flows, then turns into charred, leafless trees with faint ember-lit bark, and is gone on the hot ground.
- **Volcanic highlands:** the needle ranges behind the cone are smoothed into ridged massifs, with two smoking stratovolcanoes whose lava flows run down their gullies. Their cliffs use basalt-column paint.
- **Volcano:** a 100 m stratovolcano with an open, smoking crater. A lava cascade spills from a rim notch down its south-west flank into the flank river.
- **Lava tube:** the west entrance is a 50 m tube with a walkway beside the lava that drains out of the caldera and becomes the outer river.
- **Boss caldera:** a 30 m raised island of cracked stone sits in a lava moat, reached by a causeway from the tube, with four broken basalt pillars for cover. A wide lavafall pours from a vent opposite the entrance, open to the sky above. `Boss caldera/Boss spawn point` faces the entrance and `Boss arena bounds` is a trigger covering the island, both ready for the boss encounter.

Rebuilding is repeatable: the first build stores the untouched terrain region, paint, grass and trees in `Assets/Environment/Volcano/Terrain baseline v2.bytes`, and every build sculpts from it. If you ever re-run `MountainLandscapeBuilder`, run the volcano rebuild afterwards.

**Wulang > Cinematics > Author smooth cinematics** rebuilds both landmark previews as dense, banking drone flights with lens moves and mild handheld noise, checking that neither camera path clips terrain or rock. The freeze reveal is a crane move driven by the freeze's progress: it waits by the statue until the ice reaches the channel, chases the front, then rises for a held wide shot of the frozen river. Run **Wulang > Volcano > Run volcano checks (Edit mode)** with the Editor focused; it walks the route from the lava fields into the arena, checks the player capsule fits, confirms a player can climb out of every lava surface, and repeats the held-jump test on the volcano floors.

## Dependencies and builds

The project uses **URP 17.3.0**, **Cinemachine 3.1.7**, **Input System 1.20.0**, **Timeline 1.8.13**, and Starter Assets. Package Manager resolves the complete dependency list from `Packages/manifest.json` and `Packages/packages-lock.json`.

For a local build, open **File → Build Profiles**, select the target platform with its editor module installed, confirm the six scenes listed above are enabled in that order, and build into `Builds/`. Keyboard and mouse are the documented controls; other control schemes and target platforms need their own validation.

Imported environment and effects include Idyllic Fantasy Nature, Stylized Fantasy Armory, Stylized Magic Books, PBR Statues, Yeopjeon, Stylized Rock Pack, Unique Projectiles Volume 1, AOE Magic Spells Vol.1, and Cartoon Sky. Third-party assets retain their respective licenses and attribution files; this README does not assign them a new license.

## Verification and troubleshooting

- **Terrain fails to load after cloning ("Unknown error occurred while loading"):** older commits stored the binary terrain assets as text, and Git stripped bytes from them. `.gitattributes` now marks them binary; commit the intact local copies so the repository holds working terrain.
- **Missing models, textures, or tiny placeholder files:** run `git lfs pull`, then let Unity finish importing. Preserve `.meta` files so asset references remain intact.
- **Controls do not respond:** click the Game view. During a cinematic, controls and HUD are intentionally suspended until playback finishes.
- **No statue prompt:** the statue only answers after the Earth Guardian is defeated; then aim at it within 3 m.
- **No landmark prompt:** move within range, face the landmark, and check that unrelated geometry is not blocking the view. Each preview plays once per session.
- **No visible time progression while working in another app:** focus the Game view; the project is not configured to run in the background.
- **Import or compilation errors:** check the Console, editor version, and Package Manager resolution before testing the scene.

Run **Wulang > Checks > Prototype structure (edit mode)** and, in Play mode, **Wulang > Checks > Combat and story rules (Play mode)** for the Assignment 3 systems. `Assets/Editor/` also contains targeted checks for area visuals, cinematic wiring/playback, terrain, volcanic traversal, and character animation. These are Editor helper methods with individual Edit/Play mode requirements, rather than a single automated acceptance suite. Read each method's preconditions before invoking it. A complete verification pass should also include the demonstration route above and a launch test of the selected build target.

Keep `Assets`, `Packages`, `ProjectSettings`, and their `.meta` files in source control. Generated folders such as `Library`, `Temp`, `Logs`, and `Builds` are ignored. Large binary asset types use Git LFS through `.gitattributes`.


Known issue: **Wulang > Volcano > Run volcano checks** reports that the walking route crosses lava at (774, 8.7, 644.4), just past the lava-tube walkway. This predates the Assignment 3 branch (it fails on the original `SampleScene` too).
