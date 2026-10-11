#!/usr/bin/env python3
"""从英雄联盟 UI.wad.client 重新导出海克斯选择界面复刻用的原版特效（海克斯大乱斗 KiwiAugmentSelection）。

默认只读：解包、解析、解码后与仓库里的现有文件比对并报告差异，不写任何文件。
加 --write 才写入：
  assets/images/effects/kiwi_selection/kiwi_selection_vfx.json 与其引用的贴图 PNG（像素未变的 PNG 不重写），
  tools/kiwi_vfx/reference/ 下的原版界面数据转储（uibase.json、particle_systems.json）。
依赖：pip install xxhash zstandard pillow。出处与换算见 assets/images/effects/kiwi_selection/SOURCE.md。
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops

sys.path.insert(0, str(Path(__file__).resolve().parent))
import riot_bin  # noqa: E402
import riot_tex  # noqa: E402
from riot_wad import Wad  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
IMAGES = ROOT / "assets/images"
DATA_FILE = IMAGES / "effects/kiwi_selection/kiwi_selection_vfx.json"
REFERENCE = Path(__file__).resolve().parent / "reference"

PARTICLES_PREFIX = "ClientStates/Gameplay/UX/LoL/Kiwi/KiwiAugmentSelection/Particles/"
UIBASE_PREFIX = "ClientStates/Gameplay/UX/LoL/Kiwi/KiwiAugmentSelection/UIBase/"
# 16.20 的条目路径哈希；版本更新后对不上时退回全量扫描。
KNOWN_PARTICLES_BIN = 0x51A58C4E83212753
KNOWN_UIBASE_BIN = 0xB577A497B41D91E3

BUTTON_SYSTEMS = ["Augment_GoldReroll", "Augment_GoldReroll_Click"]
# 界面接入的系统；与 HextechRuneSelectionScreen.Vfx 和测试 KiwiSelectionVfxDataIsCompleteAndAssetsExist 一致。
SYSTEMS = BUTTON_SYSTEMS + [
    f"Augment_{tier}_{kind}"
    for kind in ("FlashInVFX", "RefreshVFX", "RefreshOverlayVFX", "SelectedVFX", "HoverVFX", "IdleVFX")
    for tier in ("Silver", "Gold", "Prismatic")] + ["Augment_StaticVFX"]


def find_bin(wad: Wad, known: int, marker: bytes) -> tuple[int, bytes]:
    if known in wad.entries:
        data = wad.read(known)
        if marker in data:
            return known, data
    for ph, data in wad.iter_bins():
        if marker in data:
            return ph, data
    raise SystemExit(f"bin containing {marker!r} not found in {wad.path}")


def vec(value) -> list[float]:
    return [float(c) for c in value] if isinstance(value, list) else [float(value)]


def normalize(value, default):
    """原版 Value* 归一化成 {t, v, p}：每个关键值都是浮点列表，常量写成 t=[0], v=[[x]]。"""
    if value is None:
        return {"t": [0.0], "v": [vec(default)]}
    if isinstance(value, (int, float, list)):
        return {"t": [0.0], "v": [vec(value)]}
    dynamics = value.get("dynamics") or {}
    if dynamics.get("times") and dynamics.get("values"):
        out = {"t": dynamics["times"], "v": [vec(x) for x in dynamics["values"]]}
    else:
        constant = value.get("constantValue", default)
        out = {"t": [0.0], "v": [vec(constant if constant is not None else default)]}
    tables = dynamics.get("probabilityTables")
    if tables:
        out["p"] = [[t.get("keyTimes"), t.get("keyValues")] if t.get("keyTimes") else None for t in tables]
    return out


def shape(spawn):
    if not spawn:
        return None
    if spawn.get("__class") == "VfxShapeBox":
        return {"type": "box", "size": spawn.get("size", [0, 0, 0])}
    if spawn.get("__class") == "VfxShapeSphere":
        return {"type": "sphere", "radius": spawn.get("radius", 0)}
    if "emitOffset" in spawn:
        return {"type": "offset", "offset": normalize(spawn["emitOffset"], [0, 0, 0])}
    return None


def emitter(e: dict, system: str, texture) -> dict:
    mult = e.get("textureMult")
    erosion = e.get("alphaErosionDefinition")
    lifetime = e.get("lifetime")
    out = {
        "name": e.get("emitterName"),
        "pass": e.get("pass", 0),
        "additive": e.get("blendMode") == 4,
        "single": bool(e.get("isSingleParticle")),
        "delay": e.get("timeBeforeFirstEmission", 0.0),
        "lifetime": lifetime if isinstance(lifetime, (int, float)) else None,
        "rate": normalize(e.get("rate"), 0.0),
        "particleLifetime": normalize(e.get("particleLifetime"), 1.0),
        "position": normalize(e.get("emitterPosition"), [0, 0, 0]),
        "shape": shape(e.get("spawnShape")),
        "birthVelocity": normalize(e.get("birthVelocity"), [0, 0, 0]),
        "velocity": normalize(e.get("velocity"), [0, 0, 0]) if e.get("velocity") else None,
        "acceleration": normalize(e.get("worldAcceleration"), [0, 0, 0]) if e.get("worldAcceleration") else None,
        "drag": normalize(e.get("birthDrag"), [0, 0, 0]) if e.get("birthDrag") else None,
        "birthScale": normalize(e.get("birthScale0"), [1, 1, 1]),
        "uniformScale": bool(e.get("isUniformScale")),
        "scale": normalize(e.get("scale0"), [1, 1, 1]) if e.get("scale0") else None,
        "birthRotation": normalize(e.get("birthRotation0"), [0, 0, 0]) if e.get("birthRotation0") else None,
        "birthColor": normalize(e.get("birthColor"), [1, 1, 1, 1]),
        "color": normalize(e.get("color"), [1, 1, 1, 1]) if e.get("color") else None,
        "texture": texture(e.get("texture")),
        "addressBase": e.get("texAddressModeBase", 0),
        "uvScale": (e.get("uvScale") or {}).get("constantValue", [1, 1]) if isinstance(e.get("uvScale"), dict) else [1, 1],
        "uvOffset": normalize(e.get("birthUVOffset"), [0, 0]) if e.get("birthUVOffset") else None,
        "uvScroll": normalize(e.get("birthUVScrollRate"), [0, 0]) if e.get("birthUVScrollRate") else None,
        "colorTexture": texture(e.get("particleColorTexture")),
    }
    if mult and mult.get("textureMult"):
        out["mult"] = {
            "texture": texture(mult["textureMult"]),
            "address": mult.get("texAddressModeMult", 0),
            "scale": normalize(mult.get("uvScaleMult"), [1, 1]),
            "offset": normalize(mult.get("birthUVOffsetMult"), [0, 0]),
            "scroll": normalize(mult.get("birthUVScrollRateMult"), [0, 0]),
            "rotation": normalize(mult.get("uvRotationMult"), 0.0),
            "rotateRate": normalize(mult.get("birthUVRotateRateMult"), 0.0),
        }
    if erosion and erosion.get("erosionMapName"):
        mixer = erosion.get("erosionMapChannelMixer")
        out["erosion"] = {
            "texture": texture(erosion["erosionMapName"]),
            "drive": normalize(erosion.get("erosionDriveCurve"), 0.0) if erosion.get("erosionDriveCurve") else None,
            "slice": erosion.get("erosionSliceWidth", 0.1),
            "mixer": mixer.get("constantValue") if isinstance(mixer, dict) and mixer.get("constantValue") else [1, 0, 0, 0],
        }
    # 只有挂在重随按钮上的两个系统按按钮缩放外框、边缘火花与镜头光斑；挂在卡上的系统一律按卡缩放。
    position = out["position"]["v"][0]
    near_button = max(abs(position[0]), abs(position[1])) <= 30
    out["space"] = "button" if system in BUTTON_SYSTEMS and (
        (out["texture"] or "").split("/")[-1].startswith("reroll_button_gold")
        or (near_button and (out["name"].startswith("Sparklies_") or out["name"].startswith("LensFlare")))) else "card"
    return {k: v for k, v in out.items() if v is not None}


class TextureExport:
    """原版贴图路径 → 相对 images/ 的路径；卡框复用 images/ui/ 下逐像素相同的现有文件。"""

    def __init__(self, wad: Wad):
        self.wad = wad
        self.table: dict[str, dict] = {}
        self.images: dict[str, Image.Image] = {}

    def __call__(self, source: str | None):
        if not source:
            return None
        base = Path(source).stem.lower()
        rel = ("ui/" if base.startswith("augmentcard_frame_") else "effects/kiwi_selection/") + base
        if rel not in self.table:
            image, _fmt = riot_tex.decode(self.wad.read_path(source))
            image = image.convert("RGBA")
            cap = 1024 if "sheenglow" in base else 512
            if max(image.size) > cap:
                image.thumbnail((cap, cap), Image.LANCZOS)
            self.table[rel] = {"source": source, "size": list(image.size)}
            self.images[rel] = image
        return rel


def particle_index(systems: dict[str, dict], uibase: list[dict], exported: dict) -> dict:
    """全部原版系统的索引：挂接它的界面节点与 Layer、音效、按数据估算的时长和各发射器概要；未导出的系统附完整归一化数据。"""
    nodes: dict[str, list[dict]] = {}
    for element in uibase:
        link = element.get("vfxSystem")
        if isinstance(link, str) and link.startswith("link:"):
            nodes.setdefault(link[5:], []).append({"node": element.get("name", "").removeprefix(UIBASE_PREFIX), "layer": element.get("layer")})

    def max_lifetime(e: dict) -> float:
        value = normalize(e.get("particleLifetime"), 1.0)
        base = max(v[0] for v in value["v"])
        if base < 0:
            return -1.0
        table = (value.get("p") or [None])[0]
        return base * (max(table[1]) if table else 1.0)

    def duration(emitters: list[dict]) -> float | None:
        total = 0.0
        for e in emitters:
            life = max_lifetime(e)
            if life < 0 or (not e.get("isSingleParticle") and not isinstance(e.get("lifetime"), (int, float))):
                return None
            emitting = 0.0 if e.get("isSingleParticle") else e["lifetime"]
            total = max(total, e.get("timeBeforeFirstEmission", 0.0) + emitting + life)
        return round(total, 3)

    index = {}
    for name in sorted(systems):
        system = systems[name]
        emitters = system.get("complexEmitterDefinitionData") or []
        ui_nodes = nodes.get(system["__key"], [])
        silver = name.replace("_Gold_", "_Silver_").replace("_Prismatic_", "_Silver_")
        if not ui_nodes and silver != name and silver in systems:
            # 界面数据只写 Silver 变体，运行时按卡牌稀有度换成 Gold / Prismatic。
            ui_nodes = [dict(node, viaTierOf=silver) for node in nodes.get(systems[silver]["__key"], [])]
        entry = {
            "key": system["__key"],
            "uiNodes": ui_nodes,
            "exported": name in exported,
            "sounds": sorted(v for v in system.values() if isinstance(v, str) and v.startswith("Play_")),
            "estimatedSeconds": duration(emitters),
            "emitters": [{
                "name": e.get("emitterName"),
                "pass": e.get("pass", 0),
                "blendMode": e.get("blendMode"),
                "single": bool(e.get("isSingleParticle")),
                "delay": e.get("timeBeforeFirstEmission", 0.0),
                "lifetime": e.get("lifetime"),
                "maxParticleLifetime": round(max_lifetime(e), 3),
                "texture": e.get("texture"),
            } for e in emitters],
        }
        if name not in exported:
            entry["data"] = [emitter(e, name, lambda source: source) for e in emitters]
        index[name] = entry
    return index


def dump_json(value) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2) + "\n"


def same_pixels(path: Path, image: Image.Image) -> bool:
    if not path.exists():
        return False
    existing = Image.open(path).convert("RGBA")
    return existing.size == image.size and ImageChops.difference(existing, image).getbbox() is None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--wad", required=True, type=Path, help="英雄联盟 Game/DATA/FINAL/UI.wad.client")
    parser.add_argument("--league-version", default="16.20", help="写进数据文件 source 字段的客户端版本")
    parser.add_argument("--write", action="store_true", help="写入数据文件、贴图与 reference 转储；默认只比对")
    args = parser.parse_args()

    with Wad(args.wad) as wad:
        particles_key, particles_raw = find_bin(wad, KNOWN_PARTICLES_BIN, PARTICLES_PREFIX.encode())
        uibase_key, uibase_raw = find_bin(wad, KNOWN_UIBASE_BIN, (UIBASE_PREFIX + "AugmentSelection/Augment_Slot").encode())
        all_systems = {e["particleName"]: e for e in riot_bin.parse(particles_raw)
                       if e["__class"] == "VfxSystemDefinitionData" and e.get("particlePath", "").startswith(PARTICLES_PREFIX)}
        uibase = sorted(riot_bin.parse(uibase_raw), key=lambda e: (e.get("name", ""), e["__key"]))
        missing = [name for name in SYSTEMS if name not in all_systems]
        if missing:
            raise SystemExit(f"systems missing from the particles bin: {missing}")

        textures = TextureExport(wad)
        data = {
            "source": f"League of Legends {args.league_version} DATA/FINAL/UI.wad.client, {PARTICLES_PREFIX.rstrip('/')}",
            "systems": {name: [emitter(e, name, textures) for e in all_systems[name]["complexEmitterDefinitionData"]] for name in SYSTEMS},
        }
        data["textures"] = textures.table
        reference = {
            "uibase.json": dump_json({"bin": f"0x{uibase_key:016x}", "entries": uibase}),
            "particle_systems.json": dump_json({"bin": f"0x{particles_key:016x}", "systems": particle_index(all_systems, uibase, data["systems"])}),
        }

    print(f"particles bin 0x{particles_key:016x}: {len(all_systems)} systems, exporting {len(SYSTEMS)}"
          f" ({sum(len(v) for v in data['systems'].values())} emitters, {len(textures.table)} textures)")
    print(f"uibase bin 0x{uibase_key:016x}: {len(uibase)} entries")

    changes: list[tuple[Path, str | Image.Image]] = []
    text = dump_json(data)
    if not DATA_FILE.exists() or DATA_FILE.read_text(encoding="utf-8") != text:
        changes.append((DATA_FILE, text))
    for rel, image in textures.images.items():
        path = IMAGES / f"{rel}.png"
        if rel.startswith("ui/"):
            if not same_pixels(path, image):
                print(f"WARNING: shared frame {path.relative_to(ROOT)} differs from the game; not overwritten")
            continue
        if not same_pixels(path, image):
            changes.append((path, image))
    for name, content in reference.items():
        path = REFERENCE / name
        if not path.exists() or path.read_text(encoding="utf-8") != content:
            changes.append((path, content))
    referenced = {f"{rel}.png" for rel in textures.table}
    unused = sorted(p.name for p in (IMAGES / "effects/kiwi_selection").glob("*.png")
                    if f"effects/kiwi_selection/{p.name}" not in referenced)

    for path, _ in changes:
        print(("write " if args.write else "differs ") + str(path.relative_to(ROOT)))
    for name in unused:
        print(f"unreferenced texture (delete by hand if intended): effects/kiwi_selection/{name}")
    if not changes:
        print("up to date")
        return 0
    if not args.write:
        return 1

    for path, content in changes:
        path.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(content, str):
            path.write_text(content, encoding="utf-8", newline="\n")
        else:
            content.save(path, optimize=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
