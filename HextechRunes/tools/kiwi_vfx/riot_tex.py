"""英雄联盟 .tex 贴图解码成 Pillow 图像：BC1、BC3、BC7、BGRA8，取最大一级 mip。依赖 `Pillow`。"""
from __future__ import annotations

import struct

from PIL import Image

FORMATS = {
    10: ("BC1", "bcn", 1, 8),
    12: ("BC3", "bcn", 3, 16),
    13: ("BC7", "bcn", 7, 16),
    20: ("BGRA8", "raw", "BGRA", None),
}


def decode(data: bytes) -> tuple[Image.Image, str]:
    if data[:4] != b"TEX\0":
        raise ValueError("not a TEX file")
    width, height, _unknown, fmt, _resource, flags = struct.unpack_from("<HHBBBB", data, 4)
    if fmt not in FORMATS:
        raise ValueError(f"unsupported tex format {fmt}")
    name, decoder, argument, block_bytes = FORMATS[fmt]
    if block_bytes is None:
        size = width * height * 4
    else:
        size = max(1, (width + 3) // 4) * max(1, (height + 3) // 4) * block_bytes
    body = data[12:]
    # 有 mip 时由小到大存放，最大一级在末尾。
    pixels = body[-size:] if flags & 1 else body[:size]
    return Image.frombytes("RGBA", (width, height), pixels, decoder, argument), name
