using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImagineVR
{
    public sealed class BlueprintSystem
    {
        readonly World _world;
        readonly Dictionary<string,Dictionary<string,object>> _definitions = new Dictionary<string,Dictionary<string,object>>();
        readonly List<Build> _jobs = new List<Build>();
        readonly List<BlueprintActor> _instances = new List<BlueprintActor>();
        readonly List<GameObject> _shots = new List<GameObject>();
        readonly Dictionary<string,GameObject> _projectileTemplates=new Dictionary<string,GameObject>();
        int _serial;
        public bool PlayerDamageEnabled;
        public float PlayerHealth { get; private set; } = 100;
        public bool Busy => _jobs.Count>0;
        public IEnumerable<BlueprintActor> Instances => _instances.Where(a=>a!=null && a.gameObject.activeInHierarchy);
        public bool Has(string id) => id!=null && _definitions.ContainsKey(id);
        public void Modify(Statement s)
        {
            string id=s.Str("id");
            foreach(var a in Instances.Where(a=>a.Entity!=null && (a.Entity.GroupId==id || a.Entity.GroupId.StartsWith(id+".",StringComparison.Ordinal))))
            { if(s.TryColor("tint",out Color color)) a.Entity.Tint(color); if(s.Has("scale")) a.Entity.Resize(s.Num("scale",1)); }
        }
        public Dictionary<string,object> Definition(string id) => Has(id)?BlueprintData.Clone(_definitions[id]):null;
        public List<object> Definitions => _definitions.Values.Select(d=>(object)d).ToList();
        public Dictionary<string,object> Overrides => Instances.Where(a=>a.IsOverride && a.Entity!=null).ToDictionary(a=>a.Entity.Id,a=>(object)a.Recipe);
        public bool CanOverride(BlueprintActor actor,Dictionary<string,object> recipe)
        { var trial=Overrides; if(actor.Entity!=null) trial[actor.Entity.Id]=recipe; return MiniJson.Serialize(trial).Length<=128000; }

        sealed class Build
        {
            public Dictionary<string,object> Recipe;
            public Statement Spawn;
            public GameObject Root, Visual, Placeholder;
            public BlueprintActor Edit;
            public bool Editing;
            public Group Group;
            public List<Dictionary<string,object>> Remaining;
            public Dictionary<string,Transform> Parts = new Dictionary<string,Transform>();
            public List<Mesh> Meshes = new List<Mesh>();
            public int Triangles;
        }
        public BlueprintSystem(World world)
        {
            _world=world;
            var hit=new GameObject("PlayerHitRegion"); hit.layer=2; hit.transform.SetParent(world.transform,false);
            var region=hit.AddComponent<PlayerHitRegion>(); region.System=this; region.Head=world.Head;
            var c=hit.AddComponent<SphereCollider>(); c.radius=.3f; c.isTrigger=true;
            hit.AddComponent<Rigidbody>().isKinematic=true;
        }
        public void DamagePlayer(float damage)
        {
            if(!PlayerDamageEnabled || PlayerHealth<=0) return;
            PlayerHealth=Mathf.Max(0,PlayerHealth-Mathf.Clamp(damage,0,1000));
            _world.Emit(PlayerHealth<=0?"Session health depleted — reset health locally to continue":"Health: "+Mathf.CeilToInt(PlayerHealth));
            Rig rig=_world.Head!=null?_world.Head.GetComponentInParent<Rig>():null; rig?.Haptic(true,.25f,.1f);
        }
        public void ResetHealth() { PlayerHealth=100; }
        public bool CanMove(BlueprintActor actor) => Instances.Count(a=>a!=actor && a.Moving)+_world.Rules.Live.Count(e=>e.Object!=null && e.Object.GetComponent<Creature>()!=null)<Creature.MaxLive;
        public void Apply(Statement s)
        {
            try
            {
                if(s.Op=="blueprint_remove") { _definitions.Remove(s.Str("id")); return; }
                if(s.Op=="blueprint_edit")
                {
                    RuleContract.Selector(s.Obj("selector")); var recipe=s.Obj("blueprint"); BlueprintContract.Validate(recipe);
                    CheckReferences(recipe);
                    var targets=Instances.Where(a=>_world.Rules.Selects(s.Obj("selector"),a.Entity)).ToArray();
                    if(targets.Length==0) throw new InvalidOperationException("No living blueprint targets");
                    if(_jobs.Count+targets.Length>16) throw new InvalidOperationException("Build queue limit");
                    var overrides=Overrides; foreach(var a in targets) overrides[a.Entity.Id]=recipe;
                    BlueprintContract.Require(MiniJson.Serialize(overrides).Length<=128000,"Override byte budget");
                    foreach(var a in targets) Queue(recipe,null,a);
                    return;
                }
                BlueprintContract.Validate(s.Fields);
                string id=s.Str("id");
                BlueprintContract.Require(Library.Asset(id)==null,"Blueprint ID conflicts with baked asset");
                BlueprintContract.Require(Has(id)||_definitions.Count<64,"Blueprint registry limit");
                var recipeCopy=BlueprintData.Clone(s.Fields);
                if(!recipeCopy.ContainsKey("revision")) recipeCopy["revision"]=(double)(Has(id)?BlueprintData.Wrap(_definitions[id]).Int("revision",1)+1:1);
                CheckReferences(recipeCopy,id);
                var trial=new Dictionary<string,object>(_definitions.ToDictionary(p=>p.Key,p=>(object)p.Value)); trial[id]=recipeCopy;
                BlueprintContract.Require(MiniJson.Serialize(trial).Length<=256000,"Blueprint registry byte limit");
                _definitions[id]=recipeCopy;
            }
            catch(Exception ex) { _world.Rules.Fail("Blueprint rejected: "+ex.Message); }
        }
        void CheckReferences(Dictionary<string,object> recipe,string installing=null)
        {
            foreach(var emitter in BlueprintData.List(BlueprintData.Wrap(recipe).Obj("capabilities"),"emitters"))
            { string asset=BlueprintData.Wrap(emitter).Str("asset"); BlueprintContract.Require(Library.Asset(asset)!=null || Has(asset) || asset==installing,"Unknown projectile asset"); }
        }
        public bool Spawn(Statement s)
        {
            if(!Has(s.Str("asset"))) return false;
            try
            {
                BlueprintContract.Require(_jobs.Count<16,"Build queue limit");
                BlueprintContract.Require(_world.Rules.Live.Count(e=>e.Object!=null && e.GroupId!="shot")+_jobs.Count<RuleEngine.MaxInteractive,"Interaction budget exceeded");
                BlueprintContract.Require(s.Op=="give" || _world.Objects.CountKind("spawn")<ObjectSystem.MaxSpawned,"Object budget exceeded");
                Queue(Definition(s.Str("asset")),s,null);
            }
            catch(Exception ex) { _world.Rules.Fail("Blueprint spawn rejected: "+ex.Message); }
            return true;
        }
        public void Respawn(RuleEntity entity,Dictionary<string,object> recipe)
        {
            try
            {
                BlueprintContract.Validate(recipe);
                BlueprintContract.Require(_jobs.Count<16 && _world.Rules.Live.Count(e=>e.Object!=null && e.GroupId!="shot")+_jobs.Count<RuleEngine.MaxInteractive,"Respawn interaction budget exceeded");
                BlueprintContract.Require(_world.Objects.CountKind("spawn")<ObjectSystem.MaxSpawned,"Respawn object budget exceeded");
                Vector3 at=entity.BirthPosition; at.y-=_world.Terrain.HeightAt(at.x,at.z);
                var spawn=Statement.Make("spawn","asset",entity.Asset,"id",entity.GroupId,"at",BlueprintData.Vector(at),"scale",BlueprintData.Vector(entity.BirthScale),"rot",BlueprintData.Vector(entity.BirthRotation.eulerAngles),"tags",entity.Tags.Cast<object>().ToList(),"blueprint_respawn",true);
                Queue(recipe,spawn,null);
            }
            catch(Exception ex) { _world.Rules.Fail("Blueprint respawn rejected: "+ex.Message); }
        }
        void Queue(Dictionary<string,object> recipe,Statement spawn,BlueprintActor edit)
        {
            // An edit never changes the old visual until the entire staged recipe is built.
            if(edit!=null) Cancel(edit);
            var job=new Build { Recipe=BlueprintData.Clone(recipe), Spawn=spawn, Edit=edit, Editing=edit!=null, Remaining=BlueprintData.List(recipe,"parts") };
            if(edit!=null) job.Recipe["revision"]=(double)(BlueprintData.Wrap(edit.Recipe).Int("revision",1)+1);
            job.Root=new GameObject("Blueprint_"+recipe["id"]); job.Root.transform.SetParent(_world.transform,false); job.Root.SetActive(false);
            job.Visual=new GameObject("Visual"); job.Visual.transform.SetParent(job.Root.transform,false);
            Vector3 position; Quaternion rotation;
            if(edit!=null) { position=edit.transform.position; rotation=edit.transform.rotation; job.Root.transform.localScale=edit.transform.localScale; }
            else
            {
                string id=spawn.Str("id","blueprint_"+(++_serial)); spawn.Fields["id"]=id;
                if(spawn.Op=="give")
                {
                    Vector3 forward=_world.Head!=null?_world.Head.forward:Vector3.forward; forward.y=0; forward=forward.normalized;
                    position=(_world.Head!=null?_world.Head.position:new Vector3(0,1.6f,0))+forward*.6f-Vector3.up*.3f; rotation=Quaternion.identity;
                }
                else
                {
                    Vector3 local;
                    if(!spawn.TryVec3("at",out local))
                    { float d=spawn.Num("distance",4),h=spawn.Num("height",0); string place=spawn.Str("place","front"); local=place=="left"?new Vector3(-d,h,0):place=="right"?new Vector3(d,h,0):place=="behind"?new Vector3(0,h,-d):place=="center"?new Vector3(0,h,0):place=="above"?new Vector3(0,Mathf.Max(3,d)+h,0):new Vector3(0,h,d); }
                    position=Statement.InWorld(spawn.Frame(),local); position.y+=_world.Terrain.HeightAt(position.x,position.z);
                    rotation=spawn.Frame().rotation*Quaternion.Euler(BlueprintData.Vec(spawn.Fields,"rot",new Vector3(0,spawn.Num("rot_y"),0)));
                    job.Group=_world.Objects.Begin(id,"spawn",spawn,.2f); job.Group.Extras.Add(job.Root);
                    job.Group.Source.Fields["blueprint_instance"]=true;
                }
                job.Root.transform.localScale=spawn.Scale("scale",1);
            }
            job.Root.transform.SetPositionAndRotation(position,rotation);
            job.Placeholder=GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(job.Placeholder.GetComponent<Collider>());
            job.Placeholder.transform.SetParent(_world.transform,false); job.Placeholder.transform.position=position+Vector3.up*.25f; job.Placeholder.transform.localScale=Vector3.one*.3f;
            job.Placeholder.GetComponent<Renderer>().sharedMaterial=_world.Objects.LitMaterial;
            var label=new GameObject("BuildingLabel"); label.transform.SetParent(job.Placeholder.transform,false); label.transform.localPosition=Vector3.up*1.5f;
            var tmp=label.AddComponent<TextMeshPro>(); tmp.font=_world.Font; tmp.text="Building "+recipe["id"]; tmp.fontSize=3; tmp.alignment=TextAlignmentOptions.Center; label.AddComponent<FaceUser>();
            if(job.Group!=null) job.Group.Extras.Add(job.Placeholder);
            _jobs.Add(job); _world.Note("Building "+recipe["id"]+" from "+job.Remaining.Count+" parts");
        }
        void Cancel(BlueprintActor actor)
        { foreach(var j in _jobs.Where(j=>j.Edit==actor && actor!=null).ToArray()) { Destroy(j); _jobs.Remove(j); } }
        void Destroy(Build j)
        { if(j.Root!=null) Object.Destroy(j.Root); if(j.Placeholder!=null) Object.Destroy(j.Placeholder); foreach(var mesh in j.Meshes) if(mesh!=null) Object.Destroy(mesh); }
        public void CancelPending()
        { foreach(var j in _jobs) Destroy(j); _jobs.Clear(); }
        public void Reset(bool definitions=false)
        { CancelPending(); foreach(var shot in _shots) if(shot!=null) Object.Destroy(shot); _shots.Clear(); foreach(var template in _projectileTemplates.Values) if(template!=null) Object.Destroy(template); _projectileTemplates.Clear(); if(definitions) { _definitions.Clear(); PlayerDamageEnabled=false; ResetHealth(); } }
        public void Tick()
        {
            _instances.RemoveAll(a=>a==null); _shots.RemoveAll(a=>a==null);
            foreach(var group in _world.Objects.Groups.Where(g=>g.Source!=null && g.Source.Flag("blueprint_instance") && g.Extras.All(x=>x==null)).ToArray()) _world.Objects.Remove(group.Id,.2f);
            var clock=Stopwatch.StartNew();
            while(_jobs.Count>0 && clock.Elapsed.TotalMilliseconds<2)
            {
                var j=_jobs[0];
                try
                {
                    if(j.Root==null || j.Editing && (j.Edit==null || j.Edit.Entity==null || !j.Edit.Entity.Alive)) { Destroy(j); _jobs.RemoveAt(0); continue; }
                    var part=j.Remaining.FirstOrDefault(p=>!p.ContainsKey("parent") || j.Parts.ContainsKey((string)p["parent"]));
                    if(part!=null) { BuildPart(j,part); j.Remaining.Remove(part); continue; }
                    BlueprintContract.Require(j.Remaining.Count==0,"Unresolvable hierarchy"); Complete(j); _jobs.RemoveAt(0);
                }
                catch(Exception ex)
                {
                    _world.Rules.Fail("Blueprint build failed: "+ex.Message+(j.Editing?"; prior object kept":"; using a stand-in")); Destroy(j); _jobs.RemoveAt(0);
                    if(!j.Editing && j.Spawn!=null) { if(j.Spawn.Op=="give") _world.Props.Give(j.Spawn); else _world.Objects.Spawn(j.Spawn); }
                }
            }
        }
        void BuildPart(Build j,Dictionary<string,object> part)
        {
            Statement p=BlueprintData.Wrap(part); var go=new GameObject(p.Str("id"));
            go.transform.SetParent(p.Has("parent")?j.Parts[p.Str("parent")]:j.Visual.transform,false);
            go.transform.localPosition=BlueprintData.Vec(part,"position",Vector3.zero); go.transform.localRotation=Quaternion.Euler(BlueprintData.Vec(part,"rotation",Vector3.zero)); go.transform.localScale=BlueprintData.Vec(part,"scale",Vector3.one);
            Mesh mesh=Mesh(part,out bool owned); if(owned) j.Meshes.Add(mesh);
            j.Triangles+=(int)mesh.GetIndexCount(0)/3; if(mesh.subMeshCount>1) j.Triangles+=(int)mesh.GetIndexCount(1)/3;
            BlueprintContract.Require(j.Triangles<=8000,"Triangle budget exceeded");
            go.AddComponent<MeshFilter>().sharedMesh=mesh; var r=go.AddComponent<MeshRenderer>();
            r.sharedMaterials=mesh.subMeshCount>1?new[]{_world.Objects.LitMaterial,_world.Objects.GlowMaterial}:new[]{_world.Objects.LitMaterial}; r.enabled=p.Flag("visible",true); r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            if(p.Has("collider")) AddCollider(go,p.Str("collider"),mesh.bounds);
            j.Parts.Add(p.Str("id"),go.transform);
        }
        static void AddCollider(GameObject go,string kind,Bounds b)
        { if(kind=="sphere") { var c=go.AddComponent<SphereCollider>(); c.center=b.center; c.radius=b.extents.magnitude; } else if(kind=="capsule") { var c=go.AddComponent<CapsuleCollider>(); c.center=b.center; c.height=Mathf.Max(b.size.y,.05f); c.radius=Mathf.Min(c.height*.5f,Mathf.Max(b.extents.x,b.extents.z)); } else { var c=go.AddComponent<BoxCollider>(); c.center=b.center; c.size=Vector3.Max(b.size,Vector3.one*.02f); } }
        static Mesh Mesh(Dictionary<string,object> part,out bool owned)
        {
            Statement p=BlueprintData.Wrap(part); owned=p.Str("shape")!="asset";
            if(!owned) return Library.Asset(p.Str("asset")).Mesh;
            var mb=new MeshBuilder(); mb.Col(p.TryColor("color",out Color color)?color:Color.white).Emit(p.Num("emission"));
            switch(p.Str("shape"))
            {
                case "cube": case "plank": mb.Box(Vector3.zero,Vector3.one); break;
                case "sphere": mb.Sphere(Vector3.zero,.5f); break;
                case "cylinder": mb.Cyl(Vector3.down*.5f,.5f,.5f,1); break;
                case "cone": mb.Cone(Vector3.down*.5f,.5f,1); break;
                case "capsule": mb.Capsule(Vector3.down*.5f,.25f,1); break;
                case "pyramid": mb.Pyramid(Vector3.down*.5f,1,1); break;
                case "torus": mb.Torus(Vector3.zero,.35f,.15f); break;
                case "disc": mb.Disc(Vector3.zero,.5f,12,true); break;
                case "wedge": mb.Wedge(Vector3.down*.5f,Vector3.one); break;
                case "tube": mb.Tube(((List<object>)part["points"]).Select(v=>BlueprintData.Vec(new Dictionary<string,object>{{"v",v}},"v",Vector3.zero)).ToList(),p.Num("radius",.1f),6); break;
                case "surface":
                    var verts=((List<object>)part["vertices"]).Select(v=>BlueprintData.Vec(new Dictionary<string,object>{{"v",v}},"v",Vector3.zero)).ToArray(); var indices=(List<object>)part["triangles"];
                    for(int i=0;i<indices.Count;i+=3) mb.TriTwoSided(verts[(int)(double)indices[i]],verts[(int)(double)indices[i+1]],verts[(int)(double)indices[i+2]]); break;
            }
            return mb.Build(p.Str("id"));
        }
        void Complete(Build j)
        {
            var caps=BlueprintData.Wrap(j.Recipe).Obj("capabilities"); Statement c=BlueprintData.Wrap(caps);
            BlueprintContract.Require(j.Edit!=null || !c.Flag("grabbable") || _world.Props.All.Count(p=>p!=null && p.GroupId!="shot")<PropSystem.MaxPhysics,"Physics budget exceeded");
            if(c.Flag("physics")) BlueprintContract.Require(_world.Props.All.Count(p=>p!=null && p.GroupId!="shot")+Instances.Count(a=>a!=j.Edit && a.GetComponent<Prop>()==null && !a.GetComponent<Rigidbody>().isKinematic)<PropSystem.MaxPhysics,"Physics budget exceeded");
            GameObject root=j.Edit!=null?j.Edit.gameObject:j.Root;
            if(j.Edit!=null)
            {
                Prop prop=root.GetComponent<Prop>(); if(prop!=null && prop.Held) prop.Holder.ForceRelease();
                foreach(var contact in root.GetComponentsInChildren<BlueprintContact>()) Object.Destroy(contact.gameObject);
                var old=j.Edit.Visual; j.Visual.transform.SetParent(root.transform,false); Object.Destroy(old.gameObject); Object.Destroy(j.Root); j.Root=null;
            }
            var actor=j.Edit ?? root.AddComponent<BlueprintActor>();
            if(j.Edit!=null) { var oldBodyCollider=root.GetComponent<Collider>(); if(oldBodyCollider!=null) { oldBodyCollider.enabled=false; Object.Destroy(oldBodyCollider); } }
            actor.Configure(_world,this,j.Recipe,j.Visual.transform,j.Parts,j.Meshes,j.Edit!=null || (j.Spawn!=null && j.Spawn.Flag("blueprint_respawn"))); j.Meshes.Clear();
            var body=root.GetComponent<Rigidbody>(); if(body==null) body=root.AddComponent<Rigidbody>(); body.mass=c.Num("mass",1); body.isKinematic=!c.Flag("physics"); body.useGravity=true; body.interpolation=RigidbodyInterpolation.Interpolate;
            if(root.GetComponentsInChildren<Collider>(true).Length==0) { var fallback=root.AddComponent<BoxCollider>(); fallback.center=actor.Bounds.center; fallback.size=Vector3.Max(actor.Bounds.size,Vector3.one*.05f); }
            if(c.Flag("grabbable") || j.Spawn?.Op=="give" || root.GetComponent<Prop>()!=null)
            {
                var prop=_world.Props.AttachGenerated(root,actor.Def,j.Spawn!=null?j.Spawn.Str("id"):actor.Entity.GroupId,j.Visual.transform); prop.Given=j.Spawn!=null?j.Spawn.Op=="give":prop.Given;
                if(j.Spawn?.Op=="give") { prop.FloatAt(root.transform.position); _world.OnGiven(prop); }
            }
            root.SetActive(true);
            if(j.Edit==null)
            {
                _instances.Add(actor); _world.Rules.RegisterBlueprint(actor,j.Spawn.Str("id"),j.Spawn.StrList("tags"));
                if(j.Spawn.TryColor("tint",out Color tint)) actor.Entity.Tint(tint);
            }
            actor.Begin(); Object.Destroy(j.Placeholder); j.Placeholder=null;
            if(j.Spawn!=null && j.Spawn.Flag("blueprint_respawn")) _world.Rules.Emit("respawn",actor.Entity);
            if(j.Spawn!=null) _world.Placed(j.Spawn,(string)j.Recipe["id"],actor.Def,false,root.transform.position,null);
            if(j.Spawn!=null && j.Spawn.Has("behavior")) actor.SetMovement(new Dictionary<string,object>{{"mode",j.Spawn.Str("behavior")}});
            _world.Note("Built "+j.Recipe["id"]+" ("+j.Triangles+" triangles)");
        }
        public void Emit(BlueprintActor actor,Dictionary<string,object> emitter)
        {
            _shots.RemoveAll(s=>s==null); if(_shots.Count>=32) { _world.Rules.Fail("Projectile budget exceeded"); return; }
            Statement e=BlueprintData.Wrap(emitter); if(!actor.Sockets.TryGetValue(e.Str("socket"),out Transform socket)) return;
            string asset=e.Str("asset"); var go=new GameObject("BlueprintProjectile"); go.transform.SetParent(_world.transform,false); go.transform.position=socket.position;
            // Cache bounded small visual recipes, without recursively attaching actor capabilities.
            var def=Has(asset)?null:Library.Asset(asset);
            if(def==null && Has(asset))
            {
                var recipe=Definition(asset); string key=MiniJson.Serialize(recipe);
                if(!_projectileTemplates.TryGetValue(key,out GameObject template) && _projectileTemplates.Count<16)
                { template=new GameObject("ProjectileTemplate"); template.transform.SetParent(_world.transform,false); template.SetActive(false); BuildProjectileVisual(template,recipe); _projectileTemplates.Add(key,template); }
                if(template!=null) { var visual=Object.Instantiate(template,go.transform); visual.transform.localPosition=Vector3.zero; visual.transform.localRotation=Quaternion.identity; visual.SetActive(true); }
                else { go.AddComponent<MeshFilter>().sharedMesh=Library.Asset("sphere").Mesh; go.AddComponent<MeshRenderer>().sharedMaterial=_world.Objects.LitMaterial; _world.Note("Projectile cache limit; using a sphere approximation"); }
            }
            else if(def!=null) { go.AddComponent<MeshFilter>().sharedMesh=def.Mesh; go.AddComponent<MeshRenderer>().sharedMaterials=def.Mesh.subMeshCount>1?new[]{_world.Objects.LitMaterial,_world.Objects.GlowMaterial}:new[]{_world.Objects.LitMaterial}; }
            else { Object.Destroy(go); _world.Rules.Fail("Projectile definition removed"); return; }
            if(e.TryColor("color",out Color tint)) foreach(var r in go.GetComponentsInChildren<Renderer>()) { var b=new MaterialPropertyBlock(); b.SetColor("_Tint",tint); b.SetFloat("_Appear",1); r.SetPropertyBlock(b); }
            var renderers=go.GetComponentsInChildren<Renderer>(); Bounds bounds=new Bounds(go.transform.position,Vector3.one*.05f); foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            float fit=.25f/Mathf.Max(.05f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))); go.transform.localScale=Vector3.one*fit;
            var collider=go.AddComponent<SphereCollider>(); collider.radius=.12f/fit; collider.isTrigger=true;
            var body=go.AddComponent<Rigidbody>(); body.useGravity=false; body.isKinematic=true;
            Vector3 direction=socket.forward; Vector3? target=actor.TargetPoint(emitter,"forward"); if(target.HasValue) direction=(target.Value-socket.position).normalized;
            var shot=go.AddComponent<BlueprintProjectile>(); shot.Init(_world,actor,emitter,direction); _shots.Add(go);
        }
        void BuildProjectileVisual(GameObject go,Dictionary<string,object> recipe)
        {
            // Emission is bounded: small recipes only; larger recipes use a visible approximation.
            var parts=BlueprintData.List(recipe,"parts");
            if(parts.Count>8) { go.AddComponent<MeshFilter>().sharedMesh=Library.Asset("sphere").Mesh; go.AddComponent<MeshRenderer>().sharedMaterial=_world.Objects.LitMaterial; _world.Note("Complex projectile approximated as a sphere"); return; }
            var byName=new Dictionary<string,Transform>(); var owned=go.AddComponent<BlueprintMeshOwner>();
            while(parts.Count>0)
            {
                var p=parts.First(x=>!x.ContainsKey("parent")||byName.ContainsKey((string)x["parent"])); var child=new GameObject((string)p["id"]); child.transform.SetParent(p.ContainsKey("parent")?byName[(string)p["parent"]]:go.transform,false);
                child.transform.localPosition=BlueprintData.Vec(p,"position",Vector3.zero); child.transform.localRotation=Quaternion.Euler(BlueprintData.Vec(p,"rotation",Vector3.zero)); child.transform.localScale=BlueprintData.Vec(p,"scale",Vector3.one);
                var mesh=Mesh(p,out bool own); if(own) owned.Meshes.Add(mesh); child.AddComponent<MeshFilter>().sharedMesh=mesh; child.AddComponent<MeshRenderer>().sharedMaterials=mesh.subMeshCount>1?new[]{_world.Objects.LitMaterial,_world.Objects.GlowMaterial}:new[]{_world.Objects.LitMaterial};
                byName[(string)p["id"]]=child.transform; parts.Remove(p);
            }
        }
    }
    public sealed class BlueprintMeshOwner : MonoBehaviour
    { public readonly List<Mesh> Meshes=new List<Mesh>(); void OnDestroy() { foreach(var m in Meshes) if(m!=null) Object.Destroy(m); } }
    public sealed class PlayerHitRegion : MonoBehaviour
    { public BlueprintSystem System; public Transform Head; void LateUpdate() { if(Head!=null) transform.position=Head.position-Vector3.up*.25f; } }
}
