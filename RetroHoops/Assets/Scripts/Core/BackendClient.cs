using System;
using System.Collections;
using System.Text;
using CallerRetroBall.Logic;
using UnityEngine;
using UnityEngine.Networking;

namespace CallerRetroBall.Core
{
    public enum BackendState { Off = 0, Connecting = 1, Ready = 2, Failed = 3 }

    /// <summary>
    /// Talks to the Retro Hoops Live server (<see cref="BackendConfig.Url"/>): Game Center sign-in, the
    /// subscription check (the server asks Apple), and Live game start / result reports. The server is the
    /// only place Live ratings change. With no server address set, everything here stays Off and Live
    /// keeps local ratings. The session token lives in memory only (never saved to disk).
    /// </summary>
    public sealed class BackendClient : MonoBehaviour
    {
        private const int TimeoutSeconds = 12;
        private static BackendClient _instance;
        private static string _token;
        private static float _tokenUntil;

        public static BackendState State { get; private set; }
        public static string Error { get; private set; } = "";
        public static bool SubscriptionActive { get; private set; }
        public static Backend.PlayerInfo Player { get; private set; }
        public static bool Ready => State == BackendState.Ready && !string.IsNullOrEmpty(_token) && Time.unscaledTime < _tokenUntil;

        private static BackendClient Runner()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[BackendClient]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BackendClient>();
            return _instance;
        }

        /// <summary>Signs in (Game Center proof → session), links the subscription, and loads your server rating.</summary>
        public static void Connect()
        {
            if (!BackendConfig.Enabled) { State = BackendState.Off; return; }
            if (State == BackendState.Connecting || Ready) return;
            State = BackendState.Connecting;
            Error = "";
            Runner().StartCoroutine(Runner().ConnectRoutine());
        }

        private IEnumerator ConnectRoutine()
        {
            LiveLink.FetchIdentity();
            float until = Time.unscaledTime + 15f;
            while (LiveLink.IdentityState == 1 && Time.unscaledTime < until) yield return null;
            if (LiveLink.IdentityState != 2) { Fail("Game Center sign-in is needed for Live."); yield break; }
            var id = LiveLink.Identity;
            string me = LiveLink.LocalId;
            string body = Backend.SessionBody(me, LiveLink.LocalName, id.KeyUrl, id.Signature, id.Salt, id.Timestamp);
            Response r = default;
            yield return Send("POST", "/v1/session", body, false, x => r = x);
            if (!r.Ok) { Fail(r.Message("Couldn't sign in to Retro Hoops Live.")); yield break; }
            _token = Backend.Str(r.Json, "token");
            _tokenUntil = Time.unscaledTime + (float)Backend.Num(r.Json, "expiresIn", 3600) - 120f;
            Player = Backend.ReadPlayer(Backend.Obj(r.Json, "player"));

            // Phase 41: LIVE is free (Pro.LiveIsFree), so no subscription is linked to the player any more.
            yield return Send("GET", "/v1/me", null, true, x => r = x);
            if (!r.Ok) { Fail(r.Message("Couldn't reach Retro Hoops Live.")); yield break; }
            Player = Backend.ReadPlayer(Backend.Obj(r.Json, "player"));
            SubscriptionActive = Backend.Bool(Backend.Obj(r.Json, "subscription"), "active");
            Backend.Mirror(App.Career?.live, Player);
            App.SaveCareer();
            State = BackendState.Ready;
        }

        private static void Fail(string why)
        {
            State = BackendState.Failed;
            Error = why;
            _token = null;
        }

        /// <summary>Forget the session (sign in again next time).</summary>
        public static void Reset()
        {
            _token = null;
            State = BackendConfig.Enabled ? BackendState.Failed : BackendState.Off;
        }

        /// <summary>Registers a Live game at tip-off. The callback gets true if the server will rate it.</summary>
        public static void StartMatch(string matchKey, string opponentId, int seat, Action<bool, string> done)
        {
            if (!Ready) { done?.Invoke(false, "not signed in to the Live server"); return; }
            Runner().StartCoroutine(Send("POST", "/v1/match/start", Backend.StartBody(matchKey, opponentId, seat, NowMs()), true,
                r => done?.Invoke(r.Ok, r.Ok ? null : r.Message("This game won't be rated."))));
        }

        /// <summary>Reports a finished Live game. The callback gets the match state ("open" until both reports are in), your change, and your numbers.</summary>
        public static void ReportResult(string matchKey, int scoreA, int scoreB, uint hash, Backend.Outcome outcome, Action<string, int, Backend.PlayerInfo> done)
        {
            if (!Ready) { done?.Invoke("offline", 0, null); return; }
            Runner().StartCoroutine(Send("POST", "/v1/match/result", Backend.ResultBody(matchKey, scoreA, scoreB, hash, outcome, NowMs()), true, r =>
            {
                if (!r.Ok) { done?.Invoke("error", 0, null); return; }
                var p = Backend.ReadPlayer(Backend.Obj(r.Json, "player"));
                if (p != null)
                {
                    Player = p;
                    Backend.Mirror(App.Career?.live, p);
                    App.SaveCareer();
                }
                done?.Invoke(Backend.Str(r.Json, "state") ?? "open", (int)Backend.Num(r.Json, "change"), p);
            }));
        }

        /// <summary>Erases your Live data on the server (rating, record, subscription link). Signs out afterwards.</summary>
        public static void DeleteMyData(Action<bool> done)
        {
            if (!Ready) { done?.Invoke(false); return; }
            Runner().StartCoroutine(Send("POST", "/v1/me/delete", "{}", true, r =>
            {
                if (r.Ok) { _token = null; State = BackendState.Failed; Error = "Your Live data was deleted. Come back to LIVE to start fresh."; }
                done?.Invoke(r.Ok);
            }));
        }

        /// <summary>Fetches your latest server numbers (after a game settles).</summary>
        public static void Refresh(Action<Backend.PlayerInfo> done)
        {
            if (!Ready) { done?.Invoke(null); return; }
            Runner().StartCoroutine(Send("GET", "/v1/me", null, true, r =>
            {
                var p = r.Ok ? Backend.ReadPlayer(Backend.Obj(r.Json, "player")) : null;
                if (p != null)
                {
                    Player = p;
                    SubscriptionActive = Backend.Bool(Backend.Obj(r.Json, "subscription"), "active");
                    Backend.Mirror(App.Career?.live, p);
                    App.SaveCareer();
                }
                done?.Invoke(p);
            }));
        }

        private struct Response
        {
            public long Status;
            public System.Collections.Generic.Dictionary<string, object> Json;
            public bool Ok => Status >= 200 && Status < 300 && Json != null;

            /// <summary>The server's short error message, or a fallback (never raw network details).</summary>
            public string Message(string fallback)
            {
                string e = Backend.Str(Json, "error");
                return string.IsNullOrEmpty(e) ? fallback : char.ToUpperInvariant(e[0]) + e.Substring(1) + ".";
            }
        }

        private static IEnumerator Send(string method, string path, string body, bool auth, Action<Response> done)
        {
            if (!BackendConfig.Enabled) { done(new Response { Status = 0 }); yield break; }
            string url = BackendConfig.Url.TrimEnd('/') + path;
            using (var req = new UnityWebRequest(url, method))
            {
                req.timeout = TimeoutSeconds;
                req.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (auth && !string.IsNullOrEmpty(_token)) req.SetRequestHeader("Authorization", "Bearer " + _token);
                req.redirectLimit = 0; // the server never redirects; refuse to follow one
                yield return req.SendWebRequest();
                var resp = new Response { Status = req.responseCode };
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(text) && text.Length < 64 * 1024) resp.Json = Backend.Parse(text);
                if (req.responseCode == 401 && auth) _token = null; // session expired: sign in again next time
                done(resp);
            }
        }

        private static double NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
