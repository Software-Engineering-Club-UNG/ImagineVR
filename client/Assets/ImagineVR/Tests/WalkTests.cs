using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using System.Reflection;

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
        public IEnumerator GrapplingPullUsesWalkingCollisionAndCanBeCancelled()
        {
            for (int i = 0; i < 30; i++) yield return null;
            Vector3 from = _rigObject.transform.position;
            _vehicle.PullTo(from + Vector3.right * 4);
            for (int i = 0; i < 80; i++) yield return null;
            Assert.Greater(_rigObject.transform.position.x - from.x, 3.7f);
            _vehicle.PullTo(_rigObject.transform.position + Vector3.forward * 8);
            yield return Travel(Vector2.left, 30);
            Assert.Less(_walked.x, -0.5f);
            Assert.Less(Mathf.Abs(_walked.z), 0.2f);
        }

        void Ride(string asset, string extra = "")
        {
            Assert.IsTrue(Statement.TryParse("{\"op\":\"ride\",\"asset\":\"" + asset + "\"" + extra + "}", out Statement ride));
            _world.Apply(ride);
            Assert.IsTrue(_vehicle.Riding, asset);
        }

        IEnumerator Travel(Vector2 input, int frames = 120)
        {
            _vehicle.Autopilot = input;
            Vector3 from = _rigObject.transform.position;
            for (int i = 0; i < frames; i++) yield return null;
            _walked = _rigObject.transform.position - from;
        }

        [UnityTest]
        public IEnumerator AllRideKindsStrafeWithoutSteering()
        {
            foreach (string asset in new[] { "car", "horse", "dragon" })
            {
                Ride(asset);
                float yaw = _rigObject.transform.eulerAngles.y;
                yield return Travel(Vector2.right);
                Assert.Greater(_walked.x, 2f, asset);
                Assert.Less(Mathf.Abs(_walked.z), 0.05f, asset);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yaw, _rigObject.transform.eulerAngles.y)), 0.01f, asset);
                if (asset == "dragon") Assert.Greater(_walked.y, 1f, "flight altitude still rises");
                _vehicle.Autopilot = Vector2.zero;
                _vehicle.Dismount();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RidingIsHeadRelativeAndBrakesToAStop()
        {
            Ride("car");
            _rig.enabled = false;
            _rig.Head.localRotation = Quaternion.Euler(0, 90, 0);
            yield return Travel(Vector2.up);
            Assert.Greater(_walked.x, 2f);
            Assert.Less(Mathf.Abs(_walked.z), 0.05f);
            yield return Travel(Vector2.zero, 120);
            Assert.Less(_vehicle.Speed, 0.01f);
            yield return Travel(Vector2.zero, 30);
            Assert.Less(new Vector2(_walked.x, _walked.z).magnitude, 0.01f);
        }

        [UnityTest]
        public IEnumerator ReverseAndDiagonalHaveTheSameTopSpeed()
        {
            Ride("car"); yield return Travel(Vector2.up, 160);
            float forward = _vehicle.Speed;
            _vehicle.Dismount(); yield return null;
            Ride("car"); yield return Travel(Vector2.down, 160);
            Assert.AreEqual(forward, _vehicle.Speed, 0.05f);
            _vehicle.Dismount(); yield return null;
            Ride("car"); yield return Travel(Vector2.one, 160);
            Assert.AreEqual(forward, _vehicle.Speed, 0.05f);
        }

        [UnityTest]
        public IEnumerator MountedXrUsesLeftMovementAndRightSnapAcrossDismount()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var left = new InputAction("TestMove", InputActionType.Value, "<Gamepad>/leftStick");
            var right = new InputAction("TestTurn", InputActionType.Value, "<Gamepad>/rightStick");
            left.Enable(); right.Enable();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            _rig.enabled = false;
            typeof(Rig).GetField("_stickL", fields).SetValue(_rig, left);
            typeof(Rig).GetField("_stickR", fields).SetValue(_rig, right);
            typeof(Rig).GetField("<XrActive>k__BackingField", fields).SetValue(_rig, true);
            try
            {
                Ride("car");
                InputSystem.QueueDeltaStateEvent(gamepad.leftStick, Vector2.right); InputSystem.Update();
                Vector3 from = _rigObject.transform.position;
                for (int i = 0; i < 120; i++) yield return null;
                Assert.Greater(_rigObject.transform.position.x - from.x, 2);
                Assert.AreEqual(0, _rigObject.transform.eulerAngles.y, 0.01f);
                InputSystem.QueueDeltaStateEvent(gamepad.leftStick, Vector2.zero); InputSystem.Update();
                for (int i = 0; i < 120; i++) yield return null;
                _rig.Head.localPosition += Vector3.right * 0.3f;
                Vector3 head = _rig.Head.position;
                InputSystem.QueueDeltaStateEvent(gamepad.rightStick, Vector2.right); InputSystem.Update();
                for (int i = 0; i < 4; i++) yield return null;
                Assert.AreEqual(45, _rigObject.transform.eulerAngles.y, 0.01f);
                Assert.Less(Vector2.Distance(new Vector2(head.x, head.z), new Vector2(_rig.Head.position.x, _rig.Head.position.z)), 0.01f);
                Assert.Less(_vehicle.Speed, 0.01f);
                _vehicle.Dismount(); yield return null;
                Ride("horse"); yield return null;
                Assert.AreEqual(45, _rigObject.transform.eulerAngles.y, 0.01f);
                InputSystem.QueueDeltaStateEvent(gamepad.rightStick, Vector2.zero); InputSystem.Update(); yield return null;
                InputSystem.QueueDeltaStateEvent(gamepad.rightStick, Vector2.left); InputSystem.Update(); yield return null;
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0, _rigObject.transform.eulerAngles.y)), 0.01f);
            }
            finally { left.Dispose(); right.Dispose(); InputSystem.RemoveDevice(gamepad); }
        }

        [UnityTest]
        public IEnumerator ScriptedRoutesStillControlRides()
        {
            Ride("car", ",\"path\":[[0,0,0],[0,0,20]],\"loop\":false");
            yield return Travel(Vector2.right, 120);
            Assert.Greater(_walked.z, 2f);
            Assert.Less(Mathf.Abs(_walked.x), 0.05f);
        }

        [UnityTest]
        public IEnumerator GroundRideStillStopsAtSolidWalls()
        {
            var wall = new GameObject("RideWall");
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0, 2, 4);
            wall.AddComponent<BoxCollider>().size = new Vector3(12, 4, 0.2f);
            Physics.SyncTransforms();
            Ride("car");
            yield return Travel(Vector2.up, 180);
            Assert.Greater(_walked.z, 0.5f);
            Assert.Less(_rigObject.transform.position.z, 3.6f);
            Assert.Less(_vehicle.Speed, 0.1f);
        }

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
