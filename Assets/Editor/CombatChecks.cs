using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

public static class CombatChecks
{
    public static string ChasePhysicsResult { get; private set; }

    [MenuItem("Wulang/Combat/Check grounded chase (Play mode)")]
    public static void BeginChasePhysicsCheck()
    {
        Assert.IsTrue(Application.isPlaying);
        ChasePhysicsResult = "Running";
        Object.FindFirstObjectByType<SpellCaster>().StartCoroutine(ChasePhysicsCheck());
    }

    private static IEnumerator ChasePhysicsCheck()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Chase physics test floor";
        floor.transform.position = new Vector3(20000,99.5f,20000);
        floor.transform.localScale = new Vector3(80,1,80);
        var cameraObject = new GameObject("Chase physics target");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(20000,101.5f,20030);
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Combat/Lightning vampire.prefab"), new Vector3(20000,100.05f,20000), Quaternion.identity);
        try
        {
            var behavior = instance.GetComponent<VampireMelee>();
            Set(behavior,"player",camera);
            var sight = instance.GetComponent<EnemyDetection>(); sight.enabled = false;
            Assert.IsFalse(sight.CanDetect(camera.transform.position),"Unalerted detection keeps its normal range");
            Set(behavior,"sawPlayer",true);
            Assert.IsTrue(sight.CanDetect(camera.transform.position),"Alerted vampires maintain sight beyond 18 m");
            Set(sight,"<CanSeePlayer>k__BackingField",true);
            Physics.SyncTransforms();
            Vector3 start = instance.transform.position;
            for (int i=0;i<350;i++) yield return new WaitForFixedUpdate();
            float displacement = Vector3.ProjectOnPlane(instance.transform.position-start,Vector3.up).magnitude;
            var animator = instance.GetComponentInChildren<Animator>();
            Assert.IsTrue(displacement>15f,$"Extended chase stopped: only {displacement:0.00} m in seven seconds");
            Assert.IsTrue(behavior.IsPursuing,"Visible player inside pursuit domain must retain priority");
            Assert.IsTrue((float)typeof(VampireMelee).GetField("displayRemaining",Private).GetValue(behavior)<=0,"No flex while pursuing");
            Assert.IsTrue(animator.GetFloat("Speed")>2.5f,"Chase must reach the run blend");
            ChasePhysicsResult = $"PASS: moved {displacement:0.00} m in 350 physics steps past the 8 m roaming boundary; retained pursuit without flex; run blend speed {animator.GetFloat("Speed"):0.00}.";
            Debug.Log(ChasePhysicsResult);
        }
        finally
        {
            if (ChasePhysicsResult=="Running") ChasePhysicsResult="FAILED";
            Object.Destroy(instance); Object.Destroy(floor); Object.Destroy(cameraObject);
        }
    }
    public static string MotionResult { get; private set; }
    public static string SteamResult { get; private set; }
    public static string SeismicResult { get; private set; }
    public static string ArcResult { get; private set; }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Tick(CombatTarget target, float seconds) => typeof(CombatTarget).GetMethod("Advance", Private).Invoke(target, new object[] { seconds });
    private static void Equal(float expected, float actual, string message) => Assert.IsTrue(Mathf.Abs(expected - actual) < 0.001f, $"{message}: expected {expected}, got {actual}");

    [MenuItem("Wulang/Combat/Run combat checks (Play mode)")]
    public static void Run()
    {
        Assert.IsTrue(Application.isPlaying, "Run combat checks in Play mode.");
        Charges();
        var fixture = new GameObject("Combat check fixture");
        fixture.transform.position = new Vector3(-2000, 100, -2000);
        fixture.hideFlags = HideFlags.DontSave;
        try
        {
            var a = Target(fixture.transform, Vector3.zero, ElementType.Fire);
            var b = Target(fixture.transform, Vector3.right * 2, ElementType.Ice);
            var c = Target(fixture.transform, Vector3.right * 4, ElementType.Earth);
            var d = Target(fixture.transform, Vector3.right * 6, ElementType.Lightning);
            Matchups();
            a.ReceiveSpell(20, ElementType.Fire, a.transform.position - Vector3.forward);
            Equal(990, a.Health, "Same affinity resists direct damage");
            Tick(a, 1);
            Equal(986, a.Health, "Burn ticks at four HP/second");
            a.ReceiveSpell(20, ElementType.Fire, a.transform.position);
            Equal(4, a.BurnRemaining, "Repeated burn refreshes");
            Tick(a, 4);
            Equal(960, a.Health, "Burn refresh does not stack its rate");
            a.ResetTarget();
            a.ReceiveSpell(20, ElementType.Ice, a.transform.position);
            Equal(970, a.Health, "Opposing affinity is weak");
            Equal(3f, a.FreezeRemaining, "Freeze starts for three seconds");
            Tick(a, 1);
            a.ReceiveSpell(20, ElementType.Ice, a.transform.position);
            Equal(3f, a.FreezeRemaining, "Repeated freeze refreshes");
            a.ReceiveSpell(20, ElementType.Earth, a.transform.position - Vector3.forward);
            Assert.IsTrue(a.IsFrozen && a.LastFeedback.Contains("SLIPPERY"), "Earth preserves Freeze and produces slippery impact");
            a.ReceiveSpell(20, ElementType.Fire, a.transform.position);
            Assert.IsFalse(a.IsFrozen, "Steam consumes Freeze");
            Assert.IsTrue(a.LastFeedback.Contains("STEAM"), "Fire on Frozen produces steam");
            float before = a.Health;
            a.ReceiveSpell(20, ElementType.Ice, a.transform.position);
            Equal(before - 40, a.Health, "Thermal bonus is ten after resistance");
            Equal(0, a.BurnRemaining, "Thermal shock consumes Burn");
            Assert.IsTrue(a.IsFrozen, "Thermal shock applies Freeze");
            Tick(a, 3.1f);
            Assert.IsFalse(a.IsFrozen, "Freeze expires");

            a.ResetTarget(); b.ResetTarget(); c.ResetTarget(); d.ResetTarget();
            a.ReceiveSpell(1, ElementType.Fire, a.transform.position);
            Tick(a, 1);
            Physics.SyncTransforms();
            new SpellHit(20, ElementType.Lightning, a.transform.position - Vector3.forward).Apply(a.GetComponentInChildren<Collider>());
            Equal(3, b.BurnRemaining, "Wildfire transfers remaining burn to the first arc");
            Equal(3, c.BurnRemaining, "Wildfire transfers remaining burn to the second arc");
            Equal(990, b.Health, "First arc has half neutral direct damage");
            Equal(985, c.Health, "Second arc applies target weakness");
            Equal(1000, d.Health, "Lightning never exceeds two extra targets");
            a.ReceiveSpell(10000, ElementType.None, a.transform.position);
            a.ResetTarget(); b.ResetTarget(); c.ResetTarget(); d.ResetTarget();
            a.ReceiveSpell(1, ElementType.Fire, a.transform.position);
            new SpellHit(1000, ElementType.Lightning, a.transform.position).Apply(a.GetComponentInChildren<Collider>());
            Assert.IsTrue(a.IsDead, "A lethal source still launches its chains");
            Equal(4, b.BurnRemaining, "A lethal source still transfers its remaining Burn");
            a.ResetTarget(); b.ResetTarget(); c.ResetTarget(); d.ResetTarget();
            new SpellHit(40, ElementType.Lightning, a.transform.position).Apply(new[] { a.GetComponentInChildren<Collider>(), b.GetComponentInChildren<Collider>(), b.GetComponentInChildren<Collider>() });
            Equal(960, b.Health, "A direct area hit is neither replaced by a chain nor repeated by extra colliders");
            Equal(970, c.Health, "Area chain uses half of area damage");

            ReactionsAndVision(fixture.transform, a);

            a.ResetTarget(); b.ResetTarget(); c.ResetTarget(); d.ResetTarget();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.localPosition = new Vector3(1, 1, 0);
            wall.transform.localScale = new Vector3(0.1f, 3, 10);
            Physics.SyncTransforms();
            new SpellHit(20, ElementType.Lightning, a.transform.position).Apply(a.GetComponentInChildren<Collider>());
            Equal(1000, b.Health, "Arcs cannot cross a wall");
            a.ReceiveSpell(10000, ElementType.None, a.transform.position);
            Assert.IsTrue(a.IsDead && !a.GetComponentInChildren<Collider>().enabled, "Defeat disables hitboxes");
            a.ResetTarget();
            Assert.IsTrue(!a.IsDead && a.GetComponentInChildren<Collider>().enabled, "Training reset restores hitboxes and health");
            CheckScene();
            Debug.Log("PASS: combat matchups, resources, status timing, reactions including seismic stun, chains, walls, vision cone, steam occlusion/expiry, child colliders, defeat/reset and scene wiring.");
        }
        finally { Object.DestroyImmediate(fixture); }
    }

    private static void ReactionsAndVision(Transform fixture, CombatTarget target)
    {
        target.ResetTarget();
        target.ReceiveSpell(1, ElementType.Earth, target.transform.position - Vector3.forward);
        Tick(target, 3.5f);
        Assert.IsTrue(target.IsGrounded, "Earth leaves enough time to follow up after 3.5 seconds");
        target.ReceiveSpell(1, ElementType.Lightning, target.transform.position);
        Equal(3, target.StunRemaining, "Earth then Lightning stuns for three seconds");
        Assert.IsTrue(target.LastFeedback.Contains("SEISMIC"));
        Assert.IsFalse(target.IsGrounded || target.IsCharged, "Seismic stun consumes the combination marks");
        Tick(target, 3.1f);
        Assert.IsFalse(target.IsStunned, "Stun expires");
        target.ResetTarget();
        target.ReceiveSpell(1, ElementType.Lightning, target.transform.position);
        target.ReceiveSpell(1, ElementType.Earth, target.transform.position - Vector3.forward);
        Equal(3, target.StunRemaining, "Lightning then Earth also stuns");
        target.ResetTarget();
        target.ReceiveSpell(1, ElementType.Earth, target.transform.position);
        Tick(target, 4.6f);
        target.ReceiveSpell(1, ElementType.Lightning, target.transform.position);
        Equal(1.25f, target.StunRemaining, "Expired Earth marks do not produce seismic stun");
        var vision = target.gameObject.AddComponent<EnemyDetection>();
        var point = target.HeadPosition + Vector3.forward * 8;
        Physics.SyncTransforms();
        Assert.IsTrue(vision.CanSee(point), "A target in front is visible");
        Assert.IsFalse(vision.CanSee(target.HeadPosition - Vector3.forward * 8), "Targets behind the enemy are outside its view cone");
        Assert.IsFalse(vision.CanSee(target.HeadPosition + Vector3.forward * 20), "Detection respects distance");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = target.HeadPosition + Vector3.forward * 4;
        wall.transform.localScale = new Vector3(3, 3, 0.2f);
        wall.transform.SetParent(fixture);
        Physics.SyncTransforms();
        Assert.IsFalse(vision.CanSee(point), "Walls block enemy detection");
        Object.DestroyImmediate(wall);
        Physics.SyncTransforms();
        var cloudObject = new GameObject("Steam occlusion fixture");
        cloudObject.transform.SetParent(fixture);
        cloudObject.transform.position = target.HeadPosition + Vector3.forward * 3;
        var cloud = cloudObject.AddComponent<SteamCloud>();
        Assert.IsFalse(vision.CanSee(point), "Steam between enemy and player blocks detection");
        cloudObject.transform.position = target.HeadPosition;
        Assert.IsFalse(vision.CanSee(point), "An enemy inside steam cannot see out");
        cloudObject.transform.position = point + Vector3.forward * 4;
        Assert.IsTrue(vision.CanSee(point), "Steam beyond the player does not block sight");
        cloudObject.transform.position = target.HeadPosition;
        Set(cloud, "expiresAt", Time.time - 1);
        Assert.IsTrue(vision.CanSee(point), "Detection returns after steam expires");
        Object.DestroyImmediate(cloudObject);
        target.ResetTarget();
    }

    public static void BeginKnockbackCheck()
    {
        MotionResult = "running";
        Object.FindFirstObjectByType<SpellCaster>().StartCoroutine(KnockbackCheck());
    }

    public static void BeginArcCheck()
    {
        ArcResult = "running";
        Object.FindFirstObjectByType<SpellCaster>().StartCoroutine(ArcCheck());
    }

    private static IEnumerator ArcCheck()
    {
        var fixture = new GameObject("Lightning visibility fixture");
        fixture.transform.position = new Vector3(-2000, 100, -2000);
        var a = Target(fixture.transform, Vector3.zero, ElementType.Fire);
        var b = Target(fixture.transform, Vector3.right * 2, ElementType.Ice);
        var c = Target(fixture.transform, Vector3.right * 4, ElementType.Earth);
        var caster = new SerializedObject(Object.FindFirstObjectByType<SpellCaster>());
        var material = (Material)caster.FindProperty("chainMaterial").objectReferenceValue;
        try
        {
            Physics.SyncTransforms();
            new SpellHit(20, ElementType.Lightning, a.transform.position, material).Apply(a.GetComponentInChildren<Collider>());
            var arcs = Object.FindObjectsByType<LightningArc>(FindObjectsSortMode.None).Where(arc => Vector3.Distance(arc.transform.position, fixture.transform.position) < 10).ToArray();
            Assert.AreEqual(2, arcs.Length);
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(arcs.All(arc => arc != null), "Chains must remain visible beyond the old 0.45 second lifetime.");
            Equal(990, b.Health, "Lingering chain visuals must not repeat damage");
            Equal(985, c.Health, "Lingering secondary chain must not repeat damage");
            b.transform.position += Vector3.forward;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            foreach (var arc in arcs)
            {
                var source = (CombatTarget)typeof(LightningArc).GetField("source", Private).GetValue(arc);
                var destination = (CombatTarget)typeof(LightningArc).GetField("destination", Private).GetValue(arc);
                var core = arc.transform.Find("White core").GetComponent<LineRenderer>();
                Assert.IsTrue(Vector3.Distance(core.GetPosition(0), source.AimPoint) < 0.01f);
                Assert.IsTrue(Vector3.Distance(core.GetPosition(core.positionCount - 1), destination.AimPoint) < 0.01f, "Longer arcs must follow moving targets.");
            }
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(arcs.All(arc => arc == null), "Finished arcs and lights must clean themselves up.");
            ArcResult = "PASS: both chains lasted beyond 0.8 seconds, followed moving targets, dealt damage only once, and cleaned up after 1.5 seconds.";
            Debug.Log(ArcResult);
        }
        finally { Object.Destroy(fixture); }
    }

    private static IEnumerator KnockbackCheck()
    {
        var fixture = new GameObject("Knockback physics fixture");
        fixture.transform.position = new Vector3(-2000, 100, -2000);
        var target = Target(fixture.transform, Vector3.zero, ElementType.Fire);
        var body = target.GetComponent<Rigidbody>();
        body.mass = 2f;
        body.linearDamping = 2f;
        var origin = target.transform.position;
        try
        {
            target.ReceiveSpell(1, ElementType.Earth, origin - Vector3.forward);
            yield return new WaitForSeconds(0.35f);
            float normal = Vector3.Distance(origin, target.transform.position);
            Assert.IsTrue(normal > 1f, "Earth must move a target visibly in real physics.");
            target.ResetTarget();
            target.ReceiveSpell(1, ElementType.Ice, origin);
            target.ReceiveSpell(1, ElementType.Earth, origin - Vector3.forward);
            yield return new WaitForSeconds(0.35f);
            float slippery = Vector3.Distance(origin, target.transform.position);
            Assert.IsTrue(slippery > normal * 1.3f, "Earth on Frozen must produce stronger displacement.");
            Assert.IsTrue(target.IsFrozen, "The stronger push preserves Freeze.");
            MotionResult = $"PASS: Earth moved {normal:0.00}m; Earth on Frozen moved {slippery:0.00}m in 0.35 seconds.";
            Debug.Log(MotionResult);
        }
        finally { Object.Destroy(fixture); }
    }

    // Run after positioning the player to aim at the Ice training target.
    public static void BeginSteamCastCheck()
    {
        SteamResult = "running";
        Object.FindFirstObjectByType<SpellCaster>().StartCoroutine(SteamCastCheck());
    }

    private static IEnumerator SteamCastCheck()
    {
        var caster = Object.FindFirstObjectByType<SpellCaster>();
        var enemy = GameObject.Find("Combat training").GetComponentsInChildren<CombatTarget>().First(t => t.Affinity == ElementType.Ice);
        enemy.ResetTarget();
        yield return null;
        yield return null;
        Assert.IsTrue(enemy.GetComponent<EnemyDetection>().CanSeePlayer, "The actual player must be detected before steam.");
        Assert.IsTrue(enemy.GetComponent<EnemyOverheadUI>().AlertVisible, "The overhead exclamation must be visible before steam.");
        PlayerMagicAffinity.Instance.UnlockElement(ElementType.Ice);
        PlayerMagicAffinity.Instance.SetActiveElement(ElementType.Ice);
        Assert.IsTrue(caster.CastProjectile());
        yield return new WaitForSeconds(0.65f);
        Assert.IsTrue(enemy.IsFrozen, "The real Ice projectile must freeze its target.");
        PlayerMagicAffinity.Instance.SetActiveElement(ElementType.Fire);
        Assert.IsTrue(caster.CastProjectile());
        yield return new WaitForSeconds(0.65f);
        Assert.IsFalse(enemy.IsFrozen, "The real Fire projectile must consume Freeze.");
        Assert.IsNotNull(Object.FindFirstObjectByType<SteamCloud>());
        Assert.IsFalse(enemy.GetComponent<EnemyDetection>().CanSeePlayer, "Steam must remove detection of the actual player.");
        Assert.IsFalse(enemy.GetComponent<EnemyOverheadUI>().AlertVisible, "Steam must remove the actual overhead exclamation.");
        SteamResult = "PASS: real Ice then Fire casts created steam and removed player detection and the overhead exclamation.";
        Debug.Log(SteamResult);
        Time.timeScale = 0f;
    }

    public static void BeginSeismicCastCheck()
    {
        SeismicResult = "running";
        Object.FindFirstObjectByType<SpellCaster>().StartCoroutine(SeismicCastCheck());
    }

    private static IEnumerator SeismicCastCheck()
    {
        var caster = Object.FindFirstObjectByType<SpellCaster>();
        var enemy = GameObject.Find("Combat training").GetComponentsInChildren<CombatTarget>().First(t => t.Affinity == ElementType.Fire);
        enemy.ResetTarget();
        Vector3 originalPosition = enemy.transform.position;
        caster.PlayerCamera.transform.LookAt(enemy.AimPoint);
        PlayerMagicAffinity.Instance.UnlockElement(ElementType.Earth);
        PlayerMagicAffinity.Instance.UnlockElement(ElementType.Lightning);
        PlayerMagicAffinity.Instance.SetActiveElement(ElementType.Earth);
        Assert.IsTrue(caster.CastProjectile());
        yield return new WaitForSeconds(0.7f);
        Assert.IsTrue(enemy.IsGrounded, "A real Earth projectile must mark the enemy.");
        Assert.IsTrue(Vector3.Distance(originalPosition, enemy.transform.position) > 0.5f, "A real Earth projectile must visibly push the enemy.");
        caster.PlayerCamera.transform.LookAt(enemy.AimPoint);
        PlayerMagicAffinity.Instance.SetActiveElement(ElementType.Lightning);
        Assert.IsTrue(caster.CastProjectile());
        yield return new WaitForSeconds(0.7f);
        Assert.IsTrue(enemy.IsStunned && enemy.StunRemaining > 1f, "A real Lightning follow-up must produce seismic stun.");
        Assert.AreEqual("SEISMIC STUN", enemy.LastFeedback);
        Assert.AreEqual(0f, enemy.GetComponentInChildren<Animator>().speed);
        SeismicResult = "PASS: real Earth then Lightning projectiles pushed, marked and stunned the enemy and stopped its animation.";
        Debug.Log(SeismicResult);
        Time.timeScale = 0f;
    }

    private static CombatTarget Target(Transform parent, Vector3 position, ElementType element)
    {
        var go = new GameObject("Test " + element);
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var hitbox = new GameObject("Child hitbox");
        hitbox.transform.SetParent(go.transform, false);
        var capsule = hitbox.AddComponent<CapsuleCollider>();
        capsule.center = Vector3.up;
        capsule.height = 2;
        capsule.radius = 0.3f;
        var target = go.AddComponent<CombatTarget>();
        Set(target, "affinity", element);
        Set(target, "maxHealth", 1000f);
        Set(target, "trainingRespawnDelay", 0f);
        go.GetComponent<Rigidbody>().useGravity = false;
        go.SetActive(true);
        return target;
    }

    private static void Matchups()
    {
        foreach (ElementType attack in Enum.GetValues(typeof(ElementType)))
        foreach (ElementType target in Enum.GetValues(typeof(ElementType)))
        {
            float expected = 1f;
            if (attack != ElementType.None && target != ElementType.None)
            {
                if (attack == target) expected = 0.5f;
                else if (((int)attack + (int)target == 3 && (int)attack <= 2 && (int)target <= 2) ||
                         ((int)attack + (int)target == 7)) expected = 1.5f;
            }
            Equal(expected, CombatTarget.Multiplier(attack, target), $"{attack} against {target}");
        }
    }

    private static void Charges()
    {
        var charges = new SpellCharges();
        for (int i = 0; i < 15; i++) Assert.IsTrue(charges.SpendProjectile());
        Assert.IsFalse(charges.SpendProjectile(), "An empty pool blocks firing");
        charges.Reload();
        charges.Advance(1);
        charges.Reload();
        Equal(2, charges.ReloadRemaining, "Repeated R cannot restart reload");
        Assert.IsFalse(charges.SpendProjectile(), "Reload blocks projectiles");
        Assert.IsTrue(charges.SpendArea(), "Area remains available while reloading");
        charges.Advance(2);
        Assert.AreEqual(15, charges.Projectiles);
        for (int i = 0; i < 4; i++) Assert.IsTrue(charges.SpendArea());
        Assert.IsFalse(charges.SpendArea(), "Empty area pool blocks casting");
        charges.Advance(8);
        Assert.AreEqual(1, charges.Areas);
        charges.Advance(45);
        Assert.AreEqual(5, charges.Areas);
        Equal(0, charges.RecoveryElapsed, "A full pool clears the recovery clock");
        charges.Reload();
        Assert.IsFalse(charges.IsReloading, "Reloading a full pool does nothing");
    }

    public static void CheckScene()
    {
        Assert.IsNotNull(Object.FindFirstObjectByType<CombatHUD>());
        Assert.AreEqual(4, GameObject.Find("Combat training").GetComponentsInChildren<CombatTarget>().Length);
        Assert.IsNull(Object.FindFirstObjectByType<ResponsiveHUD>().Content.Find("Target info"), "The top-of-screen target panel must be removed");
        foreach (var target in GameObject.Find("Combat training").GetComponentsInChildren<CombatTarget>())
        {
            Assert.IsNotNull(target.GetComponent<EnemyDetection>());
            Assert.IsNotNull(target.GetComponent<EnemyOverheadUI>());
        }
        var caster = Object.FindFirstObjectByType<SpellCaster>();
        var settings = new SerializedObject(caster);
        Equal(40, settings.FindProperty("aoeDamage").floatValue, "Scene area damage");
        Assert.IsNotNull(settings.FindProperty("chainMaterial").objectReferenceValue);
        var spells = settings.FindProperty("spells");
        for (int i = 0; i < spells.arraySize; i++)
        {
            var prefab = (GameObject)spells.GetArrayElementAtIndex(i).FindPropertyRelative("projectilePrefab").objectReferenceValue;
            Assert.IsNotNull(prefab.GetComponent<SpellProjectile>(), prefab.name + " must use gameplay hit handling.");
            Assert.IsNull(prefab.GetComponent<ProjectileMoveScript>(), prefab.name + " must not use demo movement.");
        }
        var input = new SerializedObject(Object.FindFirstObjectByType<SpellCastInput>());
        var actions = (UnityEngine.InputSystem.InputActionAsset)input.FindProperty("inputActions").objectReferenceValue;
        Assert.IsNotNull(actions.FindAction("Gameplay/Reload"));
    }
}
