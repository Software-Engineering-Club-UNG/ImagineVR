using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    public class HeldItemTests
    {
        GameObject root, head, hand;
        World world;
        GameObject releaseRig;
        [SetUp] public void Setup()
        {
            Time.captureDeltaTime = 1f / 72;
            head = new GameObject("HeldTestHead"); head.transform.position = new Vector3(0, 1.6f, 0);
            hand = new GameObject("HeldTestHand"); hand.transform.position = new Vector3(0, 1.2f, 0);
            root = new GameObject("HeldTestWorld"); world = root.AddComponent<World>(); world.Init(head.transform, null);
        }
        [TearDown] public void Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (releaseRig != null) Object.DestroyImmediate(releaseRig);
            Object.DestroyImmediate(root); Object.DestroyImmediate(head); Object.DestroyImmediate(hand);
            foreach (ParticleSystem p in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) Object.DestroyImmediate(p.gameObject);
            Library.ForgetMade();
        }
        Prop Give(string asset)
        {
            Assert.IsTrue(Statement.TryParse("{\"op\":\"give\",\"asset\":\"" + asset + "\"}", out Statement statement));
            world.Apply(statement);
            return world.Props.All.Last(p => p != null && p.Def.Id == asset);
        }
        void Throw(Prop prop, Vector3 velocity)
        {
            prop.Floating = false; prop.Body.isKinematic = false;
            prop.Body.position = new Vector3(0, 1.2f, 0);
            prop.Body.linearVelocity = velocity; prop.LetGoAt = Time.time;
            prop.Thrown = velocity.magnitude >= Grabber.ThrowSpeed;
            prop.GetComponent<HeldItemRuntime>().Released(true, velocity, hand.transform, prop.Thrown);
        }

        Grabber HoldForRelease(Prop prop, Vector3 current, Vector3 angular, params Vector3[] samples)
        {
            releaseRig = new GameObject("ReleaseIntegrationRig");
            var rig = releaseRig.AddComponent<Rig>(); rig.Build(); rig.enabled = false;
            rig.LeftGrabber.enabled = rig.RightGrabber.enabled = false;
            Grabber grabber = rig.RightGrabber;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Grabber).GetMethod("Grab", flags).Invoke(grabber, new object[] { prop, "test" });
            typeof(Grabber).GetField("_velocity", flags).SetValue(grabber, current);
            typeof(Grabber).GetField("_angular", flags).SetValue(grabber, angular);
            var recent = (Vector3[])typeof(Grabber).GetField("_recent", flags).GetValue(grabber);
            var times = (float[])typeof(Grabber).GetField("_recentAt", flags).GetValue(grabber);
            for (int i = 0; i < times.Length; i++) times[i] = -100;
            for (int i = 0; i < samples.Length; i++) { recent[i] = samples[i]; times[i] = Time.time; }
            return grabber;
        }

        static void ReleaseFromHand(Grabber grabber)
        {
            typeof(Grabber).GetMethod("Release", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grabber, new object[] { true });
        }

        [TestCase("sword")]
        [TestCase("sport_frisbee")]
        [TestCase("wooden_boomerang")]
        [TestCase("frag_grenade")]
        public void NativeItemsKeepRecentPeakAndCentreOfMassThrowVelocity(string asset)
        {
            Prop prop = Give(asset);
            Vector3 angular = Vector3.up * 4;
            Grabber grabber = HoldForRelease(prop, Vector3.forward * 0.5f, angular,
                Vector3.forward * 6, Vector3.forward * 4, Vector3.forward * 2);
            Vector3 flung = Vector3.forward * 4;
            Assert.Less((grabber.ThrowVelocity() - flung).magnitude, 0.001f);
            Vector3 arm = prop.Body.worldCenterOfMass - grabber.transform.position;
            Vector3 expected = (flung + Vector3.ClampMagnitude(Vector3.Cross(angular, arm), flung.magnitude * 0.5f)) * 1.2f;
            ReleaseFromHand(grabber);
            Assert.IsTrue(prop.Thrown);
            Assert.Less((prop.Body.linearVelocity - expected).magnitude, 0.001f, "Native behavior must not overwrite Israel's launch velocity");
            if (asset == "sport_frisbee" || asset == "wooden_boomerang") Assert.IsTrue(prop.GetComponent<HeldItemRuntime>().Busy);
            else Assert.Less((prop.Body.angularVelocity - angular).magnitude, 0.001f);
            if (asset == "frag_grenade") Assert.IsTrue(prop.GetComponent<HeldItemRuntime>().Armed);
        }

        [TestCase("sport_frisbee")]
        [TestCase("wooden_boomerang")]
        [TestCase("sword")]
        public void SlowDeliberateDropStaysPutAndDoesNotStartFlight(string asset)
        {
            Prop prop = Give(asset);
            Grabber grabber = HoldForRelease(prop, Vector3.forward * 0.9f, Vector3.up * 4, Vector3.forward * 0.9f);
            ReleaseFromHand(grabber);
            Assert.IsFalse(prop.Thrown);
            Assert.Less((prop.Body.linearVelocity - Vector3.forward * 0.9f).magnitude, 0.001f);
            Assert.AreEqual(Vector3.zero, prop.Body.angularVelocity);
            if (prop.GetComponent<HeldItemRuntime>() != null) Assert.IsFalse(prop.GetComponent<HeldItemRuntime>().Busy);
            prop.LetGoAt = Time.time - 6;
            world.Props.Tick(0.01f);
            Assert.IsFalse(prop.Floating, "A put-down item must not be recalled");
        }

        [Test] public void GenericRecoveryDoesNotInterruptNativeBoomerangFlight()
        {
            Prop prop = Give("wooden_boomerang"); Throw(prop, Vector3.forward * 5);
            prop.LetGoAt = Time.time - 5;
            world.Props.Tick(0.01f);
            Assert.IsFalse(prop.Floating);
            Assert.IsTrue(prop.GetComponent<HeldItemRuntime>().Busy);
        }

        [Test] public void OrdinaryThrownToolRecoversAfterLanding()
        {
            Prop prop = Give("sword"); prop.Drop(); prop.Thrown = true;
            prop.Body.linearVelocity = Vector3.zero; prop.LetGoAt = Time.time - 2;
            world.Props.Tick(0.01f);
            Assert.IsTrue(prop.Floating); Assert.IsFalse(prop.Thrown);
        }

        [Test] public void GrenadeDropArmsButForcedReleaseDoesNot()
        {
            Prop prop = Give("frag_grenade");
            Grabber grabber = HoldForRelease(prop, Vector3.zero, Vector3.zero, Vector3.zero);
            grabber.ForceRelease(); Assert.IsFalse(prop.GetComponent<HeldItemRuntime>().Armed);
            typeof(Grabber).GetMethod("Grab", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grabber, new object[] { prop, "test" });
            ReleaseFromHand(grabber);
            Assert.IsFalse(prop.Thrown); Assert.IsTrue(prop.GetComponent<HeldItemRuntime>().Armed);
        }
        [Test] public void ExactlyOneHundredNewHeldRecipesBuildRealMeshes()
        {
            Library.EnsureLoaded();
            Assert.AreEqual(100, Library.ExtendedHeldIds.Count);
            Assert.AreEqual(100, Library.ExtendedHeldIds.Distinct().Count());
            foreach (string id in Library.ExtendedHeldIds)
            {
                AssetDef def = Library.Asset(id);
                Assert.IsNotNull(def, id); Assert.IsTrue(def.Holdable && def.Prop, id);
                Assert.IsNotNull(def.Held, id);
                Assert.Greater(def.Mesh.vertexCount, 20, id); Assert.Greater(def.Mesh.triangles.Length, 30, id);
                Assert.Less(def.Mesh.triangles.Length / 3, 12000, id);
                Assert.IsFalse(float.IsNaN(def.Bounds.size.magnitude), id);
                Assert.Greater(def.Bounds.size.magnitude, 0.05f, id);
            }
        }
        [Test] public void EveryNewRecipeCanBeGivenWithoutStandIns()
        {
            Library.EnsureLoaded();
            foreach (string id in Library.ExtendedHeldIds)
            {
                Prop prop = Give(id);
                Assert.AreEqual(id, prop.Def.Id);
                Assert.IsNotNull(prop.GetComponent<HeldItemRuntime>(), id);
                Assert.IsNotNull(prop.Visual.GetComponent<MeshRenderer>(), id);
                world.Props.Destroy(prop);
            }
        }
        [Test] public void ExistingThrowingAndFishingItemsReceiveBehaviors()
        {
            Assert.AreEqual("returning", Library.Asset("boomerang").Held.Action);
            Assert.AreEqual("disc", Library.Asset("frisbee").Held.Action);
            Assert.AreEqual("fishing", Library.Asset("fishing_rod").Held.Action);
            Assert.AreEqual("light", Library.Asset("flashlight").Held.Action);
        }

        [Test] public void SpawningAnExtendedItemMakesAnInteractivePropByDefault()
        {
            Assert.IsTrue(Statement.TryParse("{\"op\":\"spawn\",\"asset\":\"frag_grenade\",\"id\":\"grenade\",\"at\":[0,1,3]}", out Statement statement));
            world.Apply(statement);
            Prop p = world.Props.All.Single(p => p != null && p.GroupId == "grenade");
            Assert.IsNotNull(p.GetComponent<HeldItemRuntime>());
            Assert.IsFalse(p.Floating); Assert.IsFalse(p.GetComponent<HeldItemRuntime>().Armed);
        }
        [UnityTest] public IEnumerator GrenadesOnlyArmOnDeliberateReleaseAndConsumeOnce()
        {
            Prop grenade = Give("frag_grenade");
            HeldItemRuntime action = grenade.GetComponent<HeldItemRuntime>();
            action.Released(false, Vector3.zero, hand.transform); Assert.IsFalse(action.Armed);
            Throw(grenade, Vector3.forward * 2); Assert.IsTrue(action.Armed);
            action.Detonate(); action.Detonate();
            Assert.IsFalse(world.Props.All.Contains(grenade));
            yield return null; Assert.IsTrue(grenade == null);
        }
        [UnityTest] public IEnumerator GrenadeFuseRunsWithoutTriggerAndNotOnCreation()
        {
            Prop grenade = Give("smoke_grenade");
            HeldItemRuntime action = grenade.GetComponent<HeldItemRuntime>();
            Assert.IsFalse(action.Armed);
            Throw(grenade, Vector3.forward);
            float until = Time.time + 2.7f;
            while (Time.time < until) yield return null;
            Assert.IsTrue(grenade == null);
        }
        [UnityTest] public IEnumerator ReturningThrowFliesBackInsteadOfTeleporting()
        {
            Prop boomerang = Give("wooden_boomerang");
            Throw(boomerang, Vector3.forward * 5);
            float farthest = 0;
            float until = Time.time + 4;
            while (Time.time < until && !boomerang.Floating) { farthest = Mathf.Max(farthest, Vector3.Distance(boomerang.transform.position, hand.transform.position)); yield return null; }
            Assert.Greater(farthest, 1); Assert.IsTrue(boomerang.Floating);
            Assert.Less(Vector3.Distance(boomerang.transform.position, hand.transform.position), 0.35f);
        }
        [UnityTest] public IEnumerator DiscGetsSpinAndLiftButStopsFlightOnGround()
        {
            Prop disc = Give("sport_frisbee");
            Throw(disc, Vector3.forward * 8);
            Assert.Greater(disc.Body.angularVelocity.magnitude, 15);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(disc.transform.position.z, 1);
            Assert.Greater(disc.transform.position.y, -0.3f);
        }
        [UnityTest] public IEnumerator FishingCastsReelsAndCleansUpItsLine()
        {
            Prop rod = Give("fishing_rod");
            Grabber holder = hand.AddComponent<Grabber>(); holder.enabled = false; rod.Holder = holder;
            var action = rod.GetComponent<HeldItemRuntime>();
            action.Use(true, true, new Vector3(0, 1.5f, 0), Vector3.forward);
            Assert.IsTrue(action.Cast); Assert.IsTrue(rod.GetComponentInChildren<LineRenderer>().enabled);
            float until = Time.time + 1; while (Time.time < until) yield return null;
            action.Use(true, true, new Vector3(0, 1.5f, 0), Vector3.forward);
            until = Time.time + 1; while (Time.time < until) yield return null;
            Assert.IsFalse(action.Cast);
            rod.Holder = null;
            world.Props.Destroy(rod); yield return null;
            Assert.IsFalse(world.GetComponentsInChildren<Transform>().Any(t => t.name == "FishingBobber"));
        }
        [UnityTest] public IEnumerator DrinksHavePerInstanceServingsAndLightsToggle()
        {
            Prop a = Give("soda_can"), b = Give("cola_can");
            var action = a.GetComponent<HeldItemRuntime>();
            for (int i = 0; i < 4; i++) { action.Use(true, true, a.transform.position, Vector3.forward); yield return new WaitForSeconds(0.4f); }
            Assert.AreEqual(0, action.Servings); Assert.AreEqual(3, b.GetComponent<HeldItemRuntime>().Servings);
            var light = Give("tactical_flashlight").GetComponent<HeldItemRuntime>();
            light.Use(true, true, Vector3.zero, Vector3.forward); Assert.IsTrue(light.LightOn);
            yield return new WaitForSeconds(0.4f);
            light.Use(true, true, Vector3.zero, Vector3.forward); Assert.IsFalse(light.LightOn);
        }
        [UnityTest] public IEnumerator FreezeRestoresPhysicsAndPaintChangesTint()
        {
            Prop target = Give("soda_can"); target.Floating = false; target.Body.isKinematic = false;
            target.transform.position = new Vector3(1, 1.2f, 0);
            HeldStasis.Apply(target, 0.1f); Assert.IsTrue(target.Body.isKinematic);
            yield return new WaitForSeconds(0.2f); Assert.IsFalse(target.Body.isKinematic);
            Prop grenade = Give("paint_grenade"); Throw(grenade, Vector3.zero);
            grenade.GetComponent<HeldItemRuntime>().Detonate();
            Assert.AreNotEqual(Color.white, target.Tint);
        }
        [UnityTest] public IEnumerator ProjectileEffectsUseOneRuntimeAndDetonateOnContact()
        {
            AssetDef gun = Library.Asset("rocket_launcher");
            world.Props.Fire(new Vector3(0, 1.5f, 0), Vector3.forward, Color.red, gun);
            Prop shot = world.Props.All.Last(p => p != null && p.GroupId == "shot");
            Assert.AreEqual(1, shot.GetComponents<HeldItemRuntime>().Length);
            Assert.IsTrue(shot.GetComponent<HeldItemRuntime>().Armed);
            var wall = new GameObject("PayloadWall"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1.5f, 2); wall.AddComponent<BoxCollider>().size = new Vector3(4, 4, 0.2f);
            Physics.SyncTransforms();
            for (int i = 0; i < 30 && shot != null; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(shot == null);
        }

        [UnityTest] public IEnumerator GrenadeEffectsDoNotPassThroughSolidWalls()
        {
            Prop target = Give("soda_can"); target.Floating = false; target.Body.isKinematic = false;
            target.Body.useGravity = false; target.Body.position = new Vector3(0, 1.2f, 2);
            var wall = new GameObject("BlastWall"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1.2f, 1); wall.AddComponent<BoxCollider>().size = new Vector3(4, 4, 0.2f);
            Prop grenade = Give("paint_grenade"); Throw(grenade, Vector3.zero); Physics.SyncTransforms();
            grenade.GetComponent<HeldItemRuntime>().Detonate();
            Assert.AreEqual(Color.white, target.Tint);
            yield return null;
        }

        [UnityTest] public IEnumerator LaserPointerHasAVisibleLineAndEndsOnRelease()
        {
            Prop laser = Give("laser_pointer"); var action = laser.GetComponent<HeldItemRuntime>();
            action.Use(true, true, laser.transform.position, Vector3.forward);
            var line = laser.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line); Assert.IsTrue(line.enabled); Assert.AreEqual(2, line.positionCount);
            action.Use(false, false, laser.transform.position, Vector3.forward);
            Assert.IsFalse(line.enabled);
            world.Props.Destroy(laser); yield return null;
        }
    }
}
