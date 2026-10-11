"""英雄联盟 WAD v3（*.wad.client）只读解包：按路径哈希读条目，支持 zstd 与分块 zstd。

路径哈希是小写路径的 xxh64。依赖 `xxhash`、`zstandard`。
"""
from __future__ import annotations

import re
import struct
import zlib
from pathlib import Path

import xxhash
import zstandard

HEADER_SIZE = 4 + 256 + 8
ENTRY = struct.Struct("<QIIIBBHQ")


def path_hash(path: str) -> int:
    return xxhash.xxh64(path.lower().encode()).intdigest()


class Wad:
    def __init__(self, path: Path):
        self.path = Path(path)
        self._file = self.path.open("rb")
        magic = self._file.read(4)
        if magic[:2] != b"RW" or magic[2] < 3:
            raise ValueError(f"not a WAD v3 file: {self.path}")
        self._file.seek(HEADER_SIZE)
        (count,) = struct.unpack("<I", self._file.read(4))
        self.entries: dict[int, tuple[int, int, int, int, int]] = {}
        for _ in range(count):
            ph, offset, compressed, size, kind, _dup, first_subchunk, _checksum = ENTRY.unpack(self._file.read(ENTRY.size))
            self.entries[ph] = (offset, compressed, size, kind, first_subchunk)
        self._subchunks: list[tuple[int, int, int]] = []
        # 分块条目的块表：WAD 相对 Game 目录的路径，去掉 .client，加 .subchunktoc。
        match = re.search(r"(data[\\/]final[\\/].*)\.client$", str(self.path), re.I)
        if match:
            toc = path_hash(match.group(1).replace("\\", "/") + ".subchunktoc")
            if toc in self.entries:
                raw = self.read(toc)
                self._subchunks = [struct.unpack_from("<IIQ", raw, i) for i in range(0, len(raw), 16)]

    def close(self) -> None:
        self._file.close()

    def __enter__(self) -> "Wad":
        return self

    def __exit__(self, *_exc) -> None:
        self.close()

    def has(self, path: str) -> bool:
        return path_hash(path) in self.entries

    def read_path(self, path: str) -> bytes:
        return self.read(path_hash(path))

    def read(self, ph: int) -> bytes:
        offset, compressed, size, kind, first_subchunk = self.entries[ph]
        self._file.seek(offset)
        data = self._file.read(compressed)
        kind_type = kind & 0xF
        if kind_type == 0:
            return data
        if kind_type == 1:
            return zlib.decompress(data, 47)
        if kind_type == 3:
            return zstandard.ZstdDecompressor().decompress(data, max_output_size=size)
        if kind_type == 4:
            parts = []
            position = 0
            for i in range(kind >> 4):
                chunk_compressed, chunk_size, _ = self._subchunks[first_subchunk + i]
                chunk = data[position:position + chunk_compressed]
                position += chunk_compressed
                parts.append(chunk if chunk_compressed == chunk_size
                             else zstandard.ZstdDecompressor().decompress(chunk, max_output_size=chunk_size))
            return b"".join(parts)
        raise ValueError(f"unsupported WAD entry type {kind_type}")

    def iter_bins(self, max_size: int = 8 << 20):
        """遍历所有 PROP/PTCH 条目（ph, bytes）；无法解压的条目跳过。"""
        for ph, (_offset, _compressed, size, _kind, _first) in self.entries.items():
            if size < 8 or size > max_size:
                continue
            try:
                data = self.read(ph)
            except Exception:
                continue
            if data[:4] in (b"PROP", b"PTCH"):
                yield ph, data
