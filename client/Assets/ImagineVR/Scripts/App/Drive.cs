using UnityEngine;

namespace ImagineVR
{
    /// <summary>What the user is doing with the controls this frame, already read from the headset or the keyboard.</summary>
    public struct DriveInput
    {
        public float Throttle;     // 0..1: right trigger, W
        public float Brake;        // 0..1: left trigger, S (then reverse)
        public float Steer;        // -1..1: left stick X, A/D, or the hands on the wheel
        public float Pitch;        // -1..1: right stick Y (back is up), W/S for things that fly
        public float Roll;         // -1..1: right stick X, A/D for things that fly
        public float Lift;         // -1..1: right stick Y for things that hover; arrows up/down
        public float Yaw;          // -1..1: right stick X for things that hover; arrows left/right
        public float ThrottleAxis; // -1..1: left stick Y for planes (moves the throttle lever), arrows up/down
        public Vector2 Move;       // left stick, for things that hover
        public bool Handbrake;
    }

    /// <summary>
    /// How something ridden moves: as a car, a tank, a boat, a horse, a plane, a helicopter, a hover
    /// bike, a spaceship. Each frame it is given the controls and says how far to turn and move the
    /// rider; the Vehicle does the moving, the colliding and everything the rider hears and feels.
    ///
    /// The rider is only ever turned about the vertical and carried: the horizon stays level, and
    /// pitch, roll and suspension are shown on the vehicle around them (VisualPitch, VisualRoll).
    /// That is what keeps a headset comfortable at speed.
    /// </summary>
    public sealed class DriveModel
    {
        public readonly string Kind;
        public float Speed;            // metres a second along its heading, negative in reverse
        public float Side;             // sliding sideways, metres a second
        public float Altitude;         // above the ground, for things that leave it
        public float VisualPitch, VisualRoll;
        public float SteerAngle;       // degrees the front wheels are turned
        public float ThrottleLever;    // a plane's throttle stays where it was put
        public bool Airborne;
        public float Top, Accel = 7f, Grip = 0.85f;
        public string Gear = "D";

        readonly float _wheelbase, _width, _length;
        float _vy, _climb, _pitch, _roll, _suspension, _suspensionV;
        readonly bool _onWater;

        public bool Flies => Kind == "plane" || Kind == "glider" || Kind == "heli" || Kind == "hover" || Kind == "space";

        /// <summary>How an asset drives, when nothing says: from what it is and what moves on it.</summary>
        public static string KindFor(AssetDef def)
        {
            if (!string.IsNullOrEmpty(def.Drive) && def.Drive != "auto") return def.Drive;
            string id = def.Id;
            Mesh unused = def.Mesh;   // builds its parts
            bool rotor = false, propeller = false, wheels = false;
            foreach (PartDef p in def.Parts)
            {
                if (p.Role == "wheel") wheels = true;
                if (p.Role == "spinner") { if (Mathf.Abs(p.Axis.y) > 0.7f && p.Pivot.y > def.Bounds.center.y) rotor = true; else propeller = true; }
            }
            if (id.Contains("balloon") || id.Contains("ufo") || id.Contains("saucer") || id.Contains("hover")) return "hover";
            if (id.Contains("rocket") || id.Contains("starfighter") || id.Contains("spaceship") || id.Contains("lander")) return "space";
            if (rotor || id.Contains("helicopter") || id.Contains("chopper")) return "heli";
            if (def.Winged || def.Category == "animals" && def.Airborne) return "hover";
            if (def.Airborne || propeller && !wheels) return "plane";
            if (def.OnWater) return "boat";
            if (id.Contains("tank")) return "tank";
            if (id.Contains("bike") || id.Contains("motorcycle") || id.Contains("bicycle") || id.Contains("scooter")) return "bike";
            if (def.Category == "animals" || def.Category == "people") return "mount";
            if (def.Category == "vehicles" || wheels) return "car";
            return "simple";
        }

        public DriveModel(string kind, AssetDef def, float speedScale)
        {
            Kind = kind;
            Bounds b = def.Bounds;
            _length = Mathf.Max(b.size.z, 0.6f);
            _width = Mathf.Max(b.size.x, 0.4f);
            _wheelbase = Mathf.Clamp(_length * 0.62f, 0.8f, 8f);
            _onWater = def.OnWater;
            // a fast thing needs room: on small ground it is held back so the world's edge is not reached in seconds
            float room = World.Radius >= 1000f ? 1.6f : World.Radius >= 380f ? 1.25f : 1f;
            switch (kind)
            {
                case "car": Top = 22f * room; Accel = 7f; Grip = 0.9f; break;
                case "bike": Top = 24f * room; Accel = 8f; Grip = 0.85f; break;
                case "tank": Top = 9f; Accel = 3f; Grip = 1f; break;
                case "mount": Top = 9f * Mathf.Min(room, 1.3f); Accel = 5f; Grip = 1f; break;
                case "boat": Top = 14f * room; Accel = 4f; Grip = 0.35f; break;
                case "plane": Top = 55f * room; Accel = 6f; ThrottleLever = 0.6f; break;
                case "glider": Top = 30f * room; Accel = 0f; ThrottleLever = 0f; break;
                case "heli": Top = 28f * room; Accel = 6f; break;
                case "hover": Top = 20f * room; Accel = 7f; break;
                case "space": Top = 60f * room; Accel = 10f; break;
                default: Top = 7f; break;
            }
            _baseTop = Top;
            _baseAccel = Accel;
            Top = _baseTop * Mathf.Clamp(speedScale, 0.2f, 4f);
        }
        readonly float _baseTop, _baseAccel;

        /// <summary>`modify id:"ride"`: faster, grippier, quicker off the line.</summary>
        public void Tune(Statement s)
        {
            if (s.Has("speed")) Top = _baseTop * Mathf.Clamp(s.Num("speed"), 0.2f, 4f);
            if (s.Has("grip")) Grip = Mathf.Lerp(0.15f, 1.1f, Mathf.Clamp01(s.Num("grip")));
            if (s.Has("accel")) Accel = _baseAccel * Mathf.Clamp(s.Num("accel"), 0.2f, 3f);
        }

        /// <summary>A plane or a hover thing put straight into the sky (`ride.altitude`).</summary>
        public void StartAloft(float altitude)
        {
            Altitude = Mathf.Max(0f, altitude);
            Airborne = Altitude > 1f;
            if (Airborne && (Kind == "plane" || Kind == "glider")) { Speed = Top * 0.65f; ThrottleLever = 0.7f; }
        }

        /// <summary>
        /// One step. Says how far to turn the rider (degrees) and how far to carry them (world metres,
        /// level); the new height above the ground is in Altitude. `ground` is how grippy the surface is.
        /// </summary>
        public void Step(float dt, in DriveInput input, Vector3 forward, float groundGrip, float slope, out float turn, out Vector3 move)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            turn = 0f;
            move = Vector3.zero;
            switch (Kind)
            {
                case "plane": case "glider": Fly(dt, input, groundGrip, forward, out turn, out move); return;
                case "heli": case "hover": case "space": Hover(dt, input, forward, right, out turn, out move); return;
                case "tank": Tracks(dt, input, forward, out turn, out move); return;
                default: Wheels(dt, input, groundGrip, slope, forward, right, out turn, out move); return;
            }
        }

        /// <summary>A car, a bike, a horse, a boat: bicycle steering geometry, a grip limit that lets it slide, and a body on springs.</summary>
        void Wheels(float dt, in DriveInput input, float groundGrip, float slope, Vector3 forward, Vector3 right, out float turn, out Vector3 move)
        {
            float mu = Mathf.Clamp(groundGrip * Grip, 0.05f, 1.2f);
            if (Kind == "boat") mu = 0.25f * Grip;
            float speed01 = Mathf.Clamp01(Mathf.Abs(Speed) / Mathf.Max(Top * 0.6f, 1f));
            float lock_ = Kind == "mount" ? 50f : Kind == "boat" ? 30f : Mathf.Lerp(34f, 7f, speed01);
            SteerAngle = Mathf.MoveTowards(SteerAngle, input.Steer * lock_, 160f * dt);

            // throttle, brakes and reverse
            float drive = 0f;
            if (input.Throttle > 0.02f && Speed > -0.5f) drive = Accel * input.Throttle * (1f - Mathf.Clamp01(Speed / Top) * Mathf.Clamp01(Speed / Top));
            if (input.Brake > 0.02f)
            {
                if (Speed > 0.4f) drive -= 13f * input.Brake;                                   // braking
                else drive -= Accel * 0.6f * input.Brake * (1f - Mathf.Clamp01(-Speed / (Top * 0.3f)));   // then reverse, slowly
            }
            if (input.Throttle > 0.02f && Speed < -0.4f) drive += 13f * input.Throttle;        // throttle while rolling back is a brake
            if (input.Handbrake) drive -= Mathf.Sign(Speed) * 9f;
            float rolling = (Kind == "boat" ? 1.2f : 0.35f) + 0.0018f * Speed * Speed;
            drive -= Mathf.Sign(Speed) * rolling;
            drive -= slope * 9.81f * 0.6f;        // it slows climbing a hill and gathers speed down one
            float before = Speed;
            Speed += drive * dt;
            if (Mathf.Abs(Speed) < 0.05f && input.Throttle < 0.02f && input.Brake < 0.02f) Speed = 0f;
            if (Mathf.Sign(before) != Mathf.Sign(Speed) && before != 0f && input.Brake < 0.02f && input.Throttle < 0.02f) Speed = 0f;
            Gear = Speed < -0.3f ? "R" : Mathf.Abs(Speed) < 0.3f ? "N" : (1 + Mathf.Min(5, (int)(Mathf.Abs(Speed) / Mathf.Max(Top / 5.5f, 1f)))).ToString();

            // turning: what the wheels ask for, as far as the grip allows; past it, the back steps out
            float yawRate = Speed / _wheelbase * Mathf.Tan(SteerAngle * Mathf.Deg2Rad);     // radians a second
            if (Kind == "mount" && Mathf.Abs(Speed) < 1f) yawRate = input.Steer * 1.6f;      // a horse turns on the spot
            float need = Mathf.Abs(Speed * yawRate), limit = mu * 9.81f * (input.Handbrake ? 0.45f : 1f);
            if (need > limit && need > 0.01f)
            {
                float held = limit / need;
                Side += (1f - held) * Speed * yawRate * dt * -0.9f;
                yawRate *= Mathf.Lerp(held, 1f, 0.35f);       // a slide still turns: that is a drift
            }
            Side *= Mathf.Exp(-mu * (Kind == "boat" ? 1.2f : 5f) * dt);
            turn = yawRate * Mathf.Rad2Deg * dt;
            move = (forward * Speed + right * Side) * dt;

            // the body: it squats as it pulls away, dives under braking, leans out of a bend, follows the ground
            float accel = (Speed - before) / Mathf.Max(dt, 0.0001f);
            float lean = Kind == "bike" ? Mathf.Clamp(-yawRate * Speed * 3.5f, -35f, 35f) : Mathf.Clamp(Speed * yawRate * 1.1f, -4f, 4f);
            _suspensionV += (-accel * 0.35f - _suspension) * 30f * dt;
            _suspensionV *= Mathf.Exp(-8f * dt);
            _suspension += _suspensionV * dt;
            VisualPitch = Mathf.Clamp(_suspension, -4f, 4f) + Mathf.Atan(slope) * Mathf.Rad2Deg * -1f;
            VisualRoll = Mathf.Lerp(VisualRoll, lean, 1f - Mathf.Exp(-8f * dt));
            if (Kind == "boat") { VisualPitch += Mathf.Sin(Time.time * 1.3f) * 1.5f - Mathf.Clamp01(Speed / Top) * 5f; VisualRoll += Mathf.Sin(Time.time * 0.9f) * 1.5f; }
            Altitude = 0f;
        }

        /// <summary>A tank: each track driven, so it turns on the spot and never slides.</summary>
        void Tracks(float dt, in DriveInput input, Vector3 forward, out float turn, out Vector3 move)
        {
            float target = (input.Throttle - input.Brake) * Top;
            if (input.Brake > 0.02f && Speed > 0.5f) target = 0f;
            Speed = Mathf.MoveTowards(Speed, target, Accel * dt);
            turn = input.Steer * 55f * (1f - 0.5f * Mathf.Clamp01(Mathf.Abs(Speed) / Top)) * dt;
            SteerAngle = input.Steer * 20f;
            move = forward * Speed * dt;
            VisualPitch = Mathf.Lerp(VisualPitch, -(target - Speed) * 0.4f, 1f - Mathf.Exp(-6f * dt));
            VisualRoll = 0f;
            Gear = Speed < -0.3f ? "R" : Mathf.Abs(Speed) < 0.3f ? "N" : "D";
            Altitude = 0f;
        }

        /// <summary>
        /// A plane. The throttle lever stays where it is put; pull back to climb, roll to turn (the
        /// nose follows the wing round), too slow and it stalls and the nose drops. It takes off by
        /// getting fast enough on the ground and pulling back, and lands by coming down gently.
        /// </summary>
        void Fly(float dt, in DriveInput input, float groundGrip, Vector3 forward, out float turn, out Vector3 move)
        {
            ThrottleLever = Mathf.Clamp01(ThrottleLever + input.ThrottleAxis * 0.5f * dt + (input.Throttle - input.Brake) * 0.6f * dt);
            float takeoff = Top * 0.42f, stall = Top * 0.32f;
            float thrust = Kind == "glider" ? 0f : Accel * ThrottleLever * 1.6f;
            float drag = 0.012f * Speed * Speed * (Airborne ? 1f : 1.6f) + (Airborne ? 0f : 0.4f);
            if (!Airborne && input.Brake > 0.02f && Speed > 0.3f) drag += 8f * input.Brake;
            Speed += (thrust - drag - (Airborne ? 9.81f * Mathf.Sin(_pitch * Mathf.Deg2Rad) : 0f)) * dt;
            Speed = Mathf.Clamp(Speed, 0f, Top * 1.25f);

            if (!Airborne)
            {
                // on the ground it steers like a car, by the nose wheel
                float yawRate = Speed / Mathf.Max(_wheelbase, 2f) * Mathf.Tan(input.Steer * Mathf.Lerp(30f, 4f, Speed / takeoff) * Mathf.Deg2Rad);
                turn = yawRate * Mathf.Rad2Deg * dt;
                _pitch = Mathf.MoveTowards(_pitch, Speed > takeoff * 0.85f ? Mathf.Max(0f, -input.Pitch) * 12f : 0f, 20f * dt);
                if (Speed > takeoff && -input.Pitch > 0.2f) { Airborne = true; _climb = 2f; }
                _roll = Mathf.MoveTowards(_roll, 0f, 60f * dt);
                Altitude = 0f;
            }
            else
            {
                _roll = Mathf.Clamp(_roll + input.Roll * 80f * dt, -70f, 70f);
                if (Mathf.Abs(input.Roll) < 0.1f) _roll = Mathf.MoveTowards(_roll, 0f, 25f * dt);     // it levels its wings by itself
                _pitch = Mathf.Clamp(_pitch - input.Pitch * 40f * dt, -35f, 35f);
                if (Mathf.Abs(input.Pitch) < 0.1f) _pitch = Mathf.MoveTowards(_pitch, 0f, 6f * dt);
                bool stalled = Speed < stall;
                if (stalled) _pitch = Mathf.MoveTowards(_pitch, -25f, 30f * dt);
                float yawRate = 9.81f * Mathf.Tan(_roll * Mathf.Deg2Rad) / Mathf.Max(Speed, stall);     // a banked wing turns the plane
                yawRate += input.Steer * 0.25f;     // and the rudder, a little
                turn = yawRate * Mathf.Rad2Deg * dt;
                _climb = Speed * Mathf.Sin(_pitch * Mathf.Deg2Rad) - (stalled ? (stall - Speed) * 0.8f : 0f);
                Altitude += _climb * dt;
                if (Altitude <= 0f)
                {
                    Altitude = 0f;
                    bool gentle = _climb > -6f && Mathf.Abs(_roll) < 25f;
                    Airborne = false;
                    LastLanding = gentle ? Landing.Soft : Landing.Hard;
                    if (!gentle) Speed *= 0.4f;
                    _pitch = 0f;
                }
            }
            VisualPitch = -_pitch;
            VisualRoll = -_roll;
            SteerAngle = input.Steer * 25f;
            move = forward * Speed * Mathf.Cos(_pitch * Mathf.Deg2Rad) * dt;
            Gear = Airborne ? "AIR" : "GND";
        }

        public enum Landing { None, Soft, Hard }
        /// <summary>Set when a plane touches down: the Vehicle makes a crash of a hard one. Read and cleared by it.</summary>
        public Landing LastLanding;

        /// <summary>A helicopter, a hover bike, a dragon, a saucer, a spaceship: it goes where the stick points, turns on the other stick, rises and falls.</summary>
        void Hover(float dt, in DriveInput input, Vector3 forward, Vector3 right, out float turn, out Vector3 move)
        {
            Vector3 want = (forward * input.Move.y + right * input.Move.x) * Top;
            if (input.Throttle > 0.02f) want += forward * input.Throttle * Top;      // the trigger also goes forwards, as in anything else ridden
            _hover = Vector3.MoveTowards(_hover, want, Accel * dt);
            turn = input.Yaw * (Kind == "space" ? 60f : 70f) * dt;
            float climbRate = Kind == "space" ? 18f : Kind == "heli" ? 8f : 6f;
            _vy = Mathf.MoveTowards(_vy, input.Lift * climbRate, 10f * dt);
            Altitude = Kind == "space" ? Altitude + _vy * dt : Mathf.Max(0f, Altitude + _vy * dt);
            if (Kind == "hover" && Altitude < 1.2f && _vy <= 0f) Altitude = Mathf.MoveTowards(Altitude, 1.2f, 2f * dt);   // a hover bike floats above the ground
            Airborne = Altitude > 0.5f;
            move = _hover * dt;
            Speed = Vector3.Dot(_hover, forward);
            Side = Vector3.Dot(_hover, right);
            // it tilts into where it is going, and banks in a turn
            VisualPitch = Mathf.Lerp(VisualPitch, Mathf.Clamp(Speed / Top * 14f, -14f, 14f), 1f - Mathf.Exp(-4f * dt));
            VisualRoll = Mathf.Lerp(VisualRoll, Mathf.Clamp(-Side / Top * 14f - input.Yaw * 12f, -25f, 25f), 1f - Mathf.Exp(-4f * dt));
            Gear = Airborne ? "AIR" : "GND";
        }
        Vector3 _hover;

        /// <summary>It hit something it could not push aside.</summary>
        public void Collide()
        {
            Speed *= Speed > 4f ? -0.2f : 0.3f;
            Side *= 0.3f;
            _hover *= -0.2f;
        }
    }
}
