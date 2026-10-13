# Editing the freeze reveal

In `SampleScene`, interacting with the Ice statue starts a six-second freeze. The camera follows the advancing edge across the full water plane from right to left, about 35 metres above the water. It returns to the player over 0.75 seconds after the freeze-finished event. Controls and HUD pause during the shot. It plays once per Play session.

## Open the Timeline

1. Select **FreezeRevealCutscene** in the Hierarchy.
2. Open **Window > Sequencing > Timeline**.
3. Use Play mode to preview the actual flight. Timeline preview shows camera blending; the flight itself follows runtime freeze progress.

The **Cinemachine reveal** track chooses the camera and controls the transitions. The controller advances Timeline manually using `WaterFreezer.Progress`, then plays the final blend only after `OnFreezeFinished`. Independent animation keys no longer move the drone.

## Adjust the shot

- Change **Duration** on **WaterFreezeController** to adjust both the freeze and flight speed. The camera automatically follows the same progress, so changing the duration does not require new animation keys.
- **Return Duration** on **FreezeRevealCutscene** controls the return after freezing. Keep the Timeline camera clip's ease-out duration equal to this value; both are currently 0.75 seconds.
- The drone keeps a fixed heading and stays behind the average freeze front. `FollowFreeze()` in the cutscene controller sets its height, setback and shoreline position. Its route derives from the current ice bounds.
- `FreezeDroneFlight.anim` is retained for reference but is no longer bound to the Timeline.

## Adjust the freezing edge

The ice plane stays at full size. `FreezingIce.mat` preserves the ice textures, normal/height maps and lighting. The `Game/FreezingIce` shader reveals them with an irregular, feathered front sweeping right to left. **Edge Softness** controls feathering. The material's **Freeze Progress** previews coverage in Edit mode; Play mode drives it automatically. The ice collider enables when freezing completes.

The reveal is configured for the current Unity Plane mesh and its UV orientation. A different mesh layout needs corresponding UV mapping changes.

## How the cameras connect

**Main Camera** renders the game. Its **Cinemachine Brain** applies the selected view. **PlayerCameraRoot > PlayerViewCamera** supplies the normal first-person view, and **FreezeRevealCutscene > DroneCamera** supplies the freeze-following view. Cinemachine cameras do not need their own Unity Camera or AudioListener components.

The cutscene controller listens to `WaterFreezer.OnFreezeStarted`. It stores and restores each control and Canvas's enabled state, clears buffered movement/look input, and also restores control if the Timeline is stopped early.

The path follows the current water plane's world X direction. Rotating the river or changing its mesh layout requires corresponding camera and UV mapping changes. Day/night lighting continues throughout the cutscene.

## Validation performed

Play mode checks covered camera alignment with freeze progress, statue triggering, paused inputs/HUD, return timing after the freeze-finished event, camera restoration and repeated-interaction prevention. Daylight and nighttime flights were inspected. A standalone build was not run.
