using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Assertions;

public static class CharacterAnimationChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Run()
    {
        Assert.IsFalse(Application.isPlaying, "Run animation checks in Edit mode.");
        var original = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
        var copy = Object.Instantiate(original.gameObject);
        copy.hideFlags = HideFlags.HideAndDontSave;
        var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            copy.transform.rotation = Quaternion.identity;
            foreach (var camera in copy.GetComponentsInChildren<Camera>()) camera.enabled = false;
            var player = copy.GetComponent<StarterAssets.FirstPersonController>();
            var controller = copy.GetComponent<CharacterController>();
            var animation = copy.GetComponentInChildren<CharacterAnimationController>();
            var animator = animation.GetComponent<Animator>();
            var playerType = player.GetType();
            playerType.GetMethod("Start", Private).Invoke(player, null);
            copy.GetComponent<StarterAssets.StarterAssetsInputs>().move = Vector2.zero;
            animation.GetType().GetMethod("Awake", Private).Invoke(animation, null);
            animator.Rebind();
            var velocity = playerType.GetField("_verticalVelocity", Private);
            var checkGround = playerType.GetMethod("GroundedCheck", Private);
            var updateAnimation = animation.GetType().GetMethod("Update", Private);
            slope.layer = wall.layer = LayerMask.NameToLayer("Ground");
            slope.transform.position = new Vector3(0, 300, 0);
            slope.transform.localScale = new Vector3(30, 1, 30);
            slope.transform.rotation = Quaternion.Euler(20, 0, 0);
            wall.transform.position = new Vector3(0, 310, 8);
            wall.transform.localScale = new Vector3(8, 12, 1);
            controller.enabled = false; copy.transform.position = new Vector3(0, 302, 0); controller.enabled = true;
            Physics.SyncTransforms(); controller.Move(Vector3.down * 3);
            int contacts = 0;
            foreach (float direction in new[] { 1f, -1f })
            for (int i = 0; i < 30; i++)
            {
                copy.GetComponent<StarterAssets.StarterAssetsInputs>().move = new Vector2(0, direction);
                controller.Move(new Vector3(0, -.04f, direction * .15f));
                velocity.SetValue(player, -2f); checkGround.Invoke(player, null);
                playerType.GetMethod("Move", Private).Invoke(player, null);
                updateAnimation.Invoke(animation, null);
                Assert.IsTrue(player.Grounded, "Walkable slopes must retain support: iteration=" + i + " direction=" + direction + " position=" + copy.transform.position + " grounded=" + controller.isGrounded + " hit=" + playerType.GetField("_groundHit", Private).GetValue(player));
                Assert.IsFalse(animator.GetBool("isJumping"), "Walking uphill must not trigger jumping.");
                Assert.IsFalse(animator.GetBool("isFalling"), "Walking downhill must not trigger falling.");
                if (Time.deltaTime > 0 && i > 5)
                    Assert.IsTrue(animator.GetBool("IsMoving"), "Ground following must preserve the walking blend.");
                contacts++;
            }
            copy.GetComponent<StarterAssets.StarterAssetsInputs>().move = Vector2.zero;
            velocity.SetValue(player, 6f); checkGround.Invoke(player, null); updateAnimation.Invoke(animation, null);
            Assert.IsTrue(animator.GetBool("isJumping"), "A real takeoff must animate immediately.");
            Assert.IsFalse(animator.GetBool("isGrounded"));
            controller.Move(Vector3.up * .18f);
            velocity.SetValue(player, -3f); checkGround.Invoke(player, null);
            Assert.IsFalse(player.Grounded, "An airborne character must not land before its feet reach the floor.");
            var inputs = copy.GetComponent<StarterAssets.StarterAssetsInputs>();
            inputs.jump = true;
            playerType.GetMethod("JumpAndGravity", Private).Invoke(player, null);
            Assert.IsTrue(inputs.jump, "Animation polish must preserve held jump input in the air.");
            inputs.jump = false;
            velocity.SetValue(player, -3f); player.Grounded = false;
            animation.GetType().GetField("_groundedChangeTimer", Private).SetValue(animation, .3f);
            updateAnimation.Invoke(animation, null);
            Assert.IsTrue(animator.GetBool("isFalling"), "A sustained unsupported descent must animate falling.");
            controller.Move(Vector3.down * .3f);
            velocity.SetValue(player, -2f); checkGround.Invoke(player, null); updateAnimation.Invoke(animation, null);
            Assert.IsTrue(animator.GetBool("isGrounded"), "Landing must bypass ground debounce.");
            Assert.IsFalse(animator.GetBool("isFalling"));
            controller.enabled = false; copy.transform.position = new Vector3(0, 306, 7); controller.enabled = true;
            Physics.SyncTransforms(); controller.Move(Vector3.forward * .3f);
            velocity.SetValue(player, -2f); checkGround.Invoke(player, null);
            Assert.IsFalse(player.Grounded, "Side contact with a wall must not count as a landing.");
            var volcano = GameObject.Find("Redwood Lava Cavern");
            if (volcano != null)
            foreach (Transform step in volcano.transform)
            {
                if (!step.name.StartsWith("Ascent landing ")) continue;
                var bounds = step.GetComponent<Collider>().bounds;
                controller.enabled = false;
                copy.transform.position = new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z);
                controller.enabled = true; Physics.SyncTransforms(); controller.Move(Vector3.down * .3f);
                checkGround.Invoke(player, null); updateAnimation.Invoke(animation, null);
                Assert.IsTrue(player.Grounded && animator.GetBool("isGrounded"), step.name + " must land and animate in the same frame.");
                contacts++;
            }
            var body = animator.gameObject;
            body.transform.position = Vector3.zero; body.transform.rotation = Quaternion.identity;
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/Animations/Locomotion Pack/idle.anim");
            idle.SampleAnimation(body, 0);
            float referenceYaw = animator.GetBoneTransform(HumanBodyBones.Hips).eulerAngles.y;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name != "jump up" && clip.name != "fall idle" && clip.name != "landing") continue;
                foreach (float fraction in new[] { 0f, .3f, .6f, 1f })
                {
                    clip.SampleAnimation(body, clip.length * fraction);
                    Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(referenceYaw, animator.GetBoneTransform(HumanBodyBones.Hips).eulerAngles.y)) < 2,
                        clip.name + " must face the player's forward direction.");
                }
                if (clip.name == "landing")
                {
                    clip.SampleAnimation(body, clip.length * .2f);
                    float feet = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                    Assert.IsTrue(feet < .16f, "Landing must begin at foot contact, not in midair.");
                }
            }
            Assert.IsTrue(original.GetComponentInChildren<Animator>().transform.localPosition.z <= -.25f, "Body must sit behind the first-person camera.");
            Assert.IsFalse(animator.applyRootMotion, "The movement controller must own character displacement.");
            return contacts + " slope/landing checks passed; takeoff, falling, immediate landing, wall rejection and aligned clip facing passed.";
        }
        finally
        {
            Object.DestroyImmediate(copy); Object.DestroyImmediate(slope); Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
        }
    }
}
