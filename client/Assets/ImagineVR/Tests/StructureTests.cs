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

        [UnityTest] public IEnumerator AnInteriorTakesItsOwnColoursAndAMisnamedRoomStillGetsItsThing()
        {
            Assert.IsTrue(Build(Plan.Replace("\"theme\":\"dungeon\"", "\"theme\":\"castle\",\"wall\":\"#FF00FF\",\"floor\":\"#00FF00\",\"light\":\"#FFAA00\",\"brightness\":0.8")));
            yield return null;
            Mesh room = root.GetComponentsInChildren<MeshFilter>().First(f => f.name == "Room_entry").sharedMesh;
            Assert.IsTrue(room.colors.Any(c => c.r > 0.3f && c.b > 0.3f && c.g < 0.05f), "the walls are the colour asked for");
            Assert.IsTrue(room.colors.Any(c => c.g > 0.5f && c.r < 0.05f && c.b < 0.05f), "and the floor");
            Assert.IsTrue(Statement.TryParse("{\"op\":\"spawn\",\"id\":\"lost\",\"asset\":\"chair\",\"room\":\"ballroom\",\"at\":[0,0,2]}", out var spawn));
            world.Apply(spawn);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.IsTrue(root.GetComponentsInChildren<MeshRenderer>().Any(r => r.transform.parent.name == "Group_lost"), "nothing is refused for naming a room that is not there");
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

        void Play(string json) { Assert.IsTrue(Statement.TryParse(json, out var s), json); world.Apply(s); }

        const string Cabin = "{\"op\":\"structure\",\"id\":\"jet\",\"theme\":\"plain\",\"entry\":\"cabin\",\"ceiling\":\"vault\",\"altitude\":40,\"halls\":false," +
            "\"rooms\":[{\"id\":\"cabin\"},{\"id\":\"cockpit\"}],\"zones\":[" +
            "{\"id\":\"cabin\",\"center\":[0,0,-1.45],\"size\":[4,2.4,18],\"openings\":[{\"side\":\"north\"}],\"windows\":[\"east\",\"west\"]}," +
            "{\"id\":\"cockpit\",\"center\":[0,0,9.2],\"size\":[3,2.4,2.5],\"openings\":[{\"side\":\"south\"}],\"windows\":[\"north\",\"east\",\"west\"]}," +
            "{\"id\":\"corridor_1\",\"center\":[0,0,7.75],\"size\":[2,2.4,0.4],\"openings\":[{\"side\":\"north\"},{\"side\":\"south\"}]}],\"doors\":[]}";

        /// <summary>
        /// "Put me in the cockpit of an airplane" came out as a windowless 5 by 6 box on the ground.
        /// An interior is the size and shape of the thing: narrow, long, arched, windowed, high up,
        /// its rooms wall to wall with an open doorway between.
        /// </summary>
        [UnityTest] public IEnumerator AnInteriorIsTheShapeOfTheThingItIs()
        {
            Assert.IsTrue(Build(Cabin)); yield return null; Physics.SyncTransforms();
            Assert.AreEqual(3, world.Structures.ZoneCount); Assert.AreEqual(0, world.Structures.DoorCount);
            float floor = world.Structures.FloorAt(Vector3.zero);
            Assert.Greater(floor, 39f, "it is up in the air");
            // glass or wall, nothing passes; and the way from cabin to cockpit is open
            Assert.IsTrue(Physics.Raycast(new Vector3(0, floor + 1.4f, 0), Vector3.right, out var wall, 4)); Assert.AreEqual("Wall", wall.collider.name);
            Assert.IsFalse(Physics.Raycast(new Vector3(0, floor + 1, 0), Vector3.forward, 9.5f), "the doorway to the cockpit is open");
            Assert.IsTrue(world.Structures.TryRoom("cockpit", out var cockpit, out var pose));
            Assert.IsFalse(world.Structures.TryRoom("corridor_1", out _, out _), "the wall between two rooms is not a room");
            Assert.AreEqual("cabin", world.Structures.RoomAt(new Vector3(1, 0, -3)).Id);
            Assert.IsTrue(cockpit.Windows.Contains("north") && !cockpit.Windows.Contains("south"));
            // a window is a hole in the wall's mesh: a windowed cabin has more to its walls than a plain one, and an arch overhead
            MeshFilter cabin = root.GetComponentsInChildren<MeshFilter>().First(m => m.name == "Room_cabin");
            Bounds b = cabin.sharedMesh.bounds;
            Assert.AreEqual(2.4f, b.max.y, 0.35f, "the arch rises to the room's height");
            world.Structures.Clear(); yield return null;
            Assert.IsFalse(world.Structures.Active);
        }

        /// <summary>
        /// Twelve desks scattered "front, radius 2 to 4" landed in a heap in a classroom, and the
        /// desks for the next room landed there too. In a room, many of one thing are arranged.
        /// </summary>
        [UnityTest] public IEnumerator ManyOfOneThingInARoomAreArrangedThere()
        {
            const string big = "{\"id\":\"entry\",\"center\":[0,0,0],\"size\":[9,3,8],\"openings\":[{\"side\":\"south\",\"door\":\"test_exit\"}]}";
            Assert.IsTrue(Build("{\"op\":\"structure\",\"id\":\"test\",\"theme\":\"house\",\"entry\":\"entry\",\"rooms\":[{\"id\":\"entry\"}],\"zones\":[" + big + "],\"doors\":[{\"id\":\"test_exit\"}]}"));
            yield return null;
            Play("{\"op\":\"scatter\",\"id\":\"desks\",\"asset\":\"table\",\"count\":12,\"room\":\"entry\",\"arrange\":\"rows\"}");
            Play("{\"op\":\"scatter\",\"id\":\"chairs\",\"asset\":\"chair\",\"count\":12,\"room\":\"entry\",\"arrange\":\"rows\",\"at\":[0,0,-0.7]}");
            Play("{\"op\":\"scatter\",\"id\":\"lockers\",\"asset\":\"bookshelf\",\"count\":8,\"room\":\"entry\",\"arrange\":\"walls\"}");
            Play("{\"op\":\"scatter\",\"id\":\"crates\",\"asset\":\"crate\",\"count\":6,\"area\":\"front\",\"radius\":[2,4]}");
            var desks = world.Objects.Get("desks").Items; var chairs = world.Objects.Get("chairs").Items;
            Assert.AreEqual(12, desks.Count); Assert.AreEqual(12, chairs.Count);
            Bounds table = Library.Asset("table").Bounds;
            for (int i = 0; i < desks.Count; i++)
            {
                Assert.Less(Mathf.Abs(desks[i].Position.x), 4.5f - table.extents.x); Assert.Less(Mathf.Abs(desks[i].Position.z), 4f - table.extents.z);
                Assert.Greater(Mathf.Abs(desks[i].Position.x), 0.5f, "an aisle runs down the middle");
                Assert.AreEqual(0.7f, (desks[i].Position - chairs[i].Position).magnitude, 0.01f, "each chair is behind its desk");
                for (int j = i + 1; j < desks.Count; j++)
                {
                    Vector3 d = desks[i].Position - desks[j].Position;
                    Assert.IsTrue(Mathf.Abs(d.x) >= table.size.x * 0.99f || Mathf.Abs(d.z) >= table.size.z * 0.99f, "desks " + i + " and " + j + " overlap");
                }
            }
            var lockers = world.Objects.Get("lockers").Items;
            Assert.Greater(lockers.Count, 3);
            foreach (Placement p in lockers)
                Assert.IsTrue(4.5f - Mathf.Abs(p.Position.x) < 1f || 4f - Mathf.Abs(p.Position.z) < 1f, "a locker stands away from every wall");
            // said without a room by someone standing in one, it still lands inside it
            foreach (Placement p in world.Objects.Get("crates").Items) { Assert.Less(Mathf.Abs(p.Position.x), 4.5f); Assert.Less(Mathf.Abs(p.Position.z), 4f); }
            world.Structures.Clear(); yield return null;
            Assert.IsNull(world.Objects.Get("desks"), "what furnished the interior goes with it");
        }
    }
}
