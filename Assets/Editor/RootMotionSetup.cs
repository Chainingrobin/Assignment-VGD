using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Switches every enemy animator to animation-driven movement: Locomotion blends on a gait
// (0 idle, 1 walk, 2 run) instead of a speed, foot IK is on, attack states blend out smoothly,
// and each model gets RootMotionBody so its root motion moves the rigidbody. Re-runnable.
public static class RootMotionSetup
{
    private const string VampireController = "Assets/Combat/Vampire animations/Vampire melee.controller";
    private static readonly string[] Vampires = { "Earth", "Fire", "Ice", "Lightning" };

    [MenuItem("Wulang/Combat/Apply root motion and foot IK")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        Configure(AssetDatabase.LoadAssetAtPath<AnimatorController>(VampireController));
        foreach (var element in Vampires) AddBody($"Assets/Combat/{element} vampire.prefab");
        foreach (var boss in new[] { BossSetup.EarthBoss, BossSetup.IceBoss, BossSetup.FireBoss, BossSetup.FinalBoss })
            if (AssetDatabase.LoadAssetAtPath<GameObject>(boss) != null) AddBody(boss);
        AssetDatabase.SaveAssets();
    }

    public static void Configure(AnimatorController controller)
    {
        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            var state = child.state;
            state.iKOnFeet = true;
            if (state.motion is BlendTree tree && state.name == "Locomotion")
            {
                tree.useAutomaticThresholds = false;
                var children = tree.children;
                for (int i = 0; i < children.Length; i++) children[i].threshold = i; // idle 0, walk 1, run 2
                tree.children = children;
            }
            // One-shot actions ease back into Locomotion over their last few frames.
            foreach (var transition in state.transitions.Where(t => t.destinationState != null && t.destinationState.name == "Locomotion"))
            {
                transition.hasExitTime = true;
                transition.exitTime = 0.85f;
                transition.hasFixedDuration = true;
                transition.duration = 0.35f;
            }
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);
    }

    public static void AddBody(string prefabPath)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            if (animator.GetComponent<RootMotionBody>() == null) animator.gameObject.AddComponent<RootMotionBody>();
            animator.applyRootMotion = true;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            // Bosses fill the screen; never freeze their pose when the camera looks away briefly.
            if (root.GetComponent<BossBrain>() != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
