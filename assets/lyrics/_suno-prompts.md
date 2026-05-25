# Suno Prompts — Pending Generation

Copy the **Style** field into Suno Custom Mode "Style of Music",
copy the **Lyrics** field into "Lyrics". Generate v1+v2 per track.
After generation, drop mp3 files into `assets/music/` with the
suggested filename.

---

## 1. 월요일이 또 와요 (Comedy Ballad, Korean)

**Filename target**: `vocal-comedy-monday-v1.mp3` / `-v2.mp3`
**Lyric file (already exists)**: `assets/lyrics/comedy-monday.txt`

### Style
```
korean ballad, slow 70bpm, piano-led, soft strings, melancholic male vocal, intimate sincere tone, comedy ballad with self-deprecation
```

### Lyrics
```
[Verse 1]
일요일 밤 열한 시 반
내일을 미루고 싶어
드라마는 끝났는데
왜 시간은 안 멈추니

[Chorus]
월요일이 또 와요
어쩌면 좋아요
주말은 너무 짧고
회의는 너무 길어요

[Verse 2]
출근길 지하철 안
모두가 같은 표정
누구도 웃지 않아요
오늘이 금요일이면 좋을 텐데

[Bridge]
한 주만 더 버티면
또 다른 한 주가 와요

[Final Chorus]
월요일이 또 와요
누구도 못 막아요
커피만이 친구예요
어떻게든 살아남아요
```

**Status**: ✅ generated on 2026-05-23, files in `assets/music/`

---

## 2. 이천오백원의 행복 (Comedy Lofi Ballad, Korean)

**Filename target**: `vocal-comedy-convenience-v1.mp3` / `-v2.mp3`
**Lyric file (already exists)**: `assets/lyrics/comedy-convenience.txt`

### Style
```
korean lofi ballad, minimal piano, late-night warmth, soft female vocal whisper, intimate slice-of-life, heize-jangkiha sincerity
```

### Lyrics
```
[Verse 1]
새벽 두 시 편의점
형광등 아래 나
삼각김밥 하나와
바나나우유 하나

[Chorus]
이천오백원의 행복
오늘 하루 마무리
화려하진 않지만
충분해 충분해요

[Verse 2]
계산대 앞에 서서
포인트 적립도 했어요
영수증 짧게 받고
밤거리로 나가요

[Bridge]
누군가는 오마카세
누군가는 호텔뷔페
나는 여기 이 편의점
이게 내 작은 천국

[Final Chorus]
이천오백원의 행복
내일도 또 올게요
사장님 안녕히 계세요
이 밤도 잘 보내세요
```

**Status**: ✅ generated on 2026-05-23, files in `assets/music/`
**Note**: whisper.cpp small model 가사 transcribe 실패 (vocal too breathy).
lyric-align 파이프라인 쓰려면 큰 whisper model 필요하거나, 수동 timestamp.

---

## 3. 그 작은 손 (Folk-Indie Ballad, Korean)

**Filename target**: `vocal-folk-small-hand-v1.mp3` / `-v2.mp3`
**Lyric file**: `assets/lyrics/folk-small-hand.txt`

### Style
```
korean folk-indie ballad, slow 72bpm, acoustic guitar fingerpicked, light harmonica accent, soft male vocal with breath, sincere narrative storytelling, jim-kwangsuk-style sparseness
```

### Lyrics
```
[Verse 1]
낯선 길 위에서
투박한 잔 하나에
따뜻한 차 한 모금
잠깐의 위로였어

[Verse 2]
말이 없는 작은 손
허공에 멈춘 손
어른들은 손사래 쳤고
내 걸음도 지나갔어

[Chorus]
주려는 내 마음이
너를 가두는 일이 될까
주지 않은 그 손이
너를 더 사랑한 걸까

[Verse 3]
함께 걷던 어른이 말했어
오래 본 사람이 말했지
가난이 너를 만든 게 아니라
우리가 너를 만든 거라고

[Bridge]
그 말이 무거웠고
나는 그제야 알았어
내가 건네던 손이
누굴 위한 손이었을까

[Final Chorus]
주려는 내 마음이
누구를 위한 거였을까
그 작은 손 위에
내 욕심이 있었던 걸까

[Outro]
잠깐의 위로가
내 안에 남았어
말이 없던 그 손도
내 안에 남았어
```

**Status**: ⏳ pending Suno generation

---

## 4. A Small Hand (Folk-Indie Ballad, English)

**Filename target**: `vocal-folk-small-hand-en-v1.mp3` / `-v2.mp3`
**Lyric file**: `assets/lyrics/folk-small-hand-en.txt`

### Style
```
english folk ballad, slow 70bpm, fingerpicked acoustic guitar, sparse arrangement, soft male vocal storytelling, sufjan-stevens damien-rice style intimacy
```

### Lyrics
```
[Verse 1]
On a road I didn't know
A simple cup rough in my hand
A warm tea was a comfort
For just a little while

[Verse 2]
A small hand reaching out
Silent in the open air
The grown-ups waved her away
And I walked past her too

[Chorus]
Would my open hand
Be the chain that holds you down
Was the hand that gave you nothing
The one that loved you more

[Verse 3]
Someone walking with me said
Someone who had seen this said
It isn't hunger that made you
It's the coins we drop that did

[Bridge]
Those words sat heavy in me
And only then I understood
The hand I thought was kindness
Whose was it really for

[Final Chorus]
Was my willing hand
Reaching out for me or you
Was there only my own want
Resting on your small hand

[Outro]
That small comfort stays
Somewhere inside of me
That silent reaching hand
Stays inside me too
```

**Status**: ⏳ pending Suno generation

---

## Workflow note

After Suno generation:
1. Download v1+v2 mp3 from Suno
2. Drop into `/Users/melons/ai/assets/music/` with the **Filename target** name above
3. Tell Claude "옮겼어" — Claude will verify + run music-video render
