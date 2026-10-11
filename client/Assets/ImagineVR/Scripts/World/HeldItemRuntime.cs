using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImagineVR
{
    /// <summary>Per-instance held item state. No generated code; every action is bounded native logic.</summary>
    public sealed class HeldItemRuntime : MonoBehaviour
    {
        Prop prop;
        HeldProfile profile;
        Transform returningHand;
        Vector3 launchPosition, castFrom, castTo, tip, aim;
        float releasedAt, nextUse, castAt;
        bool armed, spent, returning, flying, lightOn, reeling;
        Transform stuckTo;
        Vector3 stuckPoint;
        Quaternion stuckRotation;
        int servings = 3;
        LineRenderer line;
        Material lineMaterial;
        GameObject bobber;
        Mesh bobberMesh;
        static int photoSlot;
        static readonly RaycastHit[] Hits = new RaycastHit[48];
        static readonly Collider[] LandingHits = new Collider[32];
        public bool Busy => armed || returning || flying || bobber != null;
        public bool Armed => armed;
        public bool Cast => bobber != null;
        public bool LightOn => lightOn;
        public int Servings => servings;
        public HeldProfile Profile => profile;
        World World => prop.System.World;

        public void Init(Prop owner, HeldProfile tuning) { prop = owner; profile = tuning; }

        public void Grabbed()
        {
            returning = flying = false;
            stuckTo = null;
            prop.Body.useGravity = true;
            prop.Body.angularDamping = 0.4f;
            if (!armed) releasedAt = 0;
        }

        public void Released(bool deliberateRelease, Vector3 velocity, Transform hand, bool launched = true)
        {
            EndUse();
            if (!deliberateRelease || spent) return; // cleanup/forced release never arms an explosive
            returningHand = hand; launchPosition = transform.position; releasedAt = Time.time;
            if (profile.Action == "explosive" || profile.Action == "payload")
            {
                armed = true;
                SessionLog.Write("item_armed", "asset", prop.Def.Id, "fuse", profile.Fuse);
            }
            if (profile.Action == "returning" || profile.Action == "disc")
            {
                if (!launched || velocity.magnitude < 0.8f) return;
                returning = profile.Action == "returning"; flying = true;
                prop.Body.angularDamping = 0.05f;
                prop.Body.angularVelocity = Vector3.up * (returning ? 18f : 24f);
                prop.Body.rotation = Quaternion.Euler(0, prop.Body.rotation.eulerAngles.y, 0);
            }
        }

        public void Use(bool pressed, bool rising, Vector3 useTip, Vector3 direction)
        {
            tip = useTip; aim = direction.normalized;
            if (spent || profile == null) return;
            if (profile.Action == "beam")
            {
                if (!pressed) { HideLine(); return; }
                EnsureLine();
                Vector3 end = Ray(tip, aim, 20, out RaycastHit hit) ? hit.point : tip + aim * 20;
                line.positionCount = 2; line.SetPosition(0, tip); line.SetPosition(1, end); line.enabled = true;
                return;
            }
            if (!pressed) return;
            bool continuous = profile.Action == "powered_tool" || profile.Action == "weld" || profile.Action == "extinguish" || profile.Action == "spray";
            if ((!rising && !continuous) || Time.time < nextUse) return;
            nextUse = Time.time + (continuous ? 0.12f : 0.35f);
            switch (profile.Action)
            {
                case "explosive":
                    World.Emit("Release " + prop.Def.Id.Replace('_', ' ') + " to start its fuse.");
                    Sfx.Play("tap", tip, 0.5f); break;
                case "fishing":
                    if (bobber == null) CastLine(); else { reeling = true; castFrom = bobber.transform.position; castAt = Time.time; }
                    break;
                case "light": lightOn = !lightOn; Sfx.Play("tap", tip, 0.4f); break;
                case "scan":
                    if (Ray(tip, aim, 20, out RaycastHit scanned))
                    {
                        Prop p = scanned.collider.GetComponentInParent<Prop>();
                        Creature creature = scanned.collider.GetComponentInParent<Creature>();
                        World.Emit("Scanner: " + (p != null ? p.Def.Id.Replace('_', ' ') : creature != null ? creature.name : scanned.collider.name));
                    }
                    else World.Emit("Scanner: no target within 20 metres.");
                    Rules.Burst("sparkles", tip, profile.Color); break;
                case "signal": Sfx.Play("chime", tip, 0.5f); World.Emit("Radio: signal acknowledged."); break;
                case "repair":
                    if (TargetProp(3, out Prop repaired))
                    {
                        repaired.SetTint(Color.white);
                        if (!repaired.Body.isKinematic) repaired.Body.linearVelocity = repaired.Body.angularVelocity = Vector3.zero;
                        repaired.transform.rotation = Quaternion.identity; Rules.Burst("sparks", repaired.transform.position);
                    }
                    break;
                case "powered_tool": case "weld":
                    Rules.Burst("sparks", tip, profile.Color);
                    if (Ray(tip, aim, 1.6f, out RaycastHit struck))
                    {
                        Creature c = struck.collider.GetComponentInParent<Creature>();
                        Prop p = struck.collider.GetComponentInParent<Prop>();
                        if (c != null && !World.Rules.Fire("hit", Rules.Of(c))) c.Knock(aim);
                        if (p != null && !World.Rules.Fire("hit", Rules.Of(p)) && !p.Body.isKinematic) p.Body.AddForce(aim * 3, ForceMode.VelocityChange);
                    }
                    Sfx.Play("tap", tip, 0.25f, 0.7f); break;
                case "extinguish":
                    Fx.Burst(tip + aim * 0.2f, new Color(0.8f, 0.95f, 1, 0.55f), Color.white, 6, 1.5f, 0.08f, 0.5f, 0.1f, false);
                    if (TargetProp(3, out Prop cooled) && !cooled.Body.isKinematic) { cooled.Body.linearVelocity *= 0.4f; cooled.Body.angularVelocity *= 0.4f; }
                    foreach (ParticleSystem fire in World.GetComponentsInChildren<ParticleSystem>())
                        if (fire.name.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0 && Vector3.Distance(fire.transform.position, tip) < 3)
                            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    break;
                case "spray":
                    Fx.Burst(tip, profile.Color, Color.white, 4, 0.8f, 0.025f, 0.4f, 0, false);
                    if (TargetProp(3, out Prop painted)) painted.SetTint(profile.Color); break;
                case "camera":
                    string path = System.IO.Path.Combine(Application.persistentDataPath, "ImagineVR-photo-" + (photoSlot++ % 5) + ".png");
                    ScreenCapture.CaptureScreenshot(path); Sfx.Play("tap", tip, 0.6f); World.Emit("Camera photo: " + path); break;
                case "grapple": case "teleport":
                    if (Ray(tip, aim, profile.Action == "grapple" ? 12 : 15, out RaycastHit landing) && World.Vehicle != null && landing.normal.y > 0.65f && landing.collider.attachedRigidbody == null)
                    {
                        Vector3 feet = landing.point + Vector3.up * 0.03f;
                        if (SafeLanding(feet))
                        {
                            Rules.Burst("sparkles", feet, profile.Color);
                            if (profile.Action == "grapple") World.Vehicle.PullTo(feet);
                            else World.Vehicle.PlaceAt(feet, World.Vehicle.transform.eulerAngles.y);
                        }
                    }
                    else World.Emit("Point at nearby clear ground to land.");
                    break;
                case "music": Sfx.Play("chime", tip, 0.7f, 0.8f + servings * 0.15f); Rules.Burst("sparkles", tip, profile.Color); break;
                case "drink":
                    if (servings <= 0) { World.Emit(prop.Def.Id.Replace('_', ' ') + " is empty."); break; }
                    servings--; Rules.Burst("splash", transform.position, profile.Color); Sfx.Play("pop", tip, 0.3f);
                    World.Emit(servings == 0 ? "Empty." : "Sip! " + servings + " servings left.");
                    if (servings == 0) prop.SetTint(new Color(0.5f, 0.5f, 0.5f));
                    break;
            }
        }

        bool TargetProp(float distance, out Prop target)
        {
            target = null;
            if (Ray(tip, aim, distance, out RaycastHit hit)) target = hit.collider.GetComponentInParent<Prop>();
            return target != null && target != prop && !target.Held;
        }

        bool SafeLanding(Vector3 feet)
        {
            if (new Vector2(feet.x, feet.z).magnitude > World.Radius - 8 || World.Structures.HeadBlocked(feet + Vector3.up * 1.5f)) return false;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.4f, 0.3f, LandingHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (LandingHits[i].attachedRigidbody == null && LandingHits[i].GetComponentInParent<Rig>() == null) return false;
            return count < LandingHits.Length;
        }

        bool Ray(Vector3 from, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(from, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider.GetComponentInParent<Rig>() != null || collider.GetComponentInParent<Prop>() == prop) continue;
                if (Hits[i].distance < closest) { closest = Hits[i].distance; best = Hits[i]; }
            }
            return closest < float.PositiveInfinity;
        }

        void CastLine()
        {
            castFrom = tip; castTo = tip + aim * 8 + Vector3.up * 0.5f;
            if (Ray(tip, aim, 8, out RaycastHit hit)) castTo = hit.point;
            float floor = World.SurfaceHeight(castTo.x, castTo.z), water = World.Terrain.WaterY;
            castTo.y = Mathf.Max(floor, float.IsNaN(water) ? floor : water) + 0.05f;
            castAt = Time.time; reeling = false;
            bobber = new GameObject("FishingBobber"); bobber.transform.SetParent(World.transform, false);
            var builder = new MeshBuilder(); builder.Col(Pal.Red).Sphere(Vector3.zero, 0.035f, 1);
            bobberMesh = builder.Build("Bobber");
            bobber.AddComponent<MeshFilter>().sharedMesh = bobberMesh;
            bobber.AddComponent<MeshRenderer>().sharedMaterial = World.Objects.LitMaterial;
            bobber.transform.position = tip;
            EnsureLine(); line.positionCount = 16; line.enabled = true;
            Sfx.Play("whoosh", tip, 0.5f);
        }

        void EnsureLine()
        {
            if (line != null) return;
            var go = new GameObject("HeldLine"); go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            lineMaterial = new Material(Shader.Find("ImagineVR/Unlit"));
            lineMaterial.SetColor("_Color", Color.white);
            line.sharedMaterial = lineMaterial; line.useWorldSpace = true;
            line.startWidth = line.endWidth = profile.Action == "fishing" ? 0.003f : 0.008f;
            line.startColor = line.endColor = profile.Action == "fishing" ? Color.white : profile.Color;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        }
        void HideLine() { if (line != null) line.enabled = false; }
        public void EndUse() { if (profile != null && profile.Action == "beam") HideLine(); }

        void Update()
        {
            if (prop == null || profile == null || spent) return;
            if (armed && Time.time - releasedAt >= profile.Fuse) { Detonate(); return; }
            if (stuckTo != null && armed && !prop.Held)
            {
                prop.Body.MovePosition(stuckTo.TransformPoint(stuckPoint));
                prop.Body.MoveRotation(stuckTo.rotation * stuckRotation);
            }
            if (bobber != null)
            {
                if (!prop.Held) { Retract(); return; }
                // The visual tracks the hand in LateUpdate; derive the tip from it instead of the lagging rigidbody.
                Vector3 start = prop.Visual.position + prop.Visual.up * prop.Length;
                float t = Mathf.Clamp01((Time.time - castAt) / 0.8f);
                Vector3 end = reeling ? Vector3.Lerp(castFrom, start, t) : Vector3.Lerp(castFrom, castTo, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.5f;
                bobber.transform.position = end;
                for (int i = 0; i < 16; i++) { float u = i / 15f; line.SetPosition(i, Vector3.Lerp(start, end, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 0.12f); }
                if (reeling && t >= 1) Retract();
            }
        }

        void FixedUpdate()
        {
            if (prop == null || !flying || prop.Held || prop.Floating || spent) return;
            Rigidbody body = prop.Body;
            float elapsed = Time.time - releasedAt;
            if (returning && elapsed > 0.7f)
            {
                Vector3 destination = returningHand != null ? returningHand.position : World.Head.position + World.Head.forward * 0.3f;
                Vector3 delta = destination - transform.position;
                if (delta.magnitude < 0.3f)
                {
                    flying = returning = false; body.linearVelocity = body.angularVelocity = Vector3.zero;
                    prop.FloatAt(destination); Sfx.Play("grab", destination, 0.4f); return;
                }
                if (elapsed > 8) { flying = returning = false; body.useGravity = true; return; }
                // Sweep each return step: do not drag a returning item through a wall.
                Vector3 wanted = delta.normalized * Mathf.Min(12, delta.magnitude * 5);
                Vector3 velocity = Vector3.MoveTowards(body.linearVelocity, wanted, 30 * Time.fixedDeltaTime);
                if (Ray(transform.position, velocity.normalized, velocity.magnitude * Time.fixedDeltaTime + 0.08f, out _)) velocity = Vector3.zero;
                body.useGravity = false; body.linearVelocity = velocity;
            }
            else
            {
                Vector3 velocity = body.linearVelocity;
                float speed = new Vector2(velocity.x, velocity.z).magnitude;
                // Stylized disc aerodynamics: speed-dependent lift, modest drag, spin-stabilised attitude.
                body.AddForce(Vector3.up * Mathf.Min(8.5f, speed * speed * 0.08f), ForceMode.Acceleration);
                body.AddForce(-velocity * (profile.Action == "disc" ? 0.12f : 0.08f), ForceMode.Acceleration);
                if (elapsed > 8 || speed < 0.4f) flying = false;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (profile == null || spent) return;
            if (profile.Action == "payload" && armed) { Detonate(); return; }
            if (profile.Sticky && armed)
            {
                prop.Body.linearVelocity = Vector3.zero; prop.Body.isKinematic = true;
                stuckTo = collision.transform; stuckPoint = stuckTo.InverseTransformPoint(transform.position);
                stuckRotation = Quaternion.Inverse(stuckTo.rotation) * transform.rotation;
            }
            if (profile.Action == "disc") flying = false;
        }

        public void Detonate()
        {
            if (spent || !armed) return;
            spent = true; armed = false;
            Vector3 at = prop.Body.position + prop.Body.rotation * Vector3.Scale(prop.Def.Bounds.center, transform.localScale);
            string effect = profile.Effect;
            if (effect == "smoke") Fx.Burst(at, new Color(0.4f, 0.4f, 0.45f, 0.7f), new Color(0.7f, 0.7f, 0.7f, 0.3f), 48, 1.5f, 0.5f, 7, -0.05f, false);
            else if (effect == "confetti") Fx.Confetti(at);
            else Rules.Burst(effect == "explosion" ? "fire" : effect == "stun" ? "sparkles" : effect == "freeze" ? "splash" : effect == "paint" ? "splash" : effect.Length == 0 ? "sparks" : effect, at, profile.Color);
            bool force = effect == "explosion" || effect == "fire";
            if (force) Sfx.Play("boom", at, 0.8f);
            else Sfx.Play("pop", at, 0.6f);
            foreach (Prop p in prop.System.All)
            {
                if (p == null || p == prop || p.Held || p.Body == null || Vector3.Distance(p.Body.position, at) > profile.Radius || !BlastVisible(at, p.Body.position)) continue;
                if (effect == "paint") p.SetTint(profile.Color);
                else if (effect == "freeze" || effect == "stun") HeldStasis.Apply(p, effect == "freeze" ? 4 : 2);
                else if (force && !p.Body.isKinematic) p.Body.AddExplosionForce(8, at, profile.Radius, 0.4f, ForceMode.VelocityChange);
            }
            foreach (Creature creature in new System.Collections.Generic.List<Creature>(Creature.All))
            {
                if (creature == null || Vector3.Distance(creature.transform.position, at) > profile.Radius || !BlastVisible(at, creature.transform.position + Vector3.up * 0.5f)) continue;
                if (World.Rules.Fire("hit", Rules.Of(creature))) continue;
                if (effect == "freeze" || effect == "stun") creature.Stasis(effect == "freeze" ? 4 : 2);
                else if (force) creature.Knock(creature.transform.position - at);
            }
            SessionLog.Write("item_detonated", "asset", prop.Def.Id, "effect", effect);
            prop.System.Destroy(prop);
        }

        bool BlastVisible(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, Hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;
                if (c.attachedRigidbody == null && c.GetComponentInParent<Creature>() == null && c.GetComponentInParent<Rig>() == null) return false;
            }
            return count < Hits.Length; // saturated queries fail closed rather than ignore a wall
        }

        void Retract()
        {
            if (bobber != null) Destroy(bobber); bobber = null;
            if (bobberMesh != null) Destroy(bobberMesh); bobberMesh = null;
            HideLine();
        }

        void LateUpdate()
        {
            if (prop != null && profile != null && profile.Action == "light")
                HeldLights.Publish(this, lightOn ? prop.Visual.position + prop.Visual.up * prop.Length : Vector3.zero,
                    prop.Visual.up, profile.Color, prop.Def.Id.Contains("lantern"), lightOn);
        }

        void OnDestroy()
        {
            Retract(); if (lineMaterial != null) Destroy(lineMaterial);
            HeldLights.Remove(this);
        }
    }

    public sealed class HeldStasis : MonoBehaviour
    {
        Prop prop;
        float until;
        bool wasKinematic;
        public static void Apply(Prop p, float seconds)
        {
            HeldStasis state = p.GetComponent<HeldStasis>();
            if (state == null) { state = p.gameObject.AddComponent<HeldStasis>(); state.prop = p; state.wasKinematic = p.Body.isKinematic; }
            state.until = Mathf.Max(state.until, Time.time + seconds);
            if (!p.Body.isKinematic) p.Body.linearVelocity = p.Body.angularVelocity = Vector3.zero;
            p.Body.isKinematic = true;
        }
        void Update()
        {
            if (Time.time < until) return;
            if (prop != null) prop.Body.isKinematic = wasKinematic || prop.Held || prop.Floating;
            Destroy(this);
        }
    }

    /// <summary>At most four portable lights, shared by the custom vertex-lighting shaders.</summary>
    static class HeldLights
    {
        static readonly HeldItemRuntime[] Owners = new HeldItemRuntime[4];
        static readonly Vector4[] Positions = new Vector4[4], Directions = new Vector4[4], Colors = new Vector4[4];
        public static void Publish(HeldItemRuntime owner, Vector3 at, Vector3 direction, Color color, bool omni, bool on)
        {
            int slot = Array.IndexOf(Owners, owner);
            if (!on) { if (slot >= 0) Remove(owner); return; }
            if (slot < 0) for (int i = 0; i < 4; i++) if (Owners[i] == null) { slot = i; Owners[i] = owner; break; }
            if (slot < 0) return;
            Positions[slot] = new Vector4(at.x, at.y, at.z, omni ? 5 : 10);
            Directions[slot] = new Vector4(direction.x, direction.y, direction.z, omni ? -1 : 0.8f);
            Color linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            Colors[slot] = new Vector4(linear.r, linear.g, linear.b, 2);
            Upload();
        }
        public static void Remove(HeldItemRuntime owner)
        {
            int slot = Array.IndexOf(Owners, owner); if (slot < 0) return;
            Owners[slot] = null; Positions[slot] = Colors[slot] = Vector4.zero; Upload();
        }
        static void Upload()
        {
            Shader.SetGlobalVectorArray("_ImHeldLightPositions", Positions);
            Shader.SetGlobalVectorArray("_ImHeldLightDirections", Directions);
            Shader.SetGlobalVectorArray("_ImHeldLightColors", Colors);
        }
    }
}
