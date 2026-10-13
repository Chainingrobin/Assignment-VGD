using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

public static class VolcanoChecks
{
    public static string Run()
    {
        Assert.IsFalse(Application.isPlaying, "Run these checks in Edit mode.");
        var root = GameObject.Find("Redwood Lava Cavern").transform;
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
        int landings = 0, launches = 0, samples = 0;
        try
        {
            controllerField.SetValue(player, controller); inputField.SetValue(player, input);
            Assert.IsTrue(Time.deltaTime > 0 && Time.deltaTime < .1f, "A valid simulation timestep is required.");
            foreach (Transform step in root)
            {
                if (!step.name.StartsWith("Ascent landing ")) continue;
                Assert.IsTrue((player.GroundLayers.value & (1 << step.gameObject.layer)) != 0, step.name + " must be detected as ground.");
                var bounds = step.GetComponent<Collider>().bounds;
                controller.enabled = false;
                player.transform.position = new Vector3(bounds.center.x, bounds.max.y + .02f, bounds.center.z);
                controller.enabled = true; Physics.SyncTransforms();
                velocity.SetValue(player, 0f); timeout.SetValue(player, 0f); input.jump = true;
                groundedCheck.Invoke(player, null);
                Assert.IsTrue(player.Grounded, step.name + " must be grounded.");
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
                Assert.IsTrue(count >= 2, step.name + " must allow repeated held jumps; got " + count);
                input.jump = false; velocity.SetValue(player, 0f); timeout.SetValue(player, 0f); player.Grounded = true;
                jump.Invoke(player, null);
                Assert.IsTrue((float)velocity.GetValue(player) <= 0, "Releasing Space must stop repeated jumps.");
                launches += count; landings++;
            }
            foreach (Transform surface in root)
            {
                if (!surface.name.StartsWith("Grounded lava trail ") && !surface.name.StartsWith("Liquid lava pool ")) continue;
                Assert.AreEqual(Vector3.zero, surface.localPosition, "Grounded lava must have no stale transform offset.");
                var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
                foreach (var vertex in mesh.vertices)
                {
                    var world = surface.TransformPoint(vertex);
                    float ground = Terrain.activeTerrain.SampleHeight(world) + Terrain.activeTerrain.transform.position.y;
                    var apron = root.Find("Grounded volcanic apron").GetComponent<Collider>();
                    if (apron.Raycast(new Ray(new Vector3(world.x, 100, world.z), Vector3.down), out var hit, 200)) ground = Mathf.Max(ground, hit.point.y);
                    Assert.IsTrue(world.y - ground >= .05f && world.y - ground <= .12f, surface.name + " must closely follow the floor.");
                    samples++;
                }
            }
            var fall = root.Find("High volcanic lavafall").GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(fall.bounds.size.x >= 8 && fall.bounds.size.z >= 3, "Lavafall must have width and volume.");
            var fallBounds = root.Find("High volcanic lavafall").GetComponent<Renderer>().bounds;
            fallBounds.Expand(1.2f);
            foreach (Transform step in root)
                if (step.name.StartsWith("Ascent landing ") || step.name.StartsWith("Shelf landing "))
                    Assert.IsFalse(fallBounds.Intersects(step.GetComponent<Collider>().bounds), step.name + " must clear the lavafall with room for the player.");
            var smoke = root.Find("Summit smoke").GetComponent<ParticleSystem>();
            Assert.IsTrue(smoke.emission.enabled && smoke.emission.rateOverTime.constant > 0, "Summit smoke must emit.");
            Assert.IsNotNull(smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetTexture("_BaseMap"));
            return landings + " landings passed, " + launches + " held jumps, " + samples + " lava vertices grounded; release, lavafall volume and smoke passed.";
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
