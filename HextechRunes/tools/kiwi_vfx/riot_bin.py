"""英雄联盟 PROP bin 只读解析：类型名、字段名都是小写名的 FNV-1a 32 位哈希，已知名字还原成字符串，其余保留 0x 十六进制。

名字表来自原版数据里能对上的候选名；哈希不区分大小写，表里每个名字只写一种拼法（导出脚本按这种拼法取字段）。
"""
from __future__ import annotations

import struct


def fnv1a(name: str) -> int:
    value = 0x811C9DC5
    for byte in name.lower().encode():
        value = ((value ^ byte) * 0x01000193) & 0xFFFFFFFF
    return value


# 粒子：VfxSystemDefinitionData / VfxEmitterDefinitionData 及其值类型。
_PARTICLE_NAMES = """
VfxSystemDefinitionData VfxEmitterDefinitionData complexEmitterDefinitionData simpleEmitterDefinitionData
particleName particlePath emitterName texture textureMult particleColorTexture blendMode lifetime particleLifetime
rate birthColor color birthScale0 scale0 birthRotation0 rotation0 birthVelocity velocity birthAlpha alpha
primitive VfxPrimitiveMesh VfxPrimitiveArbitraryQuad VfxPrimitiveCameraUnitQuad VfxPrimitivePlanarProjection
mMesh VfxMeshDefinitionData mSimpleMeshName mMeshName mMeshSkeletonName mAnimationName
ValueFloat ValueVector2 ValueVector3 ValueColor constantValue dynamics VfxAnimatedFloatVariableData
VfxAnimatedVector2fVariableData VfxAnimatedVector3fVariableData VfxAnimatedColorVariableData times values
probabilityTables VfxProbabilityTableData keyTimes keyValues singleValue
isSingleParticle timeBeforeFirstEmission emitterLinger texDiv texAddressModeBase alphaRef disableBackfaceCull
emitterPosition bindWeight isUniformScale isLocalOrientation isDirectionOriented directionVelocityScale
pass depthBiasFactors miscRenderFlags importance useNavmeshMask sound soundPersistent soundOnCreateDefault
flags visibilityRadius scaleOverride hudLayerDimension hudAnchorPositionFromWorldProjection
textureFlipU textureFlipV uvScrollClamp uvMode VfxTextureMultDefinitionData textureMultFilter uvScaleMult uvScrollMult
birthUVOffset uvOffset birthUVScrollRate uvScrollRate erosionDefinition VfxEmitterLegacySimple
particleColorLookupScale colorLookupScale colorLookupType colorLookupOffset fresnelDefinition
VfxFieldCollectionDefinitionData fieldCollectionDefinition reflectionDefinition VfxReflectionDefinitionData
distortionDefinition VfxDistortionDefinitionData materialOverrideDefinitions VfxMaterialOverrideDefinitionData
particleIsLocalOrientation isFollowingTerrain chance period emitterUvScrollRate
birthFrameRate frameRate numFrames startFrame birthDrag drag birthAcceleration acceleration
erosionMapName erosionSliceWidth erosionFeatherIn erosionFeatherOut erosionMapChannelMixer
VfxEmissionSurfaceData spawnShape VfxShapeLegacy VfxShapeBox VfxShapeSphere VfxShapeCylinder emitOffset emitRotationAngles emitRotationAxes
paletteDefinition VfxPaletteDefinitionData paletteTexture
alphaErosionDefinition VfxAlphaErosionDefinitionData linger VfxLingerDefinitionData
childParticleSetDefinition VfxChildParticleSetDefinitionData childrenIdentifiers VfxChildIdentifier effectKey effect
material shaderMacros VfxMaterialDefinitionData materialDrivers particleUVScrollRate size uvScrollClampMult uvScale
particleLingerType worldAcceleration particleIntegratedUVRotateMult birthUVScrollRateMult birthUVOffsetMult erosionDriveCurve
texAddressModeMult particleLinger drawingLayer uvScrollAlphaMult birthUVRotateRateMult erosionMapAddressMode radius uvRotationMult
"""

# 界面：KiwiAugmentSelection/UIBase 里的 UiSceneData 与 UiElement*Data。
_UI_NAMES = """
UiSceneData UiElementGroupData UiElementGroupButtonData UiElementGroupFramedData UiElementIconData UiElementRegionData
UiElementTextData UiElementParticleSystemData UiPositionRect UiElementRect
name scene enabled layer position UIRect anchors anchor size ignoreGlobalScale SourceResolutionWidth SourceResolutionHeight
elements region vfxSystem soundEvents textureData textureName atlasData mTextureName mTextureUV iconScale traKey
defaultStateElements hoverStateElements clickedStateElements selectedStateElements inactiveStateElements hitRegionElement
textAlignmentHorizontal textAlignmentVertical rowVerticalAlignment
sceneTransitionIn sceneTransitionOut transitionTime easingType startAlpha endAlpha
"""

NAMES = {fnv1a(name): name for name in (_PARTICLE_NAMES + _UI_NAMES).split()}


def name_of(value: int) -> str:
    return NAMES.get(value, f"0x{value:08x}")


class _Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.position = 0

    def take(self, fmt: str):
        values = struct.unpack_from("<" + fmt, self.data, self.position)
        self.position += struct.calcsize("<" + fmt)
        return values if len(values) > 1 else values[0]

    def string(self) -> str:
        length = self.take("H")
        value = self.data[self.position:self.position + length].decode("utf-8", "replace")
        self.position += length
        return value


def _value(reader: _Reader, kind: int):
    if kind == 0:
        return None
    if kind in (1, 3, 0x87):
        return reader.take("B")
    if kind == 2:
        return reader.take("b")
    if kind == 4:
        return reader.take("h")
    if kind == 5:
        return reader.take("H")
    if kind == 6:
        return reader.take("i")
    if kind == 7:
        return reader.take("I")
    if kind == 8:
        return reader.take("q")
    if kind == 9:
        return reader.take("Q")
    if kind == 10:
        return round(reader.take("f"), 4)
    if kind in (11, 12, 13):
        return [round(x, 4) for x in reader.take(f"{kind - 9}f")]
    if kind == 14:
        return list(reader.take("16f"))
    if kind == 15:
        return list(reader.take("4B"))
    if kind == 16:
        return reader.string()
    if kind == 17:
        return name_of(reader.take("I"))
    if kind == 18:
        return f"0x{reader.take('Q'):016x}"
    if kind in (0x80, 0x81):
        item_kind = reader.take("B")
        reader.take("I")
        count = reader.take("I")
        return [_value(reader, item_kind) for _ in range(count)]
    if kind in (0x82, 0x83):
        class_hash = reader.take("I")
        if class_hash == 0:
            return None
        reader.take("I")
        return _fields(reader, {"__class": name_of(class_hash)})
    if kind == 0x84:
        return "link:" + name_of(reader.take("I"))
    if kind == 0x85:
        item_kind = reader.take("B")
        present = reader.take("B")
        return _value(reader, item_kind) if present else None
    if kind == 0x86:
        key_kind, value_kind = reader.take("B"), reader.take("B")
        reader.take("I")
        count = reader.take("I")
        return {str(_value(reader, key_kind)): _value(reader, value_kind) for _ in range(count)}
    raise ValueError(f"unknown bin value type {kind} at {reader.position}")


def _fields(reader: _Reader, target: dict) -> dict:
    for _ in range(reader.take("H")):
        field_hash, kind = reader.take("I"), reader.take("B")
        target[name_of(field_hash)] = _value(reader, kind)
    return target


def parse(data: bytes) -> list[dict]:
    """解析一个 PROP bin，返回条目列表；每个条目带 `__class` 与 `__key`（条目路径哈希）。"""
    reader = _Reader(data)
    if reader.take("4s") != b"PROP":
        raise ValueError("not a PROP bin")
    version = reader.take("I")
    if version >= 2:
        for _ in range(reader.take("I")):
            reader.string()
    count = reader.take("I")
    types = [reader.take("I") for _ in range(count)]
    entries = []
    for class_hash in types:
        length = reader.take("I")
        end = reader.position + length
        key = reader.take("I")
        entries.append(_fields(reader, {"__class": name_of(class_hash), "__key": f"0x{key:08x}"}))
        reader.position = end
    return entries
