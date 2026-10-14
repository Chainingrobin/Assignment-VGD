# Mountain landscape update

The playable scene now uses `Assets/Environment/Mountains/Refined mountain terrain.asset` with its own paint textures.

- Background heights have ridge variation, erosion channels and small rock detail; the original large mountain layout is retained.
- Default mountains use realistic weathered rock and scree textures, with slope-based painting and embedded LOD cliff prefabs.
- The ice range uses glacial faces, altitude/slope snow coverage and snow meshes copied directly from upper crag surfaces.
- The volcanic range uses dark dry-lava basalt, nine narrow terrain-fitted lava fissures, animated lava materials and local orange light.
- There are 54 cliff outcrops and 14 fitted snow crowns. High, steep slopes shed inappropriate tree placements; low-ground vegetation remains.
- Changes are authored into normal Unity terrain, assets and prefab instances; no mountain-generation code runs during gameplay.

`MountainLandscapeChecks.Run()` passed height preservation for 150,560 low-ground samples, normalized painting, terrain collider assignments, paint texture isolation, fitted snow/fissures, key gameplay locations and both biome preview flights. Before/after renders are in `Temp/MountainQA`.

The original height sculpt remains in `Assets/New Terrain.asset`. During the first authoring pass, Unity's shallow clone shared paint textures with that asset; the source paint weights were normalized during repair. It is therefore a height-sculpt reference, **not an untouched backup of the previous painting**. The final working terrain has independent paint textures and subsequent rebuilds do not share them.

No standalone build, full traversal playtest or target-hardware performance profile was completed. Existing flower-as-terrain-tree shader warnings remain outside this change.
