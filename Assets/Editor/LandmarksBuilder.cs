using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using FX = ElementalEffectsSetup;
using Object = UnityEngine.Object;

// Gives every biome places worth leaving the path for, and every guardian a stage worth the fight.
//  - Points of interest: three per region, each a landmark you can see from a distance, a small story
//    told by its props and a journal page, usually a guard pack, and a chest marked by a light beam.
//    Six chests hold a vitality relic (max health +10, saved as a story flag); three hold Wubucks
//    and potions. Each region's own materials and motifs: runestones and blossoms in the Forest,
//    crystal, frozen travellers and old stone in the Frozen Reach, chains, forges and idols in the Volcano.
//  - Arenas: the Blossom Glade becomes a henge under a giant heart tree; the Frozen Amphitheater gets
//    towering crystal spires, frozen spectators, a gate and the guardian's seat; the caldera gets the
//    fire idol, a jagged crown, braziers and a gate; the Sanctum becomes a floating island under
//    Ganfaul's gilded throne, with element obelisks and drifting rocks.
//  - Coherence: no green grass, lime firs or blossoms on the snow.
// Static architecture lives in the World (seen from other regions, no pop-in on entry); chests,
// guards and butterflies live in their region scene. Re-runnable: each run replaces its own roots.
// Run after the biome builders, Populate regions and the story setup.
public static class LandmarksBuilder
{
    private const string Folder = "Assets/Environment/Landmarks";
    private const string Rocks = "Assets/Stylized Rock Pack - 8 styles, 3 shapes, LODs, PBR/Prefabs/";
    private const string Nature = "Assets/Idyllic Fantasy Nature/Prefabs/";
    private const string NatureDemo = "Assets/Idyllic Fantasy Nature/Demo/Prefabs/";
    private const string Armory = "Assets/Stylized Fantasy Armory/Prefabs/";
    private const string Props = Armory + "Decorative Props/";
    private const string Runestone = Armory + "Extra Content/Fantasy Nature Pack - Forest/SFNP_Runestone_00";
    private const string Statues = "Assets/PBR_Statues_pack/";
    private const string Effects = "Assets/Combat/Effects/";
    private const string OpenEffect = "Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/Magic hit.prefab";
    private const string WorldRoot = "Landmarks and arenas", RegionRoot = "Exploration", SanctumRoot = "Sanctum dressing";

    private static readonly Vector2 Glade = ForestBiomeBuilder.ArenaCenter;
    private static readonly Vector2 Amphitheater = SnowBiomeBuilder.ArenaCenter;
    private static readonly Vector2 Caldera = new(795f, 642f);
    private static readonly Color Gold = new(1f, 0.78f, 0.35f), Rose = new(1f, 0.38f, 0.5f);

    // A chest (with its guards) for the region pass, positioned by the World pass.
    private class Cache
    {
        public string region, id, title, guardElement;
        public Vector3 at;
        public Quaternion facing;
        public bool vitality;
        public int wubucks, potions, guards;
        public Vector2 guardsNear;
    }

    private static readonly List<Cache> caches = new();
    private static Texture2D beamTexture;
    private static System.Random random;
    private static float Rand() => (float)random.NextDouble();
    private static float Range(float a, float b) => Mathf.Lerp(a, b, Rand());

    [MenuItem("Wulang/World/Build landmarks, rewards and arenas")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Environment", "Landmarks");
        caches.Clear();
        beamTexture = null;
        random = new System.Random(2026);

        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        var root = NewRoot(world, WorldRoot);
        SnowCoherence();
        BlossomGlade(Group(root, "Blossom Glade henge"));
        FrozenAmphitheater(Group(root, "Frozen Amphitheater stage"));
        CalderaStage(Group(root, "Caldera stage"));
        ForestSites(Group(root, "Forest points of interest"));
        IceSites(Group(root, "Frozen Reach points of interest"));
        VolcanoSites(Group(root, "Volcano points of interest"));
        Terrain.activeTerrain.Flush();
        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.SaveScene(world);

        foreach (var region in new[] { "Forest", "Ice", "Volcano" })
        {
            var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath(region), OpenSceneMode.Additive);
            var regionRoot = NewRoot(scene, RegionRoot);
            foreach (var cache in caches.Where(c => c.region == region)) Chest(regionRoot, cache);
            if (region == "Forest") Butterflies(regionRoot);
            Save(scene);
        }
        var final = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath("FinalArena"), OpenSceneMode.Additive);
        Sanctum(NewRoot(final, SanctumRoot));
        Save(final);
        AssetDatabase.SaveAssets();
        Debug.Log($"LandmarksBuilder: {caches.Count} points of interest, four arenas dressed.");
    }

    // ---------- the Forest: runestones, blossoms, the old faith in the guardians ----------

    private static void ForestSites(Transform parent)
    {
        // The knoll in the northern woods: the old Earth god among a crown of runestones.
        var knoll = new Vector2(398f, 705f);
        var crown = Group(parent, "Runestone crown");
        ClearTrees(knoll, 9f);
        var toward = Dir(knoll, new Vector2(520f, 600f)); // the way the player climbs up from the armory
        Place(crown, Statues + "Statue_God/Prefab/Statue_God_stone1.prefab", Floor(knoll) - Vector3.up * 0.1f, Look(toward), 1.9f);
        Ring(crown, knoll, 5.5f, 7, 0f, (p, a) => Place(crown, Runestone + (1 + random.Next(6)) + ".prefab", Floor(p) - Vector3.up * 0.15f,
            Look(knoll - p) * Quaternion.Euler(Range(-6f, 6f), 0f, Range(-6f, 6f)), Range(1.6f, 2.1f)));
        Scatter(crown, knoll, 4f, 7f, 5, Nature + "FlowerMeadow_White.prefab", 1f);
        Add("Forest", "Runestone crown", knoll + toward * 2.2f, toward, vitality: true, wubucks: 15, potions: 0,
            "Runestone inscription",
            "Earth", knoll + toward * 16f, 2);

        // An abandoned camp by the creek's head: the book-bearer who carried the Earth tome.
        var near = Flat(SceneSplitSetup.Flattest(new Vector2(352f, 812f), 14f));
        var camp = Group(parent, "Book-bearer's camp");
        ClearTrees(near, 8f);
        Place(camp, Props + "Cauldron and Campfire/Campfire.prefab", Floor(near), 0f, 1f);
        Place(camp, Armory + "Wagon/Wagon.002.prefab", Floor(near + new Vector2(-4.2f, 2.5f)), Quaternion.Euler(0f, 70f, 9f), Vector3.one);
        Place(camp, Armory + "Nature/Wooden Logs/WoodenLog.001.prefab", Floor(near + new Vector2(1.8f, 1.2f)), 35f, 1f);
        Place(camp, Armory + "Nature/Wooden Logs/WoodenLog.002.prefab", Floor(near + new Vector2(-1.6f, -1.7f)), 120f, 1f);
        Place(camp, Props + "Barrel/Barrel.002.prefab", Floor(near + new Vector2(-5.6f, -0.8f)), Quaternion.Euler(84f, 30f, 0f), Vector3.one);
        Place(camp, Props + "Box/Box.002.prefab", Floor(near + new Vector2(-3.4f, -2.6f)), 18f, 1f);
        for (int i = 0; i < 6; i++)
        {
            var p = near + new Vector2(Range(-3f, 3f), Range(-3f, 3f));
            if ((p - near).magnitude < 1.2f) continue;
            Place(camp, Props + $"Books/Book.00{1 + random.Next(7)}.prefab", Floor(p) + Vector3.up * 0.02f, Quaternion.Euler(0f, Range(0f, 360f), 90f * random.Next(2)), Vector3.one, solid: false);
        }
        Place(camp, Props + "Scroll/Scroll.002.prefab", Floor(near + new Vector2(0.9f, -1.4f)) + Vector3.up * 0.02f, 50f, 1f, solid: false);
        var campFacing = Dir(near, new Vector2(450f, 700f));
        Add("Forest", "Book-bearer's camp", near + campFacing * 2.4f + Perp(campFacing) * 2f, campFacing, vitality: false, wubucks: 30, potions: 2,
            "Book-bearer's diary",
            "Lightning", near + new Vector2(14f, -10f), 2);

        // A shrine by the glade's pond to someone the land remembers and the player doesn't.
        var shrineAt = Flat(SceneSplitSetup.Flattest(new Vector2(410f, 474f), 6f));
        var shrine = Group(parent, "Blossom shrine");
        ClearTrees(shrineAt, 5f);
        var facePond = Dir(shrineAt, new Vector2(428f, 448f));
        var floor = Floor(shrineAt);
        var dais = Slab(shrine, Rocks + "Plate_forest.prefab", shrineAt, floor.y + 0.45f, new Vector2(3.4f, 3.4f));
        Place(shrine, Statues + "Statue_Woman/Prefab/Statue_Women_Granite.prefab", new Vector3(shrineAt.x, Top(dais), shrineAt.y), Look(facePond), 1.25f);
        Ring(shrine, shrineAt, 3.6f, 4, 45f, (p, a) => Place(shrine, Runestone + (1 + random.Next(6)) + ".prefab", Floor(p) - Vector3.up * 0.1f, Look(shrineAt - p), 1.3f));
        Scatter(shrine, shrineAt, 2.5f, 5.5f, 6, Nature + "FlowerMeadow_Pink.prefab", 1f);
        Place(shrine, NatureDemo + "FloatingLeafs_Pink.prefab", floor + Vector3.up * 4f, 0f, 1.2f, solid: false);
        Place(shrine, Nature + "GodRays.prefab", floor + Vector3.up * 9f, 0f, 0.6f, solid: false);
        Add("Forest", "Blossom shrine", shrineAt + facePond * 2.6f, -facePond, vitality: true, wubucks: 0, potions: 1,
            "Shrine plaque",
            null, default, 0);
    }

    // ---------- the Frozen Reach: crystal, old stone, travellers caught where they ran ----------

    private static void IceSites(Transform parent)
    {
        var frozen = AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/Frozen shell.mat");

        // A caravan that carried the Ice tome, overturned and frozen with its drivers mid-flight.
        var at = Flat(SceneSplitSetup.Flattest(new Vector2(505f, 245f), 10f));
        var caravan = Group(parent, "Frozen caravan");
        ClearTrees(at, 9f);
        var away = Dir(Amphitheater, at);
        Place(caravan, Armory + "Wagon/Wagon.001.prefab", Floor(at + new Vector2(-1.5f, 2f)) + Vector3.up * 0.5f, Quaternion.Euler(0f, 30f, 82f), Vector3.one);
        Place(caravan, Armory + "Combinations/Wagon.003.prefab", Floor(at + new Vector2(4.5f, -2f)) - Vector3.up * 0.3f, Quaternion.Euler(-8f, 120f, 6f), Vector3.one);
        foreach (var (offset, path) in new[] { (new Vector2(1.5f, 4.5f), "Barrel/Barrel.001.prefab"), (new Vector2(2.6f, 3.6f), "Box/Box.001.prefab"), (new Vector2(-4f, -1.5f), "Box/Box.002.prefab"), (new Vector2(0.4f, -3.8f), "Barrel/Barrel.002.prefab") })
            Frost(Place(caravan, Props + path, Floor(at + offset), Range(0f, 360f), 1f), frozen);
        var travellers = new[] { "Statue_Woman/Prefab/Statue_Women_Stone.prefab", "Statue_God/Prefab/Statue_God_stone2.prefab", "Statue_Woman/Prefab/Statue_Women_Granite.prefab" };
        for (int i = 0; i < travellers.Length; i++)
        {
            var p = at + away * (6f + i * 2.5f) + Perp(away) * (i - 1) * 3.2f;
            Frost(Place(caravan, Statues + travellers[i], Floor(p) - Vector3.up * 0.1f, Look(away) * Quaternion.Euler(8f, Range(-20f, 20f), 0f), 1.05f), frozen);
        }
        Scatter(caravan, at, 3f, 9f, 6, Rocks + "Rock_ice.prefab", 0.6f, tilt: true);
        // The flash freeze that caught them: crystal bursting from the snow all around the wreck.
        for (int i = 0; i < 8; i++)
        {
            var p = at + Polar(i * 45f + Range(-15f, 15f), Range(7f, 11f));
            Spire(caravan, Rocks + "Cliff_ice.prefab", p, Range(4f, 8f), Range(1.2f, 1.8f), outward: Flat(p - at).normalized * 16f);
        }
        Add("Ice", "Frozen caravan", at - away * 2.5f, -away, vitality: false, wubucks: 30, potions: 1,
            "Caravan manifest",
            "Ice", at + away * 18f, 2);

        // A crown of crystal on the eastern terraces, the relic at its heart.
        var crownAt = Flat(SceneSplitSetup.Flattest(new Vector2(805f, 235f), 12f));
        var crown = Group(parent, "Crystal crown");
        ClearTrees(crownAt, 9f);
        var crownFloor = Floor(crownAt);
        Ring(crown, crownAt, 6.5f, 7, 15f, (p, a) => Spire(crown, Rocks + "Cliff_ice.prefab", p, Range(9f, 13f), 1.6f, outward: Flat(p - crownAt).normalized * 10f));
        var crownDais = Slab(crown, Rocks + "Plate_ice.prefab", crownAt, crownFloor.y + 0.4f, new Vector2(3f, 3f));
        Glow(crown, crownFloor + Vector3.up * 4f, new Color(0.55f, 0.85f, 1f), 14f, 1.2f);
        Motes(crown, crownFloor + Vector3.up * 2f, "Snowflake", new Color(0.75f, 0.92f, 1f), 8f, 6f);
        var toLanding = Dir(crownAt, new Vector2(655f, 328f));
        Add("Ice", "Crystal crown", crownAt, toLanding, vitality: true, wubucks: 10, potions: 0,
            "Ice-carved verse",
            "Ice", crownAt + toLanding * 16f, 2, standOn: Top(crownDais));

        // The sealed gate behind the Amphitheater, flanked by the old stone watchers.
        var gateAt = Flat(SceneSplitSetup.Flattest(new Vector2(650f, 175f), 8f));
        var gate = Group(parent, "Glacier gate");
        ClearTrees(gateAt + Dir(gateAt, Amphitheater) * 4f, 11f); // keep the view through the gate open
        var north = Dir(gateAt, Amphitheater);
        var side = Perp(north);
        var left = Spire(gate, Rocks + "Cliff_ice.prefab", gateAt + side * 3.4f, 10f, 2f, outward: Vector3.zero);
        Spire(gate, Rocks + "Cliff_ice.prefab", gateAt - side * 3.4f, 10f, 2f, outward: Vector3.zero);
        Place(gate, Rocks + "Rock_ice.prefab", new Vector3(gateAt.x, Top(left) + 0.4f, gateAt.y), Look(side), new Vector3(1.2f, 0.9f, 4.4f)).name = "Lintel";
        foreach (var s in new[] { 1f, -1f })
            Place(gate, Statues + "Statues_Moak/Prefab/Statues_Moak.prefab", Floor(gateAt + side * s * 6.5f + north * 0.5f) - Vector3.up * 0.1f, Look(north), 1.9f);
        Glow(gate, Floor(gateAt) + Vector3.up * 3f, new Color(0.5f, 0.8f, 1f), 10f, 1f);
        Add("Ice", "Glacier gate", gateAt + north * 1.2f, north, vitality: true, wubucks: 0, potions: 1,
            "Gate inscription",
            null, default, 0);
    }

    // ---------- the Volcano: Ganfaul's forges, chains and burnt idols ----------

    private static void VolcanoSites(Transform parent)
    {
        // The chain forge on top of the basalt mesa, reached by jumping up stepping slabs.
        var mesa = new Vector2(735f, 742f);
        var forge = Group(parent, "Chain forge");
        var toSpawn = Dir(mesa, new Vector2(655f, 690f));
        var side = Perp(toSpawn);
        Place(forge, Props + "Oven/Oven.prefab", Floor(mesa - toSpawn * 3.5f), Look(toSpawn), Vector3.one * 1.5f);
        Place(forge, Props + "Anvil and Airblower/Anvil.prefab", Floor(mesa - toSpawn * 0.5f + side * 1.2f), Look(side), Vector3.one * 2.2f);
        Place(forge, Props + "Anvil and Airblower/Airblower.prefab", Floor(mesa - toSpawn * 2.6f - side * 2.2f), Look(side), Vector3.one * 1.8f);
        for (int i = 0; i < 5; i++)
            Place(forge, Armory + $"Armory Structure and Attachables/Armory Attachables/Chain.00{1 + i % 3}.prefab",
                Floor(mesa + side * (2.5f + i * 0.6f) - toSpawn * Range(-1f, 2f)) + Vector3.up * 0.05f, Quaternion.Euler(90f, Range(0f, 360f), 0f), Vector3.one * 1.4f, solid: false);
        Place(forge, Props + "Barrel/Barrel.001.prefab", Floor(mesa - toSpawn * 3.6f + side * 2.4f), 0f, 1f);
        Place(forge, Armory + "Combinations/WeaponRack.002.prefab", Floor(mesa - toSpawn * 1.5f - side * 4f), Look(side), Vector3.one);
        Brazier(forge, Floor(mesa + side * 3.6f + toSpawn * 1.5f), 0.9f, light: true);
        Brazier(forge, Floor(mesa - side * 3.6f + toSpawn * 1.5f), 0.9f, light: false);
        // Two chimney stacks mark the forge from the lava fields; the plateau's cliffs are climbed from the west.
        foreach (var s in new[] { -1f, 1f })
        {
            var stack = Spire(forge, Rocks + "Cliff_infernal.prefab", mesa - toSpawn * 5.5f + side * s * 2.4f, 11f, 1.6f, outward: Vector3.zero);
            var smoke = FX.Emitter(forge, "Chimney smoke", FX.Mat("Forge smoke", "Smoke16", false, 0.55f, Folder), 3f, new Vector2(5f, 8f), new Vector2(1.5f, 3.5f), new Color(0.3f, 0.27f, 0.25f, 0.5f), new Color(0.45f, 0.4f, 0.37f, 0.35f));
            smoke.transform.position = new Vector3(stack.transform.position.x, Top(stack), stack.transform.position.z);
            var rise = smoke.velocityOverLifetime;
            rise.enabled = true;
            rise.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f); // all three axes in the same curve mode
            rise.y = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            rise.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            smoke.Play();
        }
        Steps(forge, mesa, new Vector2(-1f, 0f));
        Add("Volcano", "Chain forge", mesa + toSpawn * 1.2f, toSpawn, vitality: true, wubucks: 20, potions: 0,
            "Forge ledger",
            "Fire", mesa + toSpawn * 22f, 2);

        // A bronze idol on the rim of the great cinder cone, where pilgrims begged the guardian's pardon.
        var cone = new Vector2(668f, 702f);
        var rim = RimSite(cone, 12f, 24f);
        var shrine = Group(parent, "Ember shrine");
        var outward = Dir(cone, rim);
        var across = Perp(outward);
        var dais = Slab(shrine, Rocks + "Plate_infernal.prefab", rim, Floor(rim).y + 0.4f, new Vector2(3.2f, 3.2f));
        Place(shrine, Statues + "Statue_God/Prefab/Statue_God_Bronze1.prefab", new Vector3(rim.x, Top(dais), rim.y), Look(outward), 1.5f);
        Brazier(shrine, Floor(rim + across * 2.6f + outward * 1.2f), 0.8f, light: true);
        Brazier(shrine, Floor(rim - across * 2.6f + outward * 1.2f), 0.8f, light: false);
        for (int i = 0; i < 5; i++)
        {
            var p = rim + across * Range(-6f, 6f) - outward * Range(2f, 4.5f);
            Spire(shrine, Rocks + "Rock_infernal.prefab", p, Range(2.5f, 4.5f), 1.2f, outward: Flat(p - rim).normalized * 12f);
        }
        Add("Volcano", "Ember shrine", rim + outward * 2.8f, outward, vitality: false, wubucks: 35, potions: 2,
            "Scorched prayer",
            "Fire", rim + outward * 15f, 2);

        // A watch post at the burnt forest's edge, the last thing the watchman saw.
        var post = Flat(SceneSplitSetup.Flattest(new Vector2(650f, 600f), 10f));
        var watch = Group(parent, "Burnt watch post");
        ClearTrees(post, 5f);
        var toCone = Dir(post, Caldera);
        var along = Perp(toCone);
        for (int i = 0; i < 5; i++)
            Place(watch, Props + $"Fences/Fence.00{1 + i % 3}.prefab", Floor(post + toCone * 3.5f + along * (i - 2) * 1.9f), Look(along) * Quaternion.Euler(0f, 0f, i == 3 ? 70f : Range(-8f, 8f)), Vector3.one);
        Place(watch, Armory + "Wagon/Wagon.001.prefab", Floor(post - toCone * 3.5f + along * 3f), Quaternion.Euler(4f, Range(0f, 360f), 14f), Vector3.one);
        Place(watch, Props + "Lamp/Lamp.002.prefab", Floor(post + along * 3.2f) + Vector3.up * 0.15f, Quaternion.Euler(0f, 0f, 80f), Vector3.one, solid: false);
        Place(watch, Props + "Weapon Racks/WeaponRack.003.prefab", Floor(post - along * 3.4f), Look(toCone), Vector3.one);
        Brazier(watch, Floor(post + along * 1.6f - toCone * 1.2f), 0.7f, light: true);
        Add("Volcano", "Burnt watch post", post - toCone * 0.8f, toCone, vitality: true, wubucks: 10, potions: 1,
            "Watchman's last note",
            "Fire", post - toCone * 16f, 1);
    }

    // ---------- guardian arenas ----------

    // The Blossom Glade: a henge of runestones around the standing stones, gates of taller stones where
    // the trails come in, and the guardian's heart tree towering behind its shrine.
    private static void BlossomGlade(Transform root)
    {
        float floor = Floor(Glade).y;
        var gaps = new[] { new Vector2(501f, 468f) - Glade, new Vector2(505f, 424f) - Glade }.Select(v => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg).ToArray();
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f + 6f;
            if (gaps.Any(g => Mathf.Abs(Mathf.DeltaAngle(a, g)) < 20f)) continue;
            var p = Glade + Polar(a, 28f);
            ClearTrees(p, 2.5f);
            Place(root, Runestone + (1 + random.Next(6)) + ".prefab", Floor(p) - Vector3.up * 0.15f, Look(Flat(Glade - p)) * Quaternion.Euler(Range(-4f, 4f), 0f, Range(-4f, 4f)), Range(2.3f, 2.9f));
        }
        foreach (var g in gaps)
            foreach (var s in new[] { -10f, 10f })
            {
                var p = Glade + Polar(g + s, 28.5f);
                ClearTrees(p, 2.5f);
                Place(root, Runestone + (1 + random.Next(6)) + ".prefab", Floor(p) - Vector3.up * 0.15f, Look(Flat(Glade - p)), 3.6f).name = "Gate stone";
                Brazier(root, Floor(Glade + Polar(g + s * 0.55f, 27f)), 0.7f, light: s > 0f);
            }
        // The heart tree behind the guardian's shrine, its roots spilling toward the glade.
        var heart = Glade + new Vector2(0f, 36f);
        ClearTrees(heart, 9f);
        Place(root, Nature + "BlossomTree_02.prefab", Floor(heart) - Vector3.up * 0.4f, 200f, 3.4f).name = "Heart tree";
        for (int i = 0; i < 6; i++)
        {
            float a = -90f + (i - 2.5f) * 32f;
            Place(root, Nature + $"Branch_0{1 + i % 6}.prefab", Floor(heart + Polar(a, 4.5f)) - Vector3.up * 0.25f, Quaternion.Euler(0f, -a + 90f, 0f), Vector3.one * 4.2f);
        }
        // No god rays over the heart tree: from afar they read as a beacon marking something to do there.
        Motes(root, new Vector3(Glade.x, floor + 3f, Glade.y), "Spore", new Color(1f, 0.85f, 0.55f), 24f, 10f);
    }

    // The Frozen Amphitheater: crystal spires towering over the ring, the audience frozen where they sat,
    // a gate toward the landing and the guardian's seat under the tallest spires.
    private static void FrozenAmphitheater(Transform root)
    {
        var frozen = AssetDatabase.LoadAssetAtPath<Material>("Assets/Combat/Frozen shell.mat");
        float floor = Floor(Amphitheater).y;
        foreach (var a in new[] { 30f, 58f, 122f, 150f, 210f, 238f, 302f, 330f })
        {
            var p = Amphitheater + Polar(a, 28f + Range(-1f, 2f));
            ClearTrees(p, 4f);
            Spire(root, Rocks + "Cliff_ice.prefab", p, Range(13f, 19f), 3f, outward: Flat(p - Amphitheater).normalized * 7f);
        }
        var audience = new[] { "Statue_Woman/Prefab/Statue_Women_Stone.prefab", "Statue_God/Prefab/Statue_God_stone1.prefab", "Statue_Woman/Prefab/Statue_Women_Granite.prefab", "Statue_God/Prefab/Statue_God_stone2.prefab", "Statue_Woman/Prefab/Statue_Women_Stone.prefab" };
        var seats = new[] { 72f, 108f, 195f, 255f, 285f };
        for (int i = 0; i < seats.Length; i++)
        {
            var p = Amphitheater + Polar(seats[i], 24.5f);
            Frost(Place(root, Statues + audience[i], Floor(p) - Vector3.up * 0.15f, Look(Flat(Amphitheater - p)) * Quaternion.Euler(Range(-6f, 6f), Range(-15f, 15f), 0f), 1.1f), frozen);
        }
        // The guardian's seat to the south, between the two tallest spires.
        var seat = Amphitheater + Polar(270f, 32f);
        ClearTrees(seat, 7f);
        var dais = Slab(root, Rocks + "Plate_ice.prefab", seat, Floor(seat).y + 0.8f, new Vector2(7f, 4.5f));
        Place(root, Statues + "Statues_Moak/Prefab/Statues_Moak.prefab", new Vector3(seat.x, Top(dais), seat.y), Look(Vector3.forward), 2.8f).name = "Guardian's seat";
        foreach (var s in new[] { -5f, 5f }) Spire(root, Rocks + "Cliff_ice.prefab", seat + new Vector2(s, -1.5f), 24f, 3.5f, outward: new Vector3(s, 0f, -3f));
        Glow(root, new Vector3(seat.x, floor + 6f, seat.y), new Color(0.55f, 0.85f, 1f), 30f, 1.4f);
        // The north gate toward the landing.
        var gate = Amphitheater + Polar(90f, 26.5f);
        var pillars = new[] { gate + new Vector2(-4.2f, 0f), gate + new Vector2(4.2f, 0f) };
        var post = Spire(root, Rocks + "Cliff_ice.prefab", pillars[0], 10f, 2.4f, outward: Vector3.zero);
        Spire(root, Rocks + "Cliff_ice.prefab", pillars[1], 10f, 2.4f, outward: Vector3.zero);
        Place(root, Rocks + "Rock_ice.prefab", new Vector3(gate.x, Top(post) + 0.3f, gate.y), Look(Vector3.right), new Vector3(1.4f, 1.1f, 5.2f)).name = "Gate lintel";
        Glow(root, new Vector3(gate.x, floor + 5f, gate.y), new Color(0.55f, 0.85f, 1f), 18f, 0.9f);
        Motes(root, new Vector3(Amphitheater.x, floor + 4f, Amphitheater.y), "Snowflake", new Color(0.8f, 0.95f, 1f), 22f, 14f);
    }

    // The caldera: the fire god's idol before the lavafall under a jagged crown of basalt, braziers at
    // the idol and at the gate where the causeway arrives.
    private static void CalderaStage(Transform root)
    {
        const float level = 9.9f;
        var idol = Caldera + new Vector2(13f, 0f);
        var dais = Slab(root, Rocks + "Plate_infernal.prefab", idol, level + 0.7f, new Vector2(4.2f, 4.6f));
        Place(root, Statues + "Statue_God/Prefab/Statue_God_Bronze2.prefab", new Vector3(idol.x, Top(dais), idol.y), Look(Vector3.left), 2.3f).name = "Fire idol";
        foreach (var s in new[] { -3.4f, 3.4f }) Brazier(root, new Vector3(idol.x - 1.5f, level, idol.y + s), 1f, light: s > 0f);
        foreach (var a in new[] { -36f, -22f, -9f, 9f, 22f })
        {
            var p = Caldera + Polar(a, 15.5f);
            Spire(root, Rocks + "Rock_infernal.prefab", p, Range(6f, 10f), 2.6f, outward: Flat(p - Caldera).normalized * 10f, ground: level - 0.5f);
        }
        var gate = Caldera + Polar(180f, 14.5f);
        foreach (var s in new[] { -5.5f, 5.5f })
        {
            Spire(root, Rocks + "Cliff_infernal.prefab", gate + new Vector2(0f, s), 10f, 2.4f, outward: new Vector3(0f, 0f, s * 0.6f), ground: level - 0.5f);
            Brazier(root, new Vector3(gate.x + 1.4f, level, gate.y + s * 0.62f), 0.9f, light: s > 0f);
        }
    }

    // The Sanctum: the disc becomes the top of a floating island, Ganfaul's gilded throne at its north
    // edge, an obelisk behind each element pillar casting that element's beam, and rocks drifting around.
    private static void Sanctum(Transform root)
    {
        var c = SceneSplitSetup.ArenaCenter;
        var center = new Vector2(c.x, c.z);
        // The platform's top, not a ray down the middle (Abe and Ganfaul stand there).
        var platform = GameObject.Find("Arena/Floating platform");
        float floor = platform != null ? platform.GetComponent<Renderer>().bounds.max.y : c.y;
        var island = Group(root, "Island underside");
        for (int i = 0; i < 9; i++)
        {
            var p = center + (i == 0 ? Vector2.zero : Polar(i * 40f + Range(-10f, 10f), Range(8f, 20f)));
            float size = i == 0 ? 15f : Range(7f, 11f);
            var rock = Place(island, Rocks + (i % 3 == 0 ? "Cliff_fancy.prefab" : "Rock_fancy.prefab"), new Vector3(p.x, floor - 3f, p.y),
                Quaternion.Euler(180f + Range(-12f, 12f), Range(0f, 360f), 0f), new Vector3(size, size * (i == 0 ? 2.2f : Range(1.2f, 1.8f)), size), solid: false);
            rock.transform.position += Vector3.up * (floor - 1.2f - Bounds(rock).max.y);
        }
        var drift = Group(root, "Drifting isles");
        drift.position = c;
        drift.gameObject.AddComponent<Spin>();
        for (int i = 0; i < 10; i++)
        {
            var p = center + Polar(i * 36f + Range(-12f, 12f), Range(44f, 72f));
            float size = Range(2f, 5.5f);
            var y = floor + Range(-18f, 14f);
            var isle = Place(drift, Rocks + (i % 2 == 0 ? "Plate_fancy.prefab" : "Rock_fancy.prefab"), new Vector3(p.x, y, p.y), Quaternion.Euler(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f)), Vector3.one * size, solid: false);
            Place(drift, Rocks + "Rock_fancy.prefab", isle.transform.position - Vector3.up * size * 0.7f, Quaternion.Euler(180f, Range(0f, 360f), 0f), new Vector3(size * 0.7f, size * 1.1f, size * 0.7f), solid: false);
        }
        // Ganfaul's gilded throne at the north edge, just outside the fighting floor.
        var gold = AssetDatabase.LoadAssetAtPath<Material>(Statues + "Statue_God/Materials/Statue_God_Bronze1.mat");
        var seat = center + new Vector2(0f, 24.8f);
        // A dais on the floating platform, not a column reaching down to the terrain 700 m below.
        var dais = Slab(root, Rocks + "Plate_fancy.prefab", seat, floor + 1f, new Vector2(10f, 4.5f), maxDepth: 2.5f);
        var throne = Place(root, Props + "Chair/Chair.prefab", new Vector3(seat.x, Top(dais), seat.y + 0.6f), Look(Vector3.back), Vector3.one * 6f);
        throne.name = "Gilded throne";
        foreach (var r in throne.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(gold, r.sharedMaterials.Length).ToArray();
        foreach (var s in new[] { -4f, 4f })
            Place(root, Statues + "Statue_God/Prefab/Statue_God_Bronze2.prefab", new Vector3(seat.x + s, Top(dais), seat.y), Look(Vector3.back), 1.7f);
        foreach (var s in new[] { -6.5f, 0f, 6.5f })
            Spire(root, Rocks + "Cliff_fancy.prefab", center + new Vector2(s, 30f), s == 0f ? 18f : 13f, 3.2f, outward: new Vector3(s * 0.8f, 0f, 4f), ground: floor - 1f);
        // Element obelisks behind the four element pillars, each crowned with its element's stone and beam.
        var elements = new[] { ElementType.Ice, ElementType.Fire, ElementType.Lightning, ElementType.Earth };
        var crowns = new[] { "Rock_ice", "Rock_infernal", "Rock_fancy", "Rock_forest" };
        for (int i = 0; i < 4; i++)
        {
            var p = center + Polar(45f + i * 90f, 26.2f);
            var obelisk = Spire(root, Rocks + "Cliff_basic.prefab", p, 10f, 1.8f, outward: Vector3.zero, ground: floor - 0.5f);
            obelisk.name = elements[i] + " obelisk";
            var color = ElementVisuals.ColorFor(elements[i]);
            Place(root, Rocks + crowns[i] + ".prefab", new Vector3(p.x, Top(obelisk) + 1.2f, p.y), Quaternion.Euler(30f, i * 40f, 20f), Vector3.one * 1.1f, solid: false);
            Beam(root, new Vector3(p.x, Top(obelisk) + 1f, p.y), color, elements[i].ToString(), 60f, 0.7f);
        }
        // The arrival: two spires frame the way in from the rift.
        foreach (var s in new[] { -12f, 12f })
            Spire(root, Rocks + "Cliff_fancy.prefab", center + Polar(270f + s, 27f), 11f, 2.4f, outward: Vector3.zero, ground: floor - 1f);
        Motes(root, new Vector3(c.x, floor + 2f, c.z), "Glint", new Color(0.8f, 0.6f, 1f), 26f, 12f);
    }

    // ---------- chests, guards and life (region scenes) ----------

    // A chest at `at` (on the ground, or on a dais top at `standOn`), opening toward `facing`.
    private static void Add(string region, string id, Vector2 at, Vector2 facing, bool vitality, int wubucks, int potions,
        string title, string guardElement, Vector2 guardsNear, int guards, float standOn = float.NaN)
    {
        caches.Add(new Cache
        {
            region = region, id = id, at = float.IsNaN(standOn) ? Floor(at) : new Vector3(at.x, standOn, at.y), facing = Look(facing),
            vitality = vitality, wubucks = wubucks, potions = potions, title = title,
            guardElement = guardElement, guardsNear = guardsNear, guards = guards,
        });
    }

    private static void Chest(Transform parent, Cache cache)
    {
        var group = Group(parent, cache.id);
        var root = new GameObject("Chest").transform;
        root.SetParent(group, false);
        root.SetPositionAndRotation(cache.at, cache.facing);
        var model = Place(root, Props + (cache.vitality ? "Chest/ChestL.001 1.prefab" : "Chest/ChestS.001.prefab"), cache.at, cache.facing, Vector3.one * 1.3f);
        var lid = model.transform.Find(cache.vitality ? "ChestL.002" : "ChestS.002");
        // The beacon sits beside the chest, not under it, so the interaction outline skips the beam.
        var beacon = Beacon(group, cache.at, cache.vitality ? Rose : Gold, cache.vitality ? "Vitality" : "Supplies");
        var interactable = new SerializedObject(root.gameObject.AddComponent<Interactable>());
        interactable.FindProperty("prompt").stringValue = cache.vitality ? "Press F to open the reliquary" : "Press F to open the chest";
        interactable.ApplyModifiedPropertiesWithoutUndo();
        var chest = new SerializedObject(root.gameObject.AddComponent<TreasureChest>());
        chest.FindProperty("id").stringValue = cache.id;
        chest.FindProperty("lid").objectReferenceValue = lid;
        chest.FindProperty("beacon").objectReferenceValue = beacon;
        chest.FindProperty("wubucks").intValue = cache.wubucks;
        chest.FindProperty("vitality").boolValue = cache.vitality;
        chest.FindProperty("openEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(OpenEffect);
        var items = new List<ItemData>();
        var potion = AssetDatabase.LoadAssetAtPath<ItemData>(ItemsSetup.PotionPath);
        for (int i = 0; i < cache.potions; i++) items.Add(potion);
        if (StoryScript.Lore.ContainsKey(cache.title)) items.Add(StorySetup.Journal(cache.title)); // wording in StoryScript
        var list = chest.FindProperty("items");
        list.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        chest.ApplyModifiedPropertiesWithoutUndo();
        if (cache.guards > 0) RegionContentSetup.Pack(group, cache.guardElement, cache.guardsNear, cache.guards);
    }

    // A light beam over an unopened chest, with a soft glow and motes rising from the lid.
    private static GameObject Beacon(Transform parent, Vector3 foot, Color color, string name)
    {
        var beacon = Group(parent, "Beacon");
        beacon.position = foot;
        Beam(beacon, foot + Vector3.up * 1.5f, color, name, 70f, 0.8f);
        Glow(beacon, foot + Vector3.up * 1f, color, 5f, 1.6f);
        Motes(beacon, foot + Vector3.up * 0.6f, "Glint", color, 0.7f, 5f, rise: 1.4f);
        return beacon.gameObject;
    }

    private static void Beam(Transform parent, Vector3 foot, Color color, string name, float height, float width)
    {
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Light beam";
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.transform.SetParent(parent, false);
        beam.transform.position = foot + Vector3.up * height * 0.5f;
        beam.transform.localScale = new Vector3(width, height * 0.5f, width);
        var renderer = beam.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = BeamMaterial(name, color);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void Butterflies(Transform parent)
    {
        foreach (var (at, size) in new[] { (Glade + new Vector2(0f, -6f), 30f), (new Vector2(410f, 474f), 14f), (new Vector2(540f, 480f), 24f), (new Vector2(440f, 640f), 20f) })
        {
            var area = Place(parent, Nature + "ButterflySpawnArea.prefab", Floor(at) + Vector3.up * 1.5f, 0f, 1f, solid: false);
            area.GetComponent<BoxCollider>().size = new Vector3(size, 2f, size); // scaling the transform would scale the butterflies too
            area.layer = 2; // Ignore Raycast: spell aiming passes through the trigger
        }
    }

    // ---------- coherence: the snow keeps to its own palette ----------

    private static void SnowCoherence()
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        var area = Rect.MinMaxRect(SceneSplitSetup.IceDomain.xMin - 20f, SceneSplitSetup.IceDomain.yMin - 20f, SceneSplitSetup.IceDomain.xMax + 20f, SceneSplitSetup.IceDomain.yMax + 30f);
        var snowLayers = data.terrainLayers.Select((l, i) => (l, i)).Where(e => e.l != null && (e.l.name.Contains("snow") || e.l.name.Contains("Snow") || e.l.name.Contains("Glacier") || e.l.name.Contains("Frosted"))).Select(e => e.i).ToArray();
        float alphaCell = TerrainBaseline.AlphaCell(data);
        var alphaRegion = TerrainBaseline.Region(area, data.alphamapResolution, alphaCell);
        var maps = data.GetAlphamaps(alphaRegion.x, alphaRegion.y, alphaRegion.width, alphaRegion.height);
        float Snow(Vector2 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(p.x / alphaCell) - alphaRegion.x, 0, alphaRegion.width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(p.y / alphaCell) - alphaRegion.y, 0, alphaRegion.height - 1);
            return snowLayers.Sum(l => maps[z, x, l]);
        }

        // Trees: warm-palette trees on snow become the biome's blue and purple broadleaves; understory goes.
        var prototypes = data.treePrototypes.ToList();
        int Proto(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            int i = prototypes.FindIndex(t => t.prefab == asset);
            if (i < 0) { prototypes.Add(new TreePrototype { prefab = asset }); i = prototypes.Count - 1; }
            return i;
        }
        var cold = Enumerable.Range(1, 5).SelectMany(i => new[] { Proto($"{Nature}BroadleafTree_0{i}_Blue.prefab"), Proto($"{Nature}BroadleafTree_0{i}_Purple.prefab") }).ToArray();
        data.treePrototypes = prototypes.ToArray();
        bool Warm(string name) => name.StartsWith("Fir") || name.Contains("_Green") || name.Contains("_Red") || name.StartsWith("Blossom") || name.Contains("Pink");
        bool Understory(string name) => name.Contains("NoCollision") || name.StartsWith("Flower") || name.StartsWith("Bush") || name.StartsWith("Plant");
        var trees = new List<TreeInstance>();
        int swapped = 0, removed = 0;
        foreach (var tree in data.treeInstances)
        {
            var p = new Vector2(tree.position.x * data.size.x, tree.position.z * data.size.z);
            var prefab = prototypes[tree.prototypeIndex].prefab;
            if (prefab == null || !area.Contains(p) || Snow(p) < 0.5f) { trees.Add(tree); continue; }
            if (Understory(prefab.name)) { removed++; continue; }
            var kept = tree;
            if (Warm(prefab.name)) { kept.prototypeIndex = cold[Mathf.Abs((int)(p.x * 7f + p.y * 13f)) % cold.Length]; swapped++; }
            trees.Add(kept);
        }
        data.SetTreeInstances(trees.ToArray(), true);

        // Grass: none where the ground is snow.
        float detailCell = TerrainBaseline.DetailCell(data);
        var detailRegion = TerrainBaseline.Region(area, data.detailResolution, detailCell);
        int cleared = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var detail = data.GetDetailLayer(detailRegion.x, detailRegion.y, detailRegion.width, detailRegion.height, layer);
            for (int z = 0; z < detailRegion.height; z++)
                for (int x = 0; x < detailRegion.width; x++)
                {
                    if (detail[z, x] == 0) continue;
                    var p = new Vector2((detailRegion.x + x + 0.5f) * detailCell, (detailRegion.y + z + 0.5f) * detailCell);
                    if (Snow(p) < 0.35f) continue;
                    detail[z, x] = 0;
                    cleared++;
                }
            data.SetDetailLayer(detailRegion.x, detailRegion.y, layer, detail);
        }
        Debug.Log($"LandmarksBuilder: snow palette: {swapped} trees recoloured, {removed} shrubs and {cleared} grass cells removed.");
    }

    // ---------- building blocks ----------

    private static Transform NewRoot(Scene scene, string name)
    {
        var old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
        if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go.transform;
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static Transform Group(Transform parent, string name) => FX.Group(parent, name);

    private static GameObject Place(Transform parent, string path, Vector3 position, Quaternion rotation, Vector3 scale, bool solid = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new FileNotFoundException(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scale);
        // The rock and statue packs ship without colliders; landmarks must stop the player and spells.
        if (solid && instance.GetComponentInChildren<Collider>() == null)
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        foreach (var wheel in instance.GetComponentsInChildren<WheelCollider>()) Object.DestroyImmediate(wheel);
        return instance;
    }

    private static GameObject Place(Transform parent, string path, Vector3 position, float yaw, float scale, bool solid = true) =>
        Place(parent, path, position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale, solid);

    private static GameObject Place(Transform parent, string path, Vector3 position, Quaternion rotation, float scale, bool solid = true) =>
        Place(parent, path, position, rotation, Vector3.one * scale, solid);

    // A tall stone or crystal standing on the ground, `height` by about `width` metres, leaning away from `outward` (degrees).
    private static GameObject Spire(Transform parent, string path, Vector2 at, float height, float width, Vector3 outward, float ground = float.NaN)
    {
        float floor = float.IsNaN(ground) ? Floor(at).y : ground;
        var spire = Place(parent, path, new Vector3(at.x, floor, at.y), Quaternion.identity, Vector3.one);
        var native = Bounds(spire).size;
        float across = width / Mathf.Max(0.01f, (native.x + native.z) * 0.5f);
        spire.transform.localScale = new Vector3(across, height / Mathf.Max(native.y, 0.01f), across);
        var lean = outward.sqrMagnitude > 0.01f ? Quaternion.AngleAxis(Mathf.Min(outward.magnitude, 14f), Vector3.Cross(Vector3.up, outward.normalized)) : Quaternion.identity;
        spire.transform.rotation = lean * Quaternion.Euler(0f, Range(0f, 360f), 0f);
        spire.transform.position += Vector3.up * (floor - 0.6f - Bounds(spire).min.y);
        return spire;
    }

    // A flat rock slab whose top sits at `top`, `size` metres across, reaching down into the ground.
    private static GameObject Slab(Transform parent, string path, Vector2 at, float top, Vector2 size, float maxDepth = float.MaxValue)
    {
        var slab = Place(parent, path, new Vector3(at.x, top, at.y), 0f, 1f);
        var b = Bounds(slab);
        float depth = Mathf.Min(maxDepth, top - Floor(at).y + 1.2f);

        slab.transform.localScale = new Vector3(size.x / b.size.x, depth / b.size.y, size.y / b.size.z);
        slab.transform.position += Vector3.up * (top - Bounds(slab).max.y);
        return slab;
    }

    // Stepping slabs up the plateau's cliff along `dir`, each a jump (about a metre) above the last.
    // The edge is where the ground first drops 1.5 m below the highest point crossed from `mesa`.
    private static void Steps(Transform parent, Vector2 mesa, Vector2 dir)
    {
        float peak = Floor(mesa).y, edge = 0f;
        for (float d = 1f; d < 45f && edge == 0f; d += 0.5f)
        {
            float h = Floor(mesa + dir * d).y;
            if (h > peak) peak = h;
            else if (h < peak - 1.5f) edge = d - 1.5f;
        }
        if (edge == 0f) { Debug.LogWarning("LandmarksBuilder: no plateau edge found; no steps placed."); return; }
        float top = Floor(mesa + dir * edge).y, foot = edge + 12f, bottom = Floor(mesa + dir * foot).y;
        int count = Mathf.CeilToInt((top - bottom) / 1f);
        var steps = Group(parent, "Stepping slabs");
        for (int i = 1; i <= count; i++) // the last slab sits flush with the rim
        {
            float t = (float)i / count;
            var p = mesa + dir * Mathf.Lerp(foot, edge + 1.2f, t) + Perp(dir) * (i % 2 == 0 ? 0.9f : -0.9f);
            Slab(steps, Rocks + "Plate_infernal.prefab", p, bottom + (top - bottom) * t, new Vector2(2.4f, 2.4f));
        }
    }

    // The highest gently sloped point on a ring around a cone: a rim to stand on.
    private static Vector2 RimSite(Vector2 cone, float from, float to)
    {
        var data = Terrain.activeTerrain.terrainData;
        Vector2 best = cone + new Vector2(0f, -from);
        float bestHeight = float.MinValue;
        for (float r = from; r <= to; r += 1f)
            for (float a = 0f; a < 360f; a += 6f)
            {
                var p = cone + Polar(a, r);
                if (data.GetSteepness(p.x / data.size.x, p.y / data.size.z) > 22f || Hazard(p)) continue;
                float h = Floor(p).y;
                if (h > bestHeight) { bestHeight = h; best = p; }
            }
        return best;
    }

    private static bool Hazard(Vector2 p) =>
        Physics.RaycastAll(new Vector3(p.x, 300f, p.y), Vector3.down, 400f, ~0, QueryTriggerInteraction.Collide)
            .Any(h => h.collider.GetComponentInParent<HazardZone>() != null || h.collider.name.ToLowerInvariant().Contains("lava"));

    private static void Brazier(Transform parent, Vector3 at, float scale, bool light)
    {
        var bowl = Place(parent, Props + "Cauldron and Campfire/Campfire.prefab", at, Range(0f, 360f), scale);
        bowl.name = "Brazier";
        var flame = FX.Emitter(bowl.transform, "Flame", FX.Mat("Brazier flame", "Fire1", true, 1.6f, Folder), 14f, new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.35f, 0.08f));
        flame.transform.localPosition = Vector3.up * 0.25f;
        var main = flame.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        var shape = flame.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.25f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        flame.Play();
        var sparks = FX.Emitter(bowl.transform, "Sparks", AssetDatabase.LoadAssetAtPath<Material>(Effects + "Ember.mat"), 4f, new Vector2(1.2f, 2f), new Vector2(0.04f, 0.08f), new Color(1f, 0.7f, 0.3f), new Color(1f, 0.4f, 0.1f));
        sparks.transform.localPosition = Vector3.up * 0.5f;
        var velocity = sparks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        sparks.Play();
        if (light) Glow(bowl.transform, at + Vector3.up * 1.2f, new Color(1f, 0.55f, 0.2f), 12f, 2.2f);
    }

    private static void Glow(Transform parent, Vector3 at, Color color, float range, float intensity)
    {
        var light = new GameObject("Glow").AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.position = at;
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
    }

    // Slow drifting motes over an area (`radius` metres around `at`); `rise` makes them climb.
    private static void Motes(Transform parent, Vector3 at, string material, Color color, float radius, float rate, float rise = 0.15f)
    {
        var motes = FX.Emitter(parent, "Motes", AssetDatabase.LoadAssetAtPath<Material>(Effects + material + ".mat"), rate, new Vector2(4f, 7f), new Vector2(0.06f, 0.14f), color, Color.white);
        motes.transform.position = at;
        var main = motes.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var shape = motes.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.5f, rise);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        motes.Play();
    }

    private static void Frost(GameObject target, Material frozen)
    {
        if (frozen == null) return;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = Enumerable.Repeat(frozen, renderer.sharedMaterials.Length).ToArray();
    }

    private static void Scatter(Transform parent, Vector2 center, float from, float to, int count, string path, float scale, bool tilt = false)
    {
        for (int i = 0; i < count; i++)
        {
            var p = center + Polar(Range(0f, 360f), Range(from, to));
            var rotation = Quaternion.Euler(tilt ? Range(-20f, 20f) : 0f, Range(0f, 360f), tilt ? Range(-20f, 20f) : 0f);
            Place(parent, path, Floor(p) - Vector3.up * (tilt ? 0.3f : 0.05f), rotation, Range(0.8f, 1.2f) * scale, solid: false);
        }
    }

    private static void Ring(Transform parent, Vector2 center, float radius, int count, float offset, Action<Vector2, float> place)
    {
        for (int i = 0; i < count; i++)
        {
            float a = offset + i * 360f / count;
            place(center + Polar(a, radius), a);
        }
    }

    // Terrain trees too close to a landmark would grow through it.
    private static void ClearTrees(Vector2 center, float radius)
    {
        var data = Terrain.activeTerrain.terrainData;
        var kept = data.treeInstances.Where(t => (new Vector2(t.position.x * data.size.x, t.position.z * data.size.z) - center).magnitude > radius).ToArray();
        if (kept.Length != data.treeInstances.Length) data.SetTreeInstances(kept, true);
    }

    private static Material BeamMaterial(string name, Color color)
    {
        string path = $"{Folder}/{name} beam.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Effects + "Glint.mat"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", BeamTexture());
        material.SetColor("_BaseColor", color * 0.6f);
        material.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Bright at the foot of the beam, fading out toward the sky.
    private static Texture2D BeamTexture()
    {
        if (beamTexture != null) return beamTexture;
        const string path = Folder + "/Beam gradient.png";
        var pixels = new Texture2D(4, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
        {
            float v = y / 127f;
            float alpha = Mathf.Pow(1f - v, 2.2f) * Mathf.SmoothStep(0f, 1f, v / 0.08f) * 0.55f;
            for (int x = 0; x < 4; x++) pixels.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        Object.DestroyImmediate(pixels);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return beamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Bounds Bounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    private static float Top(GameObject go) => Bounds(go).max.y;
    private static Vector3 Floor(Vector2 p) => SceneSplitSetup.Ground(p) - Vector3.up * 0.1f;
    private static Vector2 Flat(Vector3 v) => new(v.x, v.z);
    private static Vector3 Flat(Vector2 v) => new(v.x, 0f, v.y);
    private static Vector2 Polar(float degrees, float radius) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;
    private static Vector2 Dir(Vector2 from, Vector2 to) => (to - from).normalized;
    private static Vector2 Perp(Vector2 v) => new Vector2(v.y, -v.x).normalized;
    private static Quaternion Look(Vector2 direction) => Look(Flat(direction));
    private static Quaternion Look(Vector3 direction) => Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z).sqrMagnitude > 1e-4f ? new Vector3(direction.x, 0f, direction.z) : Vector3.forward);
}
