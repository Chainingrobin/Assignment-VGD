using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

public static class VolcanoChecks
{
    [UnityEditor.MenuItem("Wulang/Volcano/Run volcano checks (Edit mode)")]
    public static string Run()
    {
        Assert.IsFalse(Application.isPlaying, "Run these checks in Edit mode.");
        var root = GameObject.Find("Redwood Lava Cavern").transform;
        Physics.SyncTransforms();
        int ground = 1 << LayerMask.NameToLayer("Ground");
        const float climb = 1.45f; // 1.2 m jump plus the controller's step offset

        // The route from the lava fields to the arena must be continuous, walkable and fit the 2 m player capsule.
        var route = new List<Vector3>();
        foreach (var local in VolcanoBiomeBuilder.Route())
        {
            var point = root.TransformPoint(local);
            Assert.IsTrue(Physics.Raycast(point + Vector3.up * 2.5f, Vector3.down, out var hit, 5f, ground, QueryTriggerInteraction.Ignore), "No walkable ground at " + point);
            Assert.AreNotEqual("Lava hazard floor", hit.collider.name, "The route crosses lava at " + hit.point);
            Assert.IsTrue(hit.normal.y >= Mathf.Cos(45 * Mathf.Deg2Rad), "Too steep at " + hit.point);
            Assert.IsFalse(Physics.CheckCapsule(hit.point + Vector3.up * .6f, hit.point + Vector3.up * 1.55f, .45f, ground, QueryTriggerInteraction.Ignore), "Player capsule blocked at " + hit.point);
            if (route.Count > 0) Assert.IsTrue(Mathf.Abs(hit.point.y - route[route.Count - 1].y) <= .5f, "Step too high at " + hit.point);
            route.Add(hit.point);
        }

        // Lava floors are continuous, so from anywhere in them a player can wade at most 20 m (the lake's half-width) and climb out.
        int lavaSamples = 0;
        foreach (var hazard in root.GetComponentsInChildren<MeshCollider>().Where(c => c.name == "Lava hazard floor"))
        {
            var vertices = hazard.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i += Mathf.Max(1, vertices.Length / 60))
            {
                var p = hazard.transform.TransformPoint(vertices[i]);
                if (Physics.Raycast(p + Vector3.up * 20, Vector3.down, out var top, 40, ground, QueryTriggerInteraction.Ignore) && top.collider != hazard) continue;
                bool escape = false;
                for (int a = 0; a < 16 && !escape; a++)
                    for (float d = 1; d <= 20 && !escape; d += 1)
                    {
                        var probe = p + Quaternion.Euler(0, a * 22.5f, 0) * Vector3.forward * d;
                        escape = Physics.Raycast(probe + Vector3.up * (climb + .05f), Vector3.down, out var land, climb + 2, ground, QueryTriggerInteraction.Ignore)
                            && !land.collider.name.StartsWith("Lava hazard") && land.normal.y >= Mathf.Cos(45 * Mathf.Deg2Rad);
                    }
                Assert.IsTrue(escape, hazard.transform.parent.name + " traps a player at " + p);
                lavaSamples++;
            }
        }

        var smoke = root.Find("Summit smoke").GetComponent<ParticleSystem>();
        Assert.IsTrue(smoke.emission.enabled && smoke.emission.rateOverTime.constant > 0, "Summit smoke must emit.");
        Assert.IsNotNull(smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetTexture("_BaseMap"));
        Assert.IsTrue(root.Find("Volcano preview landmark").localPosition.y > 90, "The preview landmark marks the summit.");
        Assert.IsNotNull(root.Find("Boss caldera/Boss spawn point"), "Boss spawn point is missing.");
        Assert.IsTrue(root.Find("Boss caldera/Boss arena bounds").GetComponent<Collider>().isTrigger, "Arena bounds must be a trigger.");
        var fall = root.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "Grand lavafall").bounds;
        Assert.IsTrue(fall.size.y > 30 && Mathf.Max(fall.size.x, fall.size.z) > 10, "The grand lavafall must be tall and wide.");

        // Highland lava flows lie on the reshaped ranges, the big cinder cone keeps its crater lava, and charred trees ring the fields.
        var terrain = Terrain.activeTerrain;
        var flows = root.GetComponentsInChildren<MeshFilter>().Where(f => f.name == "Mountain lava flow").ToArray();
        Assert.IsTrue(flows.Length >= 3, "The volcanic ranges need lava flows.");
        foreach (var flow in flows)
            foreach (var vertex in flow.sharedMesh.vertices)
            {
                var world = flow.transform.TransformPoint(vertex);
                float lift = world.y - terrain.SampleHeight(world) - terrain.transform.position.y;
                Assert.IsTrue(lift > .1f && lift < .9f, "Mountain lava must drape the slope at " + world);
            }
        Assert.IsNotNull(root.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Cinder crater lava"), "The cinder cone lost its crater lava.");
        var data = terrain.terrainData;
        int charred = data.treeInstances.Count(t => data.treePrototypes[t.prototypeIndex].prefab.name == "Charred tree");
        Assert.IsTrue(charred > 50, "Charred trees should ring the lava fields; found " + charred);

        int launches = HeldJumps(new[] { route[0], route[route.Count / 2], route[route.Count - 1] });
        return route.Count + " route samples walkable, " + lavaSamples + " lava samples escapable, " + launches + " held jumps; summit, boss markers and lavafall passed.";
    }

    // Held Space keeps jumping on volcano floors and releasing it stops, exercised through the controller's own methods.
    static int HeldJumps(Vector3[] floors)
    {
        var player = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        var controller = player.GetComponent<CharacterController>();
        Assert.AreEqual(UnityEngine.InputSystem.InputActionType.Value, player.GetComponent<UnityEngine.InputSystem.PlayerInput>().actions["Jump"].type,
            "SendMessages requires a Value action to deliver jump releases.");
        var input = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        var type = player.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var velocity = type.GetField("_verticalVelocity", flags);
        var timeout = type.GetField("_jumpTimeoutDelta", flags);
        var groundedCheck = type.GetMethod("GroundedCheck", flags);
        var jump = type.GetMethod("JumpAndGravity", flags);
        var controllerField = type.GetField("_controller", flags);
        var inputField = type.GetField("_input", flags);
        var originalController = controllerField.GetValue(player);
        var originalInput = inputField.GetValue(player);
        var originalVelocity = velocity.GetValue(player);
        var originalTimeout = timeout.GetValue(player);
        var originalFall = type.GetField("_fallTimeoutDelta", flags).GetValue(player);
        var originalPosition = player.transform.position;
        bool originalJump = input.jump, originalGrounded = player.Grounded, originalEnabled = controller.enabled;
        int launches = 0;
        try
        {
            controllerField.SetValue(player, controller); inputField.SetValue(player, input);
            Assert.IsTrue(Time.deltaTime > 0 && Time.deltaTime < .1f, "A valid simulation timestep is required.");
            foreach (var floor in floors)
            {
                controller.enabled = false;
                player.transform.position = floor + Vector3.up * .02f;
                controller.enabled = true; Physics.SyncTransforms();
                velocity.SetValue(player, 0f); timeout.SetValue(player, 0f); input.jump = true;
                groundedCheck.Invoke(player, null);
                Assert.IsTrue(player.Grounded, floor + " must be grounded.");
                int count = 0;
                for (int i = 0; i < Mathf.CeilToInt(3.5f / Time.deltaTime); i++)
                {
                    float before = (float)velocity.GetValue(player);
                    groundedCheck.Invoke(player, null); jump.Invoke(player, null);
                    float after = (float)velocity.GetValue(player);
                    if (before <= 0 && after > 0) count++;
                    controller.Move(Vector3.up * after * Time.deltaTime);
                    Assert.IsTrue(input.jump, "Held jump must survive airborne frames.");
                }
                Assert.IsTrue(count >= 2, floor + " must allow repeated held jumps; got " + count);
                input.jump = false; velocity.SetValue(player, 0f); timeout.SetValue(player, 0f); player.Grounded = true;
                jump.Invoke(player, null);
                Assert.IsTrue((float)velocity.GetValue(player) <= 0, "Releasing Space must stop repeated jumps.");
                launches += count;
            }
            return launches;
        }
        finally
        {
            controller.enabled = false; player.transform.position = originalPosition; controller.enabled = originalEnabled;
            input.jump = originalJump; player.Grounded = originalGrounded;
            controllerField.SetValue(player, originalController); inputField.SetValue(player, originalInput);
            velocity.SetValue(player, originalVelocity); timeout.SetValue(player, originalTimeout);
            type.GetField("_fallTimeoutDelta", flags).SetValue(player, originalFall);
            Physics.SyncTransforms();
        }
    }

    public static void CheckJumpRelease()
    {
        Assert.IsTrue(Application.isPlaying, "Run the input event check in Play mode.");
        var originalBackground = InputSystem.settings.backgroundBehavior;
        var originalEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var keyboard = InputSystem.AddDevice<Keyboard>("Volcano check keyboard");
        var mouse = InputSystem.AddDevice<Mouse>("Volcano check mouse");
        var go = new GameObject("Temporary jump input check");
        go.SetActive(false);
        var inputs = go.AddComponent<StarterAssets.StarterAssetsInputs>();
        var playerInput = go.AddComponent<PlayerInput>();
        var actions = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions"));
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            playerInput.actions = actions; playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;
            playerInput.neverAutoSwitchControlSchemes = true;
            go.SetActive(true);
            playerInput.user.UnpairDevices();
            InputUser.PerformPairingWithDevice(keyboard, playerInput.user);
            InputUser.PerformPairingWithDevice(mouse, playerInput.user);
            playerInput.SwitchCurrentControlScheme("KeyboardMouse", keyboard, mouse);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
            Assert.IsTrue(inputs.jump, "Pressing Space must start held jump input.");
            InputSystem.Update();
            Assert.IsTrue(inputs.jump, "Space must remain held between input events.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Assert.IsFalse(inputs.jump, "Releasing Space must clear held jump input through SendMessages.");
        }
        finally
        {
            Object.DestroyImmediate(go); Object.DestroyImmediate(actions);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = originalBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInput;
        }
    }
}
