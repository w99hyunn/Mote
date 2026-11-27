using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace DoHwa.Planet
{
    [Serializable] public struct PlanetItem
    {
        public string kind, giftFrom;
        public int yaw;
        public float x, y, z;
        public Vector3 Direction => new Vector3(x,y,z);
    }
    [Serializable] public class PlanetNote { public string author, text; }
    [Serializable] public class PlanetData
    {
        public string owner;
        public int revision;
        public PlanetItem[] items = Array.Empty<PlanetItem>();
        public PlanetNote[] notes = Array.Empty<PlanetNote>();
    }

    internal static class PlanetApi
    {
        internal static Uri Endpoint { get; set; }
        internal static string Player => UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsAuthorized ? AuthenticationService.Instance.PlayerId : null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Endpoint = null;
        [Serializable] private class Gift { public int revision, slot; }
        [Serializable] private class Layout { public int revision; public PlanetItem[] items; }
        [Serializable] private class Note { public string id, text; }

        private static async Task<string> Send(string path, string method, string body = null)
        {
            if (Player == null || Endpoint == null) throw new InvalidOperationException("타이틀에서 로그인한 뒤 이용할 수 있어요.");
            using var r = new UnityWebRequest(new Uri(Endpoint, "/api/planets/" + path), method);
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Authorization", "Bearer " + AuthenticationService.Instance.AccessToken);
            if (body != null)
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                r.SetRequestHeader("Content-Type", "application/json");
            }
            r.timeout = 12;
            r.redirectLimit = 0;
            try { await r.SendWebRequest(); }
            catch (Exception) when (r.result != UnityWebRequest.Result.Success) { }
            if (r.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(r.responseCode == 404 ? "행성을 찾을 수 없어요. 주소를 확인해 주세요." :
                    r.responseCode == 409 ? "다른 기기에서 행성이 바뀌었어요. 창을 닫고 다시 열어 주세요." :
                    "서버에 연결할 수 없어요. 잠시 후 다시 시도해 주세요.");
            }
            return r.downloadHandler.text;
        }
        internal static async Task<PlanetData> Read(string owner) =>
            JsonUtility.FromJson<PlanetData>(await Send(Uri.EscapeDataString(owner), "GET"));
        internal static Task Save(int revision, PlanetItem[] items) =>
            Send("me/layout", "PUT", JsonUtility.ToJson(new Layout { revision=revision, items=items }));
        internal static Task LeaveGift(string owner, int revision, int slot) =>
            Send(Uri.EscapeDataString(owner)+"/gifts", "POST", JsonUtility.ToJson(new Gift { revision=revision, slot=slot }));
        internal static Task Write(string owner, string id, string text) =>
            Send(Uri.EscapeDataString(owner)+"/notes","POST", JsonUtility.ToJson(new Note { id=id, text=text }));
    }
}