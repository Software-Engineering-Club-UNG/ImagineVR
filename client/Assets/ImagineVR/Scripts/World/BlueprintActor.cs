using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImagineVR
{
    // Object-neutral controller: no dragon, turret, weapon or furniture branches.
    public sealed class BlueprintActor : MonoBehaviour
    {
        public World World { get; private set; }
        public BlueprintSystem System { get; private set; }
        public Dictionary<string,object> Recipe { get; private set; }
        public Dictionary<string,Transform> Parts { get; private set; }
        public readonly Dictionary<string,Transform> Sockets=new Dictionary<string,Transform>();
        public RuleEntity Entity;
        public Transform Visual { get; private set; }
        public Bounds Bounds { get; private set; }
        public AssetDef Def { get; private set; }
        public bool IsOverride { get; private set; }
        public int AttackEpoch { get; private set; }
        public bool CollisionsEnabled { get; private set; } = true;
        public void SetCollision(bool enabled)
        { CollisionsEnabled=enabled; foreach(var p in Parts) foreach(var collider in p.Value.GetComponentsInChildren<Collider>()) collider.enabled=enabled && PartVisible(p.Key); var rootCollider=GetComponent<Collider>(); if(rootCollider!=null) rootCollider.enabled=enabled; }
        public string State { get; private set; } = "default";
        public bool Moving => _movement!=null && BlueprintData.Wrap(_movement).Str("mode")!="still";
        public Dictionary<string,object> Movement => _movement;
        public List<object> ActiveAnimations => _animations.Cast<object>().ToList();
        public List<object> ActiveEmitters => _emitters.Cast<object>().ToList();
        public List<object> ActiveContacts => _contacts.Cast<object>().ToList();
        readonly List<Mesh> _owned=new List<Mesh>();
        readonly Dictionary<string,Vector3> _positions=new Dictionary<string,Vector3>(), _rotations=new Dictionary<string,Vector3>(), _scales=new Dictionary<string,Vector3>();
        readonly Dictionary<string,bool> _visible=new Dictionary<string,bool>();
        readonly Dictionary<string,float> _animationStart=new Dictionary<string,float>(), _lastShot=new Dictionary<string,float>();
        readonly HashSet<string> _animations=new HashSet<string>(), _emitters=new HashSet<string>(), _contacts=new HashSet<string>();
        readonly Dictionary<string,float> _lastContact=new Dictionary<string,float>();
        readonly HashSet<string> _sensorParts=new HashSet<string>();
        readonly List<Mesh> _batched=new List<Mesh>();
        Dictionary<string,object> _movement;
        List<Dictionary<string,object>> _animationDefs, _emitterDefs, _contactDefs;
        Vector3 _home,_goal; float _nextGoal,_distance;
        Route _route;
        static readonly RaycastHit[] Hits=new RaycastHit[24];

        public void Configure(World world,BlueprintSystem system,Dictionary<string,object> recipe,Transform visual,Dictionary<string,Transform> parts,List<Mesh> meshes,bool instanceOverride)
        {
            foreach(var mesh in _owned) if(mesh!=null) Object.Destroy(mesh); _owned.Clear(); _owned.AddRange(meshes);
            foreach(var mesh in _batched) if(mesh!=null) Object.Destroy(mesh); _batched.Clear();
            World=world; System=system; Recipe=BlueprintData.Clone(recipe); Parts=parts; Visual=visual; IsOverride=instanceOverride;
            AttackEpoch++;
            _positions.Clear(); _rotations.Clear(); _scales.Clear(); _visible.Clear(); _animations.Clear(); _animationStart.Clear(); _lastShot.Clear(); _lastContact.Clear(); _sensorParts.Clear(); Sockets.Clear();
            foreach(var p in BlueprintData.List(recipe,"parts"))
            { string id=(string)p["id"]; _positions[id]=parts[id].localPosition; _rotations[id]=parts[id].localEulerAngles; _scales[id]=parts[id].localScale; _visible[id]=BlueprintData.Wrap(p).Flag("visible",true); }
            Bounds bounds=new Bounds(Vector3.zero,Vector3.one*.05f); bool first=true;
            foreach(var r in visual.GetComponentsInChildren<MeshRenderer>(true)) { Bounds b=r.GetComponent<MeshFilter>().sharedMesh.bounds; var matrix=transform.worldToLocalMatrix*r.transform.localToWorldMatrix; foreach(int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) foreach(int z in new[]{-1,1}) { var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))); if(first) { bounds=new Bounds(point,Vector3.zero); first=false; } else bounds.Encapsulate(point); } }
            Bounds=bounds;
            if(Def!=null) Object.Destroy(Def.Mesh);
            Def=new AssetDef { Id=(string)recipe["id"], Recipe=mb=>mb.Box(bounds.center,Vector3.Max(bounds.size,Vector3.one*.05f)), Holdable=true, Prop=true, Tool=BlueprintData.Wrap(recipe).Obj("capabilities")?.ContainsKey("grip")==true };
            foreach(var s in BlueprintData.List(recipe,"sockets"))
            { Statement data=BlueprintData.Wrap(s); var socket=new GameObject(data.Str("name")); socket.transform.SetParent(parts[data.Str("part")],false); socket.transform.localPosition=BlueprintData.Vec(s,"position",Vector3.zero); socket.transform.localRotation=Quaternion.Euler(BlueprintData.Vec(s,"rotation",Vector3.zero)); Sockets[data.Str("name")]=socket.transform; }
            _animationDefs=BlueprintData.List(recipe,"animations"); var caps=BlueprintData.Wrap(recipe).Obj("capabilities");
            _emitterDefs=BlueprintData.List(caps,"emitters"); _contactDefs=BlueprintData.List(caps,"contacts");
            foreach(var a in _animationDefs) if(BlueprintData.Wrap(a).Flag("enabled",true)) _animations.Add((string)a["name"]);
            _emitters.Clear(); foreach(var e in _emitterDefs) if(BlueprintData.Wrap(e).Flag("enabled")) _emitters.Add((string)e["name"]);
            _contacts.Clear(); foreach(var c in _contactDefs) { if(BlueprintData.Wrap(c).Flag("enabled")) _contacts.Add((string)c["name"]); EnsureSensor((string)c["part"]); }
            _movement=BlueprintData.Wrap(caps).Obj("movement") ?? new Dictionary<string,object>{{"mode","still"}};
            _home=transform.position; _goal=_home; _nextGoal=0; _distance=0; _route=null;
        }
        public void Begin()
        {
            var caps=BlueprintData.Wrap(Recipe).Obj("capabilities");
            if(Entity!=null) { if(!IsOverride) { Entity.Health=BlueprintData.Wrap(caps).Num("health",Entity.Health); Entity.Durability=BlueprintData.Wrap(caps).Num("durability",Entity.Durability); } Entity.Gravity=BlueprintData.Wrap(caps).Num("gravity",1); }
            SetMovement(_movement);
            string initial=BlueprintData.Wrap(Recipe).Str("initial_state"); if(initial!=null) EnterState(initial); else State="default";
            Rebatch();
        }
        public void EnsureSensor(string part)
        {
            if(part==null || !Parts.ContainsKey(part) || !_sensorParts.Add(part)) return;
            if(_sensorParts.Count+Parts.Values.Sum(p=>p.GetComponents<Collider>().Length)>16) { _sensorParts.Remove(part); World.Rules.Fail("Part sensor collider budget"); return; }
            var t=Parts[part]; var go=new GameObject("Contact_"+part); go.transform.SetParent(t,false);
            var c=go.AddComponent<BoxCollider>(); var b=t.GetComponent<MeshFilter>().sharedMesh.bounds; c.center=b.center; c.size=Vector3.Max(b.size,Vector3.one*.03f); c.isTrigger=true;
            var sensor=go.AddComponent<BlueprintContact>(); sensor.Actor=this; sensor.Part=part;
        }
        public bool PartVisible(string part) => Entity?.Visible!=false && _visible.TryGetValue(part,out bool visible) && visible && (!Parts[part].parent || !Parts.ContainsValue(Parts[part].parent) || PartVisible(Parts[part].parent.name));
        public void SetMovement(Dictionary<string,object> config)
        {
            BlueprintContract.Movement(config);
            if(BlueprintData.Wrap(config).Str("mode")!="still" && !System.CanMove(this)) { World.Rules.Fail("Moving actor budget"); config=new Dictionary<string,object>{{"mode","still"}}; }
            _movement=BlueprintData.Clone(config); _distance=0; _route=null; _goal=transform.position; _nextGoal=0;
            foreach(var old in GetComponents<RuleMotion>()) Object.Destroy(old);
            if(BlueprintData.Wrap(config).TryPath("path",out List<Vector3> path)) _route=new Route(path,false);
            var body=GetComponent<Rigidbody>(); if(body!=null && Moving) body.isKinematic=true;
        }
        public void EnterState(string name)
        {
            var states=BlueprintData.Wrap(Recipe).Obj("states");
            if(states==null || !states.TryGetValue(name,out object value)) { World.Rules.Fail("Unknown actor state "+name); return; }
            World.Rules.CancelEntityActions(Entity);
            World.Rules.Emit("state_exit",Entity,eventName:State);
            State=name; var state=value as Dictionary<string,object>; Statement s=BlueprintData.Wrap(state);
            AttackEpoch++;
            _animations.Clear(); foreach(string n in s.StrList("animations")) _animations.Add(n);
            _emitters.Clear(); foreach(string n in s.StrList("emitters")) _emitters.Add(n);
            _contacts.Clear(); foreach(string n in s.StrList("contacts")) _contacts.Add(n);
            _lastShot.Clear(); _animationStart.Clear();
            SetMovement(s.Obj("movement") ?? new Dictionary<string,object>{{"mode","still"}});
            RestoreParts(); Rebatch(); World.Rules.Emit("state_enter",Entity,eventName:name);
        }
        void RestoreParts()
        { foreach(var p in Parts) { p.Value.localPosition=_positions[p.Key]; p.Value.localRotation=Quaternion.Euler(_rotations[p.Key]); p.Value.localScale=_scales[p.Key]; } }
        public void Action(Statement a)
        {
            try
            {
                switch(a.Op)
                {
                    case "actor_state": EnterState(a.Str("name")); break;
                    case "motion_config": SetMovement(a.Obj("config")); break;
                    case "animation_play":
                        var def=_animationDefs.Find(x=>(string)x["name"]==a.Str("name")); if(def==null) throw new InvalidOperationException("Animation not found");
                        if(a.Flag("enabled",true)) { _animations.Add(a.Str("name")); _animationStart[a.Str("name")]=Time.time; }
                        else { _animations.Remove(a.Str("name")); string p=(string)def["part"]; ApplyProperty(Parts[p],(string)def["property"],Vector3.zero,p); }
                        Rebatch(); break;
                    case "part_transform":
                        string part=a.Str("part"); if(!Parts.ContainsKey(part)) throw new InvalidOperationException("Part not found");
                        var updated=BlueprintData.Clone(Recipe); var record=BlueprintData.List(updated,"parts").First(p=>(string)p["id"]==part);
                        foreach(string key in new[]{"position","rotation","scale","visible"}) if(a.Has(key)) record[key]=a.Fields[key];
                        updated["revision"]=(double)(BlueprintData.Wrap(Recipe).Int("revision",1)+1);
                        if(!System.CanOverride(this,updated)) throw new InvalidOperationException("Override byte budget");
                        Recipe=updated; IsOverride=true;
                        if(a.TryVec3("position",out Vector3 position)) _positions[part]=position;
                        if(a.TryVec3("rotation",out Vector3 rotation)) _rotations[part]=rotation;
                        if(a.TryVec3("scale",out Vector3 scale)) _scales[part]=scale;
                        if(a.Has("visible")) _visible[part]=a.Flag("visible");
                        Parts[part].localPosition=_positions[part]; Parts[part].localRotation=Quaternion.Euler(_rotations[part]); Parts[part].localScale=_scales[part];
                        SetCollision(CollisionsEnabled); Rebatch(); break;
                    case "emit_projectile": Fire(a.Str("name")); break;
                }
            }
            catch(Exception ex) { World.Rules.Fail("Blueprint action failed: "+ex.Message); }
        }
        public Vector3? TargetPoint(Dictionary<string,object> config,string fallback="player")
        {
            if(config.TryGetValue("target",out object target) && target is Dictionary<string,object> selector)
            { var entity=World.Rules.Live.Where(e=>e!=Entity && World.Rules.Selects(selector,e)).OrderBy(e=>(e.Position-transform.position).sqrMagnitude).FirstOrDefault(); return entity!=null?(Vector3?)entity.Position:null; }
            string kind=BlueprintData.Wrap(config).Str("target",fallback);
            return kind=="player" && World.Head!=null?World.Head.position:(Vector3?)null;
        }
        public bool Fire(string name)
        {
            var emitter=_emitterDefs.Find(e=>(string)e["name"]==name);
            if(emitter==null) { World.Rules.Fail("Unknown emitter "+name); return false; }
            if(!_emitters.Contains(name) || Entity==null || !Entity.Alive || Entity.Health==0) return false;
            Statement e=BlueprintData.Wrap(emitter);
            if(_lastShot.TryGetValue(name,out float last) && Time.time-last<e.Num("cooldown",1)) return false;
            string socketPart=(string)BlueprintData.List(Recipe,"sockets").First(s=>(string)s["name"]==e.Str("socket"))["part"];
            if(!PartVisible(socketPart)) return false;
            Vector3? target=TargetPoint(emitter,"forward"); if(e.Str("target")=="player" && !target.HasValue) return false;
            if(e.Obj("target")!=null && !target.HasValue) return false;
            if(target.HasValue && !World.Rules.LineOfSight(Sockets[e.Str("socket")].position,target.Value,Entity)) return false;
            _lastShot[name]=Time.time; System.Emit(this,emitter); return true;
        }
        /// <summary>The trigger of a held thing: every emitter that is switched on fires, each at its own pace.</summary>
        public int FireHeld() { int shots=0; foreach(string name in _emitters.ToArray()) if(Fire(name)) shots++; return shots; }
        public void Contact(string part,Collider other,bool entering)
        {
            if(Entity==null || !Entity.Alive || !PartVisible(part)) return;
            RuleEntity target=other.GetComponentInParent<RuleBody>()?.Entity; if(target==Entity) return;
            if(entering) World.Rules.Emit("part_contact",Entity,target,part:part);
            if(Entity.Health==0) return;
            foreach(var contact in _contactDefs.Where(c=>(string)c["part"]==part && _contacts.Contains((string)c["name"])))
            {
                Statement c=BlueprintData.Wrap(contact); bool player=other.GetComponentInParent<PlayerHitRegion>()!=null;
                bool accepts=c.Obj("target")!=null?World.Rules.Selects(c.Obj("target"),target):player;
                if(!accepts) continue;
                string key=c.Str("name")+":"+(player?"player":target?.Id);
                if(_lastContact.TryGetValue(key,out float last) && Time.time-last<c.Num("cooldown",1)) continue;
                _lastContact[key]=Time.time;
                if(player) System.DamagePlayer(c.Num("damage")); else World.Rules.Damage(target,c.Num("damage"),Entity);
            }
        }
        void Update()
        {
            if(Entity==null || !Entity.Alive) return;
            Animate();
            if(Entity.Health==0) return;
            var prop=GetComponent<Prop>(); if(prop==null || !prop.Held && !prop.Floating) Move(Time.deltaTime);
            foreach(var e in _emitterDefs) if(BlueprintData.Wrap(e).Flag("autofire") && _emitters.Contains((string)e["name"])) Fire((string)e["name"]);
        }
        void Animate()
        {
            foreach(var data in _animationDefs)
            {
                Statement a=BlueprintData.Wrap(data); string name=a.Str("name"); if(!_animations.Contains(name)) continue;
                string part=a.Str("part"); float time=Time.time-(_animationStart.TryGetValue(name,out float start)?start:0); Vector3 value;
                if(a.Str("mode")!="keyframes" && !a.Flag("loop",true)) time=Mathf.Min(time,a.Num("duration",1));
                if(a.Str("mode")=="keyframes")
                {
                    var frames=BlueprintData.List(data,"frames"); float end=BlueprintData.Wrap(frames.Last()).Num("time"); if(a.Flag("loop",true)) time%=end; else time=Mathf.Min(time,end);
                    var left=frames.LastOrDefault(f=>BlueprintData.Wrap(f).Num("time")<=time) ?? frames[0]; var right=frames.FirstOrDefault(f=>BlueprintData.Wrap(f).Num("time")>=time) ?? frames.Last();
                    float lo=BlueprintData.Wrap(left).Num("time"),hi=BlueprintData.Wrap(right).Num("time"); value=Vector3.Lerp(BlueprintData.Vec(left,"value",Vector3.zero),BlueprintData.Vec(right,"value",Vector3.zero),hi>lo?(time-lo)/(hi-lo):0);
                }
                else { var amplitude=BlueprintData.Vec(data,"amplitude",Vector3.zero); value=a.Str("mode")=="spin"?amplitude*time:amplitude*Mathf.Sin(time*a.Num("frequency",1)*Mathf.PI*2+a.Num("phase")*Mathf.Deg2Rad); }
                ApplyProperty(Parts[part],a.Str("property"),value,part);
            }
        }
        void ApplyProperty(Transform t,string property,Vector3 value,string part)
        { if(property=="rotation") t.localRotation=Quaternion.Euler(_rotations[part]+value); else if(property=="position") t.localPosition=_positions[part]+value; else t.localScale=Vector3.Max(_scales[part]+value,Vector3.one*.01f); }
        void Move(float dt)
        {
            if(!Moving) return; Statement m=BlueprintData.Wrap(_movement); Vector3 p=transform.position,heading=Vector3.zero; Vector3? target=TargetPoint(_movement);
            string mode=m.Str("mode"); bool airborne=m.Flag("airborne"); float speed=m.Num("speed",2),radius=m.Num("radius",6);
            if(mode=="path" && _route!=null)
            { _distance=Mathf.Min(_route.Length,_distance+speed*dt); _goal=_route.At(_distance,out heading); if(_distance>=_route.Length) { _movement["mode"]="still"; World.Rules.Emit("path_complete",Entity); } }
            else if(mode=="wander")
            { if(Time.time>=_nextGoal || Vector3.Distance(p,_goal)<.5f) { Vector2 r=UnityEngine.Random.insideUnitCircle*radius; _goal=_home+new Vector3(r.x,0,r.y); _nextGoal=Time.time+UnityEngine.Random.Range(2f,5f); } }
            else if(target.HasValue)
            {
                Vector3 t=target.Value; if(!airborne) t.y=p.y;
                if(mode=="orbit") _goal=t+new Vector3(Mathf.Sin(Time.time*speed/radius),0,Mathf.Cos(Time.time*speed/radius))*radius;
                else if(mode=="flee") _goal=p+(p-t).normalized*radius;
                else { Vector3 delta=p-t; _goal=delta.magnitude>radius?t+delta.normalized*radius:p; }
            }
            else return;
            if(airborne) _goal.y=Mathf.Max(World.Terrain.HeightAt(_goal.x,_goal.z)+m.Num("altitude",3),_goal.y);
            else _goal.y=World.Terrain.HeightAt(_goal.x,_goal.z)+_home.y-World.Terrain.HeightAt(_home.x,_home.z);
            Vector3 deltaGoal=_goal-p; Vector3 next=Vector3.MoveTowards(p,_goal,speed*dt);
            if(deltaGoal.sqrMagnitude>.01f)
            {
                Vector3 direction=deltaGoal.normalized; float probe=Mathf.Max(.25f,Mathf.Min(Bounds.extents.x,Bounds.extents.z));
                int n=Physics.SphereCastNonAlloc(p+Vector3.up*(airborne?0:Mathf.Max(.5f,Bounds.center.y)),probe,direction,Hits,Mathf.Max(.8f,speed*dt+.3f),~0,QueryTriggerInteraction.Ignore);
                var hit=Hits.Take(n).Where(h=>h.collider!=null && !h.collider.transform.IsChildOf(transform)).OrderBy(h=>h.distance).FirstOrDefault();
                if(hit.collider!=null) { Vector3 slide=Vector3.ProjectOnPlane(direction,hit.normal); if(slide.sqrMagnitude>.1f) { Vector3 candidate=p+slide.normalized*speed*dt; if(!Physics.CheckSphere(candidate+Vector3.up*Mathf.Max(.5f,Bounds.center.y),probe,~0,QueryTriggerInteraction.Ignore)) next=candidate; else next=p; } else next=p; }
                if(new Vector2(next.x,next.z).magnitude>World.Radius-2) next=p;
                Vector3 look=next-p; if(!airborne) look.y=0; if(look.sqrMagnitude>.001f) transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(look),m.Num("turn_speed",90)*dt);
            }
            var body=GetComponent<Rigidbody>(); if(body!=null && body.isKinematic) body.MovePosition(next); else transform.position=next;
        }
        public void Rebatch()
        {
            foreach(var mesh in _batched) if(mesh!=null) Object.Destroy(mesh); _batched.Clear();
            foreach(Transform old in Visual) if(old.name=="CombinedStatic") { old.GetComponent<Renderer>().enabled=false; Object.Destroy(old.gameObject); }
            var dynamic=new HashSet<string>(_animationDefs.Where(a=>_animations.Contains((string)a["name"])).Select(a=>(string)a["part"]));
            bool Dynamic(Transform t) => dynamic.Contains(t.name) || t.parent!=Visual && t.parent!=null && Dynamic(t.parent);
            var staticParts=new List<Transform>();
            foreach(var part in Parts) { var r=part.Value.GetComponent<MeshRenderer>(); r.enabled=PartVisible(part.Key); if(!Dynamic(part.Value) && r.enabled) staticParts.Add(part.Value); }
            if(staticParts.Count<8) return;
            for(int sub=0;sub<2;sub++)
            {
                var combine=new List<CombineInstance>();
                foreach(var t in staticParts) { var mesh=t.GetComponent<MeshFilter>().sharedMesh; if(mesh.subMeshCount>sub) combine.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=Visual.worldToLocalMatrix*t.localToWorldMatrix}); }
                if(combine.Count==0) continue;
                var meshOut=new Mesh { name="CombinedBlueprint" }; meshOut.CombineMeshes(combine.ToArray(),true,true); _batched.Add(meshOut);
                var go=new GameObject("CombinedStatic"); go.transform.SetParent(Visual,false); go.AddComponent<MeshFilter>().sharedMesh=meshOut; go.AddComponent<MeshRenderer>().sharedMaterial=sub==0?World.Objects.LitMaterial:World.Objects.GlowMaterial;
            }
            foreach(var t in staticParts) t.GetComponent<MeshRenderer>().enabled=false;
            if(Entity!=null && Entity.Original.AbsoluteTint) Entity.Tint(Entity.Original.Tint);
        }
        void OnDestroy() { foreach(var mesh in _owned.Concat(_batched)) if(mesh!=null) Object.Destroy(mesh); if(Def!=null) { /* proxy mesh is owned by this runtime definition */ Object.Destroy(Def.Mesh); } }
    }
    public sealed class BlueprintContact : MonoBehaviour
    { public BlueprintActor Actor; public string Part; void OnTriggerEnter(Collider c) { Actor?.Contact(Part,c,true); } void OnTriggerStay(Collider c) { Actor?.Contact(Part,c,false); } }
    public sealed class BlueprintProjectile : MonoBehaviour
    {
        World _world; BlueprintActor _owner; RuleEntity _source; Statement _data; Vector3 _velocity; float _expires; bool _hit; int _epoch;
        public void Init(World world,BlueprintActor owner,Dictionary<string,object> data,Vector3 direction)
        { _world=world; _owner=owner; _source=owner.Entity; _epoch=owner.AttackEpoch; _data=BlueprintData.Wrap(data); _velocity=direction*_data.Num("speed",12); _expires=Time.time+_data.Num("lifetime",4); }
        void FixedUpdate()
        {
            if(Time.time>=_expires || _source==null || !_source.Alive || _source.Health==0 || _owner==null || _owner.AttackEpoch!=_epoch) { Object.Destroy(gameObject); return; }
            Vector3 from=transform.position; _velocity+=Physics.gravity*_data.Num("gravity",0)*Time.fixedDeltaTime; Vector3 to=from+_velocity*Time.fixedDeltaTime;
            var hits=Physics.SphereCastAll(from,.1f,(to-from).normalized,(to-from).magnitude,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance);
            foreach(var h in hits) if(!h.collider.transform.IsChildOf(transform) && (_owner==null || !h.collider.transform.IsChildOf(_owner.transform))) { Impact(h.collider,h.point); break; }
            if(!_hit) GetComponent<Rigidbody>().MovePosition(to);
        }
        void OnTriggerEnter(Collider other) { Impact(other,transform.position); }
        void Impact(Collider other,Vector3 point)
        {
            if(_owner==null || _owner.AttackEpoch!=_epoch || _source==null || !_source.Alive || _source.Health==0) { Object.Destroy(gameObject); return; }
            if(_hit || other.transform.IsChildOf(transform) || _owner!=null && other.transform.IsChildOf(_owner.transform)) return;
            _hit=true; RuleEntity target=other.GetComponentInParent<RuleBody>()?.Entity;
            bool replaced=target!=null && _world.Rules.Emit("hit",target,_source,"projectile",_velocity.magnitude);
            if(!replaced) { if(other.GetComponentInParent<PlayerHitRegion>()!=null) _world.Blueprints.DamagePlayer(_data.Num("damage")); else if(target!=null) _world.Rules.Damage(target,_data.Num("damage"),_source); }
            _world.Rules.Emit("projectile_impact",_source,target,"projectile",_velocity.magnitude,eventName:_data.Str("name"));
            if(_data.Has("impact")) _world.Rules.ParticleBurst(Statement.Make("particles","preset",_data.Str("impact"),"color",_data.Str("color","#FF8822")),point);
            Object.Destroy(gameObject);
        }
    }
}
