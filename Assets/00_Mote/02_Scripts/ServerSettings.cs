using System;
using UnityEngine;

namespace DoHwa
{
    [CreateAssetMenu(menuName = "Mote/Server Settings")]
    public sealed class ServerSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Mote 서버의 HTTPS 주소")] private string serverUrl;
        [SerializeField, Tooltip("에디터에서 서버 주소가 비어 있을 때 사용할 HTTPS 주소")] private string editorFallbackUrl;
        [SerializeField, Tooltip("UGS에 등록한 Google OpenID Connect 공급자 이름")] private string googleProvider;

        public string GoogleProvider => googleProvider;

        public Uri Endpoint
        {
            get
            {
                string url = serverUrl;
#if UNITY_EDITOR
                if (string.IsNullOrWhiteSpace(url)) url = editorFallbackUrl;
#endif
                if (!Uri.TryCreate(url, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException("Server Settings에 올바른 HTTPS 서버 주소를 입력하세요.");
                return endpoint;
            }
        }
    }
}
