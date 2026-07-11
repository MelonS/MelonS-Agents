# 생성 모델 평가 — 2026-07 (스틸·영상, 로컬 16GB)

> 곧극장(오디세이 인물도감) 실전 제작 중 최신 모델 리서치 + 로컬 A/B. 하드웨어 = RTX 4070 Ti SUPER 16GB(게임/타 세션과 공유), 런타임 = ComfyUI, 라이선스 = **Apache/오픈웨이트 우선(상업 채널)**. 결론만 급하면 맨 아래 "채택 결정" 표.

## 1. 스틸 모델

### 후보 지형 (2025 말~2026 중)
| 모델 | 출시 | 크기 | 16GB | 라이선스 | 비고 |
|---|---|---|---|---|---|
| FLUX.1-schnell(현행 베이스) | 2024 | 12B distill | ✅ | Apache | 지시준수·네거티브 잘 먹음. 현행 대비 최저 화질군 |
| **Z-Image Turbo** ⭐ | 2025-11-27 | 6B | ✅(bf16 12.3GB) | **Apache** | **로컬 시네마틱 사실감 최상**(피부·재질) |
| Qwen-Image-2512 / Edit-2511 | 2025-12 | 20B | ⚠️(Nunchaku 4bit) | Apache | 프롬프트 정확도·레퍼런스 얼굴 고정 최강. 무거움→히어로컷 |
| FLUX.1 Krea / FLUX.2 Klein | 2025-2026 | 12/9B | ✅ | **비상업(모델)** | 영화룩 좋으나 라이선스 회색 → 저작권 방침상 패스 |
| FLUX.2 Dev | 2025-11 | 32B | ❌ | 비상업 | 16GB 부적합·느림·얼굴 약함 |

### 로컬 A/B: FLUX-schnell vs Z-Image Turbo (키르케 스틸, 2026-07-11)
- **판정: Z-Image 우세.** 인물 초상은 재질·피부·마법FX가 훨씬 풍부(프리미엄 키아트급), 오브젝트(잔)는 반투명 물약·연회 디테일이 더 사실적. 속도 8~20s/장(FLUX 7~9s와 동급, 첫 로드만 느림). 16GB 여유.
- **⚠️ 트레이드오프 — Z-Image는 프롬프트 제어가 약하다**:
  - **cfg=1(증류)이라 네거티브 무시**(공식 워크플로가 네거티브를 ConditioningZeroOut으로 0처리). `"no X"`가 안 먹힘.
  - **스타일 프라이어가 강함**: "witch/sorceress/enchantress/sorcery" 넣으면 판타지 마녀 편향으로 **뿔을 자동 부착**(키르케 레퍼런스에서 관측). 부정어로 못 지움.
  - **회피법**: 부정어 대신 **긍정 서술로만**. 유발어 제거하고 고전 서술("ancient Greek goddess, chiton gown")로 바꾸면 정상 출력.
- **결론**: 시네마틱 사실감이 필요한 대부분 컷 = **Z-Image 기본**. 정확한 지시/네거티브가 중요한 컷은 FLUX 폴백. 인물 정체성 고정 심화 필요시 Qwen-Edit 검토.

### Z-Image ComfyUI 셋업 (검증됨)
- 파일(Comfy-Org/z_image_turbo): `diffusion_models/z_image_turbo_bf16.safetensors`(12.3GB) + `text_encoders/qwen_3_4b_fp8_mixed.safetensors`(5.6GB) + `vae/z_image_ae.safetensors`(335MB).
  - **int8_convrot(6.2GB)는 현 ComfyUI가 `int8_tensorwise` 미지원으로 로드 실패 → bf16 사용.** nvfp4는 Blackwell(50xx) 전용.
- 그래프: `UNETLoader(default)` + `CLIPLoader(qwen_3_4b, type=**lumina2**)` + `ModelSamplingAuraFlow(shift 3)` + `KSampler(steps 8, cfg 1, res_multistep, simple)` + `EmptySD3LatentImage` + `ConditioningZeroOut`(네거) + VAE. 768×1344 OK.
- 정본 스크립트: **`scripts/zimage-still.py`**. `gen_ep_stills.py --model zimage`(곧극장 repo).

## 2. 영상/모션 모델 (I2V)

### 후보 지형
- **Wan2.2-A14B I2V(현행) = 로컬 I2V 화질·얼굴무변형 1위 유지.** 더 나은 건 전부 API·유료(Kling3.0/Veo3.1/Runway Gen-4.5/Wan2.5·2.6).
- **Wan2.2-Lightning(lightx2v) = 이미 로컬·기본 적용 중**(4스텝 증류). 우리 파이프라인은 이미 고속 상태.
- HunyuanVideo 1.5(2025-11): 물/연기/천 물리는 우수하나 **원본 충실도 약함(얼굴 드리프트)** + 텐센트 라이선스 → 얼굴 없는 모션컷만 시험.
- LTX-2(2026-01): 기술 최상이나 텍스트 인코더 24GB+ → **16GB 부적합, 보류**.
- ("Wan 2.7 오픈웨이트" 소문은 미검증 → 배제.)

### 로컬 A/B: 4스텝 vs 8스텝 vs 신형 r64 LoRA (EP06 파도컷, 2026-07-11)
| 조건 | 시간/컷(49f) | 모션 품질 |
|---|---|---|
| 4스텝 + 구 LoRA(`wan22_i2v_*_lightning`) | 223s | 양호(포말 이동 시 약간 뭉갬) |
| 8스텝 + 구 LoRA | 319s(+43%) | 포말/물결 디테일 소폭↑ |
| **4스텝 + 신형 `*_lightx2v_4step_260412_r64`** ⭐ | **177s(−21%)** | **동등~우세, 더 깨끗** |
- **결론**: **260412 r64 LoRA를 기본으로**(더 빠르고 품질 동등~우세, 무료). 8스텝은 복잡모션 히어로컷 선택지. → `wan-a14b-i2v.py` 기본 LoRA를 260412 r64로 정렬(pipeline 문서와 일치).

## 채택 결정 (2026-07-11)
| 레버 | 이전 | → 채택 | 효과 |
|---|---|---|---|
| 스틸 기본 | FLUX.1-schnell | **Z-Image Turbo(bf16)** | 시네마틱 사실감 실질 상승. FLUX는 지시준수 폴백 |
| 스틸 히어로/얼굴 | ref_still 복사 | (검토) Qwen-Image-Edit-2511 | 다중 레퍼런스 정체성 고정 |
| 모션 LoRA | `*_lightning`(구) | **`*_lightx2v_4step_260412_r64`** | −21% 시간 + 품질 동등~우세 |
| 모션 고품질 | — | 8스텝(4+4) 선택 | 복잡모션 히어로컷 한정(+43% 시간) |
| 유료 천장 | — | Kling/Veo/Wan2.5 API | 히어로컷 한정, 운영자 '고' |

> 원 리서치 2건(이미지·영상 지형) 출처는 각 후보 옆 각주 참조. 로컬 A/B 산출물 = 곧극장 `records/ep08-circe/_review/`(스틸 FLUX↔Z-Image, 모션 4/8/r64 필름스트립).
