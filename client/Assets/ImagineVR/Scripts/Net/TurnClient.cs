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
        public string SessionId { get; private set; }
        public string CatalogVersion = "";

        void Awake()
        {
            SessionId = Guid.NewGuid().ToString("N");
        }

        string Url(string path) => Settings.ServerUrl.TrimEnd('/') + path;

        void Headers(UnityWebRequest request)
        {
            request.SetRequestHeader("X-App-Token", Settings.AppToken);
            request.SetRequestHeader("X-Session-Id", SessionId);
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
                if (Statement.TryParse(line, out Statement s)) _turn.Push(s);
            }
        }

        IEnumerator Run(Turn turn, byte[] body, string contentType)
        {
            using (var request = new UnityWebRequest(Url("/v1/turn"), UnityWebRequest.kHttpVerbPOST))
            {
                var handler = new LineHandler(turn);
                request.uploadHandler = new UploadHandlerRaw(body) { contentType = contentType };
                request.downloadHandler = handler;
                Headers(request);
                request.SetRequestHeader("Content-Type", contentType);
                request.SetRequestHeader("Accept", "application/x-ndjson");
                float started = Time.realtimeSinceStartup;
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                while (!op.isDone)
                {
                    float now = Time.realtimeSinceStartup;
                    if (turn.Closed) break;
                    if (now - handler.LastData > Settings.LineTimeout || now - started > Settings.TotalTimeout)
                    {
                        request.Abort();
                        turn.Fail("The server stopped answering. What's built so far stays; try again.");
                        yield break;
                    }
                    yield return null;
                }
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

        public IEnumerator ResetSession()
        {
            using (var request = new UnityWebRequest(Url("/v1/session/reset"), UnityWebRequest.kHttpVerbPOST))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                Headers(request);
                request.timeout = 5;
                yield return request.SendWebRequest();
            }
        }
    }
}
