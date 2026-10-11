using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    /// <summary>
    /// The world, played statement by statement with no server and no headset. What these check
    /// first is that things asked for end up in real renderers: the session log once said "placed"
    /// for a day's worth of scenery that was never drawn.
    /// </summary>
    public class WorldTests
    {
        World _world;
        GameObject _root, _head;

        [SetUp]
        public void SetUp()
        {
            _head = new GameObject("TestHead");
            _head.transform.position = new Vector3(0f, 1.6f, 0f);
            _root = new GameObject("TestWorld");
            _world = _root.AddComponent<World>();
            _world.Init(_head.transform, null);
        }

        [TearDown]
        public void TearDown()
        {
            Library.ForgetMade();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_head);
            foreach (ParticleSystem particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) Object.DestroyImmediate(particles.gameObject);
            foreach (Creature c in Object.FindObjectsByType<Creature>(FindObjectsSortMode.None)) Object.DestroyImmediate(c.gameObject);
        }

        void Play(string json)
        {
            Assert.IsTrue(Statement.TryParse(json, out Statement s), json);
            _world.Apply(s);
        }

        /// <summary>Long enough for the ground to finish generating and every group to be combined.</summary>
        static IEnumerator Settle() { for (int i = 0; i < 12; i++) yield return null; }

        MeshRenderer[] Drawn(string id)
        {
            Transform group = _root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Group_" + id);
            return group != null ? group.GetComponentsInChildren<MeshRenderer>().Where(r => r.name != "Shadows").ToArray() : new MeshRenderer[0];
        }

        const string Crossbow = "{\"op\":\"make\",\"name\":\"crossbow\",\"kind\":\"tool\",\"fires\":{\"sound\":\"whoosh\",\"every\":0.2},\"parts\":[" +
            "{\"shape\":\"slab\",\"outline\":[[-0.3,0.02],[0.3,0.03],[0.3,-0.02],[-0.3,-0.08]],\"thickness\":0.045,\"color\":\"#6B4A32\"}," +
            "{\"shape\":\"tube\",\"points\":[[-0.3,0.03,0.2],[0,0.03,0.32],[0.3,0.03,0.2]],\"radius\":0.012,\"color\":\"#3A3A40\"}," +
            "{\"shape\":\"cylinder\",\"position\":[0.1,0,0],\"scale\":[0.02,0.1,0.02],\"mirror\":\"x\"}," +
            "{\"shape\":\"lathe\",\"profile\":\"nonsense\"},{\"shape\":\"what\",\"scale\":\"big\"}]}";

        [UnityTest]
        public IEnumerator AScatterIsReallyDrawn()
        {
            Play("{\"op\":\"terrain\",\"type\":\"flat\",\"material\":\"grass\"}");
            Play("{\"op\":\"scatter\",\"id\":\"trees\",\"asset\":\"oak_tree\",\"count\":40,\"area\":\"around\",\"radius\":[8,40]}");
            yield return Settle();
            MeshRenderer[] drawn = Drawn("trees");
            Assert.Greater(drawn.Length, 0, "a scatter must own real renderers");
            Assert.LessOrEqual(drawn.Length, 8, "and only a few: one per quarter of the world, solid and glow");
            int vertices = drawn.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            Assert.AreEqual(40 * Library.Asset("oak_tree").Mesh.vertexCount, vertices, "every tree is in the combined meshes");
            Assert.IsTrue(drawn.All(r => r.sharedMaterial.IsKeywordEnabled("IM_BAKED")));
            _world.Objects.Census(out int renderers, out int items, out int triangles);
            Assert.AreEqual(40, items);
            Assert.Greater(triangles, 40);
            // each vertex knows which tree it belongs to, so trees still grow and sway one by one
            var pivots = new List<Vector4>();
            drawn[0].GetComponent<MeshFilter>().sharedMesh.GetUVs(2, pivots);
            Assert.Greater(pivots.Select(p => new Vector2(p.x, p.z)).Distinct().Count(), 1);

            Play("{\"op\":\"modify\",\"id\":\"trees\",\"count\":10}");
            yield return Settle();
            _world.Objects.Census(out renderers, out items, out triangles);
            Assert.AreEqual(10, items);
            Play("{\"op\":\"remove\",\"id\":\"trees\",\"t\":0.2}");
            for (int i = 0; i < 60 && Drawn("trees").Length > 0; i++) yield return null;
            Assert.AreEqual(0, Drawn("trees").Length, "and it goes when removed");
        }

        [UnityTest]
        public IEnumerator AMadeThingIsAnAssetLikeAnyOther()
        {
            LogAssert.ignoreFailingMessages = true;   // a bad part may log; it must not stop the rest
            Play(Crossbow);
            AssetDef def = Library.FindAsset("crossbow");
            Assert.IsNotNull(def);
            Assert.IsTrue(def.Made && def.Tool && def.Holdable && def.HasMuzzle);
            Assert.AreEqual("blaster", def.Behavior);
            Assert.Greater(def.Mesh.vertexCount, 50);
            // drawn barrel-forward, held barrel-up: the long axis is now Y, and the muzzle is at the front of it
            Assert.Greater(def.Bounds.size.y, def.Bounds.size.z);
            Assert.Greater(def.Muzzle.y, 0.2f);

            Play("{\"op\":\"give\",\"asset\":\"crossbow\"}");
            Assert.AreEqual(1, _world.Props.All.Count(p => p.Def == def && p.Given));
            Play("{\"op\":\"scatter\",\"id\":\"bows\",\"asset\":\"crossbow\",\"count\":5,\"radius\":[4,9]}");
            yield return Settle();
            Assert.Greater(Drawn("bows").Length, 0);
            Play("{\"op\":\"spawn\",\"id\":\"pet\",\"asset\":\"crossbow\",\"at\":[0,0,6],\"behavior\":\"wander\"}");
            Assert.AreEqual(1, Creature.All.Count(c => c.Def == def), "even a crossbow can be alive");

            // made again under the same name, it replaces the old one; a full reset forgets it
            Play("{\"op\":\"make\",\"name\":\"crossbow\",\"parts\":[{\"shape\":\"sphere\",\"scale\":[1,1,1]}]}");
            Assert.AreNotSame(def, Library.FindAsset("crossbow"));
            _world.ResetEverything(0f);
            Assert.IsNull(Library.Asset("crossbow"));
            Assert.IsNotNull(Library.Asset("sheep"));
        }

        /// <summary>
        /// Every made gun's muzzle is the very end of its barrel, so a shot used to begin inside the
        /// gun's own body: it counted as a hit on the gun and was gone before it had moved. In the
        /// 22:46 and 23:02 sessions a glock, an AK and a fire staff were each rebuilt again and again
        /// for it, which could not help.
        /// </summary>
        [UnityTest]
        public IEnumerator AShotLeavesTheGunThatFiredIt()
        {
            LogAssert.ignoreFailingMessages = true;
            Play(Crossbow);
            AssetDef def = Library.FindAsset("crossbow");
            Play("{\"op\":\"give\",\"asset\":\"crossbow\"}");
            Prop gun = _world.Props.All.Single(p => p.Def == def);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();   // it turns to hang level as it waits

            Vector3 muzzle = gun.transform.TransformPoint(def.Muzzle), aim = gun.transform.up;
            _world.Props.Fire(muzzle, aim, Color.white, def, gun);
            Prop shot = _world.Props.All.Single(p => p.GroupId == "shot");
            Assert.Less(Vector3.Distance(shot.GetComponent<Collider>().bounds.center, muzzle), 0.01f, "the ball starts with its middle on the muzzle");
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(shot != null, "the gun's own body must not stop its shot");
            Assert.Greater(Vector3.Dot(shot.transform.position - muzzle, aim), 1f, "and it has flown on down the barrel's line");
            Object.Destroy(shot.gameObject);
            yield return null;

            // fired with nothing said about where it came from, the same shot dies on the gun: that was the bug
            _world.Props.Fire(muzzle, aim, Color.white, def);
            Prop lost = _world.Props.All.Single(p => p != null && p.GroupId == "shot");
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(lost == null, "(if this one lives, the gun was never in its way and the test above proves nothing)");
        }

        /// <summary>"Make it shoot fish", "make it shoot confetti": what leaves the barrel is whatever was asked for, and it still counts as a shot.</summary>
        [UnityTest]
        public IEnumerator AGunMayShootAnything()
        {
            Play("{\"op\":\"make\",\"name\":\"fish_gun\",\"fires\":{\"shoots\":\"apple\",\"every\":0.2},\"parts\":[{\"shape\":\"cylinder\",\"position\":[0,0,0.2],\"rotation\":[90,0,0],\"scale\":[0.04,0.4,0.04]}]}");
            AssetDef gun = Library.FindAsset("fish_gun");
            Assert.AreEqual("apple", gun.ShotAsset);
            _world.Props.Fire(new Vector3(0f, 1.5f, 1f), Vector3.forward, Color.white, gun);
            Prop shot = _world.Props.All.Single(p => p.GroupId == "shot");
            Assert.AreSame(Library.Asset("apple"), shot.Def);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(shot.transform.position.z, 1.5f);

            Play("{\"op\":\"make\",\"name\":\"party_gun\",\"fires\":{\"shoots\":\"confetti\"},\"parts\":[{\"shape\":\"cube\"}]}");
            AssetDef party = Library.FindAsset("party_gun");
            Assert.AreEqual("confetti", party.ShotBurst);
            Assert.AreEqual("", party.ShotAsset);
            Assert.DoesNotThrow(() => _world.Props.Fire(new Vector3(0f, 1.5f, 1f), Vector3.forward, Color.white, party));
        }

        /// <summary>A row of loose things is a row: `repeat` used to be dropped for anything with physics, so four pins came out as one.</summary>
        [Test]
        public void ARowOfLooseThingsIsAllThere()
        {
            Play("{\"op\":\"spawn\",\"id\":\"back_row\",\"asset\":\"bowling_pin\",\"at\":[-0.45,0,10],\"repeat\":4,\"step\":[0.3,0,0],\"physics\":true}");
            Prop[] pins = _world.Props.All.Where(p => p.GroupId == "back_row").ToArray();
            Assert.AreEqual(4, pins.Length);
            Assert.AreEqual(0.9f, pins.Max(p => p.transform.position.x) - pins.Min(p => p.transform.position.x), 0.01f);
            Play("{\"op\":\"remove\",\"id\":\"back_row\"}");
            Assert.AreEqual(0, _world.Props.All.Count(p => p != null && p.GroupId == "back_row"));
        }

        /// <summary>
        /// A built place is laid out from stretched plain shapes and rows. Each piece used to be turned
        /// to face the user, so a lane or a wall off to one side lay skewed across the room, and a row
        /// ran the wrong way: the object's right is the user's left.
        /// </summary>
        [UnityTest]
        public IEnumerator BuildingBlocksLieSquareAndRowsRunAsWritten()
        {
            Play("{\"op\":\"terrain\",\"type\":\"flat\",\"material\":\"concrete\"}");
            Play("{\"op\":\"spawn\",\"id\":\"lanes\",\"asset\":\"cube\",\"at\":[-2.4,0,7.5],\"scale\":[1.1,0.02,13],\"repeat\":3,\"step\":[2.4,0,0]}");
            yield return Settle();
            // (from the vertices: a renderer's own bounds are padded so nothing is culled while it grows in)
            Vector3[] points = Drawn("lanes").SelectMany(r => r.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => r.transform.TransformPoint(v))).ToArray();
            Assert.AreEqual(-2.95f, points.Min(v => v.x), 0.05f, "the first lane is where it was put, not turned");
            Assert.AreEqual(2.95f, points.Max(v => v.x), 0.05f, "and the others follow to the right");
            Assert.AreEqual(1f, points.Min(v => v.z), 0.05f);
            Assert.AreEqual(14f, points.Max(v => v.z), 0.05f);
        }

        /// <summary>
        /// The terminal keeps its last 300 lines. Once it was full, the next thing said threw (the new
        /// line was looked up at a place the list no longer reached) and the request was never played:
        /// to the user, the app had stopped listening. It took about fourteen minutes to get there.
        /// </summary>
        [Test]
        public void TheTerminalTakesPromptsHoweverMuchHasBeenPrinted()
        {
            var go = new GameObject("TestTerminal");
            try
            {
                Terminal terminal = go.AddComponent<Terminal>();
                terminal.Build(null, _head.transform);
                for (int i = 0; i < 320; i++) terminal.Note("line " + i);
                object handle = null;
                Assert.DoesNotThrow(() => handle = terminal.Prompt("..."));
                Assert.IsNotNull(handle);
                Assert.DoesNotThrow(() => terminal.FillPrompt(handle, "put me in a bowling alley"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AMakeWithNothingUsableStillMakesSomething()
        {
            Play("{\"op\":\"make\",\"name\":\"blank\",\"parts\":[]}");
            Assert.Greater(Library.Asset("blank").Mesh.vertexCount, 0);
            Play("{\"op\":\"make\",\"name\":\"beast\",\"kind\":\"animal\",\"parts\":[{\"shape\":\"blob\",\"position\":[0,0.8,0],\"scale\":[0.6,0.6,1.2]},{\"shape\":\"cylinder\",\"position\":[0.2,0.3,0.4],\"scale\":[0.1,0.6,0.1],\"mirror\":\"x\",\"parent\":\"nobody\"}]}");
            AssetDef beast = Library.Asset("beast");
            Assert.AreEqual("animals", beast.Category);
            Assert.Greater(beast.Bounds.size.x, 0.45f, "the mirrored leg is on both sides");
            Play("{\"op\":\"make\",\"name\":\"jet\",\"kind\":\"flyer\",\"parts\":[{\"shape\":\"capsule\",\"scale\":[1,1,4],\"rotation\":[90,0,0]}]}");
            Assert.IsTrue(Library.Asset("jet").FliesRidden);
        }

        [UnityTest]
        public IEnumerator RulesReactToWhatHappens()
        {
            Play("{\"op\":\"spawn\",\"id\":\"a\",\"asset\":\"sheep\",\"at\":[2,0,8],\"behavior\":\"wander\"}");
            Play("{\"op\":\"spawn\",\"id\":\"b\",\"asset\":\"pig\",\"at\":[-2,0,8],\"behavior\":\"wander\"}");
            yield return null;
            Creature sheep = Creature.All.Single(c => c.Def.Id == "sheep"), pig = Creature.All.Single(c => c.Def.Id == "pig");
            Play("{\"op\":\"when\",\"id\":\"pop\",\"event\":\"hit\",\"target\":\"sheep\",\"do\":[{\"act\":\"burst\",\"kind\":\"confetti\"},{\"act\":\"score\",\"add\":5},{\"act\":\"spawn\",\"asset\":\"apple\",\"count\":2,\"physics\":true},{\"act\":\"remove\",\"delay\":0.05}]}");
            Assert.AreEqual(1, _world.Rules.Count);

            Assert.IsFalse(_world.Rules.Fire("hit", Rules.Of(pig)), "a rule about sheep is not about pigs");
            Assert.AreEqual(0, _world.Rules.Score);
            Assert.IsTrue(_world.Rules.Fire("hit", Rules.Of(sheep)), "a rule that removes the thing has taken it over");
            Assert.AreEqual(5, _world.Rules.Score);
            Assert.AreEqual(2, _world.Props.All.Count(p => p.Def.Id == "apple"));
            yield return new WaitForSeconds(0.2f);
            yield return null;
            Assert.IsTrue(sheep == null, "the delayed remove ran");
            Assert.IsTrue(pig != null);

            // a rule that only decorates leaves the ordinary knock-down to happen
            Play("{\"op\":\"when\",\"id\":\"ding\",\"event\":\"hit\",\"target\":\"any\",\"once\":true,\"do\":[{\"act\":\"sound\",\"name\":\"chime\"},{\"act\":\"tint\",\"color\":\"#0000FF\"}]}");
            Assert.IsFalse(_world.Rules.Fire("hit", Rules.Of(pig)));
            Assert.IsFalse(_world.Rules.Fire("hit", Rules.Of(pig)));
            Play("{\"op\":\"clear\"}");
            Assert.AreEqual(0, _world.Rules.Count);
            Assert.AreEqual(0, _world.Rules.Score);
        }

        [UnityTest]
        public IEnumerator ACityStreetIsBuiltSolidAndDrawn()
        {
            Play("{\"op\":\"terrain\",\"type\":\"flat\",\"material\":\"asphalt\"}");
            Play("{\"op\":\"scatter\",\"id\":\"left\",\"asset\":\"tower\",\"count\":8,\"along\":[[-14,0,0],[-14,0,80]],\"solid\":true}");
            Play("{\"op\":\"scatter\",\"id\":\"right\",\"asset\":\"office_block\",\"count\":8,\"along\":[[14,0,0],[14,0,80]],\"solid\":true}");
            Play("{\"op\":\"spawn\",\"id\":\"taxi\",\"asset\":\"car\",\"path\":[[-4,0,10],[-4,0,70],[4,0,70],[4,0,10]],\"speed\":10}");
            Play("{\"op\":\"spawn\",\"id\":\"steps\",\"asset\":\"cube\",\"at\":[3,0,4],\"scale\":[2,0.25,0.5],\"repeat\":6,\"step\":[0,0.25,0.5],\"solid\":true}");
            yield return Settle();
            Assert.Greater(Drawn("left").Length, 0);
            Assert.Greater(Drawn("right").Length, 0);
            Assert.Greater(Drawn("steps").Length, 0);
            Assert.AreEqual(8 + 8 + 6, _root.GetComponentsInChildren<BoxCollider>().Count(c => c.name == "Solid"));
            Assert.AreEqual(1, Object.FindObjectsByType<Mover>(FindObjectsSortMode.None).Length);
            _world.Objects.Census(out int renderers, out int items, out int triangles);
            Assert.AreEqual(22, items);
        }

        [UnityTest]
        public IEnumerator ThingsToPlayWithArePhysical()
        {
            Play("{\"op\":\"terrain\",\"type\":\"flat\"}");
            Play("{\"op\":\"scatter\",\"id\":\"pins\",\"asset\":\"bowling_pin\",\"count\":10,\"area\":\"front\",\"radius\":[3,4],\"physics\":true}");
            Play("{\"op\":\"spawn\",\"id\":\"ball\",\"asset\":\"bowling_ball\",\"at\":[0,0.5,1.5],\"physics\":true}");
            yield return null;
            Assert.AreEqual(10, _world.Props.All.Count(p => p.Def.Id == "bowling_pin"));
            Prop ball = _world.Props.All.Single(p => p.Def.Id == "bowling_ball");
            Assert.IsFalse(ball.Body.isKinematic);
            Play("{\"op\":\"clear\"}");
            yield return null;
            Assert.AreEqual(0, _world.Props.All.Count);
        }
    }
}
