"""Draws the mod's textures (needs Pillow). Run from anywhere:

    python Source/Art/make_textures.py

Writes, relative to the mod folder:
- Textures/EGP/Platform/High_{x}x{z}.png and Low_{x}x{z}.png for square footprints 1x1 to 4x4, 128 px per cell.
  Other sizes use the stretched 1x1 picture (DefGeneration.PlatformTexPath).
- Textures/EGP/UI/ShowFiringZone.png, the firing zone button (128 px).

Platform pictures are grey: RimWorld multiplies them by the material colour, as it does walls. Top-down view like
vanilla walls: a top face with one bolted plate per cell, a thin rim, and a front (south) face whose height shows the
platform's height: thin for the low platform, tall with support ribs for the high one. The game draws them the same
way up in every rotation (drawRotated false), so the front face always points down the screen.

Hand-drawn replacements can be dropped in with the same names and sizes; nothing else needs to change.
"""
import os
from PIL import Image, ImageDraw

PX = 128  # pixels per cell
SIZES = [(1, 1), (2, 2), (3, 3), (4, 4)]
MOD_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def platform(cells_x, cells_z, high):
    w, h = cells_x * PX, cells_z * PX
    s = PX / 128.0
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    face = int(PX * (0.30 if high else 0.12))  # front face, the same for every footprint size
    rim = max(2, int(5 * s))
    line = max(1, int(3 * s))
    top_h = h - face
    cell_h = top_h / cells_z

    # Top face and one bolted plate per cell.
    d.rectangle([0, 0, w - 1, top_h - 1], fill=(222, 222, 222, 255))
    inset, bolt, bolt_r = int(10 * s), int(9 * s), max(2, int(3 * s))
    for cx in range(cells_x):
        for cz in range(cells_z):
            x0, x1 = cx * PX + inset, (cx + 1) * PX - inset - 1
            y0, y1 = int(cz * cell_h) + inset, int((cz + 1) * cell_h) - inset - 1
            d.rectangle([x0, y0, x1, y1], outline=(176, 176, 176, 255), width=line)
            d.rectangle([x0 + line, y0 + line, x1 - line, y0 + 2 * line], fill=(232, 232, 232, 255))
            for bx, by in ((x0 + bolt, y0 + bolt), (x1 - bolt, y0 + bolt), (x0 + bolt, y1 - bolt), (x1 - bolt, y1 - bolt)):
                d.ellipse([bx - bolt_r, by - bolt_r, bx + bolt_r, by + bolt_r], fill=(156, 156, 156, 255))

    # Rim: light along the top and left, darker on the right.
    d.rectangle([0, 0, w - 1, rim - 1], fill=(242, 242, 242, 255))
    d.rectangle([0, 0, rim - 1, top_h - 1], fill=(236, 236, 236, 255))
    d.rectangle([w - rim, 0, w - 1, top_h - 1], fill=(188, 188, 188, 255))

    # Front face.
    d.rectangle([0, top_h, w - 1, h - 1], fill=(150, 150, 150, 255))
    d.line([0, top_h, w - 1, top_h], fill=(108, 108, 108, 255), width=line)
    if high:
        # Support ribs and a mid rail: reads as a taller face.
        n = cells_x * 3
        for i in range(1, n):
            x = i * w // n
            d.line([x, top_h + line, x, h - 2], fill=(126, 126, 126, 255), width=line)
        mid = top_h + face // 2
        d.line([0, mid, w - 1, mid], fill=(134, 134, 134, 255), width=max(1, line // 2))
    d.line([0, h - line, w - 1, h - line], fill=(94, 94, 94, 255), width=line)
    return im


def firing_zone_icon(size=128):
    s = size / 128.0
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    blue = (115, 205, 255, 255)
    dark = (40, 40, 40, 255)
    light = (235, 235, 235, 255)
    w3, w4, w5 = max(1, int(3 * s)), max(1, int(4 * s)), max(1, int(5 * s))
    # The zone: a faint wedge with a dashed edge, from the turret.
    cx, cy, r = 34 * s, 98 * s, 84 * s
    d.pieslice([cx - r, cy - r, cx + r, cy + r], 275, 355, fill=(115, 205, 255, 60))
    for a in range(272, 360, 8):
        d.arc([cx - r, cy - r, cx + r, cy + r], a, a + 5, fill=blue, width=w5)
    # Platform block with its front face.
    d.rectangle([14 * s, 96 * s, 54 * s, 122 * s], fill=(205, 205, 205, 255), outline=dark, width=w3)
    d.rectangle([14 * s, 114 * s, 54 * s, 122 * s], fill=(140, 140, 140, 255), outline=dark, width=w3)
    # Turret: base, and a barrel toward the zone.
    d.ellipse([22 * s, 90 * s, 46 * s, 112 * s], fill=light, outline=dark, width=w3)
    d.line([40 * s, 96 * s, 66 * s, 72 * s], fill=dark, width=max(1, int(9 * s)))
    d.line([40 * s, 96 * s, 66 * s, 72 * s], fill=light, width=w4)
    # A human inside the zone.
    hx, hy = 92 * s, 40 * s
    d.ellipse([hx - 7 * s, hy - 22 * s, hx + 7 * s, hy - 8 * s], fill=light, outline=dark, width=w3)
    d.rounded_rectangle([hx - 10 * s, hy - 7 * s, hx + 10 * s, hy + 20 * s], radius=max(1, int(6 * s)),
                        fill=light, outline=dark, width=w3)
    return im


def save(im, *parts):
    path = os.path.join(MOD_ROOT, "Textures", "EGP", *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    print(os.path.relpath(path, MOD_ROOT), im.size)


def main():
    for x, z in SIZES:
        save(platform(x, z, True), "Platform", "High_%dx%d.png" % (x, z))
        save(platform(x, z, False), "Platform", "Low_%dx%d.png" % (x, z))
    save(firing_zone_icon(128), "UI", "ShowFiringZone.png")


if __name__ == "__main__":
    main()
