using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ImagineVR.Tests
{
    public class BlueprintTests
    {
        GameObject _root,_head; World _world;
        const string Tool="{\"op\":\"blueprint\",\"id\":\"test_tool\",\"parts\":[{\"id\":\"handle\",\"shape\":\"cylinder\",\"scale\":[0.1,0.3,0.1],\"collider\":\"box\"},{\"id\":\"blade\",\"shape\":\"capsule\",\"position\":[0,1,0],\"color\":\"#0088FF\",\"emission\":2}],\"capabilities\":{\"grabbable\":true,\"health\":100},\"animations\":[{\"name\":\"spin\",\"part\":\"blade\",\"mode\":\"spin\",\"property\":\"rotation\",\"amplitude\":[0,45,0]}]}";
        [SetUp] public void Setup() { _head=new GameObject("Head"); _head.transform.position=Vector3.up*1.6f; _root=new GameObject("World"); _world=_root.AddComponent<World>(); _world.Init(_head.transform,null); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_root); Object.DestroyImmediate(_head); }
        static Statement Parse(string json) { Assert.IsTrue(Statement.TryParse(json,out Statement s)); return s; }
        void Build() { _world.Apply(Parse(Tool)); _world.Apply(Parse("{\"op\":\"spawn\",\"id\":\"tool\",\"asset\":\"test_tool\",\"at\":[0,0,5]}")); }
        IEnumerator Finished() { for(int i=0;i<100 && _world.Blueprints.Busy;i++) { _world.Blueprints.Tick(); yield return null; } Assert.IsFalse(_world.Blueprints.Busy); var failures=(List<object>)_world.Rules.Snapshot()["failures"]; Assert.IsEmpty(failures,string.Join("; ",failures)); }
        [Test] public void CyclicRecipeRejected() { var recipe=Parse(Tool).Fields; var parts=BlueprintData.List(recipe,"parts"); parts[0]["parent"]="blade"; parts[1]["parent"]="handle"; Assert.Throws<System.InvalidOperationException>(()=>BlueprintContract.Validate(recipe)); }
        [UnityTest] public IEnumerator BuildsNamedPartsAndGrabbableEntity()
        {
            Build(); yield return Finished(); var actor=_world.Blueprints.Instances.Single(); Assert.AreEqual(2,actor.Parts.Count); Assert.Greater(actor.Bounds.size.y,1); Assert.AreEqual(1,actor.GetComponentsInChildren<Collider>().Length); Assert.NotNull(actor.GetComponent<Prop>()); Assert.AreEqual("test_tool",actor.Entity.Asset); Assert.AreEqual(100,actor.Entity.Health); Assert.AreEqual(1,_world.Rules.Live.Count(e=>e.Asset=="test_tool"));
        }
        [UnityTest] public IEnumerator EditsPreserveIdentityAndDoNotChangeRecipeForOtherCopies()
        {
            Build(); yield return Finished(); var a=_world.Blueprints.Instances.Single(); string id=a.Entity.Id; a.Entity.Health=42; Vector3 at=a.transform.position;
            var data=Parse(Tool).Fields; BlueprintData.List(data,"parts")[1]["color"]="#FF0000";
            _world.Apply(Statement.Make("blueprint_edit","selector",new Dictionary<string,object>{{"instance",id}},"blueprint",data)); yield return Finished();
            Assert.AreEqual(id,a.Entity.Id); Assert.AreEqual(42,a.Entity.Health); Assert.AreEqual(at,a.transform.position); Assert.IsTrue(a.IsOverride);
            Assert.AreEqual("#0088FF",BlueprintData.List(_world.Blueprints.Definition("test_tool"),"parts")[1]["color"]);
        }
        [UnityTest] public IEnumerator PartActionsAndDamagePermission()
        {
            Build(); yield return Finished(); var a=_world.Blueprints.Instances.Single(); a.Action(Statement.Make("part_transform","part","blade","visible",false)); Assert.IsFalse(a.PartVisible("blade"));
            _world.Blueprints.DamagePlayer(20); Assert.AreEqual(100,_world.Blueprints.PlayerHealth); _world.Blueprints.PlayerDamageEnabled=true; _world.Blueprints.DamagePlayer(20); Assert.AreEqual(80,_world.Blueprints.PlayerHealth);
            _world.ResetEverything(0); Assert.IsFalse(_world.Blueprints.PlayerDamageEnabled); Assert.AreEqual(100,_world.Blueprints.PlayerHealth);
        }
        [Test] public void ResetCancelsConstruction()
        { Build(); Assert.IsTrue(_world.Blueprints.Busy); _world.ResetEverything(0); Assert.IsFalse(_world.Blueprints.Busy); Assert.IsFalse(_world.Blueprints.Has("test_tool")); }
        [UnityTest] public IEnumerator UnrelatedExampleObjectsShareTheSameCapabilities()
        {
            foreach(string name in new[]{"lightsaber","dragon","turret","door","six-legged-creature"})
            {
                string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../shared/blueprint-examples/"+name+".ndjson"));
                foreach(string line in System.IO.File.ReadAllLines(path)) _world.Apply(Parse(line));
                yield return Finished();
            }
            Assert.AreEqual(5,_world.Blueprints.Instances.Count());
            var dragon=_world.Blueprints.Instances.First(a=>a.Entity.Asset=="custom_dragon");
            Assert.IsTrue(dragon.Moving); Assert.IsTrue(dragon.Parts.ContainsKey("wing_l")); Assert.IsTrue(dragon.Sockets.ContainsKey("mouth"));
            int epoch=dragon.AttackEpoch; dragon.EnterState("hostile"); dragon.Fire("fire"); yield return null;
            Assert.IsNotEmpty(Object.FindObjectsByType<BlueprintProjectile>(FindObjectsSortMode.None));
            dragon.EnterState("friendly"); Assert.Greater(dragon.AttackEpoch,epoch); yield return new WaitForFixedUpdate(); yield return null;
            Assert.IsEmpty(Object.FindObjectsByType<BlueprintProjectile>(FindObjectsSortMode.None));
        }
        [UnityTest] public IEnumerator RulesToggleIndividualNamedPartsAndSnapshotsReflectChanges()
        {
            string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../shared/blueprint-examples/lightsaber.ndjson"));
            foreach(string line in System.IO.File.ReadAllLines(path)) _world.Apply(Parse(line)); yield return Finished();
            var actor=_world.Blueprints.Instances.Single(); Assert.IsFalse(actor.PartVisible("blade"));
            _world.Rules.Emit("use_start",actor.Entity); _world.Rules.Tick(0); Assert.IsTrue(actor.PartVisible("blade")); Assert.AreEqual("on",actor.State);
            _world.Rules.Emit("use_start",actor.Entity); _world.Rules.Tick(0); Assert.IsFalse(actor.PartVisible("blade")); Assert.AreEqual("off",actor.State);
            var snapshot=_world.Rules.Snapshot(); Assert.IsTrue(((Dictionary<string,object>)snapshot["overrides"]).ContainsKey(actor.Entity.Id));
        }
        [UnityTest] public IEnumerator InvalidEditKeepsOriginalInstanceAndPendingEditsCancelOnReset()
        {
            Build(); yield return Finished(); var actor=_world.Blueprints.Instances.Single(); var visual=actor.Visual; string id=actor.Entity.Id;
            var invalid=Parse(Tool).Fields; BlueprintData.List(invalid,"parts")[0]["parent"]="blade"; BlueprintData.List(invalid,"parts")[1]["parent"]="handle";
            _world.Apply(Statement.Make("blueprint_edit","selector",new Dictionary<string,object>{{"instance",id}},"blueprint",invalid));
            Assert.IsFalse(_world.Blueprints.Busy); Assert.AreEqual(visual,actor.Visual); Assert.AreEqual(id,actor.Entity.Id);
            _world.Apply(Statement.Make("blueprint_edit","selector",new Dictionary<string,object>{{"instance",id}},"blueprint",Parse(Tool).Fields)); Assert.IsTrue(_world.Blueprints.Busy);
            _world.ResetEverything(0); Assert.IsFalse(_world.Blueprints.Busy);
        }
        [UnityTest] public IEnumerator RespawnRebuildsEditedRecipeAfterDefinitionRemoval()
        {
            Build(); yield return Finished(); var actor=_world.Blueprints.Instances.Single(); string oldId=actor.Entity.Id;
            actor.Action(Statement.Make("part_transform","part","blade","visible",false));
            _world.Apply(Parse("{\"op\":\"rule\",\"id\":\"respawn_tool\",\"selector\":{\"group\":\"tool\"},\"event\":\"use_start\",\"actions\":[{\"action\":\"respawn\",\"target\":\"self\"}]}"));
            _world.Apply(Statement.Make("blueprint_remove","id","test_tool"));
            _world.Rules.Emit("use_start",actor.Entity); _world.Rules.Tick(0); yield return Finished(); yield return null;
            var fresh=_world.Blueprints.Instances.Single(); Assert.AreNotEqual(oldId,fresh.Entity.Id); Assert.AreEqual("tool",fresh.Entity.GroupId); Assert.IsFalse(fresh.PartVisible("blade")); Assert.AreEqual(100,fresh.Entity.Health); Assert.IsTrue(_world.Blueprints.Overrides.ContainsKey(fresh.Entity.Id));
        }
    }
}
