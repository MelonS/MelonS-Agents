# `job-hunt` 스킬 — 워크스루 (한국어 미러)

[`job-hunt.md`](job-hunt.md)의 한국어 미러.
[`skills/job-hunt/SKILL.md`](../../skills/job-hunt/SKILL.md)이 agentskills.io-spec
계약 (스킬의 정체 + 호출 방법)이라면, 이 문서는 컨트리뷰터·운영자 가이드
(소스 추가법, 라이브 모드 전환, 디버그, 반복 실행 일정 등록).

---

## 디렉토리 구조

```
skills/job-hunt/
├── SKILL.md                       # agentskills.io frontmatter + 계약
├── scripts/
│   ├── run.sh                     # 오케스트레이터: 필터 → 소스 → 필터 → dedupe → diff → render
│   ├── digest.sh                  # 마크다운 digest 렌더러
│   └── apply-assist.sh            # 소스별 apply-URL rewrite 헬퍼
├── config/
│   └── filters.example.yaml       # 문서화된 시작 필터; 운영자는 filters.yaml로 복사 + 편집
├── sources/
│   ├── README.md                  # 플러그인 계약 (새 소스 추가 시 가장 먼저 읽을 것)
│   ├── source-plugin.schema.json  # JSON Schema (Draft 2020-12) — 플러그인 출력 형식 강제
│   ├── _mock.sh                   # 테스트용 결정적 fixture
│   │
│   │   # Live-ready (API 키 불필요, 운영자 검증 없이 즉시 작동):
│   ├── global-ats.sh              # Greenhouse + Ashby + Lever 공식 보드 (~27 AI/SaaS 회사); JH_GLOBAL_ATS_LIVE=1
│   ├── global-hn-whoshiring.sh    # HN 월간 "Who is hiring?" 쓰레드 (Algolia HN Search 경유); JH_GLOBAL_HN_LIVE=1
│   ├── global-remoteok.sh         # remoteok.com/api; JH_GLOBAL_REMOTEOK_LIVE=1
│   ├── global-remotive.sh         # remotive.com/api/remote-jobs; JH_GLOBAL_REMOTIVE_LIVE=1
│   ├── kr-worknet.sh              # 정부 공공고용서비스 워크넷; JH_WORKNET_LIVE=1
│   │
│   │   # Live-ready (API 키 + 운영자 검증 필요):
│   ├── kr-wanted.sh               # 원티드 — JH_WANTED_LIVE=1 + WANTED_API_KEY
│   ├── kr-saramin.sh              # 사람인 — JH_SARAMIN_LIVE=1 + SARAMIN_KEY
│   │
│   │   # KR mini-board live-ready (키 불필요):
│   ├── kr-rallit.sh               # 랄릿 KR IT 전문; JH_RALLIT_LIVE=1
│   ├── kr-theteams.sh             # 강소기업 보드; JH_THETEAMS_LIVE=1
│   │
│   │   # 영구 mock (robots.txt 금지 / 서비스 종료):
│   ├── kr-jobkorea.sh             # 잡코리아 — robots /Search/?stext= 금지 + 2017 잡코리아 vs 사람인 판례
│   └── kr-programmers.sh          # 프로그래머스 — 2025-05-19 서비스 종료
└── tests/
    ├── smoke.sh                   # 구조 + 해피 패스 e2e (32+ 체크)
    ├── edge-cases.sh              # 실패 모드 + 엣지 입력 (20+ 체크)
    ├── schema-validation.sh       # 각 플러그인 출력의 JSON Schema 검증
    └── run-all.sh                 # 세 suite 일괄 실행
```

**플러그인 카운트**: 총 12개 (소스 11개 + `_mock` fallback).
2026-05-21 종합 라이브 테스트: 5,474개 raw → 24-동의어 "Problem
Solver" 패밀리 필터 통과 200건.  legal posture + 기술 검증은
[`docs/research/job-sources-survey-2026-05-21.md`](../research/job-sources-survey-2026-05-21.md)
참조.

스킬은 **standalone 모양** (`docs/architecture.md`의 "Skills layer — two
shapes" 참고): `agents/missions/job-hunt/` 대응본 없음, 5-에이전트 오케스트레이터
라우팅 없음, 스킬 자체의 `scripts/run.sh`가 canonical 구현입니다.

---

## 첫 사용 셋업

### v2 기본 UX — 짧은 키워드 (`--seed`)

```bash
# 최소 명령 — 키워드 1개를 role family로 자동 확장.
skills/job-hunt/scripts/run.sh --seed "Problem Solver" --dry-run
```

한 줄로:
1. `Problem Solver`를 `role-synonyms.yaml`에서 매칭 →
   `problem-solver` family 찾음.
2. ~24개 include 키워드로 확장 (FDE, Applied AI Engineer,
   Generalist, AI Product Manager, Founding Engineer 등).
3. 기본 소스 (kr-* 전부 mock-fallback) 에서 fetch.
4. 필터 + dedupe + render.
5. tmp 경로에 digest 작성 (`--dry-run` 떼면
   `./records/jobs/<YYYY-MM-DD>/digest.md`).

같은 family 내 다른 seed로 시도:
```bash
skills/job-hunt/scripts/run.sh --seed "FDE"
skills/job-hunt/scripts/run.sh --seed "Forward Deployed"
skills/job-hunt/scripts/run.sh --seed "Generalist"
# 세 개 모두 problem-solver family로 라우팅 → 동일 결과.
```

알 수 없는 seed는 exit 2 + 안내 메시지.
`config/role-synonyms.yaml`에 family 또는 synonym 추가해서 확장.

### Advanced UX — 수동 편집 필터

```bash
# 1. 예제 필터를 복사해서 직군/지역/keywords/sources를 편집.
cp skills/job-hunt/config/filters.example.yaml \
   skills/job-hunt/config/filters.yaml

# 2. --seed 없이 실행; filters.yaml이 include/exclude 직접 제공.
skills/job-hunt/scripts/run.sh
```

`config/filters.yaml`은 기본 gitignored (운영자별 컨텍스트 — 프라이버시
섹션 참고).  v2와 advanced 공존 가능; `--seed` 주면 그 run의 include
키워드는 그것이 덮어씌움.

v2 기본 출력 (`--seed "Problem Solver"`) 기대값: `_mock` fixture의
Problem Solver family 항목 3개. 커밋된 샘플:
[`docs/samples/job-hunt-digest-mock.md`](../samples/job-hunt-digest-mock.md).

---

## 플러그인을 mock에서 라이브로 전환

각 `kr-*` 플러그인은 **라이브 HTTP 경로가 작성되어 있지만 주석 처리**되어 있고
**env-var 플래그로 게이팅**되어 있습니다.  이유: 라이브 엔드포인트 shape이
사전 통보 없이 바뀜.  라이브 요청 전에 운영자 검증 단계가 반드시 필요.

`kr-wanted`를 예로 한 전환 절차:

```bash
# (a) 운영자 검증 단계.  curl 한 번 직접 날려서 응답 shape이
#     skills/job-hunt/sources/kr-wanted.sh 주석의 가정 schema와 일치하는지 확인.
curl -sS \
  -H "wanted-client-id: $WANTED_API_KEY" \
  'https://api.wanted.co.kr/v4/jobs?country=kr&limit=3' | jq '.data[0]'

# (b) 필드명이 가정 shape과 일치하면 sources/kr-wanted.sh의
#     "Placeholder live call" 헤더 아래 주석 처리된 `raw=$(curl ...)` +
#     `echo "$raw" | jq ...` 블록을 주석 해제.  아래의
#     `echo "[kr-wanted] live path not yet operator-validated"; return 1`
#     early-return을 삭제.
#
#     필드명이 안 맞으면 jq 변환 블록을 실제 응답에 맞춰 조정한 뒤 해제.

# (c) 라이브 플래그 + 키 세팅으로 실행:
JH_WANTED_LIVE=1 WANTED_API_KEY=<token> \
  skills/job-hunt/scripts/run.sh --sources=kr-wanted

# (d) digest 확인.  결과 OK면 env 변수를 .env (gitignored)에 영구화하고,
#     안정화되면 플러그인 변경 사항 커밋.
```

소스별 주의 사항:

- **`kr-wanted`**: 파트너 API 키 필요 (원티드가 승인 통합자에게 발급).
  anti-bot 없음 — 깨끗한 JSON API.
- **`kr-programmers`**: 공개 REST listing; 인증 불필요.  4개 소스 중 엔드포인트
  shape이 가장 자주 변함 — curl 검증 단계 주기적 반복 필요.
- **`kr-jobkorea`**: HTML scrape — pup 또는 python+bs4 파서 필요.
  anti-bot MEDIUM (UA + request-rate).  요청 간 500ms 미만 금지, 이 소스에
  대해 병렬 fetch 금지.
- **`kr-saramin`**: OpenAPI 파트너 키 필요 (https://oapi.saramin.co.kr/guide
  에서 등록).  Saramin 문서 기준 rate-limit: 1000 calls/day.

---

## 새 지역 추가하기

`skills/job-hunt/sources/<locale>-<board>.sh`에 플러그인을 떨어뜨리고
[`sources/README.md`](../../skills/job-hunt/sources/README.md)의 계약대로
`fetch_postings()`를 구현합니다.

미국 소스 가상 스켈레톤 예시:

```bash
#!/usr/bin/env bash
# sources/us-linkedin.sh — LinkedIn Jobs (US).

fetch_postings() {
  if [[ "${JH_LINKEDIN_LIVE:-0}" == "1" ]]; then
    # 라이브 경로.  필수 JSON 출력 shape은 SKILL.md "Adding a locale" 참고.
    echo "[us-linkedin] not yet validated" >&2
    return 1
  fi

  local fetched_at="${JH_MOCK_FETCH_AT:-$(date -Iseconds 2>/dev/null || date +%Y-%m-%dT%H:%M:%S%z)}"
  cat <<EOF
{
  "source": "us-linkedin",
  "fetched_at": "${fetched_at}",
  "postings": [
    { "title": "...", "company": "...", "region": "...",
      "posted_at": "...", "url": "...", "summary": "...", "apply_url": "..." }
  ]
}
EOF
}
```

그런 다음 `config/filters.yaml`을 갱신:

```yaml
locale: kr      # 현재 오케스트레이터는 `kr`만 검증; `us` 값은
                # run.sh의 `[[ "$locale" == "kr" ]]` 가드 변경 필요
                # (allowlist 확장).
sources:
  - us-linkedin
```

주의: 오케스트레이터의 locale 검증은 현재 단일 값 allowlist
(`[[ "$locale" == "kr" ]] || die`).  다른 locale 지원은
`scripts/run.sh`의 작은 변경이 필요합니다 — 설정된 리스트를 받도록.
KR 외 수요가 생기지 않아 가장 나중으로 미뤄둠.

---

## 필터 시맨틱 — 세부

```yaml
job_categories:
  - 백엔드 개발자       # OR 시맨틱: 포스팅 카테고리 중 하나 이상 매치
keywords:
  include: [Python, AI] # OR 시맨틱: 제목 또는 요약에 하나 이상 등장
  exclude: [SI, 파견]   # AND-of-NOT: 하나라도 등장하면 제외
```

소스 플러그인은 필터 컨텍스트를 다음 env 변수로 받습니다:

- `JH_REGIONS` — 줄바꿈 구분
- `JH_CATEGORIES` — 줄바꿈 구분
- `JH_KEYWORDS_INCLUDE` — 줄바꿈 구분
- `JH_KEYWORDS_EXCLUDE` — 줄바꿈 구분

mock 소스는 무시 (데이터 고정).  라이브 소스는 위 변수들로 upstream 쿼리를
scope해서 네트워크 burst를 운영자의 실제 범위에 비례하도록 유지.

Fetch 후 오케스트레이터가 각 포스팅의 `title + summary`에 대해 include/exclude
키워드 검사를 다시 적용 — 소스가 무엇을 반환했든 이게 최종 필터.

---

## Dedupe + diff 시맨틱

**Dedupe**: 동일 `url` 값은 collapse; 첫 등장이 이김.  소스는 filters.yaml
순서로 처리됨 — 선호 소스를 앞에 두세요.  mock fixture는 의도적으로 중복
URL 포스팅 하나를 포함해서 이 경로를 exercise.

**Diff**: 오케스트레이터가 가장 최근의 `<records_root>/<other-date>/index.json`
(오늘 제외)을 찾아서 오늘은 있지만 어제는 없는 URL을 "new since"로 표기.  이
URL들은 렌더된 digest 앞쪽에 별도 섹션 + `index.json`의 `new_urls`로 떨어짐.

prior index가 없으면 `postings_new`는 `0`이고 "new since last digest" 섹션은
마크다운에서 생략됩니다.

---

## 반복 실행 일정 등록 (launchd)

[`docs/architecture.md`](../architecture.md) + `operator-contract.md` §4 기준,
프로젝트의 스케줄러는 `scripts/com.melons.agents.*.plist` (per-machine 렌더)
launchd plist입니다.  매일 job-hunt digest 일정 추가:

```bash
cat <<'PLIST' > scripts/com.melons.agents.job-hunt.plist.template
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>com.melons.agents.job-hunt</string>
  <key>WorkingDirectory</key><string>@@REPO_ROOT@@</string>
  <key>ProgramArguments</key>
  <array>
    <string>/bin/bash</string>
    <string>-lc</string>
    <string>skills/job-hunt/scripts/run.sh --quiet</string>
  </array>
  <key>StartCalendarInterval</key>
  <dict>
    <key>Hour</key><integer>9</integer>
    <key>Minute</key><integer>0</integer>
  </dict>
  <key>StandardOutPath</key><string>@@REPO_ROOT@@/records/jobs/launchd.out.log</string>
  <key>StandardErrorPath</key><string>@@REPO_ROOT@@/records/jobs/launchd.err.log</string>
</dict>
</plist>
PLIST
```

`@@REPO_ROOT@@`는 프로젝트 표준 템플릿 플레이스홀더 — `install-claude-local.sh`
가 머신별로 렌더 (operator-contract §8 참고).  `install-scheduler.sh`에 연결
하면 머신간 이식 가능.

(이 브랜치에서는 아직 와이어링 안 됨 — 운영자가 일간 스케줄링 원할 때 결정;
수동 `/job-hunt` 호출은 오늘 동작함.)

---

## 디버그

### "all enabled sources failed"

Exit 3.  흔한 원인:

- 플러그인의 `fetch_postings()`가 malformed JSON 반환.
- 운영자 검증 없이 라이브 모드가 켜져서 플러그인의 문서화된 early-return이 firing.
- `sources/` 파일명이 `filters.yaml`의 `sources:` 항목과 다름.

빠른 진단: `bash skills/job-hunt/sources/<name>.sh; echo $?`로 소싱 시 에러
없어야 함.  그 다음 interactive shell에서:

```bash
. skills/job-hunt/sources/_mock.sh
fetch_postings | jq .
```

synthetic fixture가 출력되어야 함.  `_mock` 대신 실패 중인 플러그인 이름으로 교체.

### "no YAML parser available"

Exit 2.  `yq` 설치 (macOS: `brew install yq`), 또는 ruby가 PATH에 있는지
(macOS 기본 동봉), 또는 프로젝트 venv에 `pip install pyyaml`.

### Digest 생성됐는데 비어있음

필터가 너무 빡빡.  `index.json` 확인 — raw post-source / pre-filter 포스팅이
`.postings`에, 필터 컨텍스트가 `.filter_summary`에 담겨 있음.  `include`
키워드 완화 또는 `exclude` 키워드 일부 제거.

### 라이브 모드에서 "field not found"

upstream API surface가 바뀐 것.  해당 소스의 운영자 검증 curl 단계를 재실행
하고 플러그인의 jq 변환 블록을 현재 필드명에 맞춰 업데이트.

---

## 프라이버시 / 데이터 처리

- `config/filters.yaml`은 **기본 gitignored** (레포의 `.gitignore` 참고).
  구체 직군 / 지역 / 제외 리스트는 운영자의 개인 구직 타겟을
  드러내므로 committed 파일에 두지 않음 (`[[repo-as-credibility-signal]]`
  메모리 규칙).  일반화된 비개인 필터 (예: 도메인 starter-template
  공유용)는 `git add -f`로 명시적으로 추가 가능.
- `config/filters.example.yaml`은 in-repo로 커밋되는 일반 시작점 —
  "백엔드 개발자" / "AI 엔지니어" 같은 의도적으로 광범위한 카테고리만
  포함, 운영자 특화 정보 없음.
- `records/jobs/<date>/` 아래 출력 digest는 gitignored (레포의
  `records/` 규약).  모든 raw fetch JSON + 렌더된 마크다운은 로컬에 남음.
- 스킬에 소스 자격증명 저장 안 함.  각 플러그인은 운영자가 `.env`
  (gitignored) 또는 shell export로 세팅한 환경에서 키를 읽음.

---

## 관련 참고 자료

- [`skills/job-hunt/SKILL.md`](../../skills/job-hunt/SKILL.md) — agentskills.io-spec
  계약 표면.
- [`skills/job-hunt/sources/README.md`](../../skills/job-hunt/sources/README.md)
  — 플러그인 작성 계약.
- [`docs/architecture.md`](architecture.md) §"Skills layer — two shapes" —
  이 스킬이 missions-routed가 아닌 standalone인 이유.
- [`docs/operator-contract.md`](operator-contract.md) §8 portability 원칙 —
  부트스트랩 스크립트만큼 소스 플러그인에도 적용.
- [`docs/samples/job-hunt-digest-mock.md`](../samples/job-hunt-digest-mock.md)
  — 실제 digest 모양의 커밋된 참고.
