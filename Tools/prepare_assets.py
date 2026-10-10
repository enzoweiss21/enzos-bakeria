"""
Enzo's Bakery - asset preparation script.

Cuts the individual sprites the game uses out of the free (CC0) sprite sheets,
draws the handful of sprites I could not find online (flour sack, chef hat,
trash can, checkered floor, UI bits), and trims / normalizes the sound effects.

Everything is written into Assets/Art and Assets/Audio so Unity only ever sees
small, clearly named files. Credits for every pack are listed in CREDITS.md.

Usage:
    python3 Tools/prepare_assets.py <folder with the downloaded packs>

The downloaded packs are not committed to Git (they are big and unchanged);
only the processed output is.
"""
import os
import sys
import wave

import numpy as np
from PIL import Image, ImageEnhance

RAW = sys.argv[1]
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "Art")
AUDIO = os.path.join(PROJECT, "Assets", "Audio")

RPG_SHEET = os.path.join(RAW, "kenney_roguelike-rpg-pack/Spritesheet/roguelikeSheet_transparent.png")
INDOOR_SHEET = os.path.join(RAW, "kenney_roguelike-indoors/Tilesheets/roguelikeIndoor_transparent.png")
CHAR_SHEET = os.path.join(RAW, "kenney_roguelike-characters/Spritesheet/roguelikeChar_transparent.png")
FOOD_ICONS = os.path.join(RAW, "oga/Food_1.png")                 # thekingphoenix, 32px icons
FOOD_BLUECARROT = os.path.join(RAW, "oga/food-bluecarrot16_0.png")  # bluecarrot16, 32px icons
CAT = os.path.join(RAW, "cats/pixelcat.png")                     # "Pixel Cat", CC0
PLATFORMER_FOOD = os.path.join(RAW, "kenney_pixel-platformer-food-expansion/Tiles")
UI_SMALL = os.path.join(RAW, "kenney_ui-pack-pixel-adventure/Tiles/Small tiles/Thick outline")
UI_LARGE = os.path.join(RAW, "kenney_ui-pack-pixel-adventure/Tiles/Large tiles/Thick outline")


# --------------------------------------------------------------------------- helpers

def out(folder, name, img):
    path = os.path.join(ART, folder)
    os.makedirs(path, exist_ok=True)
    img.save(os.path.join(path, name + ".png"))


def cell(sheet, col, row, size=16, gap=1):
    """Crop one tile from a sheet laid out on a (size + gap) grid."""
    step = size + gap
    return sheet.crop((col * step, row * step, col * step + size, row * step + size))


def stack(top, bottom):
    """Put two 16px tiles on top of each other (for 2-tile tall objects)."""
    img = Image.new("RGBA", (top.width, top.height + bottom.height))
    img.alpha_composite(top, (0, 0))
    img.alpha_composite(bottom, (0, top.height))
    return img


def side_by_side(*tiles):
    img = Image.new("RGBA", (sum(t.width for t in tiles), tiles[0].height))
    x = 0
    for t in tiles:
        img.alpha_composite(t, (x, 0))
        x += t.width
    return img


def from_ascii(rows, palette):
    """Draw pixel art from a list of strings. Each character is a palette key, '.' is clear."""
    img = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
    px = img.load()
    for y, line in enumerate(rows):
        for x, ch in enumerate(line):
            if ch != ".":
                px[x, y] = palette[ch]
    return img


def recolor(img, fn):
    """Apply fn(r, g, b) -> (r, g, b) to every visible pixel."""
    img = img.copy()
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a > 0:
                nr, ng, nb = fn(r, g, b)
                px[x, y] = (int(nr), int(ng), int(nb), a)
    return img


def doughify(img):
    """Turn a baked item into its raw version: pale, creamy, a little desaturated."""
    def f(r, g, b):
        lum = 0.3 * r + 0.59 * g + 0.11 * b
        # Keep dark outlines dark so the shape still reads, lighten everything else
        if lum < 60:
            return (r * 0.9 + 40, g * 0.9 + 35, b * 0.9 + 30)
        t = lum / 255.0
        return (205 + 45 * t, 185 + 50 * t, 150 + 60 * t)
    return recolor(img, f)


def trim(img):
    box = img.getbbox()
    return img.crop(box) if box else img


def pad_square(img, size):
    """Center an image on a transparent square canvas."""
    canvas = Image.new("RGBA", (size, size))
    canvas.alpha_composite(img, ((size - img.width) // 2, (size - img.height) // 2))
    return canvas


# --------------------------------------------------------------------------- tiles & furniture

def build_environment():
    rpg = Image.open(RPG_SHEET).convert("RGBA")
    indoor = Image.open(INDOOR_SHEET).convert("RGBA")

    # Floors: wooden planks for the shop side, a classic checkered tile for the kitchen
    out("Tiles", "floor_wood", cell(rpg, 8, 2))
    out("Tiles", "floor_wood_alt", cell(rpg, 8, 3))
    checker = Image.new("RGBA", (16, 16))
    px = checker.load()
    cream, cream_dark = (238, 226, 200, 255), (226, 212, 184, 255)
    red, red_dark = (176, 92, 72, 255), (160, 82, 64, 255)
    for y in range(16):
        for x in range(16):
            light = ((x // 8) + (y // 8)) % 2 == 0
            c = cream if light else red
            if x % 8 == 7 or y % 8 == 7:          # grout lines
                c = cream_dark if light else red_dark
            px[x, y] = c
    out("Tiles", "floor_kitchen", checker)

    # Walls (beige plaster from the RPG pack)
    out("Tiles", "wall_top", cell(rpg, 14, 13))
    out("Tiles", "wall_face", cell(rpg, 15, 15))
    out("Tiles", "wall_face_low", cell(rpg, 15, 16))
    out("Tiles", "wall_side", cell(rpg, 13, 13))

    # Door the customers walk through, windows that show the sky outside
    out("Furniture", "door_open", stack(cell(rpg, 36, 1), cell(rpg, 36, 2)))
    window = stack(cell(rpg, 44, 2), cell(rpg, 44, 3))
    out("Furniture", "window", cut_window_panes(window))

    # Kitchen counters (grey stone tops) and the wooden service counter
    out("Furniture", "counter", cell(indoor, 1, 12))
    out("Furniture", "counter_left", cell(indoor, 0, 12))
    out("Furniture", "counter_right", cell(indoor, 2, 12))
    out("Furniture", "counter_drawers", cell(indoor, 1, 13))
    out("Furniture", "counter_sink", cell(indoor, 8, 12))
    out("Furniture", "service_counter", cell(indoor, 1, 15))
    out("Furniture", "service_counter_end", cell(indoor, 0, 15))

    # Ovens: unlit, and two lit frames to flicker between
    out("Furniture", "oven_off", cell(rpg, 12, 0))
    out("Furniture", "oven_on_a", cell(rpg, 13, 0))
    out("Furniture", "oven_on_b", cell(rpg, 14, 0))

    # Crates for the ingredient stations, a bowl for the mixing station
    out("Furniture", "crate", cell(rpg, 15, 7))
    out("Furniture", "bowl", cell(rpg, 55, 16))

    # Decoration
    out("Decor", "plant_a", cell(indoor, 16, 0))
    out("Decor", "plant_b", cell(indoor, 17, 0))
    out("Decor", "cuckoo_clock", cell(rpg, 27, 8))
    out("Decor", "painting_a", cell(indoor, 19, 12))
    out("Decor", "painting_b", cell(indoor, 20, 13))
    out("Decor", "shelf", cell(rpg, 41, 13))
    out("Decor", "table_round", cell(indoor, 7, 0))
    out("Decor", "chair", cell(indoor, 1, 2))
    out("Decor", "rug", side_by_side(cell(rpg, 10, 16), cell(rpg, 11, 16), cell(rpg, 12, 16)))
    out("Decor", "candle", cell(rpg, 17, 6))
    out("Decor", "coins", cell(rpg, 41, 11))


def cut_window_panes(window):
    """Make the dark glass of the window transparent so a sky sprite can show behind it."""
    window = window.copy()
    px = window.load()
    for y in range(window.height):
        for x in range(window.width):
            r, g, b, a = px[x, y]
            if a > 0 and r == g == b and r < 80:     # the flat grey glass
                px[x, y] = (0, 0, 0, 0)
    return window


# --------------------------------------------------------------------------- characters

# Customers are built at runtime from these paper-doll layers (Kenney Roguelike Characters)
BODIES = [(0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2)]
PANTS = [(3, r) for r in range(0, 10)]
SHOES = [(4, r) for r in range(0, 10)]
SHIRTS = [(c, r) for r in range(0, 10) for c in range(6, 18)]
HAIR = [(19, 0), (20, 0), (19, 1), (20, 1), (21, 1), (22, 1), (21, 2),
        (23, 0), (24, 0), (23, 1), (24, 1), (25, 1), (26, 1), (25, 2),
        (19, 4), (20, 4), (19, 5), (20, 5), (21, 5), (22, 5), (21, 6),
        (23, 4), (24, 4), (23, 5), (24, 5), (25, 5), (26, 5), (25, 6),
        (19, 8), (20, 8), (19, 9), (20, 9), (21, 9), (22, 9), (21, 10)]
BEARDS = [(22, 0), (26, 0), (22, 4), (26, 4), (22, 8)]
HATS = [(28, 8), (29, 8), (30, 8), (31, 8)]


def build_characters():
    sheet = Image.open(CHAR_SHEET).convert("RGBA")

    def export(group, coords):
        n = 0
        for (c, r) in coords:
            tile = cell(sheet, c, r)
            if tile.getbbox() is None:      # skip empty slots in the sheet
                continue
            out(os.path.join("Characters", group), f"{group}_{n:02d}", tile)
            n += 1
        return n

    counts = {g: export(g, c) for g, c in
              [("body", BODIES), ("pants", PANTS), ("shoes", SHOES), ("shirt", SHIRTS),
               ("hair", HAIR), ("beard", BEARDS), ("hat", HATS)]}
    print("character layers:", counts)

    # Enzo the baker: white shirt, dark pants, mustache, and a chef hat I drew
    baker = Image.new("RGBA", (16, 16))
    for (c, r) in [(0, 0), (3, 4), (4, 4), (12, 4), (21, 0)]:
        baker.alpha_composite(cell(sheet, c, r))
    out("Characters", "baker", baker)
    out("Characters", "chef_hat", chef_hat())


def chef_hat():
    pal = {"o": (60, 50, 50, 255), "w": (250, 250, 245, 255), "s": (215, 215, 210, 255)}
    rows = [
        "................",
        "....oooooooo....",
        "...owwwwwwwwo...",
        "..owwwwwswwwwo..",
        "..owwwswwwwswo..",
        "..owwwwwwwwwwo..",
        "...owwwwwwwwo...",
        "....owwwwwwo....",
        "....osssssso....",
        "....oooooooo....",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
    ]
    return from_ascii(rows, pal)


# --------------------------------------------------------------------------- food

def build_food():
    icons = Image.open(FOOD_ICONS).convert("RGBA")
    bc = Image.open(FOOD_BLUECARROT).convert("RGBA")

    def icon(c, r):
        return cell(icons, c, r, 32, 0)

    bread = icon(1, 2)
    cupcake = icon(0, 2)
    chocolate = icon(4, 4)
    cherries = icon(0, 1)
    cookie = add_choc_chips(cookie_base(bc))

    out("Food", "bread", bread)
    out("Food", "cupcake", cupcake)
    out("Food", "cupcake_frosted", frost(cupcake))
    out("Food", "cookies", cookie)
    out("Food", "chocolate", chocolate)
    out("Food", "berries", cherries)
    out("Food", "flour", flour_sack())

    out("Furniture", "frosting_tub", frosting_tub())

    out("Food", "dough_bread", doughify(bread))
    out("Food", "dough_cookie", add_choc_chips(doughify(cookie_base(bc))))
    out("Food", "batter_cupcake", cupcake_batter(cupcake))


def cookie_base(bc):
    """The plain round cookie, scaled up 2x so it's as big as the other 32px food icons."""
    small = trim(cell(bc, 0, 12, 32, 0))
    big = small.resize((small.width * 2, small.height * 2), Image.NEAREST)
    return pad_square(big, 32)


def add_choc_chips(cookie):
    """Stud the cookie with 2x2 chocolate chips (2x2 to match the doubled-up cookie pixels)."""
    cookie = cookie.copy()
    px = cookie.load()
    dark, light = (70, 40, 25, 255), (110, 70, 45, 255)
    for (x, y) in [(10, 9), (17, 8), (22, 13), (12, 16), (19, 19), (8, 21), (23, 21), (15, 24)]:
        if px[x, y][3] == 0 or px[x + 1, y + 1][3] == 0:
            continue
        px[x, y] = light
        px[x + 1, y] = dark
        px[x, y + 1] = dark
        px[x + 1, y + 1] = dark
    return cookie


def frost(cupcake):
    """Paint pink frosting over the top half of the cupcake and sprinkle it."""
    img = cupcake.copy()
    px = img.load()
    box = img.getbbox()
    top, bottom = box[1], box[3]
    cut = top + (bottom - top) * 0.55
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0 or y > cut:
                continue
            lum = 0.3 * r + 0.59 * g + 0.11 * b
            if lum < 50:
                px[x, y] = (110, 40, 70, a)            # outline becomes dark berry
            else:
                t = min(1.0, lum / 200.0)
                px[x, y] = (int(230 + 25 * t), int(120 + 90 * t), int(170 + 60 * t), a)
    sprinkle = [(255, 240, 120, 255), (120, 210, 255, 255), (255, 255, 255, 255)]
    for i, (x, y) in enumerate([(12, 9), (17, 8), (20, 11), (14, 13), (19, 14), (10, 12)]):
        if px[x, y][3] > 0 and y <= cut:
            px[x, y] = sprinkle[i % 3]
    return img


def cupcake_batter(cupcake):
    """Keep the paper cup, but the top is pale, unbaked batter."""
    img = cupcake.copy()
    px = img.load()
    box = img.getbbox()
    cut = box[1] + (box[3] - box[1]) * 0.55
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0 or y > cut:
                continue
            lum = 0.3 * r + 0.59 * g + 0.11 * b
            if lum < 50:
                px[x, y] = (120, 100, 80, a)
            else:
                t = lum / 255.0
                px[x, y] = (int(225 + 25 * t), int(205 + 30 * t), int(160 + 50 * t), a)
    return img


def frosting_tub():
    """A tub of pink frosting with a swirl on top, for the frosting station."""
    pal = {"o": (90, 40, 60, 255), "p": (240, 130, 175, 255), "P": (255, 200, 225, 255),
           "w": (245, 245, 250, 255), "g": (185, 200, 215, 255), "d": (140, 155, 170, 255)}
    rows = [
        "................",
        "................",
        "................",
        "................",
        ".......oo.......",
        "......oPpo......",
        ".....oPpppo.....",
        "....oppPpppo....",
        "...opppppPppo...",
        "..owwwwwwwwwwo..",
        "..oggggggggggo..",
        "..ogdgggggdggo..",
        "...oggggggggo...",
        "...oddddddddo...",
        "....oooooooo....",
        "................",
    ]
    return from_ascii(rows, pal)


def flour_sack():
    pal = {"o": (70, 50, 40, 255), "b": (222, 205, 170, 255), "s": (190, 170, 135, 255),
           "w": (250, 248, 240, 255), "y": (215, 160, 60, 255), "t": (140, 100, 60, 255)}
    rows = [
        "................................",
        "................................",
        "................................",
        "............o..o..o.............",
        "...........owowowowo............",
        "..........owwwwwwwwwo...........",
        "...........ottttttto............",
        "..........obbbbbbbbbo...........",
        ".........obbbbbbbbbbbo..........",
        "........obbbbbbbbbbbbbo.........",
        ".......obbbbbbbbbbbbbbbo........",
        "......obbbbbbbbbbbbbbbbbo.......",
        "......obbbbbbbbbbbbbbbbbo.......",
        ".....obbbbbbbbbybbbbbbbbbo......",
        ".....obbbbbbbbyybbbbbbbbbo......",
        ".....obbbbbbyybyybbbbbbbbo......",
        ".....obbbbbbbyyybbbbbbbbbo......",
        ".....obbbbbbyybyybbbbbbbbo......",
        ".....obbbbbbbyyybbbbbbbbbo......",
        ".....obbbbbbbbtbbbbbbbbbbo......",
        ".....obbbbbbbbbbbbbbbbbbbo......",
        ".....osbbbbbbbbbbbbbbbbbso......",
        ".....ossbbbbbbbbbbbbbbbsso......",
        "......ossssssssssssssssso.......",
        ".......ooooooooooooooooo........",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
        "................................",
    ]
    return from_ascii(rows, pal)


# --------------------------------------------------------------------------- the bakery cat

def build_cat():
    """Recolor the grey CC0 cat into Biscuit, an orange tabby."""
    cat = trim(Image.open(CAT).convert("RGBA"))

    def orange(r, g, b):
        lum = (0.3 * r + 0.59 * g + 0.11 * b) / 255.0
        if lum < 0.25:                      # outline and face stay dark
            return (60 + 40 * lum, 35 + 30 * lum, 25 + 20 * lum)
        if lum > 0.85:                      # eye highlights stay white
            return (r, g, b)
        return (150 + 110 * lum, 80 + 100 * lum, 30 + 70 * lum)
    out("Characters", "cat", recolor(cat, orange))


# --------------------------------------------------------------------------- UI & effects

def build_ui():
    def small(n):
        return Image.open(os.path.join(UI_SMALL, f"tile_{n:04d}.png")).convert("RGBA")

    def large(n):
        return Image.open(os.path.join(UI_LARGE, f"tile_{n:04d}.png")).convert("RGBA")

    out("UI", "panel_cream", large(0))
    out("UI", "panel_wood", large(1))
    out("UI", "panel_cream_rivets", large(18))
    out("UI", "ribbon", side_by_side(large(43), large(44), large(45)))
    out("UI", "button_cream", small(10))
    out("UI", "button_wood", small(11))
    out("UI", "bubble", speech_bubble())
    out("UI", "coin", small(76))
    out("UI", "alert", small(77))
    out("UI", "plus", small(78))

    # Simple shapes drawn here: a bar frame/fill for patience and progress bars
    out("UI", "bar_frame", from_ascii(["oooooooooooooooo",
                                       "o..............o",
                                       "o..............o",
                                       "oooooooooooooooo"], {"o": (50, 35, 30, 255)}))
    out("UI", "white", Image.new("RGBA", (16, 16), (255, 255, 255, 255)))

    out("FX", "arrow_down", from_ascii([
        "..oooooo..",
        "..owwwwo..",
        "..owwwwo..",
        "oooowwoooo",
        "owwwwwwwwo",
        ".owwwwwwo.",
        "..owwwwo..",
        "...owwo...",
        "....oo....",
    ], {"o": (60, 40, 30, 255), "w": (255, 240, 160, 255)}))
    out("FX", "heart", from_ascii([
        ".oo.oo.",
        "orrorro",
        "orrrrro",
        ".orrro.",
        "..oro..",
        "...o...",
    ], {"o": (90, 20, 30, 255), "r": (240, 80, 100, 255)}))
    out("FX", "sparkle", from_ascii([
        "...y...",
        "...y...",
        "..yWy..",
        "yyWWWyy",
        "..yWy..",
        "...y...",
        "...y...",
    ], {"y": (255, 220, 90, 255), "W": (255, 255, 230, 255)}))
    out("FX", "puff", from_ascii([
        "..oooo..",
        ".owwwwo.",
        "owwwwwwo",
        "owwwwwwo",
        "owwwwwwo",
        ".owwwwo.",
        "..oooo..",
    ], {"o": (200, 200, 200, 255), "w": (245, 245, 245, 255)}))
    out("FX", "star", from_ascii([
        "....o....",
        "...oyo...",
        "ooooyoooo",
        "oyyyyyyyo",
        ".oyyyyyo.",
        "..oyyyo..",
        ".oyyoyyo.",
        ".oyo.oyo.",
        ".oo...oo.",
    ], {"o": (120, 70, 20, 255), "y": (255, 210, 60, 255)}))
    out("FX", "zzz", from_ascii([
        "ooooo",
        "...o.",
        "..o..",
        ".o...",
        "ooooo",
    ], {"o": (255, 255, 255, 255)}))
    out("FX", "shadow", from_ascii([
        "..oooooooooo..",
        "oooooooooooooo",
        "..oooooooooo..",
    ], {"o": (0, 0, 0, 255)}))
    out("FX", "check", from_ascii([
        "........oo",
        ".......ogo",
        "......ogo.",
        "oo...ogo..",
        "ogo.ogo...",
        ".ogogo....",
        "..ogo.....",
        "...o......",
    ], {"o": (30, 70, 30, 255), "g": (110, 220, 90, 255)}))
    out("FX", "selector", from_ascii([
        "wwww..........wwww",
        "w................w",
        "w................w",
        "w................w",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "..................",
        "w................w",
        "w................w",
        "w................w",
        "wwww..........wwww",
    ], {"w": (255, 250, 220, 255)}))
    out("FX", "clock", from_ascii([
        "..oooo..",
        ".owwwwo.",
        "owwbwwwo",
        "owwbwwwo",
        "owwbbbwo",
        "owwwwwwo",
        ".owwwwo.",
        "..oooo..",
    ], {"o": (60, 40, 30, 255), "w": (255, 245, 210, 255), "b": (200, 60, 50, 255)}))
    out("FX", "lock", from_ascii([
        "..oooo..",
        ".o....o.",
        ".o....o.",
        "oooooooo",
        "oyyyyyyo",
        "oyyooyyo",
        "oyyooyyo",
        "oyyyyyyo",
        "oooooooo",
    ], {"o": (60, 45, 30, 255), "y": (240, 200, 70, 255)}))
    out("UI", "icon_shoe", from_ascii([
        "................",
        "................",
        "................",
        "....ooooo.......",
        "....orrro.......",
        "....orwro.......",
        "....orrroo......",
        "....orwrrroo....",
        "....orrrrrrroo..",
        "...orrrrrrrrrro.",
        "...owwwwwwwwwwo.",
        "...oooooooooooo.",
        "................",
        "................",
        "................",
        "................",
    ], {"o": (50, 30, 30, 255), "r": (220, 70, 60, 255), "w": (245, 245, 245, 255)}))
    out("FX", "trash_can", trash_can())
    out("FX", "sky", Image.new("RGBA", (4, 4), (255, 255, 255, 255)))


def speech_bubble():
    pal = {"o": (70, 45, 35, 255), "w": (255, 250, 235, 255), "s": (230, 220, 200, 255)}
    rows = [
        "...oooooooooooooo...",
        "..owwwwwwwwwwwwwwo..",
        ".owwwwwwwwwwwwwwwwo.",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "owwwwwwwwwwwwwwwwwwo",
        "osswwwwwwwwwwwwwwsso",
        ".osssssssssssssssso.",
        "..oooosssoooooooo...",
        "......ossso.........",
        ".......osso.........",
        "........oo..........",
    ]
    return from_ascii(rows, pal)


def trash_can():
    pal = {"o": (40, 45, 55, 255), "g": (150, 160, 170, 255), "l": (195, 205, 215, 255),
           "d": (110, 120, 130, 255)}
    rows = [
        "................",
        "......oooo......",
        "..oooooooooooo..",
        "..ollllllllllo..",
        "..oooooooooooo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oglgdglgdgo..",
        "...oddddddddo...",
        "....oooooooo....",
        "................",
    ]
    rows = [r.ljust(16, ".")[:16] for r in rows]
    return from_ascii(rows, pal)


# --------------------------------------------------------------------------- audio

def read_wav(path):
    w = wave.open(path)
    sr = w.getframerate()
    x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
    if w.getnchannels() == 2:
        x = x.reshape(-1, 2).mean(axis=1)
    return x, sr


def write_wav(path, x, sr):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.clip(x, -1, 1)
    w = wave.open(path, "wb")
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(sr)
    w.writeframes((x * 32767).astype(np.int16).tobytes())
    w.close()


def clip_sound(src, dst, start=0.0, end=None, peak=0.9, fade_in=0.005, fade_out=0.05, loop=False):
    """Cut [start, end] seconds out of a recording, normalize it and fade the edges."""
    x, sr = read_wav(src)
    a = int(start * sr)
    b = len(x) if end is None else min(len(x), int(end * sr))
    x = x[a:b].copy()
    if loop:
        # Crossfade the tail into the head so the clip loops without a click
        n = int(0.25 * sr)
        head, tail = x[:n].copy(), x[-n:].copy()
        ramp = np.linspace(0, 1, n)
        x = x[:-n]
        x[:n] = head * ramp + tail * (1 - ramp)
    else:
        n_in, n_out = int(fade_in * sr), int(fade_out * sr)
        if n_in > 0:
            x[:n_in] *= np.linspace(0, 1, n_in)
        if n_out > 0:
            x[-n_out:] *= np.linspace(1, 0, n_out)
    m = np.abs(x).max()
    if m > 0:
        x *= peak / m
    write_wav(dst, x, sr)


def build_audio():
    fs = os.path.join(RAW, "freesound")
    sfx = os.path.join(AUDIO, "SFX")
    clip_sound(f"{fs}/cash_register.wav", f"{sfx}/cash_register.wav", 0.0, 1.3, peak=0.8, fade_out=0.3)
    clip_sound(f"{fs}/cat_meow_a.wav", f"{sfx}/cat_meow_a.wav", peak=0.7)
    clip_sound(f"{fs}/cat_meow_b.wav", f"{sfx}/cat_meow_b.wav", peak=0.7)
    clip_sound(f"{fs}/cat_meow_c.wav", f"{sfx}/cat_meow_c.wav", peak=0.7)
    clip_sound(f"{fs}/cat_purr.wav", f"{sfx}/cat_purr.wav", peak=0.6, fade_out=0.4)
    clip_sound(f"{fs}/shop_bell.wav", f"{sfx}/shop_bell.wav", peak=0.7, fade_out=0.2)
    clip_sound(f"{fs}/timer_done_src.wav", f"{sfx}/oven_ding.wav", 2.0, 4.5, peak=0.6, fade_out=0.8)
    clip_sound(f"{fs}/toaster_ding_src.wav", f"{sfx}/perfect_ding.wav", 50.05, 51.6, peak=0.7, fade_out=0.6)
    clip_sound(f"{fs}/oven_door_src.wav", f"{sfx}/oven_door.wav", 2.75, 3.6, peak=0.7, fade_out=0.2)
    clip_sound(f"{fs}/whisk_src.wav", f"{sfx}/whisk_loop.wav", 6.0, 9.25, peak=0.6, loop=True)
    clip_sound(f"{fs}/oven_fire_src.wav", f"{sfx}/oven_fire_loop.wav", 2.0, 8.25, peak=0.6, loop=True)
    clip_sound(f"{fs}/sizzle_src.wav", f"{sfx}/burnt_sizzle.wav", 0.0, 1.8, peak=0.7, fade_out=0.6)
    clip_sound(f"{fs}/cafe_src.wav", f"{sfx}/cafe_ambience_loop.wav", 20.0, 40.25, peak=0.5, loop=True)

    # Kenney sounds are already clean, they are only renamed and copied
    kenney = {
        "kenney_interface-sounds/Audio/drop_002.ogg": "put_down.ogg",
        "kenney_interface-sounds/Audio/pluck_001.ogg": "pick_up.ogg",
        "kenney_interface-sounds/Audio/error_004.ogg": "error.ogg",
        "kenney_interface-sounds/Audio/glass_002.ogg": "sparkle.ogg",
        "kenney_interface-sounds/Audio/confirmation_002.ogg": "confirm.ogg",
        "kenney_interface-sounds/Audio/tick_002.ogg": "voice_blip.ogg",
        "kenney_interface-sounds/Audio/question_001.ogg": "customer_order.ogg",
        "kenney_interface-sounds/Audio/bong_001.ogg": "warning_beep.ogg",
        "kenney_interface-sounds/Audio/scratch_002.ogg": "frosting.ogg",
        "kenney_interface-sounds/Audio/back_002.ogg": "angry_leave.ogg",
        "kenney_interface-sounds/Audio/maximize_006.ogg": "upgrade_buy.ogg",
        "kenney_ui-audio/Audio/click3.ogg": "ui_click.ogg",
        "kenney_ui-audio/Audio/rollover2.ogg": "ui_hover.ogg",
        "kenney_rpg-audio/Audio/cloth2.ogg": "grab_flour.ogg",
        "kenney_rpg-audio/Audio/handleSmallLeather.ogg": "grab_soft.ogg",
        "kenney_rpg-audio/Audio/metalPot1.ogg": "bowl_add.ogg",
        "kenney_rpg-audio/Audio/metalPot3.ogg": "bowl_done.ogg",
        "kenney_rpg-audio/Audio/bookFlip2.ogg": "take_order.ogg",
        "kenney_rpg-audio/Audio/handleCoins.ogg": "coins.ogg",
        "kenney_rpg-audio/Audio/doorClose_1.ogg": "door_close.ogg",
        "kenney_impact-sounds/Audio/footstep_wood_001.ogg": "footstep_a.ogg",
        "kenney_impact-sounds/Audio/footstep_wood_003.ogg": "footstep_b.ogg",
        "kenney_impact-sounds/Audio/impactMetal_light_002.ogg": "trash.ogg",
        "kenney_impact-sounds/Audio/impactSoft_medium_001.ogg": "plop.ogg",
        "kenney_impact-sounds/Audio/impactBell_heavy_001.ogg": "service_bell.ogg",
        "kenney_casino-audio/Audio/chips-stack-2.ogg": "tip_jar.ogg",
        "kenney_music-jingles/Audio/Pizzicato jingles/jingles_PIZZI00.ogg": "jingle_day_start.ogg",
        "kenney_music-jingles/Audio/Pizzicato jingles/jingles_PIZZI10.ogg": "jingle_win.ogg",
        "kenney_music-jingles/Audio/Pizzicato jingles/jingles_PIZZI07.ogg": "jingle_lose.ogg",
        "kenney_music-jingles/Audio/Pizzicato jingles/jingles_PIZZI03.ogg": "jingle_rush.ogg",
        "kenney_music-jingles/Audio/Pizzicato jingles/jingles_PIZZI15.ogg": "jingle_victory.ogg",
    }
    import shutil
    for src, dst in kenney.items():
        path = os.path.join(RAW, src)
        if not os.path.exists(path):
            # Pack folder names differ a little between releases, so search for the file
            name = os.path.basename(src)
            pack = src.split("/")[0]
            for root, _, files in os.walk(os.path.join(RAW, pack)):
                if name in files:
                    path = os.path.join(root, name)
                    break
        shutil.copyfile(path, os.path.join(sfx, dst))


if __name__ == "__main__":
    build_environment()
    build_characters()
    build_food()
    build_cat()
    build_ui()
    build_audio()
    print("done")
