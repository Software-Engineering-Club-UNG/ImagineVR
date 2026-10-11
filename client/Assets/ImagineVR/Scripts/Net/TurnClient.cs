using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ImagineVR
{
    /// <summary>One request and the statements that came back for it.</summary>
    public sealed class Turn
    {
        public readonly Queue<Statement> Incoming = new Queue<Statement>();
        public bool Closed;             // nothing more will arrive
        public bool SawDone;
        public object PromptHandle;     // terminal line to fill in when the transcript arrives
        public bool IsAudio;
        public float StartedAt;

        public void Push(Statement s)
        {
            if (Closed) return;
            Incoming.Enqueue(s);
            if (s.Op == "done") { SawDone = true; Closed = true; }
        }

        /// <summary>Ends the turn locally with a problem the user can read.</summary>
        public void Fail(string message)
        {
            if (Closed) return;
            Incoming.Enqueue(Statement.Make("error", "message", message, "retryable", true));
            Incoming.Enqueue(Statement.Make("done", "outcome", "failed"));
            SawDone = true;
            Closed = true;
        }
    }

    /// <summary>Talks to the server: posts a request and feeds each line of the streamed reply into a Turn as it arrives.</summary>
    public sealed class TurnClient : MonoBehaviour
    {
        public ImagineSettings Settings;
        public World World;
        string _resettingSession;
        public string SessionId { get; private set; }
        public string CatalogVersion = "";
        /// <summary>
        /// Says where the user is standing and facing, as "x,z,yaw", or "home" in the dark room.
        /// Sent with each turn so the server can have things built around the user.
        /// </summary>
        public Func<string> Pose;

        readonly List<Turn> _open = new List<Turn>();
        int _count;

        void Awake()
        {
            NewSession();
        }

        /// <summary>Starts a conversation the server has never seen, and drops whatever was still on its way.</summary>
        public string NewSession()
        {
            string old = SessionId;
            foreach (Turn turn in _open) turn.Closed = true;   // their requests stop on the next frame
            _open.Clear();
            SessionId = Guid.NewGuid().ToString("N");
            World?.Rules.NewSession();
            return old;
        }

        string Url(string path) => Settings.ServerUrl.TrimEnd('/') + path;

        void Headers(UnityWebRequest request, string session = null)
        {
            request.SetRequestHeader("X-App-Token", Settings.AppToken);
            request.SetRequestHeader("X-Session-Id", session ?? SessionId);
            request.SetRequestHeader("X-Client-Capabilities", RuleEngine.Capability+","+BlueprintContract.Capability+","+BlueprintContract.RichCapability);
            if (!string.IsNullOrEmpty(CatalogVersion)) request.SetRequestHeader("X-Catalog-Version", CatalogVersion);
        }

        public Turn SendText(string text)
        {
            var turn = new Turn { StartedAt = Time.time };
            byte[] body = Encoding.UTF8.GetBytes(MiniJson.Serialize(new Dictionary<string, object> { ["text"] = text }));
            StartCoroutine(Run(turn, body, "application/json"));
            return turn;
        }

        public Turn SendAudio(byte[] wav)
        {
            var turn = new Turn { StartedAt = Time.time, IsAudio = true };
            StartCoroutine(Run(turn, wav, "audio/wav"));
            return turn;
        }

        sealed class LineHandler : DownloadHandlerScript
        {
            readonly Turn _turn;
            readonly List<byte> _pending = new List<byte>(512);
            public float LastData;
            public int Number, Lines;

            public LineHandler(Turn turn) : base(new byte[4096])
            {
                _turn = turn;
                LastData = Time.realtimeSinceStartup;
            }

            protected override bool ReceiveData(byte[] data, int length)
            {
                LastData = Time.realtimeSinceStartup;
                for (int i = 0; i < length; i++)
                {
                    byte b = data[i];
                    if (b == (byte)'\n') Flush();
                    else _pending.Add(b);
                }
                return true;
            }

            protected override void CompleteContent() { Flush(); }

            void Flush()
            {
                if (_pending.Count == 0) return;
                string line = Encoding.UTF8.GetString(_pending.ToArray());
                _pending.Clear();
                if (line.Trim().Length == 0) return;   // the server's keep-alive
                Lines++;
                bool ok = Statement.TryParse(line, out Statement s);
                SessionLog.Write("recv", "turn", Number, "line", line, "understood", ok);
                if (ok) _turn.Push(s);
            }
        }

        IEnumerator Run(Turn turn, byte[] body, string contentType)
        {
            _open.Add(turn); // include turns waiting for playback or snapshot upload in cancellation
            try { yield return RunCurrent(turn, body, contentType, SessionId); }
            finally { _open.Remove(turn); }
        }

        IEnumerator RunCurrent(Turn turn, byte[] body, string contentType, string session)
        {
            Conductor conductor = GetComponent<Conductor>();
            while (_resettingSession == session || conductor != null && !conductor.IsCurrent(turn)) { if (turn.Closed) yield break; yield return null; }
            if (turn.Closed) yield break;
            while(World!=null && World.Blueprints.Busy) { if(turn.Closed) yield break; yield return null; }
            if (World != null)
            {
                byte[] snapshot = Encoding.UTF8.GetBytes(MiniJson.Serialize(World.Rules.Snapshot()));
                using (var sync = new UnityWebRequest(Url("/v1/session/runtime"), UnityWebRequest.kHttpVerbPOST))
                {
                    sync.uploadHandler = new UploadHandlerRaw(snapshot) { contentType = "application/json" };
                    sync.downloadHandler = new DownloadHandlerBuffer(); sync.timeout = 10; Headers(sync, session);
                    yield return sync.SendWebRequest();
                    if (turn.Closed || SessionId != session) yield break;
                    SessionLog.Write("runtime_sync", "status", (int)sync.responseCode, "session", session);
                    if (sync.result != UnityWebRequest.Result.Success) { turn.Fail("Runtime sync failed (" + sync.responseCode + "). Local rules still run; reset the session and try again."); yield break; }
                }
            }
            if (turn.Closed) yield break;
            using (var request = new UnityWebRequest(Url("/v1/turn"), UnityWebRequest.kHttpVerbPOST))
            {
                var handler = new LineHandler(turn) { Number = ++_count };
                string pose = Pose != null ? Pose() : null;
                request.uploadHandler = new UploadHandlerRaw(body) { contentType = contentType };
                request.downloadHandler = handler;
                Headers(request, session);
                request.SetRequestHeader("Content-Type", contentType);
                request.SetRequestHeader("Accept", "application/x-ndjson");
                if (!string.IsNullOrEmpty(pose)) request.SetRequestHeader("X-User-Pose", pose);
                SessionLog.Write("send", "turn", handler.Number, "type", contentType, "bytes", body.Length, "pose", pose,
                    "text", contentType == "application/json" ? Encoding.UTF8.GetString(body) : null, "session", SessionId);
                float started = Time.realtimeSinceStartup;
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                while (!op.isDone)
                {
                    float now = Time.realtimeSinceStartup;
                    if (turn.Closed) break;
                    if (now - handler.LastData > Settings.LineTimeout || now - started > Settings.TotalTimeout)
                    {
                        request.Abort();
                        _open.Remove(turn);
                        SessionLog.Write("reply", "turn", handler.Number, "result", "timed out", "seconds", now - started, "lines", handler.Lines);
                        turn.Fail("The server stopped answering. What's built so far stays; try again.");
                        yield break;
                    }
                    yield return null;
                }
                _open.Remove(turn);
                SessionLog.Write("reply", "turn", handler.Number, "result", request.result.ToString(), "status", (int)request.responseCode,
                    "error", request.error, "seconds", Time.realtimeSinceStartup - started, "lines", handler.Lines, "finished", turn.SawDone);
                if (turn.Closed) yield break;

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    turn.Fail("I can't reach the server. Check the connection and try again.");
                }
                else if (request.responseCode == 401)
                {
                    turn.Fail("The server rejected this app's token.");
                }
                else if (request.responseCode == 429)
                {
                    turn.Fail("Too many requests at once. Give it a moment.");
                }
                else if (request.responseCode >= 400)
                {
                    turn.Fail("The server couldn't handle that (" + request.responseCode + "). Try again.");
                }
                else if (!turn.SawDone)
                {
                    turn.Fail("The reply was cut off. What's built so far stays; try again.");
                }
            }
        }

        /// <summary>Asks the server whether it is up. The callback gets null when it is not.</summary>
        public IEnumerator Health(Action<Dictionary<string, object>> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(Url("/v1/health")))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                Dictionary<string, object> result = null;
                if (request.result == UnityWebRequest.Result.Success) result = MiniJson.ParseObject(request.downloadHandler.text);
                callback(result);
            }
        }

        /// <summary>Tells the server to forget a conversation: this one, or an old one that was replaced.</summary>
        public IEnumerator ResetSession(string session = null)
        {
            string target = session ?? SessionId;
            if (target == SessionId) _resettingSession = target;
            using (var request = new UnityWebRequest(Url("/v1/session/reset"), UnityWebRequest.kHttpVerbPOST))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                Headers(request, target);
                request.timeout = 5;
                yield return request.SendWebRequest();
                if (target == SessionId && request.result == UnityWebRequest.Result.Success && int.TryParse(request.GetResponseHeader("X-Session-Generation"), out int generation)) World?.Rules.SetGeneration(generation);
            }
            if (_resettingSession == target) _resettingSession = null;
        }
    }
}
