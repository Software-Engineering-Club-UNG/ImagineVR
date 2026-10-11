using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    public class StructureTests
    {
        GameObject root, head;
        World world;
        const string Room = "{\"id\":\"entry\",\"center\":[0,0,0],\"size\":[6,3,8],\"openings\":[{\"side\":\"south\",\"door\":\"test_exit\"}]}";
        const string Plan = "{\"op\":\"structure\",\"id\":\"test\",\"theme\":\"dungeon\",\"entry\":\"entry\",\"zones\":[" + Room + "],\"doors\":[{\"id\":\"test_exit\"}]}";
        [SetUp] public void Setup()
        {
            root = new GameObject("TestWorld"); head = new GameObject("Head");
            head.transform.position = Vector3.up * 1.6f;
            world = root.AddComponent<World>(); world.Init(head.transform, null);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(head); }
        bool Build(string json) { Assert.IsTrue(Statement.TryParse(json, out var s)); return world.Structures.Build(s); }

        [UnityTest] public IEnumerator EnclosureHasRealWallsAndDoorGap()
        {
            Assert.IsTrue(Build(Plan)); yield return null; Physics.SyncTransforms();
            Assert.AreEqual(1, world.Structures.ZoneCount); Assert.AreEqual(1, world.Structures.DoorCount);
            Assert.Greater(root.GetComponentsInChildren<MeshRenderer>().Length, 2);
            float floor = world.Structures.FloorAt(Vector3.zero);
            Assert.IsTrue(Physics.Raycast(new Vector3(0, floor + 1, 0), Vector3.right, out var wall, 4));
            Assert.AreEqual("Wall", wall.collider.name);
            Assert.IsTrue(Physics.Raycast(new Vector3(0, floor + 1, 0), Vector3.up, out var roof, 4));
            Assert.AreEqual("Ceiling", roof.collider.name);
            var door = world.Structures.Doors.Single(); door.Set("open");
            Assert.IsTrue(door.Advance(1)); Physics.SyncTransforms();
            Assert.IsTrue(door.Open);
            Assert.IsFalse(Physics.Raycast(new Vector3(0, floor + 1, 0), Vector3.back, 5));
            door.Set("close"); door.Advance(1); Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(0, floor + 1, 0), Vector3.back, 5));
        }

        [UnityTest] public IEnumerator BlockedDoorDoesNotCloseThroughPlayer()
        {
            Assert.IsTrue(Build(Plan)); yield return null;
            var door = world.Structures.Doors.Single(); door.Set("open"); door.Advance(1);
            var player = new GameObject("PlayerProbe"); var cc = player.AddComponent<CharacterController>();
            cc.height = 1.6f; cc.radius = 0.3f; cc.center = Vector3.up * 0.8f;
            player.transform.position = new Vector3(0, world.Structures.FloorAt(Vector3.zero), -4);
            door.Set("close"); Assert.IsFalse(door.Advance(1)); Assert.IsTrue(door.Open);
            Object.Destroy(player); yield return null; Assert.IsTrue(door.Advance(1));
        }

        [UnityTest] public IEnumerator ReplacementFailurePreservesExistingRoomAndClearRemovesIt()
        {
            Assert.IsTrue(Build(Plan)); yield return null;
            Assert.IsFalse(Build("{\"op\":\"structure\",\"id\":\"bad\",\"zones\":[]}"));
            Assert.AreEqual("test", world.Structures.Id);
            Assert.IsTrue(world.Structures.HeadBlocked(new Vector3(3, 1.6f, 0)));
            world.Structures.Clear(); yield return null;
            Assert.IsFalse(world.Structures.Active); Assert.AreEqual(0, Shader.GetGlobalInt("_ImRoomCount"));
        }

        [Test] public void SnapTurnsRequireStickRelease()
        {
            bool armed = true;
            Assert.AreEqual(45, Vehicle.Snap(0.8f, ref armed));
            Assert.AreEqual(0, Vehicle.Snap(1, ref armed));
            Assert.AreEqual(0, Vehicle.Snap(-1, ref armed));
            Assert.AreEqual(0, Vehicle.Snap(0.2f, ref armed));
            Assert.AreEqual(-45, Vehicle.Snap(-0.8f, ref armed));
        }

        [Test] public void WalkingStrafesAndDoesNotSpeedUpDiagonally()
        {
            Assert.AreEqual(Vector3.right, Vehicle.WalkDirection(Vector3.forward, Vector2.right));
            Assert.AreEqual(Vector3.back, Vehicle.WalkDirection(Vector3.forward, Vector2.down));
            Assert.AreEqual(Vector3.back, Vehicle.WalkDirection(Vector3.right, Vector2.right));
            Assert.AreEqual(1, Vehicle.WalkDirection(Vector3.forward, Vector2.one).magnitude, 0.001f);
        }

        [UnityTest] public IEnumerator RoomLocalFurnishingUsesRaisedFloor()
        {
            Assert.IsTrue(Build(Plan));
            Assert.IsTrue(Statement.TryParse("{\"op\":\"spawn\",\"id\":\"chair\",\"asset\":\"chair\",\"room\":\"entry\",\"at\":[1,0,1]}", out var spawn));
            world.Apply(spawn);
            for (int i = 0; i < 12; i++) yield return null;
            var renderer = root.GetComponentsInChildren<MeshRenderer>().First(r => r.transform.parent.name == "Group_chair");
            // Scenery deliberately expands renderer bounds for shader animation; inspect geometry, not culling bounds.
            float bottom = renderer.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => renderer.transform.TransformPoint(v).y);
            Assert.AreEqual(world.Structures.FloorAt(Vector3.zero), bottom, 0.08f);
        }

        [UnityTest] public IEnumerator PlayerCapsuleIsBlockedByWallAndClosedDoor()
        {
            Assert.IsTrue(Build(Plan)); yield return null;
            var player = new GameObject("PlayerProbe"); var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.3f; cc.height = 1.6f; cc.center = Vector3.up * 0.8f;
            float floor = world.Structures.FloorAt(Vector3.zero);
            player.transform.position = Vector3.up * floor;
            Physics.SyncTransforms(); cc.Move(Vector3.right * 10);
            Assert.Less(player.transform.position.x, 2.8f);
            cc.enabled = false; player.transform.position = Vector3.up * floor; cc.enabled = true;
            cc.Move(Vector3.back * 10); Assert.Greater(player.transform.position.z, -3.8f);
            Object.Destroy(player);
        }

        [UnityTest] public IEnumerator ConnectedRoomsRequireMatchingDoorwayOpenings()
        {
            const string connected = "{\"op\":\"structure\",\"id\":\"connected\",\"entry\":\"entry\",\"zones\":[" +
                "{\"id\":\"entry\",\"center\":[0,0,0],\"size\":[6,3,8],\"openings\":[{\"side\":\"north\"}]}," +
                "{\"id\":\"hall\",\"center\":[0,0,12],\"size\":[6,3,8],\"openings\":[{\"side\":\"south\",\"door\":\"hall_door\"}]}," +
                "{\"id\":\"corridor\",\"center\":[0,0,6],\"size\":[2,3,4],\"openings\":[{\"side\":\"north\"},{\"side\":\"south\"}]}],\"doors\":[{\"id\":\"hall_door\"}]}";
            Assert.IsTrue(Build(connected)); yield return null;
            Assert.AreEqual(3, world.Structures.ZoneCount);
            Assert.IsFalse(Build(connected.Replace("\"doors\":[{\"id\":\"hall_door\"}]", "\"doors\":[{\"id\":\"missing\"}]")));
            Assert.AreEqual("connected", world.Structures.Id);
            Assert.IsFalse(Build(connected.Replace("\"id\":\"entry\",\"center\":[0,0,0],\"size\":[6,3,8],\"openings\":[{\"side\":\"north\"}]", "\"id\":\"entry\",\"center\":[0,0,0],\"size\":[6,3,8],\"openings\":[]")));
            Assert.AreEqual("connected", world.Structures.Id);
        }

        [UnityTest] public IEnumerator EntryAndRemovalPlaceTrackedHeadSafely()
        {
            var rigObject = new GameObject("Rig"); var rig = rigObject.AddComponent<Rig>(); rig.Build();
            world.Head = rig.Head;
            var vehicle = rigObject.AddComponent<Vehicle>(); vehicle.Init(rig, world, null); world.Vehicle = vehicle;
            Assert.IsTrue(Build(Plan.Replace("\"theme\":", "\"enter\":true,\"theme\":")));
            yield return null;
            Assert.AreEqual("entry", world.Structures.CurrentRoom(rig.Head.position));
            Assert.AreEqual(world.Structures.FloorAt(Vector3.zero), rigObject.transform.position.y, 0.05f);
            world.Structures.Clear(); yield return null;
            Assert.IsFalse(world.Structures.Active);
            Assert.AreEqual(world.Terrain.HeightAt(rig.Head.position.x, rig.Head.position.z), rigObject.transform.position.y, 0.05f);
            Object.Destroy(rigObject); world.Vehicle = null; yield return null;
        }
    }
}
