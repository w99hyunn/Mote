#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DoHwa.UI
{
    internal static class DesktopGoogleLogin
    {
        [Serializable] private sealed class Exchange { public string code; public string verifier; }
        [Serializable] private sealed class Token { public string idToken; }

        internal static async Task<string> GetToken(Uri server, CancellationToken cancelled)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancelled);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            string page = Resources.Load<TextAsset>("MoteLoginReturn").text;
            string verifier = RandomValue();
            string state = RandomValue();
            using var sha = SHA256.Create();
            string challenge = Encode(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            using var listener = OpenListener(out var port);
            using var stop = timeout.Token.Register(listener.Close);
            var start = new Uri(server, $"/api/auth/google/start?port={port}&challenge={challenge}&state={state}");
            using (var check = UnityWebRequest.Head(start.AbsoluteUri))
            {
                check.timeout = 10;
                check.redirectLimit = 0;
                await check.SendWebRequest();
            }
            cancelled.ThrowIfCancellationRequested();
            Application.OpenURL(start.AbsoluteUri);
            string code;
            while (true)
            {
                HttpListenerContext callback;
                try { callback = await listener.GetContextAsync().ConfigureAwait(false); }
                catch (Exception) when (timeout.IsCancellationRequested)
                {
                    cancelled.ThrowIfCancellationRequested();
                    throw new TimeoutException("Google sign-in expired.");
                }
                var query = callback.Request.QueryString;
                if (callback.Request.HttpMethod != "GET" || callback.Request.Url.AbsolutePath != "/mote/" || query["state"] != state)
                {
                    callback.Response.StatusCode = 400;
                    callback.Response.Close();
                    continue;
                }
                code = query["code"];
                var response = callback.Response;
                response.ContentType = "text/html; charset=utf-8";
                response.Headers["Cache-Control"] = "no-store";
                response.Headers["Referrer-Policy"] = "no-referrer";
                string title = "Mote로 돌아가 주세요";
                string hint = "이 창은 닫아도 괜찮아요.";
                if (query["error"] == "cancelled" || string.IsNullOrEmpty(code))
                {
                    title = query["error"] == "cancelled" ? "로그인을 취소했어요" : "로그인을 이어갈 수 없어요";
                    hint = "Mote로 돌아가 다시 시도해 주세요.";
                }
                byte[] message = Encoding.UTF8.GetBytes(page.Replace("{{title}}", title).Replace("{{message}}", hint));
                response.Close(message, false);
                if (query["error"] == "cancelled") throw new OperationCanceledException();
                if (string.IsNullOrEmpty(code)) throw new InvalidOperationException("Google sign-in failed.");
                break;
            }
            listener.Close();
            cancelled.ThrowIfCancellationRequested();
            await Awaitable.MainThreadAsync();
            cancelled.ThrowIfCancellationRequested();
            using var request = UnityWebRequest.Post(new Uri(server, "/api/auth/google/exchange").AbsoluteUri,
                JsonUtility.ToJson(new Exchange { code = code, verifier = verifier }), "application/json");
            request.redirectLimit = 0;
            request.timeout = 20;
            await request.SendWebRequest();
            cancelled.ThrowIfCancellationRequested();
            var token = JsonUtility.FromJson<Token>(request.downloadHandler.text);
            if (string.IsNullOrEmpty(token?.idToken)) throw new InvalidOperationException("Missing Google ID token.");
            return token.idToken;
        }

        private static HttpListener OpenListener(out int port)
        {
            // Ask the OS for a free loopback port, then bind the HTTP listener to that port.
            for (int attempt = 0; ; attempt++)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                try { probe.Start(); port = ((IPEndPoint)probe.LocalEndpoint).Port; }
                finally { probe.Stop(); }
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/mote/");
                try { listener.Start(); return listener; }
                catch (HttpListenerException) when (attempt < 2) { listener.Close(); }
            }
        }

        private static string RandomValue()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Encode(bytes);
        }

        private static string Encode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
#endif
