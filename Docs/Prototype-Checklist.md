# Assignment 2 prototype checklist

Reassessed: 14 October 2026, after biome previews, nighttime freeze effects and terrain-following AoE lighting.
Source: `D:/GUC/Video Game Design/assignment 2.pdf`, Part 3, pages 2–3. The examples are references, not mandatory features.

## Current coverage

| Category | Minimum | Implemented behavior types | Remaining integrations |
| --- | ---: | ---: | ---: |
| Player-triggered world-changing actions | 5 | 5 | 0 |
| Camera behaviors using Cinemachine | 3 | 3 | 0 |
| Lighting behaviors | 3 | 3 | 0 |

**Current implementation coverage: 11/11 minimum slots. No additional lighting behavior is needed for the stated minimum.** This is an implementation assessment, not a grading guarantee; demonstrate each behavior in the submitted build.

## Player actions

- [x] Collect an elemental book: it disappears and unlocks an element with HUD feedback. Different books count as one action type.
- [x] Collect a coin: it disappears and updates the wallet/HUD.
- [x] Activate the ice statue with F: the river visibly freezes and becomes traversable.
- [x] Cast a projectile: it travels, lights nearby surfaces and produces collision effects.
- [x] Cast an AoE: the selected elemental effect appears and pushes nearby dynamic props. Show this near a barrel, chair or dummy.
- [x] Extra evidence: moving into physical props pushes them through BasicRigidBodyPush.
- [x] Extra interaction: look toward an eligible biome and press F to start its one-time preview. Conservatively count both previews as one interaction type, and do not rely on a camera-only event as a world-changing action.

No active scene component implements IDamageable. Damage hooks do not establish working enemy damage or destruction. An elemental puzzle consequence such as burning vines or powering a switch would strengthen the action evidence, but is optional for the current count.

## Camera behaviors

- [x] First-person gameplay: PlayerViewCamera with the Main Camera CinemachineBrain.
- [x] Freeze-progress tracking reveal: the drone follows the advancing freeze front; Timeline/Cinemachine handles the view transition and return.
- [x] Player-requested biome reconnaissance: a timed aerial flight approaches the volcano entrance or frames the ice statue, then returns to gameplay. The paths use Timeline animation and Cinemachine tracks/blending; prompts require look direction and proximity, and each preview is limited to once per session.

The volcano and ice previews are **one camera behavior type**, not two. Returning to first-person is part of the transition, not another type. The third type is defensible because the optional authored sightseeing flight has a different trigger, purpose and motion from the event-progress tracking reveal.

The freeze camera's motion still uses custom transform tracking inside FreezeRevealCutscene; Cinemachine handles its blending. If the marker interprets “using Cinemachine” as requiring native Cinemachine motion components for every behavior, convert that tracking to native camera/path controls. This is a compliance clarification to discuss or strengthen, not an extra lighting requirement.

## Lighting behaviors

- [x] **Time-driven environment lighting:** the enabled DayNightCycle blends sunlight, moonlight, ambient light and cartoon sky colors/stars. Sun/moon overlap keeps twilight illuminated.
- [x] **Player-triggered spell lighting:** elemental projectiles carry lights. Each live meteor carries a point light with a 20 m range; ice/earth particle lights spread across the effect with a 16 m range and follow particle lifetime. Dense crystal effects sample up to 24 lights to limit cost. Different elements and individual meteors are variations of this single behavior.
- [x] **World-event lighting:** activating the statue starts a moving blue freeze-front light, luminous advancing ice and crystal particles, making the freezing sequence visible at night. This is a response to a puzzle/world event, distinct from the time cycle and transient player spells.

**Lighting still needed: 0 of 3.** Static lava glow, separate sun/moon objects, and different spell colors are useful presentation, but are not counted as additional behavior types. Cutscenes themselves contribute camera behavior; the freeze illumination contributes lighting because it actually changes the scene's illumination.

## Remaining verification and optional polish

- [ ] Demonstrate all five actions in the built game, including AoE movement of a physical prop and the traversable frozen river.
- [ ] Show first-person play, freeze tracking and an F-triggered biome preview with its automatic return.
- [ ] Show dusk/night continuity, moving spell lights and the statue-triggered freeze illumination.
- [ ] Profile simultaneous AoE casts on the target machine: distributed point lights cost more than the previous single light.
- [ ] Inspect very sharp cliffs and steps: ground layers are subdivided and projected onto cached static surface samples, which can soften abrupt edges. Moving platforms are outside the current terrain-fitting scope.
- [ ] Optional: replace the freeze camera's custom tracking with native Cinemachine motion if required by the marker.
- [ ] Optional: add one persistent elemental puzzle consequence to make the world-changing action evidence stronger.

## Checked evidence

- SampleScene assignments, two biome previews and their Cinemachine/Timeline tracks, enabled day/night, cartoon sky and assigned freeze effects passed BiomeCinematicChecks.RunScene.
- Earlier checks covered camera control restoration, once-per-session gating, look/range prompts and 240 day/night lighting samples; daytime/nighttime cinematics were rendered for review.
- AoEVisualChecks covers sloped and raised ground, overhead ceiling rejection, subdivided textured ground meshes, distributed ice lights, moving per-meteor lights, particle-light expiry and unchanged projectile lighting.
- World- and local-space particles were checked, an ice-on-slope preview was rendered and inspected, all four scene AoEs were cast through SpellCaster, and completed-effect/light cleanup passed in play mode.
- Runtime changes: SpellCaster, TerrainAoE and SpellElementLight. Existing damage/push hitbox behavior is retained.

No standalone build or full hardware performance pass has been performed for this update. The written assignment's separate 30-object and 20-mechanic requirements are not assessed here.
