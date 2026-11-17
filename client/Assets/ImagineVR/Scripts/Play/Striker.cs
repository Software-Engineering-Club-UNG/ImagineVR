using UnityEngine;

namespace ImagineVR
{
    /// <summary>
    /// A swung club, bat, racket, stick or cue hitting a ball. Physics alone gives a feeble tap: a
    /// held thing is moved by the hand, and the contact sees only the hand's speed. Here the head
    /// of the tool is moving faster than the hand by the length of the shaft, its `strike` power
    /// multiplies that, and its loft lifts the ball: a drive goes up and away, a slow putt rolls.
    /// </summary>
    public static class Striker
    {
        public static void Strike(Prop ball, Prop tool, Collision contact)
        {
            if (ball == null || tool == null || ball.Body == null || ball.Body.isKinematic || tool.Holder == null) return;
            float power = tool.Def.StrikePower;
            if (power <= 0f) return;      // anything else held: physics, as before
            Vector3 hand = tool.Holder.Velocity;
            Vector3 head = hand * (1f + tool.Length * 0.9f);
            float speed = Mathf.Min(head.magnitude * power, 70f);
            if (speed < 0.3f) return;
            Vector3 along = head.sqrMagnitude > 0.01f ? head : ball.transform.position - tool.transform.position;
            Vector3 flat = new Vector3(along.x, 0f, along.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            flat.Normalize();
            // a soft stroke stays on the ground; only a real swing gets the full loft
            float loft = tool.Def.StrikeLoft * Mathf.Clamp01((speed - 3f) / 6f);
            Vector3 v = Quaternion.AngleAxis(-loft, Vector3.Cross(Vector3.up, flat)) * flat * speed;
            if (v.magnitude <= ball.Body.linearVelocity.magnitude) return;
            ball.Body.linearVelocity = v;
            ball.Body.angularVelocity = Vector3.Cross(Vector3.up, flat) * (speed * 2f);
            Sfx.Play(speed > 12f ? "strike" : "click", ball.transform.position, Mathf.Clamp01(0.3f + speed / 30f), Mathf.Clamp(1.4f - speed / 60f, 0.8f, 1.4f));
            ImagineApp app = ImagineApp.Instance;
            if (app != null && app.Rig != null) app.Rig.Haptic(tool.Holder == app.Rig.RightGrabber, Mathf.Clamp01(0.3f + speed / 25f), 0.06f);
            SessionLog.Write("strike", "ball", ball.Def.Id, "tool", tool.Def.Id, "speed", speed, "loft", loft);
        }
    }
}
