using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Self-checks for the Assignment 3 prototype. Edit mode: scene structure, region scenes,
// boss rigs and animation wiring, story content per region. Play mode: the combat and story
// rules (weakness, Storm Quake, final-boss resistance, boss death firing once). Each check
// throws with a clear message on failure.
public static class PrototypeChecks
{
    [MenuItem("Wulang/Checks/Prototype structure (edit mode)")]
    public static void Structure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Run in Edit mode.");
        var expected = new[] { SceneSplitSetup.MenuPath, SceneSplitSetup.WorldPath }.Concat(SceneSplitSetup.Regions.Select(SceneSplitSetup.RegionPath)).ToArray();
        var build = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
        Check(build.SequenceEqual(expected), "Build order should be MainMenu, World, Forest, Ice, Volcano, FinalArena; got " + string.Join(", ", build));

        var world = EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        Check(Count<StarterAssets.FirstPersonController>(world) == 1, "World needs exactly one player.");
        Check(Count<ResponsiveHUD>(world) == 1, "World needs exactly one HUD.");
        Check(Count<RegionManager>(world) == 1, "World needs exactly one RegionManager.");
        Check(Count<ObjectiveManager>(world) == 1 && Count<InventoryUI>(world) == 1 && Count<PauseMenu>(world) == 1, "World is missing a story UI system.");

        int previous = -1;
        foreach (var region in SceneSplitSetup.Regions)
        {
            var scene = EditorSceneManager.OpenScene(SceneSplitSetup.RegionPath(region), OpenSceneMode.Additive);
            var start = Components<RegionStart>(scene).SingleOrDefault();
            Check(start != null, region + " needs one RegionStart.");
            Check(Count<StarterAssets.FirstPersonController>(scene) == 0, region + " must not contain its own player.");
            int prerequisites = new SerializedObject(start).FindProperty("prerequisites").arraySize;
            Check(prerequisites > previous, region + " should require more story flags than the region before it.");
            previous = prerequisites;
            Check(Count<BossEncounter>(scene) == 1, region + " needs exactly one boss encounter.");
            Check(Count<RegionObjectives>(scene) == 1, region + " needs its objectives.");
            int moments = Count<NarrativeTrigger>(scene) + Count<BossEncounter>(scene) + Count<MemoryEcho>(scene);
            Check(moments >= 3, $"{region} has {moments} narrative moments; the brief asks for at least 3.");
            EditorSceneManager.CloseScene(scene, true);
        }

        foreach (var path in new[] { BossSetup.EarthBoss, BossSetup.IceBoss, BossSetup.FireBoss, BossSetup.FinalBoss })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animator = prefab.GetComponentInChildren<Animator>();
            Check(animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid, path + " needs a valid Humanoid avatar.");
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToHashSet();
            Check(states.Contains("Locomotion") && states.Contains("Death") && states.Contains("Roar"), path + " controller lacks Locomotion/Death/Roar.");
            var brain = new SerializedObject(prefab.GetComponent<BossBrain>());
            var attacks = brain.FindProperty("attacks");
            for (int i = 0; i < attacks.arraySize; i++)
            {
                string state = attacks.GetArrayElementAtIndex(i).FindPropertyRelative("state").stringValue;
                Check(states.Contains(state), $"{path}: attack state '{state}' is missing from its controller.");
            }
            var target = new SerializedObject(prefab.GetComponent<CombatTarget>());
            Check(target.FindProperty("trainingRespawnDelay").floatValue == 0f, path + " must stay dead once beaten.");
        }
        EditorSceneManager.OpenScene(SceneSplitSetup.WorldPath, OpenSceneMode.Single);
        Debug.Log("PrototypeChecks: structure OK.");
    }

    [MenuItem("Wulang/Checks/Combat and story rules (Play mode)")]
    public static void Rules()
    {
        if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play mode first.");
        var origin = new Vector3(100f, 900f, 100f); // far from everything
        var ice = Spawn(BossSetup.IceBoss, origin);
        var final = Spawn(BossSetup.FinalBoss, origin + Vector3.right * 30f);
        var vampireA = Spawn("Assets/Combat/Earth vampire.prefab", origin + new Vector3(60f, 0f, 0f));
        var vampireB = Spawn("Assets/Combat/Earth vampire.prefab", origin + new Vector3(61.5f, 0f, 0f));
        try
        {
            var iceTarget = ice.GetComponent<CombatTarget>();
            float before = iceTarget.Health;
            iceTarget.TakeDamage(20f, ElementType.Fire);
            Check(Mathf.Approximately(before - iceTarget.Health, 30f), "Fire should do 1.5x to the Ice Guardian.");
            iceTarget.TakeDamage(1f, ElementType.Ice);
            Check(iceTarget.FreezeRemaining > 0f && iceTarget.FreezeRemaining < 1f, "Bosses should only freeze briefly (status scale 0.3).");

            var a = vampireA.GetComponent<CombatTarget>();
            var b = vampireB.GetComponent<CombatTarget>();
            a.TakeDamage(1f, ElementType.Earth);
            a.TakeDamage(1f, ElementType.Lightning);
            Check(a.IsStaggered && b.IsStaggered, "Earth then Lightning should Storm Quake and stagger nearby enemies.");
            Check(Object.FindObjectsByType<StormQuake>(FindObjectsSortMode.None).Length >= 1, "Storm Quake field should spawn.");

            var ganfaul = final.GetComponent<CombatTarget>();
            final.GetComponent<ElementPhases>().enabled = true;
            ganfaul.SetAffinity(ElementType.Lightning);
            before = ganfaul.Health;
            ganfaul.TakeDamage(40f, ElementType.Fire);
            Check(before - ganfaul.Health < 15f && ganfaul.LastFeedback.StartsWith("RESISTED"), "Ganfaul should resist non-weakness hits.");
            before = ganfaul.Health;
            ganfaul.TakeDamage(40f, ElementType.Earth);
            Check(Mathf.Approximately(before - ganfaul.Health, 60f), "Ganfaul should take full weakness damage.");

            int deaths = 0;
            iceTarget.Died += () => deaths++;
            iceTarget.TakeDamage(99999f, ElementType.Fire);
            iceTarget.TakeDamage(99999f, ElementType.Fire);
            Check(deaths == 1 && iceTarget.IsDead, "A boss's Died event must fire exactly once.");

            Check(StoryStateManager.Instance.gameObject.scene.name == "DontDestroyOnLoad", "Story state must survive scene loads.");
            Debug.Log("PrototypeChecks: combat and story rules OK.");
        }
        finally
        {
            Object.Destroy(ice);
            Object.Destroy(final);
            Object.Destroy(vampireA);
            Object.Destroy(vampireB);
        }
    }

    private static GameObject Spawn(string path, Vector3 position)
    {
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
        foreach (var behaviour in instance.GetComponents<MonoBehaviour>())
            if (behaviour is BossBrain || behaviour is VampireMelee || behaviour is ElementPhases) behaviour.enabled = false;
        instance.GetComponent<Rigidbody>().isKinematic = true;
        return instance;
    }

    private static T[] Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();

    private static int Count<T>(Scene scene) where T : Component => Components<T>(scene).Length;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException("PrototypeChecks failed: " + message);
    }
}
