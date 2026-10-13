# Redwood volcanic cavern

Select Redwood Lava Cavern in SampleScene and press F in Scene view. Its mountain is centred near (795, 0, 642); the entrance faces west toward the armory, rotated 90 degrees from the first cave.

The irregular mountain reaches approximately 65 m. Its ground follows the existing terrain rather than sitting on a raised slab. A winding route has 38 landings rising by 0.7 m, with three wider exploration shelves, and climbs about 26 m. The tall central lavafall drains into a ground-following outflow, with two additional exterior lava trails and scattered volcanic boulders.

Geometry and lights are editable scene objects. Materials and meshes are in Assets/Environment/LavaCave. Lava Flow > Speed controls animation: positive Y moves the waterfall downward; negative Y moves the outside trails away from the cave. Emission and orange point lights provide glow and illumination. Lava has no damage mechanic.

Checked: exterior and interior renders, standing capsule clearance on all 38 landings, terrain contact at lava vertices (4.5 cm offset to prevent flicker), and animated/emissive lava in Play mode. A complete manual jump-through and standalone build have not been tested.
