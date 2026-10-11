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
        /// <summary>Appended to a part's id to name the copy that `mirror` makes of it on the other side.</summary>
        public const string Twin="_m";
        void BuildPart(Build j,Dictionary<string,object> part)
        {
            Statement p=BlueprintData.Wrap(part); string id=p.Str("id");
            Transform parent=p.Has("parent")?j.Parts[p.Str("parent")]:j.Visual.transform;
            AddPart(j,part,id,parent,false);
            // A part has a twin when it asks for one, or when what it hangs from has one: mirror a leg and its hoof comes too.
            Transform twinParent=p.Has("parent") && j.Parts.TryGetValue(p.Str("parent")+Twin,out Transform other)?other:null;
            if((p.Has("mirror") || twinParent!=null) && !j.Parts.ContainsKey(id+Twin) && !j.Remaining.Any(r=>(string)r["id"]==id+Twin))
                AddPart(j,part,id+Twin,twinParent ?? parent,true);
        }
        void AddPart(Build j,Dictionary<string,object> part,string id,Transform parent,bool mirrored)
        {
            Statement p=BlueprintData.Wrap(part); var go=new GameObject(id); go.transform.SetParent(parent,false);
            Vector3 position=BlueprintData.Vec(part,"position",Vector3.zero), rotation=BlueprintData.Vec(part,"rotation",Vector3.zero);
            if(mirrored) { position.x=-position.x; rotation=new Vector3(rotation.x,-rotation.y,-rotation.z); }
            go.transform.localPosition=position; go.transform.localRotation=Quaternion.Euler(rotation);
            // A part's size is built into its mesh, so it never stretches the parts that hang from it. A baked asset keeps its own mesh and is scaled instead.
            go.transform.localScale=p.Str("shape")=="asset"?BlueprintData.Vec(part,"scale",Vector3.one):Vector3.one;
            Mesh mesh=Mesh(part,mirrored,out bool owned); if(owned) j.Meshes.Add(mesh);
            j.Triangles+=(int)mesh.GetIndexCount(0)/3; if(mesh.subMeshCount>1) j.Triangles+=(int)mesh.GetIndexCount(1)/3;
            BlueprintContract.Require(j.Triangles<=BlueprintContract.MaxTriangles,"Triangle budget exceeded");
            go.AddComponent<MeshFilter>().sharedMesh=mesh; var r=go.AddComponent<MeshRenderer>();
            r.sharedMaterials=mesh.subMeshCount>1?new[]{_world.Objects.LitMaterial,_world.Objects.GlowMaterial}:new[]{_world.Objects.LitMaterial}; r.enabled=p.Flag("visible",true); r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            if(p.Has("collider")) AddCollider(go,p.Str("collider"),mesh.bounds);
            j.Parts.Add(id,go.transform);
        }
        static void AddCollider(GameObject go,string kind,Bounds b)
        { if(kind=="sphere") { var c=go.AddComponent<SphereCollider>(); c.center=b.center; c.radius=Mathf.Max(b.extents.x,Mathf.Max(b.extents.y,b.extents.z)); } else if(kind=="capsule") { var c=go.AddComponent<CapsuleCollider>(); c.center=b.center; c.height=Mathf.Max(b.size.y,.05f); c.radius=Mathf.Min(c.height*.5f,Mathf.Max(b.extents.x,b.extents.z)); } else { var c=go.AddComponent<BoxCollider>(); c.center=b.center; c.size=Vector3.Max(b.size,Vector3.one*.02f); } }
        static List<Vector3> Points(Dictionary<string,object> part,string key) => ((List<object>)part[key]).Select(v=>BlueprintData.Vec(new Dictionary<string,object>{{"v",v}},"v",Vector3.zero)).ToList();
        static List<Vector2> Pairs(Dictionary<string,object> part,string key) => ((List<object>)part[key]).Select(v=>{ var l=(List<object>)v; return new Vector2((float)(double)l[0],(float)(double)l[1]); }).ToList();
        static void Taper(Dictionary<string,object> part,out float x,out float z)
        {
            x=z=1; if(!part.TryGetValue("taper",out object t)) return;
            if(t is double n) x=z=(float)n; else if(t is List<object> l && l.Count==2 && l[0] is double a && l[1] is double b) { x=(float)a; z=(float)b; }
            x=Mathf.Clamp(x,0,3); z=Mathf.Clamp(z,0,3);
        }
        /// <summary>
        /// The mesh of one part, at its real size, centred on its pivot (a dome, a lathe and a slab
        /// stand on theirs). `mirrored` builds the reflection, for the twin on the other side.
        /// </summary>
        static Mesh Mesh(Dictionary<string,object> part,bool mirrored,out bool owned)
        {
            Statement p=BlueprintData.Wrap(part); owned=p.Str("shape")!="asset";
            if(!owned) return Library.Asset(p.Str("asset")).Mesh;
            Vector3 s=BlueprintData.Vec(part,"scale",Vector3.one); string shape=p.Str("shape");
            var mb=new MeshBuilder(); mb.Shade(.045f).Emit(p.Num("emission"));
            Color color=p.TryColor("color",out Color given)?given:Color.white;
            if(p.TryColor("color_top",out Color top))
            {
                float y0=-s.y*.5f,y1=s.y*.5f;
                if(shape=="dome") { y0=0; y1=s.y; }
                else if(shape=="lathe") { var h=Pairs(part,"profile").Select(v=>v.y*s.y).ToList(); y0=h.Min(); y1=h.Max(); }
                else if(shape=="slab") { var h=Pairs(part,"outline").Select(v=>v.y*s.y).ToList(); y0=h.Min(); y1=h.Max(); }
                else if(shape=="tube") { var h=Points(part,"points").Select(v=>v.y*s.y).ToList(); y0=h.Min(); y1=h.Max(); }
                else if(shape=="surface") { var h=Points(part,"vertices").Select(v=>v.y*s.y).ToList(); y0=h.Min(); y1=h.Max(); }
                mb.Grad(color,top,y0,y1);
            }
            else mb.Col(color);
            // `repeat` lays copies along `step`, which is measured in the parent's space like `position`
            int copies=Mathf.Clamp(p.Int("repeat",1),1,12); Vector3 step=BlueprintData.Vec(part,"step",Vector3.zero), turn=BlueprintData.Vec(part,"rotation",Vector3.zero);
            if(mirrored) { step.x=-step.x; turn=new Vector3(turn.x,-turn.y,-turn.z); }
            step=Quaternion.Inverse(Quaternion.Euler(turn))*step;
            // a limb hangs from its position and a post stands on it; shapes that already stand on theirs are left alone
            bool centred=shape!="dome" && shape!="lathe" && shape!="slab" && shape!="tube" && shape!="surface";
            Vector3 anchor=!centred?Vector3.zero:p.Str("anchor")=="top"?Vector3.down*s.y*.5f:p.Str("anchor")=="bottom"?Vector3.up*s.y*.5f:Vector3.zero;
            for(int k=0;k<copies;k++)
            {
                mb.Push(step*k+anchor,Vector3.zero,mirrored?new Vector3(-1,1,1):Vector3.one);
                Shape(mb,p,part,shape,s);
                mb.Pop();
            }
            return mb.Build(p.Str("id"));
        }
        static void Shape(MeshBuilder mb,Statement p,Dictionary<string,object> part,string shape,Vector3 s)
        {
            int sides=Mathf.Clamp(p.Int("sides",12),3,24); Taper(part,out float tx,out float tz);
            float least=Mathf.Min(s.x,Mathf.Min(s.y,s.z));
            switch(shape)
            {
                case "cube": case "plank":
                    // edges are worn a little unless told otherwise: a box with knife edges reads as a placeholder
                    mb.BevelBox(Vector3.zero,s,p.Has("bevel")?Mathf.Clamp01(p.Num("bevel"))*.5f*least:Mathf.Min(.1f*least,.03f),tx,tz); break;
                case "sphere": mb.Sphere(Vector3.zero,s*.5f,Mathf.Max(s.x,Mathf.Max(s.y,s.z))>.5f?2:1); break;
                case "blob": mb.Blob(Vector3.zero,s*.5f,Mathf.Clamp(p.Num("rough",.25f),0,.6f),p.Int("seed",1),1); break;
                case "cylinder": mb.Push(Vector3.zero,Vector3.zero,new Vector3(s.x,1,s.z)).Cyl(Vector3.down*s.y*.5f,.5f,.5f*tx,s.y,sides).Pop(); break;
                case "cone": mb.Push(Vector3.zero,Vector3.zero,new Vector3(s.x,1,s.z)).Cone(Vector3.down*s.y*.5f,.5f,s.y,sides).Pop(); break;
                case "capsule":
                    float height=Mathf.Max(s.y,s.x);
                    mb.Push(Vector3.zero,Vector3.zero,new Vector3(1,1,s.z/s.x)).Capsule(Vector3.down*height*.5f,s.x*.5f,height,Mathf.Max(sides,8)).Pop(); break;
                case "pyramid": mb.Push(Vector3.zero,Vector3.zero,s).Pyramid(Vector3.down*.5f,1,1).Pop(); break;
                case "torus":
                    float tube=Mathf.Clamp(p.Num("radius",.15f),.02f,.45f);
                    mb.Push(Vector3.zero,Vector3.zero,s).Torus(Vector3.zero,.5f-tube,tube,Mathf.Max(sides,12),6).Pop(); break;
                case "disc": mb.Push(Vector3.zero,Vector3.zero,s).Disc(Vector3.zero,.5f,sides,true).Pop(); break;
                case "wedge": mb.Push(Vector3.zero,Vector3.zero,s).Wedge(Vector3.down*.5f,Vector3.one).Pop(); break;
                case "dome": mb.Push(Vector3.zero,Vector3.zero,new Vector3(s.x,1,s.z)).Dome(Vector3.zero,.5f,s.y,sides).Pop(); break;
                case "lathe": mb.Push(Vector3.zero,Vector3.zero,s).Lathe(Vector3.zero,Pairs(part,"profile"),sides).Pop(); break;
                case "slab": mb.Push(Vector3.zero,Vector3.zero,s).Slab(Pairs(part,"outline"),p.Num("thickness",.05f)).Pop(); break;
                case "tube": mb.Push(Vector3.zero,Vector3.zero,s).Tube(Points(part,"points"),p.Num("radius",.1f),Mathf.Clamp(p.Int("sides",6),3,12)).Pop(); break;
                case "surface":
                    var verts=Points(part,"vertices"); var indices=(List<object>)part["triangles"]; mb.Push(Vector3.zero,Vector3.zero,s);
                    for(int i=0;i<indices.Count;i+=3) mb.TriTwoSided(verts[(int)(double)indices[i]],verts[(int)(double)indices[i+1]],verts[(int)(double)indices[i+2]]);
                    mb.Pop(); break;
            }
        }
        void Complete(Build j)
        {
            var caps=BlueprintData.Wrap(j.Recipe).Obj("capabilities"); Statement c=BlueprintData.Wrap(caps);
            BlueprintContract.Require(j.Edit!=null || !c.Flag("grabbable") || _world.Props.All.Count(p=>p!=null && p.GroupId!="shot")<PropSystem.MaxPhysics,"Physics budget exceeded");
            if(c.Flag("physics")) BlueprintContract.Require(_world.Props.All.Count(p=>p!=null && p.GroupId!="shot")+Instances.Count(a=>a!=j.Edit && a.GetComponent<Prop>()==null && !a.GetComponent<Rigidbody>().isKinematic)<PropSystem.MaxPhysics,"Physics budget exceeded");
            GameObject root=j.Edit!=null?j.Edit.gameObject:j.Root;
            if(j.Edit!=null)
            {
                // an edit to something in the hand leaves it there: asked to make their gun louder, the user must not find it on the floor
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
            _shots.RemoveAll(s=>s==null); if(_shots.Count>=MaxShots) return;   // a held trigger simply waits for room
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
            // A shot is heard and seen leaving: with neither, a gun that works feels like one that does not.
            string sound=e.Str("sound",asset=="energy_ball"?"zap":"gunshot");
            if(sound!="none") Sfx.Play(sound,socket.position,.75f,.92f+UnityEngine.Random.value*.16f);
            Color flash=e.TryColor("color",out Color flashTint)?flashTint:new Color(1f,.82f,.4f);
            if(_flashes.Count(f=>f!=null)<6) _flashes.Add(Fx.Burst(socket.position+direction*.04f,flash,Color.white,7,2.2f,.035f,.09f,0,true));
            _flashes.RemoveAll(f=>f==null);
        }
        public const int MaxShots=40;
        readonly List<ParticleSystem> _flashes=new List<ParticleSystem>();
        void BuildProjectileVisual(GameObject go,Dictionary<string,object> recipe)
        {
            // Emission is bounded: small recipes only; larger recipes use a visible approximation.
            var parts=BlueprintData.List(recipe,"parts");
            if(parts.Count>8) { go.AddComponent<MeshFilter>().sharedMesh=Library.Asset("sphere").Mesh; go.AddComponent<MeshRenderer>().sharedMaterial=_world.Objects.LitMaterial; _world.Note("Complex projectile approximated as a sphere"); return; }
            var byName=new Dictionary<string,Transform>(); var owned=go.AddComponent<BlueprintMeshOwner>();
            while(parts.Count>0)
            {
                var p=parts.First(x=>!x.ContainsKey("parent")||byName.ContainsKey((string)x["parent"])); var child=new GameObject((string)p["id"]); child.transform.SetParent(p.ContainsKey("parent")?byName[(string)p["parent"]]:go.transform,false);
                child.transform.localPosition=BlueprintData.Vec(p,"position",Vector3.zero); child.transform.localRotation=Quaternion.Euler(BlueprintData.Vec(p,"rotation",Vector3.zero)); child.transform.localScale=(string)p["shape"]=="asset"?BlueprintData.Vec(p,"scale",Vector3.one):Vector3.one;
                var mesh=Mesh(p,false,out bool own); if(own) owned.Meshes.Add(mesh); child.AddComponent<MeshFilter>().sharedMesh=mesh; child.AddComponent<MeshRenderer>().sharedMaterials=mesh.subMeshCount>1?new[]{_world.Objects.LitMaterial,_world.Objects.GlowMaterial}:new[]{_world.Objects.LitMaterial};
                byName[(string)p["id"]]=child.transform; parts.Remove(p);
            }
        }
    }
    public sealed class BlueprintMeshOwner : MonoBehaviour
    { public readonly List<Mesh> Meshes=new List<Mesh>(); void OnDestroy() { foreach(var m in Meshes) if(m!=null) Object.Destroy(m); } }
    public sealed class PlayerHitRegion : MonoBehaviour
    { public BlueprintSystem System; public Transform Head; void LateUpdate() { if(Head!=null) transform.position=Head.position-Vector3.up*.25f; } }
}
