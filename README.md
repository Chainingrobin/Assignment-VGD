# Wulang — Elements of the Past

A single-player, first-person action-adventure prototype made for the German University in Cairo's Video Game Design course.

Wulang explores a world damaged by elemental corruption. Collectible books unlock Earth, Fire, Ice, and Lightning spells; the intended progression pairs regional book rewards with memories of her past. This assignment builds the exploration, interaction, spell, camera, and lighting foundation for that game.

**Author:** Youssef Yasser · **Student ID:** 58-3187  
**Repository:** https://github.com/Chainingrobin/Assignment-VGD  
**Gameplay video:** Link to be added after recording.

This is the **Assignment 3** prototype: the open world is split into a persistent World scene plus streamed story regions, with boss fights, a dialogue/objective/memory story layer, an intro cutscene, a final choice and two endings.

**The story.** The Vale's four elemental guardians went mad when someone began to put a price on what they gave freely. The Storm escaped and hid in an ordinary girl walking home in the rain. Wulang wakes at an old armory with lightning in her hands and no memory; Abe, the kindly armorer, says he raised her and sends her to put the guardians to rest, one element at a time. Every element she claims "returns" a memory, and every memory puts Abe at its centre, with one small thing wrong in it. In the Sanctum Abe asks for the elements, freely given, in exchange for a past worth having: kneel (the Gilded Past ending) or refuse, and Abe becomes Ganfaul, the Tallyman (the Freely Given ending, and her true, ordinary memory). Every line of the script lives in `Assets/Editor/StoryScript.cs`.

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

Pressing Play always starts at the main menu, as a build does. To play a region scene in `Assets/Scenes/Regions/` directly, untick **Wulang > Play from main menu**: the region then loads the shared World scene underneath and grants the story progress it assumes (elements, defeated guardians).

## Scenes and level streaming

| Build order | Scene | Contents |
|---|---|---|
| 0 | `Scenes/MainMenu.unity` | Continue (back to the start of the region last entered), New Game, chapter select in story order, New Game+ (after an ending), Quit |
| 1 | `Scenes/World.unity` | The whole terrain, sky, day/night, player, HUD, statue/lake freeze and landmark cinematics |
| 2 | `Scenes/Regions/Forest.unity` | Chapter I, *The Spark*: Abe's armory camp, training and field vampires, Earth Guardian (Maw J Laygo) |
| 3 | `Scenes/Regions/Ice.unity` | Chapter II, *What the Cold Keeps*: Ice Guardian (Mutant), Ice vampires, mountain-pass waystone |
| 4 | `Scenes/Regions/Volcano.unity` | Chapter III, *The Price of Fire*: Fire Guardian (Warrok) in the caldera, Fire vampires, rift |
| 5 | `Scenes/Regions/FinalArena.unity` | Chapter IV, *The Sanctum*: floating sanctum, Abe/Ganfaul, the offer and both endings |

The World stays loaded, so every region remains visible on the horizon. `RegionManager` streams region scenes additively: walking into the next region (only once its story flag is set) plays a landmark flight with the chapter title and recap while the region really loads and the previous one unloads. Locked regions push the player back with an elemental seal instead of a UI border; the unfrozen lake drowns (damage, then back to shore) and lava burns.

## Story progression

0. **Intro (New Game):** a narrated cutscene: drifting shots of the forest, the volcano and the Frozen Reach, a storm sky, the flight down to the armory, then black, Abe's voice and Wulang opening her eyes, then the chapter card. Space skips it.
1. **Forest:** wake with Lightning, meet Abe, follow the trail past two vampires to the **Blossom Glade**, defeat the **Earth Guardian** (weak to Lightning) and claim Earth. The ice statue then freezes the lake.
2. **Ice:** cross the frozen lake to the **Frozen Reach**, defeat the **Ice Guardian** in the **Frozen Amphitheater** with the Earth + Lightning **Storm Quake** combo, claim Ice, take the mountain-pass waystone.
3. **Volcano:** enter the caldera through the lava tube, defeat the **Fire Guardian** (weak to Ice), claim Fire, step into the rift.
4. **Final arena:** Abe offers Wulang her past in exchange for the elements. **Kneel** for the Submission ending, or **refuse**: a short reveal cutscene (the camera holds on Abe, the disguise burns away, Ganfaul roars) and the fight with **Ganfaul**, whose element changes every quarter of his health (only the current weakness hurts him), for the Truth ending.

Each region hides three **points of interest** off the main path, each with its own landmark, a small story told by its props, usually a guard pack, and a chest under a light beam (rose for a vitality relic, gold for supplies). Six reliquaries each raise maximum health by 10, and every chest adds a journal page that hints at who Abe really is. Forest: the runestone crown on the knoll, the book-bearer's camp, the blossom shrine by the pond. Frozen Reach: the frozen caravan, the crystal crown, the glacier gate. Volcano: the chain forge on the plateau (jump up the slabs on its west side), the ember shrine, the burnt watch post. Opened chests are story flags, so they stay looted after a restart or Continue.

Abe's horned shadow only appears at the last camp, by the volcano. Each element claim replays a **memory echo** (the world desaturates, the camera cuts to a framed shot of ghosts of Wulang and Abe acting it out). Abe sits by a camp fire in every region: F talks to him (that region's conversation, with gestures), then F trades. His camps are safe ground: vampires give up the chase there. Vampires of each biome's element roam in packs and drop Wubucks; coin piles, Wubucks bags and potions are scattered off the paths.

## Controls

| Input | Behaviour |
|---|---|
| WASD / Mouse / Left Shift / Space | Move, look, run, jump |
| Q / E | Previous / next unlocked element |
| Left mouse / Right mouse | Element projectile / element area spell |
| R | Reload projectile charges |
| F | Interact: talk to or trade with Abe, statue, waystone, rift, lore notes |
| H | Drink a healing potion |
| Tab | Satchel and journal (items, memories and lore) |
| Esc | Pause: Resume, Restart Region, Main Menu, Quit |
| Story Mode (main menu) | Halves the damage Wulang takes; remembered between sessions |
| Space / Enter / F / click | Advance dialogue; 1-9 or click to pick a choice |
| Space (during cutscenes) | Skip the intro, Ganfaul's reveal, a flight, the freeze reveal, a chapter caption or an ending card |

## Systems (Assignment 3)

| Area | Scripts |
|---|---|
| Story state | `Progress/StoryStateManager` (flags, persists across scenes), `FlagGate`, `RegionStart` |
| Level streaming | `Progress/RegionManager`, `RegionTravel`, `UI/TransitionOverlay` |
| Dialogue | `Story/DialogueData` (ScriptableObject per conversation), `DialogueManager` (logic, typewriter, choices), `DialogueUI` (view) |
| Objectives | `Story/ObjectiveManager` (HUD tracker), `RegionObjectives` (per-region list derived from flags) |
| Narrative beats | `Story/NarrativeTrigger` (zone / interact / start / flag, fires once), `MemoryEcho`, `IntroCutscene`, `EndingController`, the reveal in `BossEncounter` |
| Story script | `Editor/StoryScript` (every line of dialogue, journal page, boss line, recap and objective; **Wulang > Story > Apply story script** writes it into the assets and scenes) |
| Audio | `World/GameAudio` (one-shots by name; music and ambience that crossfade per region, boss fight, memory and ending), `World/Footsteps` |
| Bosses | `Bosses/BossBrain` (attack lists: melee, element projectiles, element ground attacks with warnings), `BossEncounter`, `BossProjectile`, `ElementPhases`, `UI/BossHealthBar` |
| Combat additions | `Combat/StormQuake` (Earth + Lightning combo), `CombatTarget` death event, stagger, status scaling, resistance |
| Player | `PlayerHealth` (HP, death and respawn), `Items/PlayerInventory` (potions on H, tomes), `ItemData`, `LootDrop`, `Merchant`, `World/HazardZone` |
| UI | `UI/MainMenu`, `PauseMenu`, `InventoryUI`, `ShopUI`, `Subtitle`, `UIKit` |
| Exploration | `World/TreasureChest` (chest rewards; vitality relics raise `PlayerHealth`'s maximum), `World/Spin` (the Sanctum's drifting rocks) |

### Rebuilding content (Editor menus)

| Menu | Builds |
|---|---|
| **Wulang > Scenes > Split world into regions** | One-off: turned `SampleScene` into `World` + region scenes + `MainMenu` and set the build order |
| **Wulang > Setup > Items, loot and hazards** | Item data, player inventory, vampire Wubucks drops, lake drowning |
| **Wulang > Bosses > Build bosses** | Imports Maw, Mutant, Warrok, Ganfaul and Abe as Humanoid, builds their animator controllers and boss prefabs in `Assets/Bosses` |
| **Wulang > Forest > Rebuild forest biome** | Hills, creek, pond, trails, zoned trees and the Blossom Glade arena (stone ring, cover rocks, shrine) |
| **Wulang > Snow > Rebuild snow biome** | Glacier ridges, terraces, frozen ponds, ice spires and the Frozen Amphitheater (ice pillars as cover) |
| **Wulang > Combat > Apply root motion and foot IK** | Gait-based locomotion, foot IK and smooth attack blends for vampires and bosses |
| **Wulang > Scenes > Populate regions** | Guardian arenas, Abe camps, field vampires, waystone and rift, lava hazards |
| **Wulang > Scenes > Build final arena** | The floating sanctum, element pillars and final encounter |
| **Wulang > Story > Lay story onto regions** | Dialogue, portraits, objectives, triggers, lore notes, memory echoes, endings (run after the arena builder) |
| **Wulang > Story > Apply story script** | Rewrites every line from `StoryScript` into the existing dialogue and journal assets and the five scenes without rebuilding anything; also the intro narration and footsteps |
| **Wulang > Bosses > Fix animation roots** | Bakes boss and Abe clip roots (no floating or body sliding) and grounds each rig's idle pose |
| **Wulang > Bosses > Abe sitting and gestures** | Abe's sitting idle and an additive upper-body Gestures Pack layer (the standing gestures add onto his seated arms instead of replacing them, so his hands stay off the ground); the standing variant for the Sanctum |
| **Wulang > Characters > Tune skin materials** | Per-element skin smoothness, specular maps where models have them, Maw's glow map |
| **Wulang > Forest > Dress the armory compound** | Fence, braziers, training ground, smithy and supply corner around the armory tower |
| **Wulang > Terrain > Add gentle ground variation** | Small undulation on open ground only (run after the biome builders, before Populate regions) |
| **Wulang > Cinematics > Build intro flight and tidy cutscenes** | The New Game intro flight (a slow descent onto the armory); no Ice fly-over (the freeze reveal shows it); no "view area" previews. Flights are timed by distance (`BiomeCinematicSetup.Paced`): a steady speed, eased at both ends, and the transitions dip to black around them instead of swinging the camera across the map |
| **Wulang > Story > Re-render portraits** | The three dialogue portraits only |
| **Wulang > World > Build landmarks, rewards and arenas** | Nine points of interest with chests, guards and journal pages; arena dressing for all four guardians; the snow's own palette (run last: after the biome builders, Populate regions and the story setup) |

Run the biome builders after **Wulang > Volcano > Rebuild volcano biome**: the volcano builder restores every terrain tree from its own snapshot. Each biome builder snapshots its region on first run (`Assets/Environment/Forest|Snow/* baseline.bytes`) and restores it before rebuilding. Rebuilding the boss prefabs replaces their components, so re-run **Populate regions**, **Build final arena** and the story setup afterwards.

Enemies move with **root motion**: the AI picks a gait (idle, walk, run) and a facing, and the animation carries the body (`Combat/RootMotionBody`), so feet stay planted during strides, leaps and swipes; foot IK keeps them on slopes.

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

Run **Wulang > Checks > Prototype structure (edit mode)** and, in Play mode, **Wulang > Checks > Combat and story rules (Play mode)** for the Assignment 3 systems. **Wulang > Checks > Full story path (Play mode)** (enter Play mode first) plays the whole game without input: the intro, every beat in order with the objective checked at each step, all four chapters, the refusal, reveal and Truth ending, then chapter select into the Sanctum and the Submission ending, then Continue. Read the outcome from `StoryPathCheck.Result`. `Assets/Editor/` also contains targeted checks for area visuals, cinematic wiring/playback, terrain, volcanic traversal, and character animation. These are Editor helper methods with individual Edit/Play mode requirements, rather than a single automated acceptance suite. Read each method's preconditions before invoking it. A complete verification pass should also include the demonstration route above and a launch test of the selected build target.

Keep `Assets`, `Packages`, `ProjectSettings`, and their `.meta` files in source control. Generated folders such as `Library`, `Temp`, `Logs`, and `Builds` are ignored. Large binary asset types use Git LFS through `.gitattributes`.


## Audio

Every sound plays by name from `Assets/Resources/Audio` through `GameAudio`. `Music/<region>` and `Ambience/<region>` crossfade in when a region loads (`RegionMood`); `Music/Boss` plays during guardian fights, `Music/Memory` under memory echoes, `Music/Truth` or `Music/Gilded` for the endings, and `Music/Menu` behind the menu and the intro. Effects cover spell casts and impacts (the AoE pack's own clips play from each effect), boss roars, swings, projectile whooshes, hurt, death, chests, coins, potions, element claims, the seal, thunder in the intro and the reveal swell. Footsteps and the landing thump are Starter Assets clips; the memory, death and victory stingers come from Paramnesia.

The music, ambience and most effects are **synthesised placeholders** made by `Tools/make_placeholder_audio.py` (numpy and scipy; the rain-song melody is the shared theme). To use a real recording, save it over the placeholder with the same name, or add more clips to a folder such as `Sfx/Footstep` for random variants.

The volcano route used to cross a 1 m strip of lava between the lava-tube walkway and the causeway, at (774, 8.7, 644.4); a causeway joint now covers it, and `VolcanoBiomeBuilder` starts the causeway at the walkway. The held-jump part of **Run volcano checks** still needs the Editor window focused.
