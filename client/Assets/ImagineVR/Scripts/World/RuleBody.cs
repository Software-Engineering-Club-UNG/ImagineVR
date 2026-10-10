using UnityEngine;

namespace ImagineVR
{
    public sealed class RuleBody : MonoBehaviour
    {
        public RuleEngine Engine;
        public RuleEntity Entity;
        float _lastImpact = -1, _settled;
        bool _placing, _lastReplaced;
        public void Released(bool thrown)
        {
            _placing = !thrown; _settled = 0;
            Engine.Emit("release", Entity);
            if (thrown) Engine.Emit("throw", Entity);
        }
        public bool Hit(RuleEntity source, string kind, float speed)
        {
            if (Time.time - _lastImpact < .08f) return _lastReplaced;
            _lastImpact = Time.time;
            bool replace = Engine.Emit("hit", Entity, source, kind, speed);
            if (!replace && (Entity.Health >= 0 || Entity.Durability >= 0))
            {
                replace = Engine.Damage(Entity, Mathf.Max(1, speed), source) || Entity.Health > 0 || Entity.Durability > 0;
            }
            _lastReplaced = replace;
            return _lastReplaced;
        }
        void OnCollisionEnter(Collision c)
        {
            RuleBody other = c.collider.GetComponentInParent<RuleBody>();
            Prop prop = c.collider.GetComponentInParent<Prop>();
            string kind = prop != null && prop.GroupId == "shot" ? "projectile" : prop != null ? "thrown" : "collision";
            Engine.Emit("collision_enter", Entity, other != null ? other.Entity : null, kind, c.relativeVelocity.magnitude);
            Engine.Emit("touch_enter", Entity, other != null ? other.Entity : null, kind, c.relativeVelocity.magnitude);
            // Existing creatures/props call Hit before their built-in reaction; scenery has no such hook.
            if (GetComponent<Creature>() == null && GetComponent<Prop>() == null)
                Hit(other != null ? other.Entity : null, kind, c.relativeVelocity.magnitude);
            if (c.relativeVelocity.y > .5f) Engine.Emit("land", Entity);
        }
        void OnCollisionExit(Collision c)
        {
            RuleBody other = c.collider.GetComponentInParent<RuleBody>();
            Engine.Emit("collision_exit", Entity, other != null ? other.Entity : null);
            Engine.Emit("touch_exit", Entity, other != null ? other.Entity : null);
        }
        void Update()
        {
            if (!_placing || Entity == null || !Entity.Alive) return;
            Rigidbody body = GetComponent<Rigidbody>();
            _settled = body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude < .01f && body.angularVelocity.sqrMagnitude < .01f ? _settled + Time.deltaTime : 0;
            if (_settled >= .5f) { _placing = false; Engine.Emit("place", Entity); }
        }
        void FixedUpdate()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic && Entity != null && body.useGravity && Entity.Gravity != 1)
                body.AddForce(Physics.gravity * (Entity.Gravity - 1), ForceMode.Acceleration);
        }
    }
}
