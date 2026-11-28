# Mote 서버

.NET 10 SDK가 설치된 개발 PC에서 프로젝트 루트 기준으로 실행합니다.

```powershell
dotnet dev-certs https --trust
dotnet run --project Server/Mote.Server.csproj
```

Unity 클라이언트의 접속 주소는 `Assets/Resources/ServerSettings.asset`의 **Server Url**에서 설정합니다. 에디터에서 이 값이 비어 있으면 같은 에셋의 **Editor Fallback Url**을 사용합니다.

Google OIDC, GPGS, 게스트 및 세션 복원은 UGS 인증 후 동일하게 `POST /api/auth/login`을 호출합니다. 본문은 비워 두고 `Authorization: Bearer <UGS AccessToken>` 헤더를 보냅니다. 서버는 Unity 공개 키로 서명과 발급자를 확인하고 토큰 유효 기간, 프로젝트 ID와 audience를 검증합니다. 성공 응답은 `200 {"playerId":"..."}`, 인증 실패는 `401`입니다.

행성 배치와 방명록은 Neon PostgreSQL의 `mote` 스키마에 저장합니다. 로컬 개발에서는 .NET 사용자 비밀의 `ConnectionStrings:Planets`, 배포 환경에서는 `ConnectionStrings__Planets`에 Npgsql 연결 문자열을 설정합니다. Neon의 풀링 호스트와 `SSL Mode=VerifyFull;Channel Binding=Require`를 사용합니다. 첫 실행 시 서버가 테이블을 생성합니다.

## PC Google 로그인

Google Cloud의 기존 웹 OAuth 클라이언트에 개발용 리디렉션 URI `https://localhost:7248/api/auth/google/callback`과 운영용 리디렉션 URI `https://server-mote.openfr.kr/api/auth/google/callback`을 등록합니다. 개발 서버는 .NET 사용자 비밀 저장소의 `Google:ClientSecret`을 읽습니다.

Google 버튼을 누르면 기본 브라우저에서 계정을 선택합니다. 서버는 브라우저를 게임의 임시 루프백 포트로 돌려보내고, 게임은 PKCE로 발급 코드를 교환한 뒤 Android와 같은 UGS 공급자로 로그인합니다.

## 운영 서버

`Production` 환경에서는 `appsettings.Production.json`이 수신 주소와 Google 콜백을 지정합니다. Caddy가 `https://server-mote.openfr.kr`에서 HTTPS를 처리하고 Kestrel의 `http://127.0.0.1:5248`로 요청을 전달합니다. Unity 설정 에셋의 **Server Url**은 `https://server-mote.openfr.kr`입니다.

운영 서비스의 환경에 `ConnectionStrings__Planets`(Neon 연결 문자열)와 `Google__ClientSecret`(기존 Google 웹 OAuth 클라이언트의 비밀값)을 설정합니다. 게시 디렉터리를 작업 디렉터리로 사용해 `dotnet Mote.Server.dll`을 실행합니다. 배포 후 공개 주소의 `POST /api/auth/login`에 토큰 없이 요청했을 때 `401`이면 Caddy를 통해 인증 API에 도달한 것입니다.

## Windows 배포 앱

바탕화면의 **OpenFrame Deploy** 바로가기로 실행합니다. 앱 소스는 바탕화면의 `Tools/ServerDeploy`에 있으며, 해당 폴더에서 `.\package.ps1`을 실행하면 `dist/OpenFrame Deploy`에 다시 빌드됩니다.

1. **Mote → 연결 확인**으로 SSH 접속을 확인합니다. **자동 시작 설정**은 Ubuntu 계정당 최초 한 번 실행합니다. 별도 SSH 창에서 sudo 인증을 마치면 로그아웃 후에도 서비스가 유지됩니다.
2. **환경 변수**에 운영용 `ConnectionStrings__Planets`와 `Google__ClientSecret`을 각각 한 줄씩 `이름=값`으로 입력하고 저장합니다. 값은 SSH로 전송되어 Ubuntu의 `~/.config/openframe-deploy/mote.env`에 보관됩니다.
3. **빌드 및 배포**를 누르면 Linux 빌드, 업로드, 서비스 재시작, 인증 API의 401 응답 확인 순으로 진행합니다. 재시작하는 동안 잠시 접속이 끊길 수 있습니다. 새 버전이 정상 응답을 주지 않으면 기존 릴리스가 있는 경우 직전 버전으로 복귀합니다. 데이터베이스 변경은 이 복귀 범위에 포함되지 않습니다.

**서버 추가**로 SSH 대상과 서비스 이름을 지정할 수 있습니다. .NET은 csproj를 선택하고, 다른 런타임은 실행 파일 또는 실행 스크립트가 들어 있는 Linux 게시 폴더를 선택합니다. 스크립트에 필요한 런타임은 대상 Ubuntu에 설치되어 있어야 합니다. 상태 확인 요청은 서버 내부 127.0.0.1로 보냅니다. 기존 서비스가 포트를 사용 중이면 첫 배포는 중단되므로, 기존 서비스를 이전할 때에는 최초 전환 시간을 지정하세요.
