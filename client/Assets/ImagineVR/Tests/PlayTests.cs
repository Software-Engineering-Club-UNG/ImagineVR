using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    /// <summary>The third version of the language in the headset: expressions, counters, rules, stages, zones, parts that move, drive models.</summary>
    public sealed class PlayTests
    {
        GameObject _root, _head;
        World _world;

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
        }

        void Play(string json)
        {
            Assert.IsTrue(Statement.TryParse(json, out Statement s), json);
            _world.Apply(s);
        }

        static IEnumerator Settle() { for (int i = 0; i < 12; i++) yield return null; }

        sealed class Table : IExprHost
        {
            public Dictionary<string, object> Vars = new Dictionary<string, object>();
            public Dictionary<string, object> Answers = new Dictionary<string, object>();
            public object Var(string name) => Vars.TryGetValue(name, out object v) ? v : 0.0;
            double IExprHost.World(string function, List<string> args) => Answers.TryGetValue(function + "(" + string.Join(",", args) + ")", out object v) ? (double)v : 0.0;
        }

        /// <summary>shared/expr-cases.json: the same answers the server's expr.py gives.</summary>
        [Test]
        public void ExpressionsAgreeWithTheServer()
        {
            string path = Path.Combine(Application.dataPath, "..", "..", "shared", "expr-cases.json");
            var root = MiniJson.ParseObject(File.ReadAllText(path));
            foreach (object item in (List<object>)root["cases"])
            {
                var c = (Dictionary<string, object>)item;
                string text = (string)c["expr"];
                Expr e = Expr.Of(text);
                if (c["want"] == null) { Assert.IsFalse(e.Valid, text); continue; }
                Assert.IsTrue(e.Valid, text);
                var host = new Table();
                if (c.TryGetValue("vars", out object vars)) host.Vars = (Dictionary<string, object>)vars;
                if (c.TryGetValue("world", out object world)) host.Answers = (Dictionary<string, object>)world;
                Assert.AreEqual((double)c["want"], e.Number(host), 1e-6, text);
            }
        }

        [Test]
        public void CountersRulesAndBanners()
        {
            Play("{\"op\":\"var\",\"name\":\"score\",\"value\":0,\"label\":\"Score\"}");
            Play("{\"op\":\"var\",\"name\":\"throws\",\"value\":0}");
            Play("{\"op\":\"when\",\"id\":\"r\",\"event\":\"start\",\"do\":[{\"act\":\"add\",\"var\":\"score\",\"by\":\"2 + 3\"},{\"act\":\"set\",\"var\":\"throws\",\"to\":4}]}");
            Assert.AreEqual(5.0, _world.Play.Number("score"));
            Assert.AreEqual(4.0, _world.Play.Number("throws"));
            Assert.AreEqual(5, _world.Rules.Score, "the old score is the counter `score`");
            StringAssert.Contains("score 5", _world.Play.Header());
        }

        [UnityTest]
        public IEnumerator BecomesFiresOnTheRisingEdgeOnly()
        {
            Play("{\"op\":\"var\",\"name\":\"n\",\"value\":0}");
            Play("{\"op\":\"var\",\"name\":\"hits\",\"value\":0}");
            Play("{\"op\":\"when\",\"id\":\"b\",\"event\":\"becomes\",\"test\":\"n >= 2\",\"do\":[{\"act\":\"add\",\"var\":\"hits\"}]}");
            _world.Play.Set("n", 2.0);
            yield return Settle();
            _world.Play.Set("n", 3.0);
            yield return Settle();
            Assert.AreEqual(1.0, _world.Play.Number("hits"), "still true is not turning true again");
            _world.Play.Set("n", 0.0);
            yield return Settle();
            _world.Play.Set("n", 5.0);
            yield return Settle();
            Assert.AreEqual(2.0, _world.Play.Number("hits"));
        }

        [Test]
        public void StagesRunTheirEnterActionsAndScopeRules()
        {
            Play("{\"op\":\"var\",\"name\":\"x\",\"value\":0}");
            Play("{\"op\":\"stage\",\"id\":\"one\",\"goal\":\"First\",\"first\":true,\"next\":\"two\",\"enter\":[{\"act\":\"set\",\"var\":\"x\",\"to\":1}]}");
            Play("{\"op\":\"stage\",\"id\":\"two\",\"goal\":\"Second\",\"enter\":[{\"act\":\"set\",\"var\":\"x\",\"to\":2}]}");
            Assert.AreEqual("one", _world.Play.Stage);
            Assert.AreEqual(1.0, _world.Play.Number("x"));
            Assert.AreEqual("First", _world.Play.Objective);
            _world.Play.GoStage("next");
            Assert.AreEqual("two", _world.Play.Stage);
            Assert.AreEqual(2.0, _world.Play.Number("x"));
        }

        [UnityTest]
        public IEnumerator ZonesNoticeWhatEntersThem()
        {
            Play("{\"op\":\"var\",\"name\":\"in\",\"value\":0}");
            Play("{\"op\":\"zone\",\"id\":\"cup\",\"at\":[0,0,3],\"radius\":1}");
            Play("{\"op\":\"when\",\"id\":\"z\",\"event\":\"enter\",\"zone\":\"cup\",\"target\":\"player\",\"do\":[{\"act\":\"add\",\"var\":\"in\"}]}");
            yield return Settle();
            Assert.AreEqual(0.0, _world.Play.Number("in"));
            _head.transform.position = new Vector3(0f, 0.5f, 3f);
            yield return Settle();
            Assert.AreEqual(1.0, _world.Play.Number("in"));
        }

        [Test]
        public void MovingPartsAreCapturedAroundTheirPivots()
        {
            Play("{\"op\":\"make\",\"name\":\"cart\",\"kind\":\"vehicle\",\"parts\":[{\"shape\":\"cube\",\"position\":[0,0.6,0],\"scale\":[1.6,0.6,3]},{\"shape\":\"cylinder\",\"position\":[0.8,0.3,1],\"rotation\":[0,0,90],\"scale\":[0.6,0.2,0.6],\"role\":\"wheel\",\"mirror\":\"x\"}]}");
            AssetDef cart = Library.Asset("cart");
            Assert.IsNotNull(cart);
            Mesh full = cart.Mesh;
            Assert.AreEqual(2, cart.Parts.Count, "each wheel of a mirrored pair is its own piece");
            Assert.AreEqual(0.8f, Mathf.Abs(cart.Parts[0].Pivot.x), 0.01f);
            Assert.Less(cart.BodyMesh.vertexCount, full.vertexCount, "the body is everything but the wheels");
            Assert.AreEqual("car", DriveModel.KindFor(cart));
        }

        [Test]
        public void ACarAcceleratesTurnsAndBrakes()
        {
            Play("{\"op\":\"make\",\"name\":\"box_car\",\"kind\":\"vehicle\",\"parts\":[{\"shape\":\"cube\",\"position\":[0,0.6,0],\"scale\":[1.8,1,4.2]}]}");
            var model = new DriveModel("car", Library.Asset("box_car"), 1f);
            var gas = new DriveInput { Throttle = 1f };
            for (int i = 0; i < 120; i++) model.Step(1f / 60f, gas, Vector3.forward, 1f, 0f, out _, out _);
            Assert.Greater(model.Speed, 5f, "two seconds of throttle");
            var turnRight = new DriveInput { Throttle = 0.5f, Steer = 1f };
            float turned = 0f;
            for (int i = 0; i < 60; i++) { model.Step(1f / 60f, turnRight, Vector3.forward, 1f, 0f, out float t, out _); turned += t; }
            Assert.Greater(turned, 10f, "steering right turns it right");
            var brake = new DriveInput { Brake = 1f };
            for (int i = 0; i < 180; i++) model.Step(1f / 60f, brake, Vector3.forward, 1f, 0f, out _, out _);
            Assert.LessOrEqual(model.Speed, 0.5f, "and it stops, then backs up slowly");
        }

        [Test]
        public void APlaneNeedsSpeedToFly()
        {
            Play("{\"op\":\"make\",\"name\":\"box_plane\",\"kind\":\"flyer\",\"parts\":[{\"shape\":\"cube\",\"position\":[0,1,0],\"scale\":[8,0.3,6]}]}");
            var model = new DriveModel("plane", Library.Asset("box_plane"), 1f);
            var pull = new DriveInput { Pitch = -1f, ThrottleAxis = 1f };
            model.Step(0.1f, pull, Vector3.forward, 1f, 0f, out _, out _);
            Assert.IsFalse(model.Airborne, "not from a standstill");
            for (int i = 0; i < 1200 && !model.Airborne; i++) model.Step(1f / 60f, pull, Vector3.forward, 1f, 0f, out _, out _);
            Assert.IsTrue(model.Airborne, "but once fast enough, pulling back lifts it");
        }
    }
}
