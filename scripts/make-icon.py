#!/usr/bin/env python3
"""Packs PNGs into a multi-resolution .ico (PNG-compressed entries, Vista+)."""
import struct
import sys


def build(png_paths, out_path):
    entries, offset = [], 6 + 16 * len(png_paths)
    blobs = []
    for path in png_paths:
        with open(path, "rb") as handle:
            blob = handle.read()
        # IHDR width/height live at byte 16..24 of a PNG.
        width, height = struct.unpack(">II", blob[16:24])
        entries.append((0 if width >= 256 else width,
                        0 if height >= 256 else height,
                        len(blob), offset))
        blobs.append(blob)
        offset += len(blob)

    with open(out_path, "wb") as out:
        out.write(struct.pack("<HHH", 0, 1, len(png_paths)))  # reserved, type=icon, count
        for width, height, size, off in entries:
            out.write(struct.pack("<BBBBHHII", width, height, 0, 0, 1, 32, size, off))
        for blob in blobs:
            out.write(blob)


if __name__ == "__main__":
    build(sys.argv[2:], sys.argv[1])
