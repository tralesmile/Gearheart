#!/usr/bin/env python3
"""Generates GEARHEART's placeholder pixel art into assets/art/.

Run:  python3 tools/make_art.py
Everything is drawn by code with fixed seeds, so the output is reproducible.
Replace the PNGs with real art later; keep the same grid layout (see docs/ART.md).
"""
import math, os, random
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "art")
T = 16  # world tile size in pixels


def noise_tile(rng, palette, weights):
    img = Image.new("RGBA", (T, T))
    px = img.load()
    for y in range(T):
        for x in range(T):
            px[x, y] = rng.choices(palette, weights)[0] + (255,)
    return img


def blobs(img, rng, colors, count, size):
    px = img.load()
    for _ in range(count):
        cx, cy = rng.randrange(2, T - 2), rng.randrange(2, T - 2)
        for _ in range(size):
            x = min(T - 1, max(0, cx + rng.randrange(-2, 3)))
            y = min(T - 1, max(0, cy + rng.randrange(-1, 2)))
            px[x, y] = rng.choice(colors) + (255,)


DIRT = [(134, 84, 57), (119, 72, 48), (150, 97, 66), (101, 60, 40)]
STONE = [(120, 124, 133), (104, 108, 118), (137, 141, 150), (88, 92, 102)]
GRASS = [(88, 168, 62), (72, 146, 52), (110, 190, 80)]


def dirt(rng):
    img = noise_tile(rng, DIRT, [16, 3, 2, 1])
    blobs(img, rng, [(96, 56, 38)], 2, 3)          # dark clods
    blobs(img, rng, [(168, 118, 84)], 1, 2)        # light pebbles
    return img


def stone(rng):
    img = noise_tile(rng, STONE, [16, 3, 2, 1])
    px = img.load()
    for _ in range(2):                              # crack lines
        x, y = rng.randrange(T), rng.randrange(T)
        for _ in range(rng.randrange(4, 8)):
            px[x % T, y % T] = (74, 78, 88, 255)
            x += rng.choice([0, 1, 1]); y += rng.choice([-1, 0, 1])
    blobs(img, rng, [(150, 154, 163)], 2, 3)
    return img


def grass(rng):
    img = dirt(rng)
    px = img.load()
    for x in range(T):
        depth = rng.choice([3, 4, 4, 5])
        for y in range(depth):
            px[x, y] = (GRASS[2] if y == 0 else rng.choices(GRASS, [5, 3, 1])[0]) + (255,)
        px[x, depth] = (58, 120, 44, 255)
    return img


def ore(rng, colors, sparkle):
    img = stone(rng)
    d = ImageDraw.Draw(img)
    dark = tuple(int(c * 0.5) for c in colors[1])
    for cx, cy in rng.sample([(3, 3), (10, 2), (6, 8), (12, 9), (3, 12), (9, 13)], 4):   # nuggets
        cx += rng.randrange(0, 2); cy += rng.randrange(0, 2)
        d.rectangle([cx - 1, cy - 1, cx + 2, cy + 2], fill=dark)
        d.rectangle([cx, cy, cx + 1, cy + 1], fill=colors[0])
        d.point((cx + 1, cy + 1), fill=colors[1])
        d.point((cx, cy), fill=sparkle)
    return img


def bedrock(rng):
    img = noise_tile(rng, [(52, 52, 62), (40, 40, 50), (66, 66, 78)], [5, 3, 1])
    blobs(img, rng, [(28, 28, 36)], 3, 5)
    return img


def wall(rng, palette):
    img = noise_tile(rng, palette, [6, 3, 2, 1])
    return Image.eval(img, lambda v: v).point(lambda v: int(v * 0.42)) if False else darken(img, 0.42)


def darken(img, f):
    r, g, b, a = img.split()
    return Image.merge("RGBA", (r.point(lambda v: int(v * f)), g.point(lambda v: int(v * f)), b.point(lambda v: int(v * f * 1.08)), a))


def crack(rng, stage):
    img = Image.new("RGBA", (T, T), (0, 0, 0, 0))
    px = img.load()
    rng2 = random.Random(77)                         # same cracks grow stage by stage
    for i in range(2 + stage * 2):
        x, y = 8, 8
        dx, dy = rng2.choice([(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1)])
        for _ in range(3 + stage * 2):
            if 0 <= x < T and 0 <= y < T:
                px[x, y] = (20, 16, 14, 200)
            x += dx + rng2.choice([0, 0, 1, -1]); y += dy + rng2.choice([0, 0, 1, -1])
    return img


def planks(rng):
    img = Image.new("RGBA", (T, T))
    px = img.load()
    for y in range(T):
        for x in range(T):
            base = (164, 116, 70) if (y // 4) % 2 == 0 else (150, 104, 62)
            if y % 4 == 3: base = (104, 70, 42)
            if rng.random() < 0.12: base = tuple(max(0, c - 14) for c in base)
            px[x, y] = base + (255,)
    return img


def icon(color, light, dark):
    img = Image.new("RGBA", (T, T), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(3, 6), (8, 2), (13, 6), (12, 12), (5, 13)], fill=color, outline=dark)
    d.line([(6, 6), (8, 4)], fill=light)
    d.point((9, 9), fill=light)
    return img


SAND = [(226, 196, 124), (212, 180, 108), (238, 212, 146), (198, 164, 96)]
SANDSTONE = [(196, 150, 96), (180, 134, 84), (210, 166, 110), (160, 116, 72)]
SNOW = [(240, 246, 252), (224, 234, 246), (250, 252, 255), (204, 220, 240)]
ICE = [(150, 208, 236), (130, 192, 226), (176, 224, 244), (112, 172, 212)]


def sand(rng):
    img = noise_tile(rng, SAND, [16, 4, 3, 1])
    px = img.load()
    for y in (5, 11):                                # wind ripples
        for x in range(T):
            if rng.random() < 0.55: px[x, (y + (x // 5) % 2) % T] = SAND[3] + (255,)
    return img


def sandstone(rng):
    img = noise_tile(rng, SANDSTONE, [16, 3, 2, 1])
    px = img.load()
    for y in (3, 8, 13):                             # layers
        for x in range(T):
            if rng.random() < 0.8: px[x, y] = SANDSTONE[3] + (255,)
    return img


def snow(rng):
    img = noise_tile(rng, SNOW, [18, 4, 3, 1])
    blobs(img, rng, [(255, 255, 255)], 2, 2)
    return img


def ice(rng):
    img = noise_tile(rng, ICE, [18, 3, 2, 1])
    px = img.load()
    for _ in range(3):                               # glints
        x, y = rng.randrange(2, T - 4), rng.randrange(2, T - 2)
        for i in range(rng.randrange(2, 5)):
            px[(x + i) % T, (y - i) % T] = (226, 244, 252, 255)
    return img


def make_tiles():
    rng = random.Random(1234)
    sheet = Image.new("RGBA", (8 * T, 10 * T), (0, 0, 0, 0))
    def put(img, col, row): sheet.paste(img, (col * T, row * T))
    for i in range(4):
        put(dirt(rng), i, 0); put(stone(rng), i, 1); put(grass(rng), i, 2); put(crack(rng, i), i, 4)
    for i in range(2):
        put(ore(rng, [(196, 201, 210), (160, 166, 176), (170, 96, 60)], (236, 240, 246)), i, 3)          # scrap
        put(ore(rng, [(181, 101, 74), (150, 78, 58), (214, 140, 104)], (240, 176, 140)), 2 + i, 3)      # iron
        put(ore(rng, [(232, 140, 60), (200, 108, 44), (80, 180, 150)], (255, 214, 130)), 4 + i, 3)      # copper
        put(bedrock(rng), 6 + i, 3)
        put(wall(rng, DIRT), 4 + i, 4); put(wall(rng, STONE), 6 + i, 4)
        put(planks(rng), i, 5)
        put(sand(rng), 2 + i, 5); put(sandstone(rng), 4 + i, 5); put(snow(rng), 6 + i, 5); put(ice(rng), 5 + i, 6)
    put(icon((134, 84, 57), (168, 118, 84), (70, 42, 28)), 0, 6)       # dirt
    put(icon((120, 124, 133), (160, 164, 172), (60, 62, 70)), 1, 6)    # stone
    put(icon((180, 186, 196), (236, 240, 246), (90, 94, 104)), 2, 6)   # scrap
    put(icon((181, 101, 74), (240, 176, 140), (96, 50, 36)), 3, 6)     # iron
    put(icon((232, 140, 60), (255, 214, 130), (120, 66, 24)), 4, 6)    # copper
    # Buried stashes (row 7), drawn last with their own random stream so older tiles stay identical.
    srng = random.Random(777)
    for i in range(2):
        put(stash_toolbox(srng, STONE), i, 7)
        put(stash_crate(srng, STONE), 2 + i, 7)
        put(stash_strongbox(srng, STONE), 4 + i, 7)
    # The Deep (rows 8-9 and the last icon), own random stream so everything above stays identical.
    drng = random.Random(4242)
    for i in range(4):
        put(slate(drng), i, 8)
        put(crystal(drng, i), 4 + i, 8)
    for i in range(2):
        put(wall(drng, SLATE), i, 9)
    for i in range(4):
        put(crystal_shapes(Image.new("RGBA", (T, T), (0, 0, 0, 0)), i), 4 + i, 9)
    put(crystal_icon(), 7, 6)
    sheet.save(os.path.join(OUT, "tiles.png"))


SLATE = [(66, 74, 98), (56, 63, 86), (78, 87, 112), (48, 54, 74)]
GLOW = [(120, 250, 240), (70, 210, 230), (200, 255, 250), (40, 150, 190)]   # light, mid, highlight, dark


def slate(rng):
    """Dark blue-grey rock in thin layers, harder-looking than stone."""
    img = noise_tile(rng, SLATE, [16, 3, 3, 2])
    px = img.load()
    for y0 in (4, 10):                               # slanted cleavage lines
        for x in range(T):
            y = (y0 + x // 6 + rng.choice([0, 0, 1])) % T
            px[x, y] = (40, 45, 62, 255)
            if rng.random() < 0.4: px[x, (y + 1) % T] = (90, 100, 128, 255)
    return img


CRYSTAL_SPOTS = [[(4, 13, 5, 9), (9, 14, 6, 11), (13, 12, 3, 6)],
                 [(3, 12, 4, 7), (8, 14, 5, 12), (12, 13, 4, 8)],
                 [(5, 14, 5, 10), (11, 11, 5, 8), (2, 6, 3, 5)],
                 [(7, 13, 6, 12), (12, 9, 3, 6), (3, 8, 3, 6)]]


def crystal_shapes(img, variant):
    """Each crystal: a pointed prism with a lit face and a bright spine."""
    d = ImageDraw.Draw(img)
    for cx, base, w, h in CRYSTAL_SPOTS[variant]:
        top = base - h
        poly = [(cx - w // 2, base), (cx - w // 2, top + 2), (cx, top), (cx + w // 2, top + 2), (cx + w // 2, base)]
        d.polygon(poly, fill=GLOW[1], outline=GLOW[3])
        d.line([(cx - w // 2 + 1, base - 1), (cx - w // 2 + 1, top + 2), (cx, top + 1)], fill=GLOW[0])
        d.line([(cx, top + 2), (cx, base - 2)], fill=GLOW[2])
    return img


def crystal(rng, variant):
    """A cluster of glowing cyan crystals growing out of slate (row 8); the same crystals alone on
    a clear background (row 9) are drawn over the darkness by the game, so they shine in the dark."""
    return crystal_shapes(slate(rng), variant)


def crystal_icon():
    img = Image.new("RGBA", (T, T), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for cx, base, w, h in [(5, 14, 5, 9), (10, 14, 6, 12)]:
        top = base - h
        poly = [(cx - w // 2, base), (cx - w // 2, top + 2), (cx, top), (cx + w // 2, top + 2), (cx + w // 2, base)]
        d.polygon(poly, fill=GLOW[1], outline=GLOW[3])
        d.line([(cx, top + 2), (cx, base - 2)], fill=GLOW[2])
    return img


# Buried stashes: a box sitting in a dark pocket of rock, so it reads as "something hidden here".

def stash_base(rng, palette):
    img = darken(noise_tile(rng, palette, [12, 4, 3, 2]), 0.45)
    return img, ImageDraw.Draw(img)


def stash_toolbox(rng, palette):
    """Old Toolbox: a dented red metal box with a handle."""
    img, d = stash_base(rng, palette)
    d.rectangle([2, 6, 13, 13], fill=(196, 52, 44), outline=(70, 18, 16))
    d.line([(3, 7), (12, 7)], fill=(236, 100, 84))                          # lid highlight
    d.line([(2, 9), (13, 9)], fill=(120, 30, 26))                           # lid seam
    d.rectangle([6, 3, 9, 5], outline=(150, 150, 160))                      # handle
    d.rectangle([7, 9, 8, 10], fill=(232, 200, 90))                         # latch
    d.point((4, 12), fill=(120, 30, 26)); d.point((11, 11), fill=(236, 100, 84))   # dents
    return img


def stash_crate(rng, palette):
    """Lost Supply Crate: planks with iron bands and a stencilled mark."""
    img, d = stash_base(rng, palette)
    d.rectangle([1, 3, 14, 14], fill=(160, 112, 62), outline=(64, 40, 20))
    for y in (6, 10):
        d.line([(2, y), (13, y)], fill=(118, 80, 42))                       # plank gaps
    d.line([(4, 3), (4, 14)], fill=(110, 116, 128)); d.line([(11, 3), (11, 14)], fill=(110, 116, 128))   # iron bands
    d.line([(2, 4), (13, 4)], fill=(196, 150, 92))                          # top highlight
    d.line([(6, 7), (9, 9)], fill=(230, 200, 120)); d.line([(9, 7), (6, 9)], fill=(230, 200, 120))      # stencil X
    return img


def stash_strongbox(rng, palette):
    """Ancient Strongbox: dark iron with gold trim and a keyhole, glinting."""
    img, d = stash_base(rng, palette)
    d.rectangle([1, 4, 14, 14], fill=(58, 52, 70), outline=(20, 16, 26))
    d.rectangle([1, 4, 14, 7], fill=(78, 70, 94), outline=(20, 16, 26))    # domed lid
    d.line([(2, 5), (13, 5)], fill=(110, 100, 130))
    gold, gold_l = (226, 176, 56), (255, 230, 130)
    for x in (3, 12):
        d.line([(x, 4), (x, 14)], fill=gold)                                # gold straps
    d.rectangle([6, 8, 9, 11], fill=gold, outline=(140, 96, 20))            # lock plate
    d.point((7, 9), fill=(20, 16, 26)); d.point((7, 10), fill=(20, 16, 26))  # keyhole
    d.point((3, 5), fill=gold_l); d.point((12, 12), fill=gold_l); d.point((6, 8), fill=gold_l)
    return img


# ---------------------------------------------------------------------------- player

OUTLINE = (34, 28, 40, 255)
SKIN, SKIN_D = (236, 188, 148), (204, 150, 112)
SUIT, SUIT_D, SUIT_L = (232, 120, 44), (190, 88, 30), (250, 160, 80)
PANTS, PANTS_D = (62, 84, 130), (46, 62, 100)
BOOT = (58, 46, 44)
HELMET, HELMET_L = (240, 196, 48), (255, 232, 120)
GOGGLE = (120, 220, 240)
GLOVE = (90, 70, 60)
PICK_H, PICK_M = (150, 104, 62), (190, 196, 206)


def outline(img):
    src = img.copy(); s = src.load(); p = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if s[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and s[nx, ny][3] != 0:
                        p[x, y] = OUTLINE; break
    return img


def player_frame(leg_a=0, leg_b=0, lift_a=0, lift_b=0, arm=0, bob=0, pick=None):
    """32x48 frame (body in the left 24 px, room for the pickaxe on the right), facing right. leg_* = forward offset px, lift_* = foot raised px, arm = swing px."""
    W, H = 32, 48
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r = lambda x0, y0, x1, y1, c: d.rectangle([x0, y0, x1, y1], fill=c)
    by = bob
    # back leg, back arm (darker)
    r(9 + leg_b, 33 + by, 12 + leg_b, 42 - lift_b, PANTS_D)
    r(8 + leg_b, 43 - lift_b, 13 + leg_b, 46 - lift_b, BOOT)
    r(7 - arm, 20 + by, 9 - arm, 29 + by, SUIT_D)
    # front leg
    r(11 + leg_a, 33 + by, 14 + leg_a, 42 - lift_a, PANTS)
    r(10 + leg_a, 43 - lift_a, 16 + leg_a, 46 - lift_a, BOOT)
    # torso
    r(7, 18 + by, 16, 32 + by, SUIT)
    r(7, 18 + by, 8, 32 + by, SUIT_D)
    r(14, 19 + by, 15, 25 + by, SUIT_L)
    r(7, 30 + by, 16, 31 + by, (70, 56, 50))            # belt
    r(11, 30 + by, 12, 31 + by, HELMET)                 # buckle
    # head
    r(8, 7 + by, 16, 16 + by, SKIN)
    r(8, 14 + by, 10, 16 + by, SKIN_D)
    r(7, 4 + by, 16, 9 + by, HELMET)                    # helmet
    r(9, 4 + by, 14, 5 + by, HELMET_L)
    r(15, 9 + by, 18, 10 + by, HELMET)                  # visor brim
    r(12, 10 + by, 16, 12 + by, GOGGLE)                 # goggles
    r(12, 10 + by, 13, 10 + by, (220, 250, 255))
    d.point((15, 15 + by), fill=(150, 90, 70))          # mouth
    # front arm (+ pickaxe)
    if pick is None:
        r(12 + arm, 20 + by, 15 + arm, 28 + by, SUIT)
        r(12 + arm, 29 + by, 15 + arm, 31 + by, GLOVE)
    else:
        ang = math.radians(pick)
        sx, sy = 13, 21 + by
        hx, hy = sx + math.cos(ang) * 8, sy + math.sin(ang) * 8
        d.line([(sx, sy), (hx, hy)], fill=SUIT, width=4)
        d.rectangle([hx - 1, hy - 1, hx + 1, hy + 1], fill=GLOVE)
        tx, ty = hx + math.cos(ang - 0.5) * 9, hy + math.sin(ang - 0.5) * 9
        d.line([(hx, hy), (tx, ty)], fill=PICK_H, width=2)
        nx, ny = math.cos(ang - 0.5 + math.pi / 2), math.sin(ang - 0.5 + math.pi / 2)
        d.line([(tx - nx * 5, ty - ny * 5), (tx + nx * 6, ty + ny * 6)], fill=PICK_M, width=2)
    return outline(img)


def make_player():
    frames = [
        player_frame(),                                            # 0 idle
        player_frame(leg_a=3, leg_b=-3, lift_b=1, arm=-2),         # 1-4 walk
        player_frame(leg_a=1, leg_b=-1, lift_a=0, lift_b=3, arm=-1, bob=-1),
        player_frame(leg_a=-3, leg_b=3, lift_a=1, arm=2),
        player_frame(leg_a=-1, leg_b=1, lift_a=3, arm=1, bob=-1),
        player_frame(leg_a=2, leg_b=-2, lift_a=4, lift_b=1, arm=-3),   # 5 jump
        player_frame(pick=-55),                                    # 6 mine up
        player_frame(pick=25),                                     # 7 mine down
    ]
    sheet = Image.new("RGBA", (32 * len(frames), 48), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * 32, 0))
    sheet.save(os.path.join(OUT, "player.png"))


# ---------------------------------------------------------------------------- vehicle parts

P = 22  # part cell size


def panel(base, light, dark):
    img = Image.new("RGBA", (P, P), base + (255,))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, P - 1, 2], fill=light)                 # highlight band
    d.rectangle([0, P - 4, P - 1, P - 1], fill=dark)          # shadow band
    d.rectangle([0, 0, P - 1, P - 1], outline=tuple(int(c * 0.55) for c in base))
    return img, d


def part_frame_light():
    img = Image.new("RGBA", (P, P), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tube, hi = (196, 170, 120), (232, 212, 164)
    d.rectangle([0, 0, P - 1, P - 1], outline=tube, width=3)
    d.line([(1, 1), (P - 2, P - 2)], fill=tube, width=3)
    d.line([(P - 2, 1), (1, P - 2)], fill=tube, width=3)
    d.line([(1, 1), (P - 2, 1)], fill=hi)
    d.rectangle([0, 0, P - 1, P - 1], outline=(110, 92, 60))
    return img


def part_steel():
    img, d = panel((206, 62, 44), (240, 110, 84), (150, 40, 32))
    d.line([(0, 12), (P - 1, 12)], fill=(150, 40, 32))        # body crease
    d.line([(3, 6), (9, 6)], fill=(255, 170, 150))            # glint
    return img


def part_heavy():
    img, d = panel((86, 94, 106), (120, 130, 144), (58, 64, 74))
    for x, y in ((3, 6), (P - 4, 6), (3, P - 8), (P - 4, P - 8)):
        d.rectangle([x - 1, y - 1, x, y], fill=(160, 168, 180))
    for i in range(-P, P, 8):                                  # hazard stripe
        d.polygon([(i, P - 4), (i + 4, P - 4), (i + 8, P - 1), (i + 4, P - 1)], fill=(232, 190, 40))
    return img


def part_engine(body, head, pipes):
    img, d = panel((70, 74, 84), (104, 110, 122), (44, 46, 54))
    d.rectangle([3, 5, P - 4, 15], fill=body)
    for i in range(pipes):
        x = 4 + i * (P - 8) // max(1, pipes - 1) if pipes > 1 else P // 2
        d.rectangle([x - 1, 2, x, 6], fill=head)
        d.point((x, 1), fill=(240, 240, 240))
    for y in (8, 11, 14):
        d.line([(4, y), (P - 5, y)], fill=tuple(int(c * 0.7) for c in body))
    d.ellipse([8, 15, 13, 20], fill=(30, 30, 36), outline=(150, 156, 166))   # pulley
    return img


def part_armor():
    img, d = panel((74, 86, 98), (116, 130, 144), (44, 52, 62))
    d.rectangle([2, 2, P - 3, P - 3], outline=(132, 146, 160))
    for x, y in ((4, 4), (P - 5, 4), (4, P - 5), (P - 5, P - 5)):
        d.point((x, y), fill=(210, 218, 228))
    d.line([(8, 14), (13, 9)], fill=(44, 52, 62))             # scratch
    return img


def part_bumper():
    img, d = panel((40, 40, 46), (70, 70, 78), (24, 24, 28))
    for i in range(-P, P * 2, 8):
        d.polygon([(i, 4), (i + 4, 4), (i - 6, P - 5), (i - 10, P - 5)], fill=(236, 150, 40))
    d.rectangle([0, 0, P - 1, P - 1], outline=(20, 20, 24))
    return img


def part_fuel():
    img, d = panel((58, 132, 76), (96, 176, 112), (38, 92, 52))
    d.rectangle([3, 5, P - 4, P - 6], outline=(38, 92, 52))
    d.line([(4, 6), (P - 5, P - 7)], fill=(38, 92, 52)); d.line([(P - 5, 6), (4, P - 7)], fill=(38, 92, 52))
    d.rectangle([8, 0, 13, 3], fill=(220, 190, 60), outline=(120, 100, 30))  # cap
    return img


def part_drill():
    img, d = panel((214, 168, 40), (250, 214, 100), (150, 112, 24))
    d.ellipse([5, 5, 16, 16], fill=(60, 60, 70), outline=(30, 30, 36))
    d.ellipse([9, 9, 12, 12], fill=(200, 206, 216))
    for a in range(0, 360, 60):
        d.point((10.5 + math.cos(math.radians(a)) * 5, 10.5 + math.sin(math.radians(a)) * 5), fill=(200, 206, 216))
    return img


def part_crystal_drill():
    # Dark steel drill housing with a glowing crystal ring (the crystal-tipped cone is drawn by the game).
    img, d = panel((60, 70, 96), (96, 108, 140), (34, 40, 58))
    d.ellipse([4, 4, 17, 17], fill=(40, 150, 190), outline=(20, 26, 40))
    d.ellipse([7, 7, 14, 14], fill=(120, 250, 240))
    d.ellipse([9, 9, 12, 12], fill=(220, 255, 252))
    return img


def part_cab():
    img, d = panel((206, 62, 44), (240, 110, 84), (150, 40, 32))
    d.rectangle([3, 3, P - 3, 14], fill=(140, 208, 232), outline=(40, 44, 54))
    d.line([(6, 12), (12, 5)], fill=(220, 244, 252)); d.line([(9, 12), (14, 6)], fill=(220, 244, 252))
    d.rectangle([4, 16, 8, 17], fill=(40, 44, 54))            # door handle
    return img


def part_booster():
    # Orange rocket casing with a dark band and rivets; the nozzle bell is drawn by the game.
    img, d = panel((214, 112, 40), (248, 160, 80), (150, 72, 24))
    d.rectangle([0, 7, P - 1, 13], fill=(52, 54, 62))                     # steel band
    d.line([(0, 8), (P - 1, 8)], fill=(96, 100, 112))
    for x in (3, 9, 15, 20):
        d.point((x, 10), fill=(200, 206, 216))                              # rivets
    for i in range(3):                                                      # chevrons pointing forward
        x = 5 + i * 5
        d.line([(x, 15), (x + 2, 17), (x, 19)], fill=(250, 214, 100))
    d.rectangle([0, 2, 2, P - 5], fill=(60, 60, 70))                        # nozzle collar at the back
    return img


def part_turbo():
    # A copper snail-shell compressor housing bolted to a dark steel mount; the game draws the spinning
    # compressor wheel in the middle of the shell.
    img, d = panel((58, 60, 70), (92, 96, 108), (36, 38, 46))
    copper, light, dark = (200, 122, 58), (240, 172, 104), (130, 72, 30)
    d.ellipse([2, 2, P - 3, P - 3], fill=copper, outline=dark)            # the shell
    d.arc([2, 2, P - 3, P - 3], 200, 300, fill=light, width=2)            # polished highlight
    d.arc([5, 5, P - 6, P - 6], 30, 330, fill=dark, width=1)              # the volute's spiral step
    d.rectangle([P - 6, 1, P - 2, 7], fill=copper, outline=dark)          # outlet pipe, top right
    d.line([(P - 5, 2), (P - 3, 2)], fill=light)
    for x, y in ((3, P - 4), (P - 4, P - 4)):
        d.point((x, y), fill=(200, 206, 216))                              # mount bolts
    return img


def part_hydraulics():
    # Lowrider hydraulics: a dark steel block with a chrome ram standing in it (the rod and its
    # collar), a yellow pressure accumulator on the left and a red hose looping to the ram.
    img, d = panel((62, 66, 78), (98, 104, 118), (36, 40, 48))
    chrome, chrome_l, chrome_d = (196, 204, 216), (246, 250, 255), (110, 118, 132)
    d.rectangle([11, 3, 16, P - 3], fill=chrome_d)                         # the cylinder
    d.rectangle([12, 3, 15, P - 3], fill=chrome)
    d.line([(13, 4), (13, P - 4)], fill=chrome_l)                          # polished stripe
    d.rectangle([10, 2, 17, 4], fill=(70, 74, 86), outline=(30, 32, 40))   # top collar
    d.rectangle([10, 12, 17, 14], fill=(70, 74, 86), outline=(30, 32, 40)) # gland
    d.rounded_rectangle([3, 6, 8, P - 4], 2, fill=(230, 186, 50), outline=(130, 96, 20))   # accumulator
    d.line([(4, 8), (4, P - 6)], fill=(252, 226, 120))
    d.line([(8, 9), (10, 9)], fill=(190, 40, 34))                          # hose
    d.line([(9, 9), (9, 16), (10, 16)], fill=(190, 40, 34))
    for x, y in ((P - 4, 3), (P - 4, P - 4)):
        d.point((x, y), fill=(200, 206, 216))                              # bolts
    return img


def wheel(radius, tread, rim_col):
    size = 56
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = size / 2 - 0.5
    for i in range(tread):                                     # tread knobs
        a = math.tau * i / tread
        x, y = c + math.cos(a) * radius, c + math.sin(a) * radius
        d.ellipse([x - 1.6, y - 1.6, x + 1.6, y + 1.6], fill=(24, 24, 28))
    d.ellipse([c - radius + 0.5, c - radius + 0.5, c + radius - 0.5, c + radius - 0.5], fill=(34, 34, 40), outline=(16, 16, 20))
    rr = radius * 0.6
    d.ellipse([c - rr, c - rr, c + rr, c + rr], fill=rim_col, outline=(60, 62, 70))
    for i in range(5):                                         # spokes
        a = math.tau * i / 5
        d.line([(c, c), (c + math.cos(a) * rr * 0.9, c + math.sin(a) * rr * 0.9)], fill=(70, 72, 82), width=2)
    d.ellipse([c - 2.5, c - 2.5, c + 2.5, c + 2.5], fill=(230, 232, 238), outline=(60, 62, 70))
    return img


def spiked_wheel(radius):
    """Studded winter tyre: a dark tyre with bright steel studs poking out of the tread, blue-steel rim."""
    img = wheel(radius, 0, (120, 150, 178))
    d = ImageDraw.Draw(img)
    c = 56 / 2 - 0.5
    studs = 10
    for i in range(studs):
        a = math.tau * (i + 0.5) / studs
        ca, sa = math.cos(a), math.sin(a)
        tip = (c + ca * (radius + 4.2), c + sa * (radius + 4.2))
        base_l = (c + math.cos(a - 0.17) * (radius - 0.5), c + math.sin(a - 0.17) * (radius - 0.5))
        base_r = (c + math.cos(a + 0.17) * (radius - 0.5), c + math.sin(a + 0.17) * (radius - 0.5))
        d.polygon([base_l, tip, base_r], fill=(232, 238, 246), outline=(70, 78, 92))
    for i in range(studs):                                     # tread grooves between the studs
        a = math.tau * i / studs
        d.line([(c + math.cos(a) * (radius - 4), c + math.sin(a) * (radius - 4)),
                (c + math.cos(a) * (radius - 1), c + math.sin(a) * (radius - 1))], fill=(18, 18, 22), width=2)
    rr = radius * 0.6                                          # a frosty ring on the rim
    d.arc([c - rr + 1.5, c - rr + 1.5, c + rr - 1.5, c + rr - 1.5], 200, 320, fill=(200, 228, 246), width=1)
    return img


def make_parts():
    parts = [part_frame_light(), part_steel(), part_heavy(),
             part_engine((150, 96, 60), (190, 150, 110), 2), part_engine((190, 60, 44), (220, 220, 226), 3),
             part_engine((224, 44, 30), (250, 210, 60), 4),
             part_armor(), part_bumper(), part_fuel(), part_drill(), part_cab(), part_booster(), part_crystal_drill(), part_turbo(), part_hydraulics()]
    sheet = Image.new("RGBA", (P * len(parts), P), (0, 0, 0, 0))
    for i, p in enumerate(parts):
        sheet.paste(p, (i * P, 0))
    sheet.save(os.path.join(OUT, "parts.png"))

    wheels = [wheel(16, 16, (176, 182, 192)), wheel(20, 20, (200, 204, 212)), wheel(26, 14, (214, 150, 60)), spiked_wheel(20)]
    sheet = Image.new("RGBA", (56 * len(wheels), 56), (0, 0, 0, 0))
    for i, w in enumerate(wheels):
        sheet.paste(w, (i * 56, 0))
    sheet.save(os.path.join(OUT, "wheels.png"))


def make_garage():
    W, H = 112, 72
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([6, 22, 12, H - 1], fill=(96, 70, 48)); d.rectangle([W - 13, 22, W - 7, H - 1], fill=(96, 70, 48))   # posts
    d.rectangle([13, 24, W - 14, H - 1], fill=(40, 36, 44, 150))                                                    # dark interior
    for x in range(0, W, 6):                                                                                         # corrugated roof
        d.polygon([(x, 22), (x + 6, 22), (x + 6, 10 + (x % 12 == 0)), (x, 10 + (x % 12 == 0))], fill=(170, 60, 50) if (x // 6) % 2 else (150, 50, 42))
    d.rectangle([0, 20, W - 1, 23], fill=(90, 30, 28))
    d.rectangle([30, 0, 82, 12], fill=(236, 190, 60), outline=(70, 50, 20))                                         # sign
    font = ImageFont.load_default()
    d.text((34, 0), "GARAGE", fill=(60, 40, 16), font=font)
    d.rectangle([20, 50, 44, 53], fill=(150, 104, 62)); d.rectangle([22, 54, 24, H - 1], fill=(110, 76, 46)); d.rectangle([40, 54, 42, H - 1], fill=(110, 76, 46))  # workbench
    d.rectangle([24, 44, 30, 49], fill=(200, 60, 50)); d.rectangle([34, 46, 40, 49], fill=(120, 126, 136))          # toolbox, vice
    d.ellipse([84, 30, 96, 42], outline=(190, 196, 206), width=2)                                                    # hanging tyre
    return outline(img).save(os.path.join(OUT, "garage.png"))


# ---------------------------------------------------------------------------- decorations

DW, DH = 48, 64   # decoration cell; art sits on the bottom edge, centred


def blob(d, cx, cy, r, colors, rng, n=26):
    for _ in range(n):
        a, q = rng.random() * math.tau, rng.random() ** 0.5 * r
        x, y = cx + math.cos(a) * q, cy + math.sin(a) * q * 0.8
        rr = rng.choice([3, 4, 5])
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=rng.choice(colors))


def decor_oak(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.rectangle([21, 34, 26, DH - 1], fill=(110, 76, 46)); d.rectangle([21, 34, 22, DH - 1], fill=(84, 56, 34))
    d.line([(24, 40), (31, 33)], fill=(110, 76, 46), width=2)
    blob(d, 24, 22, 17, [(58, 130, 52), (72, 150, 60), (48, 112, 46)], rng, 40)
    blob(d, 20, 16, 9, [(96, 178, 76), (84, 164, 68)], rng, 12)
    return outline(img)


def decor_pine(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.rectangle([22, 50, 25, DH - 1], fill=(96, 66, 42))
    for i, (y, w) in enumerate(((8, 7), (20, 11), (33, 15), (46, 19))):
        d.polygon([(24, y - 8), (24 - w, y + 10), (24 + w, y + 10)], fill=(40, 96, 72))
        d.polygon([(24, y - 8), (24 - w * 0.55, y + 2), (24 + w * 0.5, y)], fill=(240, 246, 252))     # snow cap
    return outline(img)


def decor_cactus(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    g, gl, gd = (70, 150, 84), (104, 184, 110), (48, 112, 62)
    d.rounded_rectangle([20, 22, 27, DH - 1], 3, fill=g); d.rectangle([20, 26, 21, DH - 1], fill=gd); d.rectangle([25, 26, 25, DH - 4], fill=gl)
    d.rounded_rectangle([11, 34, 16, 46], 2, fill=g); d.rectangle([14, 43, 21, 47], fill=g)
    d.rounded_rectangle([31, 28, 36, 40], 2, fill=g); d.rectangle([27, 37, 33, 41], fill=g)
    d.point((23, 21), fill=(240, 120, 160)); d.point((24, 21), fill=(240, 120, 160))
    return outline(img)


def decor_bush(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    blob(d, 24, DH - 9, 10, [(58, 130, 52), (72, 150, 60), (48, 112, 46)], rng, 18)
    for x, y in ((18, DH - 12), (27, DH - 15), (30, DH - 8)): d.point((x, y), fill=(220, 60, 70))
    return outline(img)


def decor_flowers(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for x, h, c in ((16, 9, (250, 220, 80)), (23, 12, (240, 110, 150)), (30, 8, (250, 250, 250)), (35, 11, (160, 130, 240))):
        d.line([(x, DH - 1), (x, DH - h)], fill=(60, 140, 56))
        d.rectangle([x - 1, DH - h - 2, x + 1, DH - h], fill=c); d.point((x, DH - h - 1), fill=(250, 180, 60))
    return img


def decor_grass(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    for x in range(14, 35, 2):
        h = rng.randrange(3, 8)
        d.line([(x, DH - 1), (x + rng.choice([-1, 0, 1]), DH - h)], fill=rng.choice(GRASS))
    return img


def decor_desert_rock(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.polygon([(12, DH - 1), (16, DH - 10), (26, DH - 13), (34, DH - 7), (36, DH - 1)], fill=(176, 130, 86))
    d.polygon([(16, DH - 10), (26, DH - 13), (24, DH - 8)], fill=(204, 160, 110))
    for x0, y0, x1, y1 in ((38, DH - 1, 41, DH - 9), (41, DH - 9, 45, DH - 12), (41, DH - 6, 44, DH - 6)):   # dead twig
        d.line([(x0, y0), (x1, y1)], fill=(120, 90, 60))
    return outline(img)


def decor_snow_rock(rng):
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.polygon([(12, DH - 1), (15, DH - 9), (25, DH - 12), (33, DH - 8), (36, DH - 1)], fill=(120, 128, 142))
    d.polygon([(15, DH - 9), (25, DH - 12), (33, DH - 8), (27, DH - 6), (19, DH - 6)], fill=(240, 246, 252))
    return outline(img)


def decor_kicker_sign(rng):
    """A jump warning sign on a post: yellow board, black border, a ramp with an arrow leaping off it
    (pointing right; mirrored for the west side). Big enough to read at driving zoom."""
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    wood, wood_dark = (122, 84, 50), (88, 58, 34)
    d.rectangle([22, 36, 25, DH - 1], fill=wood); d.rectangle([22, 36, 22, DH - 1], fill=wood_dark)
    yellow, yellow_light, black = (250, 204, 40), (255, 232, 120), (30, 28, 30)
    d.rectangle([8, 14, 39, 37], fill=black)                       # border
    d.rectangle([10, 16, 37, 35], fill=yellow)
    d.line([(10, 16), (37, 16)], fill=yellow_light)
    d.polygon([(12, 33), (24, 33), (24, 26)], fill=black)          # the ramp
    for x, y in ((25, 24), (27, 22), (29, 20), (31, 19)):           # the leap, as dashes over the gap
        d.rectangle([x, y, x + 1, y + 1], fill=black)
    d.polygon([(32, 16), (37, 19), (32, 22)], fill=black)          # arrow head
    for x in range(12, 37, 6):                                     # hazard stripes under the board
        d.polygon([(x, 38), (x + 3, 38), (x + 1, 41), (x - 2, 41)], fill=black)
        d.polygon([(x + 3, 38), (x + 6, 38), (x + 4, 41), (x + 1, 41)], fill=yellow)
    return outline(img)


def decor_record_flag():
    """The long-jump record flag: a chequered flag on a white-and-red striped pole with a gold knob,
    flying to the right (mirrored for the west side). It marks the longest landed jump off a kicker.
    The pole stands in the middle of the cell so the game can plant it exactly on the landing spot."""
    img = Image.new("RGBA", (DW, DH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    px = DW // 2 - 1                                               # pole: x = 23..24
    for y in range(16, DH):
        c = (236, 236, 240) if (y // 5) % 2 == 0 else (210, 52, 52)
        d.rectangle([px, y, px + 1, y], fill=c)
    d.rectangle([px - 3, DH - 2, px + 4, DH - 1], fill=(90, 90, 96))   # foot
    d.ellipse([px - 2, 12, px + 3, 17], fill=(250, 204, 40)); d.point((px, 13), fill=(255, 240, 160))
    fx, fy, cw, rows, cols = px + 2, 18, 3, 4, 6                    # flag: 6 x 4 checks of 3 px, a slight wave
    for r in range(rows):
        for c in range(cols):
            wave = 1 if c in (2, 3) else 0
            col = (24, 24, 28) if (r + c) % 2 == 0 else (248, 248, 250)
            d.rectangle([fx + c * cw, fy + r * cw + wave, fx + c * cw + cw - 1, fy + r * cw + cw - 1 + wave], fill=col)
    return outline(img)


def make_decor():
    rng = random.Random(99)
    sign = decor_kicker_sign(rng)
    flag = decor_record_flag()
    cells = [decor_oak(rng), decor_pine(rng), decor_cactus(rng), decor_bush(rng), decor_flowers(rng), decor_grass(rng), decor_desert_rock(rng), decor_snow_rock(rng),
             sign, sign.transpose(Image.FLIP_LEFT_RIGHT), flag, flag.transpose(Image.FLIP_LEFT_RIGHT)]
    sheet = Image.new("RGBA", (DW * len(cells), DH), (0, 0, 0, 0))
    for i, c in enumerate(cells):
        sheet.paste(c, (i * DW, 0))
    sheet.save(os.path.join(OUT, "decor.png"))


# ---------------------------------------------------------------------------- roadside pickups

PW, PH = 32, 32   # pickup cell; art sits on the bottom edge, centred


def pickup_fuel_can(rng):
    """A red jerry can standing upright, with a handle and a yellow cap."""
    img = Image.new("RGBA", (PW, PH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    red, light, dark = (206, 52, 44), (238, 96, 80), (148, 30, 30)
    d.rectangle([9, 12, 23, PH - 1], fill=red)
    d.rectangle([9, 12, 10, PH - 1], fill=light); d.rectangle([22, 12, 23, PH - 1], fill=dark)
    d.rectangle([9, PH - 2, 23, PH - 1], fill=dark)
    d.line([(11, 16), (21, PH - 4)], fill=dark); d.line([(21, 16), (11, PH - 4)], fill=dark)       # pressed X
    d.rectangle([12, 8, 20, 11], fill=(60, 60, 66)); d.rectangle([14, 9, 18, 10], fill=(0, 0, 0, 0))  # handle
    d.rectangle([20, 7, 23, 11], fill=(246, 204, 60)); d.point((21, 7), fill=(255, 240, 150))        # spout cap
    d.rectangle([12, 19, 20, 23], fill=(246, 230, 200)); d.rectangle([14, 20, 18, 22], fill=red)       # label
    return outline(img)


def pickup_scrap_heap(rng, tint=None, snow=False):
    """A big pile of junk filling most of the cell (readable at driving zoom): plates, a door panel, a pipe,
    a tyre, a bent rod and bolts. tint shifts it to rust (desert); snow caps it."""
    img = Image.new("RGBA", (PW, PH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    metal = [(150, 156, 166), (124, 130, 142), (176, 182, 192), (104, 108, 120)]
    if tint:
        metal = [tuple(int(c * 0.55 + t * 0.45) for c, t in zip(m, tint)) for m in metal]
    d.polygon([(0, PH - 1), (4, PH - 10), (10, PH - 18), (17, PH - 21), (24, PH - 17), (29, PH - 9), (31, PH - 1)], fill=metal[1])
    d.polygon([(4, PH - 10), (10, PH - 18), (17, PH - 21), (15, PH - 12)], fill=metal[3])               # shadowed flank
    for _ in range(12):                                                                    # plates and panels
        x, y = rng.randrange(4, 25), rng.randrange(PH - 17, PH - 3)
        w, h = rng.randrange(3, 8), rng.randrange(2, 4)
        d.rectangle([x, y, x + w, y + h], fill=rng.choice(metal))
        d.line([(x, y), (x + w, y)], fill=metal[2])
    d.polygon([(17, PH - 21), (25, PH - 25), (28, PH - 19), (21, PH - 16)], fill=metal[2])             # door panel sticking up
    d.line([(17, PH - 21), (25, PH - 25)], fill=(232, 236, 240))
    d.rectangle([22, PH - 22, 24, PH - 21], fill=(60, 66, 80))                                          # its window hole
    d.ellipse([1, PH - 14, 14, PH - 1], fill=(44, 44, 50)); d.ellipse([5, PH - 10, 10, PH - 5], fill=metal[2])  # tyre
    d.arc([1, PH - 14, 14, PH - 1], 200, 290, fill=(84, 84, 92))
    d.line([(13, PH - 19), (29, PH - 7)], fill=(196, 120, 60), width=2)                    # copper pipe
    d.line([(9, PH - 18), (6, PH - 25)], fill=metal[0], width=2)                            # bent rod sticking out
    d.line([(6, PH - 25), (9, PH - 28)], fill=metal[0], width=2)
    for x, y in ((19, PH - 5), (25, PH - 4), (17, PH - 11), (22, PH - 9)):
        d.point((x, y), fill=(240, 242, 246))                                               # bright bolts
    if snow:
        d.polygon([(5, PH - 11), (10, PH - 19), (17, PH - 22), (25, PH - 26), (28, PH - 20), (29, PH - 10), (22, PH - 14), (12, PH - 14)], fill=(240, 246, 252))
    return outline(img)


def make_pickups():
    rng = random.Random(4242)
    cells = [pickup_fuel_can(rng), pickup_scrap_heap(rng), pickup_scrap_heap(rng, tint=(170, 96, 52)), pickup_scrap_heap(rng, snow=True)]
    sheet = Image.new("RGBA", (PW * len(cells), PH), (0, 0, 0, 0))
    for i, c in enumerate(cells):
        sheet.paste(c, (i * PW, 0))
    sheet.save(os.path.join(OUT, "pickups.png"))


# ---------------------------------------------------------------------------- birds

BW, BH = 16, 16   # bird cell; a perched bird stands on the bottom edge, facing right

BIRDS = {
    # body, belly, wing, head, beak, eye
    "sparrow": ((150, 104, 64), (224, 198, 152), (104, 70, 42), (118, 78, 48), (232, 180, 64), (20, 16, 14)),
    "crow":    ((46, 46, 56), (66, 66, 78), (30, 30, 38), (40, 40, 50), (96, 96, 104), (236, 222, 120)),
    "bunting": ((236, 240, 246), (252, 252, 252), (42, 46, 56), (226, 206, 170), (62, 62, 72), (20, 16, 14)),
}


def bird_frame(kind, pose):
    """pose: sit, peck, up (wings raised in flight), down (wings beating down)."""
    body, belly, wing, head, beak, eye = BIRDS[kind]
    img = Image.new("RGBA", (BW, BH), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    if pose in ("sit", "peck"):
        d.polygon([(1, 7), (5, 9), (5, 11), (2, 10)], fill=wing)                       # tail, tipped up
        d.ellipse([3, 7, 11, 13], fill=body)                                           # body
        d.ellipse([5, 10, 10, 13], fill=belly)                                         # belly
        d.polygon([(4, 9), (9, 8), (8, 11), (5, 11)], fill=wing)                       # folded wing
        d.line([(7, 13), (7, 15)], fill=(70, 50, 36)); d.line([(9, 13), (9, 15)], fill=(70, 50, 36))   # legs
        if pose == "sit":
            d.ellipse([9, 4, 13, 8], fill=head); d.point((12, 5), fill=eye)
            d.polygon([(13, 6), (15, 6), (13, 7)], fill=beak)
        else:   # head down to the ground
            d.ellipse([10, 8, 14, 12], fill=head); d.point((13, 9), fill=eye)
            d.polygon([(13, 11), (15, 13), (13, 12)], fill=beak)
    else:
        d.polygon([(0, 7), (4, 8), (4, 10), (0, 10)], fill=wing)                       # tail, spread
        d.ellipse([3, 7, 11, 11], fill=body)
        d.ellipse([5, 9, 10, 11], fill=belly)
        d.ellipse([9, 5, 13, 9], fill=head); d.point((12, 6), fill=eye)
        d.polygon([(13, 7), (15, 7), (13, 8)], fill=beak)
        if pose == "up":
            d.polygon([(5, 8), (9, 8), (8, 1), (3, 2)], fill=wing)
            d.line([(4, 2), (8, 1)], fill=belly if kind == "bunting" else wing)
        else:
            d.polygon([(5, 9), (9, 9), (7, 15), (3, 14)], fill=wing)
    return outline(img)


def make_birds():
    poses = ("sit", "peck", "up", "down")
    kinds = ("sparrow", "crow", "bunting")
    sheet = Image.new("RGBA", (BW * len(poses), BH * len(kinds)), (0, 0, 0, 0))
    for r, k in enumerate(kinds):
        for c, pose in enumerate(poses):
            sheet.paste(bird_frame(k, pose), (c * BW, r * BH))
    sheet.save(os.path.join(OUT, "birds.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    make_tiles(); make_player(); make_parts(); make_garage(); make_decor(); make_pickups(); make_birds()
    print("art written to", os.path.abspath(OUT))
