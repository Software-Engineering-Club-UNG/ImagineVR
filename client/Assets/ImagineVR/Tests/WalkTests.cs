using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    /// <summary>
    /// Walking is a capsule pushed through the world. The user's own hands, and whatever they hold,
    /// are inside or just in front of that capsule all the time, and must never be what stops it.
    /// </summary>
    public class WalkTests
    {
        GameObject _root, _head, _rigObject;
        World _world;
        Rig _rig;
        Vehicle _vehicle;

        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = 1f / 72f;   // a headset's frame, not the thousands a second an empty test scene runs at
            _head = new GameObject("TestHead");
            _root = new GameObject("TestWorld");
            _world = _root.AddComponent<World>();
            _world.Init(_head.transform, null);
            _rigObject = new GameObject("Rig");
            _rig = _rigObject.AddComponent<Rig>();
            _rig.Build();
            _world.Head = _rig.Head;
            _vehicle = _rigObject.AddComponent<Vehicle>();
            Terminal terminal = new GameObject("TestTerminal").AddComponent<Terminal>();
            terminal.transform.SetParent(_rigObject.transform, false);
            terminal.Build(null, _rig.Head);
            _vehicle.Init(_rig, _world, terminal);
            _world.Vehicle = _vehicle;
            Assert.IsTrue(Statement.TryParse("{\"op\":\"sky\",\"preset\":\"clear_noon\",\"t\":0.2}", out Statement sky));
            _world.Apply(sky);
            Assert.IsTrue(Statement.TryParse("{\"op\":\"terrain\",\"type\":\"flat\",\"material\":\"grass\"}", out Statement ground));
            _world.Apply(ground);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            Library.ForgetMade();
            Object.DestroyImmediate(_rigObject);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_head);
        }

        IEnumerator WalkForward(float seconds)
        {
            for (int i = 0; i < 20; i++) yield return null;   // the ground finishes, the rig settles on it
            Vector3 from = _rigObject.transform.position;
            _vehicle.Autopilot = new Vector2(0f, 1f);
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
            _vehicle.Autopilot = Vector2.zero;
            _walked = _rigObject.transform.position - from;
        }
        Vector3 _walked;

        [UnityTest]
        public IEnumerator WalkingGoesForward()
        {
            yield return WalkForward(1f);
            Assert.Greater(_walked.z, 1f);
            Assert.Less(Mathf.Abs(_walked.x), 0.05f);
        }

        [UnityTest]
        public IEnumerator AHandAtTheUsersSideDoesNotPushThemAbout()
        {
            // a hand as the rig makes one, resting by the hip: inside the capsule
            var hand = new GameObject("RestingHand");
            hand.transform.SetParent(_rigObject.transform, false);
            hand.transform.localPosition = new Vector3(0.12f, 0.95f, 0.1f);
            hand.AddComponent<Rigidbody>().isKinematic = true;
            hand.AddComponent<SphereCollider>().radius = 0.05f;
            _vehicle.PassThrough(hand.GetComponent<Collider>());   // as the rig's own hands are when it is built
            for (int i = 0; i < 30; i++) yield return null;
            Vector3 stood = _rigObject.transform.position;
            for (int i = 0; i < 60; i++) yield return null;
            Assert.Less(new Vector2(_rigObject.transform.position.x - stood.x, _rigObject.transform.position.z - stood.z).magnitude, 0.02f, "standing still with a hand by the hip must not move the user");
            yield return WalkForward(1f);
            Assert.Greater(_walked.z, 1f);
            Assert.Less(Mathf.Abs(_walked.x), 0.05f);
        }

        [UnityTest]
        public IEnumerator WhatIsHeldDoesNotBlockTheWay()
        {
            Assert.IsTrue(Statement.TryParse("{\"op\":\"give\",\"asset\":\"rifle\"}", out Statement give));
            _world.Apply(give);
            Prop rifle = null;
            foreach (Prop p in _world.Props.All) if (p.Def.Id == "rifle") rifle = p;
            Assert.IsNotNull(rifle);
            for (int i = 0; i < 20; i++) yield return null;
            Vector3 from = _rigObject.transform.position;
            _vehicle.Autopilot = new Vector2(0f, 1f);
            float until = Time.time + 1f;
            while (Time.time < until)
            {
                // held across the chest, as a rifle is carried: it travels with the user
                rifle.FloatAt(_rig.Head.position + new Vector3(0f, -0.4f, 0.32f));
                Physics.SyncTransforms();
                yield return null;
            }
            _vehicle.Autopilot = Vector2.zero;
            _walked = _rigObject.transform.position - from;
            Assert.Greater(_walked.z, 1f);
            Assert.Less(Mathf.Abs(_walked.x), 0.05f, "and it does not throw them sideways");
        }
    }
}
