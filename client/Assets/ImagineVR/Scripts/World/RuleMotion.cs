using System.Collections.Generic;
using UnityEngine;

namespace ImagineVR
{
    public sealed class RuleMotion : MonoBehaviour
    {
        RuleEngine _engine;
        RuleEntity _entity;
        Route _route;
        float _speed, _distance;
        public void Init(RuleEngine engine, RuleEntity entity, Statement action)
        {
            _engine = engine; _entity = entity; _distance = 0;
            Mover oldMotion = GetComponent<Mover>(); if (oldMotion != null) oldMotion.enabled = false;
            var points = new List<Vector3>();
            if (!action.TryPath("path", out points))
            {
                action.TryVec3("position", out Vector3 end);
                points = new List<Vector3> { transform.position, end };
            }
            _route = new Route(points, action.Flag("loop", false));
            _speed = Mathf.Clamp(action.Num("speed", _route.Length / Mathf.Max(.2f, action.Num("duration", 1))), .1f, 40);
        }
        void Update()
        {
            if (_route == null || _entity == null || !_entity.Alive) return;
            _distance += _speed * Time.deltaTime;
            transform.position = _route.At(_distance, out Vector3 heading);
            if (heading.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(heading);
            if (!_route.Loop && _distance >= _route.Length) { _engine.Emit("path_complete", _entity); Destroy(this); }
        }
    }
}
