#!/usr/bin/env python3
"""Draw SlimClip's original video + compression icon with Pillow.

Run with the bundled or any Pillow-enabled Python interpreter. No external
images, fonts, or network resources are used. Outputs are placed beside this
script. The Windows ICO contains individually drawn PNG/alpha frames.
"""

from __future__ import annotations

from io import BytesIO
from pathlib import Path
import struct

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
SIZES = (16, 24, 32, 48, 256)
NAVY = "#10243A"
CYAN = "#48E9D6"
WHITE = "#FFFFFF"
AMBER = "#FFBD56"


def draw_icon(size: int) -> Image.Image:
    # Draw all sizes from the same 32-unit geometry, supersampling especially
    # small tray/menu frames so the play glyph and compression arrow survive.
    scale = max(4, (256 + size - 1) // size)
    canvas = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(canvas)
    unit = size * scale / 32

    def box(x0: float, y0: float, x1: float, y1: float) -> tuple[int, ...]:
        return tuple(round(x * unit) for x in (x0, y0, x1, y1))

    def poly(points: list[tuple[float, float]]) -> list[tuple[int, int]]:
        return [(round(x * unit), round(y * unit)) for x, y in points]

    def stroke_width(w: float) -> int:
        return max(1, round(w * unit))

    # Dark rounded tile maintains contrast on both light and dark taskbars;
    # its clipped corners remain transparent in every ICO frame.
    d.rounded_rectangle(box(0.65, 0.65, 31.35, 31.35), radius=round(6.0 * unit), fill=NAVY)

    # Cyan film silhouette: two perforations in the left ribbon, framed image
    # on the right. A single white play triangle spells out video at 16 px.
    d.rounded_rectangle(box(3.2, 6.1, 21.6, 25.9), radius=round(2.1 * unit),
                        outline=CYAN, width=stroke_width(2.0))
    d.rectangle(box(3.5, 6.5, 8.1, 25.5), fill=CYAN)
    d.rectangle(box(4.5, 9.0, 6.9, 11.7), fill=NAVY)
    d.rectangle(box(4.5, 20.3, 6.9, 23.0), fill=NAVY)
    d.polygon(poly([(11.0, 10.6), (11.0, 21.4), (18.9, 16.0)]), fill=WHITE)

    # Explicit inward arrow and a short shaft convey shrinking/compression,
    # rather than a generic second play triangle or a trademarked glyph.
    d.line(poly([(29.0, 16.0), (23.5, 16.0)]), fill=AMBER,
           width=stroke_width(2.4), joint="curve")
    d.polygon(poly([(25.0, 11.8), (20.7, 16.0), (25.0, 20.2),
                    (25.0, 17.6), (23.3, 16.0), (25.0, 14.4)]), fill=AMBER)

    return canvas.resize((size, size), Image.Resampling.LANCZOS)


def write_ico(frames: list[Image.Image], path: Path) -> None:
    """Store individually rendered frames as PNG-compressed Windows ICO data."""
    payloads = []
    for frame in frames:
        blob = BytesIO()
        frame.save(blob, format="PNG")
        payloads.append(blob.getvalue())
    offset = 6 + 16 * len(frames)
    with path.open("wb") as out:
        out.write(struct.pack("<HHH", 0, 1, len(frames)))
        for frame, payload in zip(frames, payloads):
            w, h = frame.size
            out.write(struct.pack("<BBBBHHII", w % 256, h % 256, 0, 0,
                                  1, 32, len(payload), offset))
            offset += len(payload)
        for payload in payloads:
            out.write(payload)


def main() -> None:
    frames = [draw_icon(size) for size in SIZES]
    frames[-1].save(HERE / "SlimClip.png", format="PNG")
    write_ico(frames, HERE / "SlimClip.ico")
    print("Generated SlimClip.png and SlimClip.ico:", ", ".join(map(str, SIZES)))


if __name__ == "__main__":
    main()
