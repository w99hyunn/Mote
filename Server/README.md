# Mote 서버

.NET 10 SDK가 설치된 개발 PC에서 프로젝트 루트 기준으로 실행합니다.

```powershell
dotnet dev-certs https --trust
dotnet run --project Server/Mote.Server.csproj
```

Unity 에디터는 TitleLogin의 **Server Url**이 비어 있으면 `https://localhost:7248`에 접속합니다. 기기용 빌드는 빌드 전에 이 필드에 기기에서 접속할 수 있고 인증서를 신뢰하는 HTTPS 서버 주소를 지정합니다.

Google OIDC, GPGS, 게스트 및 세션 복원은 UGS 인증 후 동일하게 `POST /api/auth/login`을 호출합니다. 본문은 비워 두고 `Authorization: Bearer <UGS AccessToken>` 헤더를 보냅니다. 서버는 Unity 공개 키로 서명과 발급자를 확인하고 토큰 유효 기간, 프로젝트 ID와 audience를 검증합니다. 성공 응답은 `200 {"playerId":"..."}`, 인증 실패는 `401`입니다. 현재 서버는 별도 세션이나 DB를 생성하지 않습니다.
## PC Google 로그인

Google Cloud의 기존 웹 OAuth 클라이언트에 승인된 리디렉션 URI `https://localhost:7248/api/auth/google/callback`을 추가합니다. 개발 서버는 .NET 사용자 비밀 저장소의 `Google:ClientSecret`을 읽습니다. 배포 서버에서는 `Google__ClientSecret`과 `Google__RedirectUri`를 지정하고, 동일한 배포용 HTTPS 콜백 주소를 Google Cloud에 등록합니다. PC 빌드의 TitleLogin **Server Url**은 해당 서버 주소로 설정합니다.

Google 버튼을 누르면 기본 브라우저에서 계정을 선택합니다. 서버는 브라우저를 게임의 임시 루프백 포트로 돌려보내고, 게임은 PKCE로 발급 코드를 교환한 뒤 Android와 같은 UGS 공급자로 로그인합니다.
