using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ImagineVR
{
    // Shared data access and defensive validation; never reflect arbitrary components from data.
    public static class BlueprintData
    {
        public static Statement Wrap(Dictionary<string, object> d) => new Statement { Op = "blueprint", Fields = d ?? new Dictionary<string, object>() };
        public static List<Dictionary<string, object>> List(Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out object v) && v is List<object> l ? l.Cast<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>>();
        public static Vector3 Vec(Dictionary<string, object> d, string key, Vector3 fallback)
            => Wrap(d).TryVec3(key, out Vector3 v) ? v : fallback;
        public static List<object> Vector(Vector3 v) => new List<object> { (double)v.x, (double)v.y, (double)v.z };
        public static Dictionary<string, object> Clone(Dictionary<string, object> d) => MiniJson.ParseObject(MiniJson.Serialize(d));
    }

    public static class BlueprintContract
    {
        public const string Capability = "runtime-blueprints-v1";
        /// <summary>Says this app understands the richer parts: bevels, tapers, lathes, slabs, domes, blobs, mirrors and repeats.</summary>
        public const string RichCapability = "runtime-blueprints-v2";
        public const int MaxTriangles = 12000;
        public static readonly string[] Shapes = {"cube","sphere","cylinder","cone","capsule","pyramid","torus","disc","wedge","plank","dome","blob","lathe","slab","tube","surface","asset"};
        public static readonly string[] Sounds = {"gunshot","zap","boom","pop","thud","whoosh","sparkle","none"};
        static void Pairs(Dictionary<string, object> d, string k, int min, int max, double bound)
        { Require(d.TryGetValue(k,out object v) && v is List<object> l && l.Count>=min && l.Count<=max && l.All(x=>x is List<object> pair && pair.Count==2 && pair.All(n=>Number(n,-bound,bound))),"Invalid "+k); }
        public static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        static bool Id(object value) => value is string s && Regex.IsMatch(s,"^[a-zA-Z0-9_.-]{1,80}$");
        static bool Number(object value, double lo, double hi) => value is double n && !double.IsNaN(n) && !double.IsInfinity(n) && n >= lo && n <= hi;
        static void Fields(Dictionary<string, object> d, string allowed, params string[] required)
            => Require(d != null && !d.Keys.Except(allowed.Split(',')).Any() && required.All(d.ContainsKey), "Unknown/missing blueprint fields");
        static void Num(Dictionary<string, object> d, string k, double lo, double hi) { if (d.ContainsKey(k)) Require(Number(d[k],lo,hi),"Invalid " + k); }
        static void Bool(Dictionary<string, object> d, string k) { if (d.ContainsKey(k)) Require(d[k] is bool,"Invalid " + k); }
        static void Vec(Dictionary<string, object> d, string k, double bound, bool positive=false)
        { if (d.ContainsKey(k)) Require(d[k] is List<object> l && l.Count == 3 && l.All(v => Number(v,positive ? .002 : -bound,bound)),"Invalid vector " + k); }
        static void Target(Dictionary<string, object> d, string k, params string[] allowed)
        { if (!d.ContainsKey(k)) return; if (d[k] is Dictionary<string,object> s) RuleContract.Selector(s); else Require(allowed.Contains(d[k] as string),"Invalid target"); }
        static void Color(Dictionary<string, object> d) { if (d.ContainsKey("color")) Require(d["color"] is string s && Regex.IsMatch(s,"^#[a-fA-F0-9]{6}$"),"Invalid color"); }
        static HashSet<string> Named(List<Dictionary<string,object>> list, int max, string fields, params string[] required)
        {
            Require(list.Count<=max,"Component limit"); var names=new HashSet<string>();
            foreach (var d in list) { Fields(d,fields,required); Require(Id(d["name"]) && names.Add((string)d["name"]),"Invalid/duplicate name"); }
            return names;
        }
        public static void Movement(Dictionary<string,object> d)
        {
            Fields(d,"mode,airborne,speed,turn_speed,radius,altitude,target,path","mode");
            Require(new[]{"still","wander","follow","chase","flee","orbit","path"}.Contains(d["mode"] as string),"Unknown movement");
            Bool(d,"airborne"); Num(d,"speed",0,15); Num(d,"turn_speed",1,360); Num(d,"radius",.5,40); Num(d,"altitude",.5,40); Target(d,"target","player");
            Require((string)d["mode"]!="path" || d.ContainsKey("path"),"Path required");
            if (d.ContainsKey("path")) { Require(d["path"] is List<object> l && l.Count>=2 && l.Count<=24,"Path limit"); foreach(object p in (List<object>)d["path"]) Vec(new Dictionary<string,object>{{"p",p}},"p",120); }
        }
        public static void ExtensionAction(Dictionary<string,object> d, Dictionary<string,object> contract)
        {
            string kind=d.TryGetValue("action",out object k)? k as string : null;
            var actions=(Dictionary<string,object>)contract["actions"]; Require(kind!=null && actions.ContainsKey(kind),"Unknown blueprint action");
            var schema=(Dictionary<string,object>)actions[kind];
            Fields(d,"action,target,delay,"+string.Join(",",((List<object>)schema["required"]).Concat((List<object>)schema["optional"])),((List<object>)schema["required"]).Cast<string>().ToArray());
            Target(d,"target","self","source"); Num(d,"delay",0,60);
            if (kind=="motion_config") Movement(d["config"] as Dictionary<string,object>);
            foreach(string key in new[]{"name","part"}) if(d.ContainsKey(key)) Require(Id(d[key]),"Invalid identifier");
            Bool(d,"enabled"); Bool(d,"visible"); Vec(d,"position",20); Vec(d,"rotation",720); Vec(d,"scale",20,true);
        }
        public static void Validate(Dictionary<string, object> d, bool resolveAssets=true)
        {
            Fields(d,"op,id,revision,parts,sockets,animations,capabilities,states,initial_state,description","op","id","parts");
            Require((string)d["op"]=="blueprint" && Id(d["id"]),"Invalid blueprint identity"); Num(d,"revision",1,int.MaxValue);
            if(d.ContainsKey("revision")) Require((double)d["revision"]==Math.Floor((double)d["revision"]),"Integer revision required");
            if(d.ContainsKey("description")) Require(d["description"] is string text && text.Length<=200,"Description limit");
            foreach(string key in new[]{"parts","sockets","animations"}) if(d.ContainsKey(key)) Require(d[key] is List<object> list && list.All(x=>x is Dictionary<string,object>),"Invalid component list");
            var parts=BlueprintData.List(d,"parts"); Require(parts.Count>0 && parts.Count<=64,"Part limit"); var ids=new Dictionary<string,Dictionary<string,object>>(); int colliders=0;
            foreach(var p in parts)
            {
                Fields(p,"id,parent,shape,asset,position,rotation,scale,color,color_top,emission,collider,visible,points,radius,vertices,triangles,bevel,taper,sides,mirror,repeat,step,profile,outline,thickness,rough,seed,anchor","id","shape");
                Require(Id(p["id"]) && !ids.ContainsKey((string)p["id"]),"Duplicate/invalid part"); ids.Add((string)p["id"],p);
                string shape=p["shape"] as string; Require(Shapes.Contains(shape),"Unknown shape");
                Vec(p,"position",20); Vec(p,"rotation",720); Vec(p,"scale",20,true); Color(p); Num(p,"emission",0,3); Bool(p,"visible");
                if(p.ContainsKey("color_top")) Require(p["color_top"] is string ct && Regex.IsMatch(ct,"^#[a-fA-F0-9]{6}$"),"Invalid color_top");
                Num(p,"bevel",0,1); Num(p,"sides",3,24); Num(p,"repeat",1,12); Vec(p,"step",20); Num(p,"thickness",.002,20); Num(p,"rough",0,.6); Num(p,"seed",0,99999);
                if(p.ContainsKey("taper")) Require(Number(p["taper"],0,3) || p["taper"] is List<object> tl && tl.Count==2 && tl.All(n=>Number(n,0,3)),"Invalid taper");
                if(p.ContainsKey("mirror")) Require(p["mirror"] as string=="x","Invalid mirror");
                if(p.ContainsKey("anchor")) Require(p["anchor"] as string=="top" || p["anchor"] as string=="bottom","Invalid anchor");
                if(shape=="lathe") { Pairs(p,"profile",2,16,20); Require(((List<object>)p["profile"]).All(v=>(double)((List<object>)v)[0]>=0),"Lathe radius"); }
                if(shape=="slab") Pairs(p,"outline",3,24,20);
                if(p.ContainsKey("parent")) Require(Id(p["parent"]),"Invalid parent");
                if(p.ContainsKey("collider")) { Require(new[]{"box","sphere","capsule"}.Contains(p["collider"] as string),"Invalid collider"); colliders+=p.ContainsKey("mirror")?2:1; }
                if(shape=="asset") Require(p.ContainsKey("asset") && Id(p["asset"]) && (!resolveAssets || Library.Asset((string)p["asset"])!=null),"Unknown baked part");
                if(shape=="tube") { Require(p.TryGetValue("points",out object points) && points is List<object> pl && pl.Count>=2 && pl.Count<=16,"Tube points"); foreach(object v in (List<object>)p["points"]) Vec(new Dictionary<string,object>{{"v",v}},"v",20); Num(p,"radius",.01,3); }
                if(shape=="surface")
                {
                    Require(p.TryGetValue("vertices",out object vs) && vs is List<object> vl && vl.Count>=3 && vl.Count<=24,"Surface vertices");
                    foreach(object v in (List<object>)p["vertices"]) Vec(new Dictionary<string,object>{{"v",v}},"v",20);
                    Require(p.TryGetValue("triangles",out object ts) && ts is List<object> tl && tl.Count>=3 && tl.Count<=132 && tl.Count%3==0,"Surface indices");
                    foreach(object n in (List<object>)p["triangles"]) Require(Number(n,0,((List<object>)p["vertices"]).Count-1) && (double)n==Math.Floor((double)n),"Invalid index");
                }
            }
            Require(colliders<=16,"Collider limit");
            foreach(var p in parts) { var seen=new HashSet<string>(); var cursor=p; while(cursor!=null) { Require(seen.Add((string)cursor["id"]) && seen.Count<=8,"Hierarchy cycle/depth"); if(!cursor.TryGetValue("parent",out object par)) break; Require(ids.ContainsKey((string)par),"Missing parent"); cursor=ids[(string)par]; } }
            var sockets=BlueprintData.List(d,"sockets"); var socketNames=Named(sockets,16,"name,part,position,rotation","name","part");
            foreach(var s in sockets) { Require(s["part"] is string part && ids.ContainsKey(part),"Socket part missing"); Vec(s,"position",20); Vec(s,"rotation",720); }
            var animations=BlueprintData.List(d,"animations"); var animNames=Named(animations,16,"name,part,mode,property,amplitude,frequency,phase,duration,loop,enabled,frames,alternate","name","part","mode","property"); var owners=new HashSet<string>();
            foreach(var a in animations)
            {
                Require(a["part"] is string part && ids.ContainsKey(part),"Animation part missing");
                Require(new[]{"oscillate","spin","keyframes"}.Contains(a["mode"] as string) && new[]{"position","rotation","scale"}.Contains(a["property"] as string),"Invalid animation");
                Require(owners.Add(a["part"]+"/"+a["property"]),"Animation owner conflict");
                Require((string)a["mode"]!="spin" || (string)a["property"]=="rotation","Spin rotation only");
                Vec(a,"amplitude",(string)a["property"]=="rotation"?360:20); Num(a,"frequency",.05,10); Num(a,"phase",-360,360); Num(a,"duration",.1,60); Bool(a,"loop"); Bool(a,"enabled"); Bool(a,"alternate");
                if((string)a["mode"]=="keyframes") { var frames=BlueprintData.List(a,"frames"); Require(frames.Count>=2 && frames.Count<=16,"Frame limit"); double prev=-1; foreach(var f in frames) { Fields(f,"time,value","time","value"); Num(f,"time",0,60); Vec(f,"value",(string)a["property"]=="rotation"?720:20); Require((double)f["time"]>prev,"Frame ordering"); prev=(double)f["time"]; } Require((double)frames[0]["time"]==0 && prev>0,"Frame timing"); }
                else Require(a.ContainsKey("amplitude"),"Amplitude required");
            }
            var caps=BlueprintData.Wrap(d).Obj("capabilities") ?? new Dictionary<string,object>();
            Fields(caps,"grabbable,physics,mass,gravity,health,durability,grip,movement,contacts,emitters"); Bool(caps,"grabbable"); Bool(caps,"physics"); Num(caps,"mass",.02,100); Num(caps,"gravity",-.5,2); Num(caps,"health",0,10000); Num(caps,"durability",0,10000);
            if(caps.ContainsKey("grip")) { var g=caps["grip"] as Dictionary<string,object>; Fields(g,"position,rotation"); Vec(g,"position",20); Vec(g,"rotation",720); }
            if(caps.ContainsKey("movement")) Movement(caps["movement"] as Dictionary<string,object>);
            foreach(string key in new[]{"contacts","emitters"}) if(caps.ContainsKey(key)) Require(caps[key] is List<object> l && l.All(x=>x is Dictionary<string,object>),"Invalid attack list");
            var contacts=BlueprintData.List(caps,"contacts"); var contactNames=Named(contacts,8,"name,part,damage,cooldown,target,enabled","name","part","damage");
            foreach(var c in contacts) { Require(c["part"] is string part && ids.ContainsKey(part),"Contact part missing"); Num(c,"damage",0,1000); Num(c,"cooldown",.1,60); Target(c,"target","player"); Bool(c,"enabled"); }
            Require(colliders+contacts.Select(c=>(string)c["part"]).Distinct().Count()<=16,"Contact collider limit");
            var emitters=BlueprintData.List(caps,"emitters"); var emitterNames=Named(emitters,8,"name,socket,asset,target,speed,lifetime,gravity,damage,cooldown,enabled,autofire,color,impact,sound","name","socket","asset");
            foreach(var e in emitters) { Require(e["socket"] is string socket && socketNames.Contains(socket) && Id(e["asset"]),"Emitter reference missing"); Target(e,"target","player","forward"); Num(e,"speed",.1,40); Num(e,"lifetime",.1,10); Num(e,"gravity",-.5,2); Num(e,"damage",0,1000); Num(e,"cooldown",.08,60); if(e.ContainsKey("sound")) Require(Sounds.Contains(e["sound"] as string),"Invalid sound"); Bool(e,"enabled"); Bool(e,"autofire"); Color(e); if(e.ContainsKey("impact")) Require(new[]{"confetti","sparkles","sparks","smoke","dust","splash","explosion"}.Contains(e["impact"] as string),"Invalid impact"); }
            var states=BlueprintData.Wrap(d).Obj("states") ?? new Dictionary<string,object>(); Require(states.Count<=8,"State limit");
            foreach(var pair in states) { Require(Id(pair.Key),"Invalid state"); var s=pair.Value as Dictionary<string,object>; Fields(s,"movement,animations,emitters,contacts"); if(s.ContainsKey("movement")) Movement(s["movement"] as Dictionary<string,object>); foreach(var key in new[]{"animations","emitters","contacts"}) if(s.ContainsKey(key)) { var known=key=="animations"?animNames:key=="emitters"?emitterNames:contactNames; Require(s[key] is List<object> names && names.Count<=16 && names.All(x=>x is string n && known.Contains(n)),"State component missing"); } }
            if(d.ContainsKey("initial_state")) Require(d["initial_state"] is string initial && states.ContainsKey(initial),"Initial state missing");
            Require(MiniJson.Serialize(d).Length<=48000,"Blueprint byte limit");
        }
    }
}
