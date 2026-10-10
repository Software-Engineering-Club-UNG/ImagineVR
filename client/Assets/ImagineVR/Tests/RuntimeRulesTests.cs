using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    public class RuntimeRulesTests
    {
        World _world;
        GameObject _root, _head;
        [SetUp]
        public void SetUp()
        {
            _head = new GameObject("TestHead");
            _root = new GameObject("TestWorld");
            _world = _root.AddComponent<World>();
            _world.Init(_head.transform, null);
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root); Object.DestroyImmediate(_head);
            foreach (ParticleSystem particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) Object.DestroyImmediate(particles.gameObject);
        }
        static Statement Parse(string json) { Assert.IsTrue(Statement.TryParse(json, out Statement s)); return s; }
        static Statement Rule(string actions, string mode = "replace") => Parse("{\"op\":\"rule\",\"id\":\"sheep.hit\",\"selector\":{\"asset\":\"sheep\"},\"event\":\"hit\",\"filters\":{\"source_kind\":\"projectile\"},\"mode\":\"" + mode + "\",\"actions\":" + actions + "}");
        RuleEntity[] Sheep()
        {
            return Object.FindObjectsByType<RuleBody>(FindObjectsSortMode.None).Where(b => b.Entity.Asset == "sheep").Select(b => b.Entity).ToArray();
        }
        void Spawn(string id, float x) => _world.Apply(Parse("{\"op\":\"spawn\",\"id\":\"" + id + "\",\"asset\":\"sheep\",\"at\":[" + x + ",0,8],\"behavior\":\"wander\"}"));
        [Test]
        public void ReplacementTargetsOnlyHitInstance()
        {
            Spawn("one", -3); Spawn("two", 3);
            Assert.IsTrue(_world.Rules.Install(Rule("[{\"action\":\"particles\",\"preset\":\"confetti\"},{\"action\":\"remove\"}]")));
            RuleEntity[] sheep = Sheep(); Assert.AreEqual(2, sheep.Length);
            Assert.IsTrue(_world.Rules.Emit("hit", sheep[0], kind: "projectile", speed: 20));
            _world.Rules.Tick(.1f);
            Assert.IsFalse(sheep[0].Alive); Assert.IsTrue(sheep[1].Alive);
            Assert.IsTrue(_world.Rules.Install(Rule("[{\"action\":\"tint\",\"color\":\"#0000FF\"}]")));
            Assert.IsTrue(_world.Rules.Emit("hit", sheep[1], kind: "projectile", speed: 20));
            _world.Rules.Tick(.1f);
            Assert.IsTrue(sheep[1].Alive); Assert.AreEqual(Color.blue, sheep[1].Original.Tint);
        }
        [Test]
        public void RuleAppliesToFutureSheepAndBadUpdateKeepsOldRule()
        {
            Assert.IsTrue(_world.Rules.Install(Rule("[{\"action\":\"tint\",\"color\":\"#0000FF\"}]")));
            Spawn("future", 0); RuleEntity sheep = Sheep().Single();
            Assert.IsFalse(_world.Rules.Install(Rule("[{\"action\":\"execute_code\"}]")));
            Assert.IsTrue(_world.Rules.Emit("hit", sheep, kind: "projectile", speed: 20));
            _world.Rules.Tick(.1f); Assert.AreEqual(Color.blue, sheep.Original.Tint);
        }
        [Test]
        public void ResetCancelsPendingActionsAndClearsRules()
        {
            Spawn("one", 0); RuleEntity sheep = Sheep().Single();
            _world.Rules.Install(Rule("[{\"action\":\"remove\",\"delay\":1}]"));
            _world.Rules.Emit("hit", sheep, kind: "projectile", speed: 20);
            _world.Rules.Reset(); _world.Rules.Tick(2);
            Assert.IsTrue(sheep.Alive); Assert.IsFalse(_world.Rules.Replaces("hit", sheep, kind: "projectile"));
        }
        [UnityTest]
        public IEnumerator ProjectileHookSuppressesCreatureKnockdown()
        {
            Spawn("one", 0);
            _world.Rules.Install(Rule("[{\"action\":\"tint\",\"color\":\"#0000FF\"}]"));
            RuleEntity sheep = Sheep().Single();
            yield return new WaitForSeconds(.6f);
            Assert.IsTrue(sheep.Object.GetComponent<RuleBody>().Hit(null, "projectile", 20));
            yield return null;
            Assert.IsTrue(sheep.Alive); Assert.AreEqual(Color.blue, sheep.Original.Tint);
        }
    }
}
