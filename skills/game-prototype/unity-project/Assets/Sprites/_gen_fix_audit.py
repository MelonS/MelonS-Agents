# -*- coding: utf-8 -*-
"""#194 - 운영자 fb "kenney 매핑이 의미와 안 맞음, 다 검증해 수정해".

sprite audit 결과 부적합 11종 PIL Kenney-style 로 재생성:
  wall_wood, floor_wood, stove, deer, wolf, crop_rice,
  stone_vein, stone_chunk, wood_pile, meat_pile, research_bench

Kenney style 가이드:
- flat colors + 1px dark outline
- limited palette (3-6 색)
- 16x16 또는 16x32 (entity 별 footprint 정합)
- warm earth tones (Tiny Town 정합)
"""
from PIL import Image
from pathlib import Path

HERE = Path(__file__).resolve().parent

# Kenney warm palette
OUTLINE   = (40, 26, 18, 255)        # dark brown outline
WOOD_DK   = (90, 58, 36, 255)        # dark wood
WOOD_MD   = (140, 88, 52, 255)       # mid wood
WOOD_LT   = (200, 142, 88, 255)      # light wood / plank
STONE_DK  = (70, 70, 80, 255)        # dark stone
STONE_MD  = (120, 120, 130, 255)
STONE_LT  = (180, 180, 190, 255)
GRASS_DK  = (50, 100, 50, 255)
LEAF      = (100, 160, 80, 255)
WHEAT     = (235, 200, 90, 255)      # 익은 작물 황금
WHEAT_DK  = (180, 140, 50, 255)
FIRE      = (240, 100, 30, 255)
FIRE_LT   = (255, 200, 80, 255)
RED       = (180, 60, 60, 255)
RED_DK    = (140, 40, 40, 255)
GREY_LT   = (180, 180, 175, 255)
DEER      = (160, 110, 75, 255)      # 사슴 갈색
DEER_DK   = (110, 75, 50, 255)
DEER_BLY  = (220, 195, 165, 255)     # 배 흰색
ANTLER    = (90, 60, 30, 255)
WOLF      = (110, 110, 120, 255)
WOLF_DK   = (70, 70, 80, 255)
WOLF_BLY  = (170, 170, 175, 255)
MEAT      = (175, 75, 60, 255)
MEAT_DK   = (130, 50, 40, 255)
MEAT_FAT  = (240, 220, 200, 255)
PAPER     = (240, 230, 200, 255)
INK       = (40, 40, 90, 255)


def new_img(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def put(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def hline(im, x0, x1, y, c):
    for x in range(x0, x1 + 1): put(im, x, y, c)


def vline(im, x, y0, y1, c):
    for y in range(y0, y1 + 1): put(im, x, y, c)


def rect_fill(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(im, x, y, c)


def rect_outline(im, x0, y0, x1, y1, c):
    hline(im, x0, x1, y0, c); hline(im, x0, x1, y1, c)
    vline(im, x0, y0, y1, c); vline(im, x1, y0, y1, c)


# ─── wall_wood 16x16 - wood plank wall (가로 plank 3줄) ───────────
def gen_wall_wood():
    im = new_img(16, 16)
    rect_fill(im, 0, 0, 15, 15, WOOD_MD)
    # 3 plank divider lines (가로)
    hline(im, 0, 15, 0, OUTLINE)
    hline(im, 0, 15, 5, WOOD_DK)
    hline(im, 0, 15, 10, WOOD_DK)
    hline(im, 0, 15, 15, OUTLINE)
    # 세로 outline
    vline(im, 0, 0, 15, OUTLINE)
    vline(im, 15, 0, 15, OUTLINE)
    # plank grain (소소한 dot)
    put(im, 4, 2, WOOD_DK); put(im, 11, 3, WOOD_DK)
    put(im, 3, 7, WOOD_DK); put(im, 9, 8, WOOD_DK)
    put(im, 5, 12, WOOD_DK); put(im, 12, 13, WOOD_DK)
    # 위 highlight
    hline(im, 1, 14, 1, WOOD_LT)
    hline(im, 1, 14, 6, WOOD_LT)
    hline(im, 1, 14, 11, WOOD_LT)
    return im


# ─── floor_wood 16x16 - wood plank floor (세로 plank) ────────────
def gen_floor_wood():
    im = new_img(16, 16)
    rect_fill(im, 0, 0, 15, 15, WOOD_LT)
    # 4 plank dividers (세로)
    for x in [3, 7, 11]:
        vline(im, x, 0, 15, WOOD_DK)
    # plank grain
    put(im, 1, 4, WOOD_MD); put(im, 5, 9, WOOD_MD)
    put(im, 9, 3, WOOD_MD); put(im, 13, 11, WOOD_MD)
    # 가장자리 outline (subtle)
    hline(im, 0, 15, 0, WOOD_MD)
    hline(im, 0, 15, 15, WOOD_MD)
    return im


# ─── stove 16x16 - 화덕 (검은 박스 + 불) ─────────────────────────
def gen_stove():
    im = new_img(16, 16)
    # frame
    rect_outline(im, 1, 2, 14, 14, OUTLINE)
    rect_fill(im, 2, 3, 13, 13, STONE_DK)
    # 상단 chimney/슬릿
    hline(im, 3, 12, 3, STONE_MD)
    # 가운데 불 입구
    rect_fill(im, 4, 7, 11, 12, OUTLINE)  # 불 입구 내부
    # 불 (안쪽 빨강/노랑)
    rect_fill(im, 5, 9, 10, 12, FIRE)
    rect_fill(im, 6, 10, 9, 11, FIRE_LT)
    put(im, 7, 8, FIRE)
    put(im, 8, 8, FIRE_LT)
    # 다리
    put(im, 2, 14, OUTLINE); put(im, 13, 14, OUTLINE)
    return im


# ─── deer 24x24 - 사슴 with antlers ──────────────────────────────
def gen_deer():
    im = new_img(24, 24)
    # body (oval)
    rect_fill(im, 4, 11, 18, 17, DEER)
    # body shadow underside
    hline(im, 5, 17, 17, DEER_DK)
    # belly highlight
    hline(im, 5, 16, 16, DEER_BLY)
    # head (front-left)
    rect_fill(im, 2, 7, 7, 12, DEER)
    put(im, 1, 9, DEER); put(im, 1, 10, DEER)
    # snout
    put(im, 1, 11, OUTLINE); put(im, 0, 11, OUTLINE)
    # eye
    put(im, 4, 9, OUTLINE)
    # ears
    put(im, 3, 6, DEER); put(im, 5, 6, DEER)
    put(im, 3, 5, DEER_DK); put(im, 5, 5, DEER_DK)
    # antlers (좌우 가지)
    put(im, 2, 4, ANTLER); put(im, 6, 4, ANTLER)
    put(im, 1, 3, ANTLER); put(im, 7, 3, ANTLER)
    put(im, 0, 2, ANTLER); put(im, 2, 2, ANTLER)
    put(im, 6, 2, ANTLER); put(im, 8, 2, ANTLER)
    put(im, 1, 1, ANTLER); put(im, 7, 1, ANTLER)
    # legs (4 다리)
    for lx in [5, 9, 13, 17]:
        vline(im, lx, 18, 22, DEER_DK)
        put(im, lx, 22, OUTLINE)
    # tail
    put(im, 19, 12, DEER); put(im, 20, 13, DEER_BLY)
    # outline body
    rect_outline(im, 4, 11, 18, 17, OUTLINE)
    rect_outline(im, 2, 7, 7, 12, OUTLINE)
    return im


# ─── wolf 24x24 - 늑대 (회색 + 이빨 + 노란 눈) ───────────────────
def gen_wolf():
    im = new_img(24, 24)
    # body
    rect_fill(im, 4, 11, 18, 17, WOLF)
    hline(im, 5, 17, 17, WOLF_DK)
    hline(im, 5, 16, 16, WOLF_BLY)
    # head
    rect_fill(im, 1, 8, 7, 13, WOLF)
    # snout (긴 코)
    rect_fill(im, 0, 11, 2, 13, WOLF_DK)
    # 이빨 (white dot)
    put(im, 0, 12, (250, 250, 245, 255))
    # 노란 눈 (사나운)
    put(im, 4, 10, (240, 200, 60, 255))
    put(im, 5, 10, (240, 200, 60, 255))
    # ears (뾰족)
    put(im, 2, 7, WOLF); put(im, 6, 7, WOLF)
    put(im, 2, 6, WOLF_DK); put(im, 6, 6, WOLF_DK)
    # 등 갈기 (mohawk)
    hline(im, 8, 16, 10, WOLF_DK)
    # legs
    for lx in [5, 9, 13, 17]:
        vline(im, lx, 18, 22, WOLF_DK)
        put(im, lx, 22, OUTLINE)
    # bushy tail (위로 휘어짐)
    put(im, 19, 13, WOLF); put(im, 20, 12, WOLF)
    put(im, 21, 11, WOLF_DK); put(im, 21, 12, WOLF)
    # outline
    rect_outline(im, 4, 11, 18, 17, OUTLINE)
    rect_outline(im, 1, 8, 7, 13, OUTLINE)
    return im


# ─── crop_rice 16x16 - 노란 곡식 (밀 비슷) ───────────────────────
def gen_crop_rice():
    im = new_img(16, 16)
    # 줄기 (녹색 5줄)
    for sx in [3, 6, 9, 12]:
        vline(im, sx, 6, 14, GRASS_DK)
    # 익은 이삭 (노란 그레인)
    for sx, top in [(3, 4), (6, 3), (9, 4), (12, 5)]:
        # 이삭 = 세로 3 dot 노랑
        put(im, sx, top, WHEAT)
        put(im, sx, top + 1, WHEAT)
        put(im, sx - 1, top + 1, WHEAT_DK)
        put(im, sx + 1, top + 1, WHEAT_DK)
        put(im, sx, top - 1, WHEAT)
    # 바닥 갈색 흙 hint
    hline(im, 0, 15, 15, WOOD_MD)
    return im


# ─── stone_vein 16x16 - 돌 광맥 (큰 회색 덩어리 + 광물 점) ───────
def gen_stone_vein():
    im = new_img(16, 16)
    # 큰 회색 락 덩어리
    rect_fill(im, 1, 2, 14, 14, STONE_MD)
    # outline
    rect_outline(im, 1, 2, 14, 14, OUTLINE)
    # darker pockets (texture)
    rect_fill(im, 3, 5, 5, 7, STONE_DK)
    rect_fill(im, 9, 4, 12, 7, STONE_DK)
    rect_fill(im, 4, 10, 8, 12, STONE_DK)
    # light highlights
    put(im, 4, 4, STONE_LT); put(im, 10, 5, STONE_LT)
    put(im, 6, 9, STONE_LT); put(im, 12, 10, STONE_LT)
    put(im, 7, 3, STONE_LT)
    # mineral specs (반짝)
    put(im, 5, 6, (240, 230, 200, 255))
    put(im, 11, 8, (240, 230, 200, 255))
    return im


# ─── stone_chunk 16x16 - 돌멩이 (작은 회색 다각형) ───────────────
def gen_stone_chunk():
    im = new_img(16, 16)
    # 다각형 돌 (불규칙)
    chunk_pixels = [
        (6, 5), (7, 5), (8, 5), (9, 5),
        (5, 6), (6, 6), (7, 6), (8, 6), (9, 6), (10, 6),
        (4, 7), (5, 7), (6, 7), (7, 7), (8, 7), (9, 7), (10, 7), (11, 7),
        (4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8), (11, 8),
        (5, 9), (6, 9), (7, 9), (8, 9), (9, 9), (10, 9),
        (6, 10), (7, 10), (8, 10), (9, 10),
    ]
    for x, y in chunk_pixels:
        put(im, x, y, STONE_MD)
    # darker shadow (밑면)
    for x, y in [(5, 9), (6, 10), (7, 10), (8, 10), (9, 10), (10, 9)]:
        put(im, x, y, STONE_DK)
    # highlight (위)
    for x, y in [(7, 5), (8, 5)]:
        put(im, x, y, STONE_LT)
    # outline
    outline_pixels = [
        (6, 4), (7, 4), (8, 4), (9, 4),
        (5, 5), (10, 5),
        (4, 6), (11, 6),
        (3, 7), (12, 7),
        (3, 8), (12, 8),
        (4, 9), (11, 9),
        (5, 10), (10, 10),
        (6, 11), (7, 11), (8, 11), (9, 11),
    ]
    for x, y in outline_pixels:
        put(im, x, y, OUTLINE)
    return im


# ─── wood_pile 16x16 - 통나무 더미 (가로 통나무 3개 쌓임) ─────────
def gen_wood_pile():
    im = new_img(16, 16)
    # 3 개 가로 통나무
    for ly, c_main in [(10, WOOD_LT), (7, WOOD_MD), (4, WOOD_LT)]:
        # 통나무 1 개 = 가로 띠
        rect_fill(im, 2, ly, 13, ly + 2, c_main)
        # outline
        hline(im, 2, 13, ly, OUTLINE)
        hline(im, 2, 13, ly + 2, OUTLINE)
        put(im, 1, ly + 1, OUTLINE); put(im, 14, ly + 1, OUTLINE)
        # 양 끝 = 통나무 단면 (어두운 동그라미)
        put(im, 2, ly + 1, WOOD_DK)
        put(im, 13, ly + 1, WOOD_DK)
        # 나이테 (가운데 dot)
        put(im, 2, ly + 1, OUTLINE)
        put(im, 13, ly + 1, OUTLINE)
    # 바닥 그림자
    hline(im, 2, 13, 13, OUTLINE)
    return im


# ─── meat_pile 16x16 - 고기 (불그스름한 raw meat) ────────────────
def gen_meat_pile():
    im = new_img(16, 16)
    # 둥근 고기 덩어리
    meat_pixels = [
        (5, 5), (6, 5), (7, 5), (8, 5), (9, 5), (10, 5),
        (4, 6), (5, 6), (6, 6), (7, 6), (8, 6), (9, 6), (10, 6), (11, 6),
        (4, 7), (5, 7), (6, 7), (7, 7), (8, 7), (9, 7), (10, 7), (11, 7),
        (4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8), (11, 8),
        (4, 9), (5, 9), (6, 9), (7, 9), (8, 9), (9, 9), (10, 9), (11, 9),
        (5, 10), (6, 10), (7, 10), (8, 10), (9, 10), (10, 10),
    ]
    for x, y in meat_pixels:
        put(im, x, y, MEAT)
    # 어두운 면 (밑)
    for x in range(5, 11):
        put(im, x, 10, MEAT_DK)
    # 지방 (흰 스트라이프)
    for x, y in [(6, 7), (7, 7), (9, 8), (10, 8)]:
        put(im, x, y, MEAT_FAT)
    # 뼈 (선택) - 가운데 흰 dot
    put(im, 7, 9, MEAT_FAT); put(im, 8, 9, MEAT_FAT)
    # outline
    outline_pixels = [
        (5, 4), (6, 4), (7, 4), (8, 4), (9, 4), (10, 4),
        (4, 5), (11, 5),
        (3, 6), (12, 6), (3, 7), (12, 7),
        (3, 8), (12, 8), (3, 9), (12, 9),
        (4, 10), (11, 10),
        (5, 11), (6, 11), (7, 11), (8, 11), (9, 11), (10, 11),
    ]
    for x, y in outline_pixels:
        put(im, x, y, OUTLINE)
    return im


# ─── research_bench 32x16 - 2x1 책상 (RimWorld wiki 정합) ────────
def gen_research_bench():
    # #195 - wiki: 2x1 footprint.  sprite 도 32x16 (가로 길게).
    im = new_img(32, 16)
    # 긴 책상 top (가로 wood plank)
    rect_fill(im, 1, 6, 30, 10, WOOD_MD)
    rect_outline(im, 1, 6, 30, 10, OUTLINE)
    # plank grain (가운데 + 좌우)
    hline(im, 2, 29, 8, WOOD_DK)
    # 4 개 다리 (양 끝 + 중간 2개)
    for lx in [2, 11, 20, 29]:
        rect_fill(im, lx, 10, lx + 1, 14, WOOD_DK)
        put(im, lx, 14, OUTLINE)
    # 책 1 (왼쪽 열린 책)
    rect_fill(im, 4, 3, 11, 6, PAPER)
    rect_outline(im, 4, 3, 11, 6, OUTLINE)
    vline(im, 7, 3, 6, OUTLINE)  # 책 중앙선
    hline(im, 4, 6, 5, INK); hline(im, 8, 10, 5, INK)
    # 책 2 (오른쪽 닫힌 책 / 두루마리)
    rect_fill(im, 14, 3, 20, 6, PAPER)
    rect_outline(im, 14, 3, 20, 6, OUTLINE)
    hline(im, 14, 20, 4, INK)
    hline(im, 14, 20, 5, INK)
    # 잉크 병
    rect_fill(im, 24, 3, 26, 5, INK)
    put(im, 25, 2, INK)  # 마개
    # 깃펜
    put(im, 27, 2, PAPER); put(im, 28, 3, PAPER)
    put(im, 28, 4, INK); put(im, 27, 5, INK)
    return im


def main():
    out = {
        "wall_wood.png":       gen_wall_wood(),
        "floor_wood.png":      gen_floor_wood(),
        "stove.png":           gen_stove(),
        "deer.png":            gen_deer(),
        "wolf.png":            gen_wolf(),
        "crop_rice.png":       gen_crop_rice(),
        "stone_vein.png":      gen_stone_vein(),
        "stone_chunk.png":     gen_stone_chunk(),
        "wood_pile.png":       gen_wood_pile(),
        "meat_pile.png":       gen_meat_pile(),
        "research_bench.png":  gen_research_bench(),
    }
    for name, im in out.items():
        p = HERE / name
        im.save(p)
        print(f"[gen_audit_fix] {name} ({im.width}x{im.height})")


if __name__ == "__main__":
    main()
