using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ImagineVR
{
    /// <summary>
    /// Takes turns in order and plays each one's statements into the terminal and the world at a
    /// readable pace. A statement is shown as a tool call at the same moment it is applied, so
    /// what the terminal says and what the user sees never drift apart.
    /// </summary>
    public sealed class Conductor : MonoBehaviour
    {
        public World World;
        public Terminal Terminal;
        public TurnClient Client;
        public Rig Rig;

        /// <summary>Seconds between world statements, so each one can be seen landing.</summary>
        public float Pace = 0.34f;

        readonly Queue<Turn> _turns = new Queue<Turn>();
        Turn _current;
        float _nextAt;
        bool _problem;
        bool _firstLight = true;
        AudioSource _voice;
        int _worldStatements;

        public bool Busy => _current != null || _turns.Count > 0;
        public bool IsCurrent(Turn turn) => _current == turn;
        public void CancelPending()
        {
            if (_current != null) _current.Closed = true;
            foreach (Turn turn in _turns) turn.Closed = true;
            _turns.Clear(); _current = null; _nextAt = 0;
        }

        /// <summary>A turn finished. True when it changed the world.</summary>
        public event System.Action<bool> TurnEnded;

        /// <summary>Stops everything in hand: nothing queued or half played is carried on with.</summary>
        public void Abort()
        {
            CancelPending();
            _problem = false;
            StopAllCoroutines();
            _voice.Stop();
        }

        void Awake()
        {
            var go = new GameObject("Voiceover");
            go.transform.SetParent(transform, false);
            _voice = go.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0.6f;
        }

        // ------------------------------------------------------------------ submitting

        public void SubmitText(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            Turn turn = Client.SendText(text);
            turn.PromptHandle = Terminal.Prompt(text);
            Enqueue(turn);
        }

        public void SubmitAudio(byte[] wav)
        {
            Turn turn = Client.SendAudio(wav);
            turn.PromptHandle = Terminal.Prompt(Terminal.Ellipsis);
            Enqueue(turn);
        }

        /// <summary>Plays statements from a script instead of the server: demos, tests, the replay tool.</summary>
        public Turn SubmitScript(string ndjson, string label = null)
        {
            var turn = new Turn { StartedAt = Time.time };
            SessionLog.Write("demo", "label", label);
            if (label != null) turn.PromptHandle = Terminal.Prompt(label);
            turn.Push(Statement.Make("status", "state", "building"));
            foreach (string line in ndjson.Split('\n'))
            {
                if (Statement.TryParse(line, out Statement s) && s.Op != "transcript") turn.Push(s);
            }
            if (!turn.SawDone) turn.Push(Statement.Make("done", "outcome", "world_changed"));
            Enqueue(turn);
            return turn;
        }

        void Enqueue(Turn turn)
        {
            _turns.Enqueue(turn);
            if (_current == null && Terminal.State != TerminalState.Listening) Terminal.SetState(TerminalState.Thinking);
        }

        // ------------------------------------------------------------------ playing

        void Update()
        {
            if (_current == null)
            {
                if (_turns.Count == 0) return;
                _current = _turns.Dequeue();
                _problem = false;
                _worldStatements = 0;
                if (Terminal.State != TerminalState.Listening) Terminal.SetState(TerminalState.Thinking);
            }

            while (_current != null && _current.Incoming.Count > 0)
            {
                Statement next = _current.Incoming.Peek();
                bool world = next.IsWorld && next.Op != "say";
                if (world && (Time.time < _nextAt || World.Terrain.Generating)) break;
                _current.Incoming.Dequeue();
                Play(next, world);
            }
        }

        void Play(Statement s, bool world)
        {
            if (s.Op != "status") SessionLog.Write("play", "op", s.Op, "statement", MiniJson.Serialize(s.Fields));
            switch (s.Op)
            {
                case "transcript":
                {
                    string text = s.Str("text", "");
                    if (_current.PromptHandle != null) Terminal.FillPrompt(_current.PromptHandle, text.Length > 0 ? text : "(nothing heard)");
                    else Terminal.Prompt(text);
                    return;
                }
                case "status":
                    if (Terminal.State == TerminalState.Listening) return;
                    Terminal.SetState(s.Str("state") == "building" ? TerminalState.Building : TerminalState.Thinking);
                    return;
                case "note":
                    Terminal.Note(s.Str("text", ""));
                    return;
                case "think":
                    Terminal.Think(s.Str("text", ""));
                    return;
                case "error":
                    _problem = true;
                    Terminal.Error(s.Str("message", "Something went wrong."));
                    Terminal.SetState(TerminalState.Problem, "Something went wrong. Try again.");
                    Sfx.Play("error", Terminal.transform.position, 0.6f);
                    if (Rig != null) Rig.Haptic(true, 0.6f, 0.15f);
                    return;
                case "done":
                    World.EndTurn();
                    if (!_problem)
                    {
                        if (Terminal.State != TerminalState.Listening) Terminal.SetState(TerminalState.Ready);
                        if (_worldStatements > 0) Sfx.Play("done", Terminal.transform.position, 0.5f);
                    }
                    _current = null;
                    TurnEnded?.Invoke(_worldStatements > 0 && !_problem);
                    return;
                case "say":
                    Terminal.Say(s.Str("text", ""));
                    if (s.Has("audio")) StartCoroutine(Speak(s.Str("audio")));
                    return;
            }
            if (!world) return;   // unknown operation from a newer server: skip it

            if (Terminal.State != TerminalState.Listening && Terminal.State != TerminalState.Building) Terminal.SetState(TerminalState.Building);
            bool firstLight = s.Op == "sky" && World.IsDark && _firstLight;
            Terminal.Tool(s);
            World.Apply(s);
            _worldStatements++;
            _nextAt = Time.time + (firstLight ? 1.1f : Pace);

            if (firstLight)
            {
                // the signature moment: light enters the dark room
                _firstLight = false;
                Sfx.Play("reveal", World.Head != null ? World.Head.position + World.Head.forward : Vector3.zero, 0.8f);
                if (Rig != null) { Rig.Haptic(true, 0.4f, 0.4f); Rig.Haptic(false, 0.4f, 0.4f); }
            }
            else
            {
                Sfx.Play("blip", Terminal.transform.position, 0.35f, PitchFor(s.Op));
                if (Rig != null) Rig.Haptic(true, 0.15f, 0.03f);
            }
        }

        static float PitchFor(string op)
        {
            switch (op)
            {
                case "sky": return 0.75f;
                case "terrain": return 0.67f;
                case "make": return 0.84f;
                case "when": return 1.5f;
                case "spawn": return 1.0f;
                case "scatter": return 1.12f;
                case "give": return 1.33f;
                case "weather": return 1.26f;
                case "remove": case "clear": return 0.56f;
                default: return 0.94f;
            }
        }

        /// <summary>Lets the dark-room reveal play again after a reset.</summary>
        public void ResetFirstLight() { _firstLight = true; }

        IEnumerator Speak(string url)
        {
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) yield break;   // a missing voice is just silence
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) yield break;
                _voice.transform.position = Terminal.transform.position;
                _voice.clip = clip;
                _voice.Play();
            }
        }
    }
}
