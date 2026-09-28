"""Copy PEAK 2.4.c's Alpine particle asset and dependencies into a small local bundle.

Read-only on the game directory. No scene, item, PhotonView or MonoBehaviour is
included. Particle settings, compiled shaders and compressed texture bytes are
preserved; only object references and the particle transform's parent change.
Requires UnityPy 1.25.3; its local installation may live in .tools/unity-inspect.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/unity-inspect'))
import UnityPy
from UnityPy.enums import ArchiveFlags, ClassIDType
from UnityPy.files.BundleFile import BundleFile
from UnityPy.files.ObjectReader import ObjectReader
from UnityPy.helpers.Tpk import get_typetree_node


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def extract(game: Path, output: Path):
    if (game / 'version.txt').read_text(encoding='utf-8-sig').splitlines()[0].strip() != '2.4.c':
        raise ValueError('This extractor is pinned to PEAK 2.4.c.')
    data_root = game / 'PEAK_Data'
    asm_hash = sha((data_root / 'Managed/Assembly-CSharp.dll').read_bytes())
    if asm_hash != 'C067125B9833EF1F2881AB97FDCA574BE7C67F45C8ADD77B9E0A8AB82C4FE9F7':
        raise ValueError('Game assembly does not match the audited 2.4.c build.')
    files = {}
    trees = {}
    ids = {}
    payload_hashes = {}
    allowed = {'GameObject', 'Transform', 'ParticleSystem', 'ParticleSystemRenderer',
               'Material', 'Shader', 'Texture2D'}

    def file(name):
        if name not in files:
            env = UnityPy.load(str(data_root / name))
            files[name] = next(iter(env.files.values()))
        return files[name]

    def pointers(tree):
        if isinstance(tree, dict):
            if set(tree) == {'m_FileID', 'm_PathID'}:
                yield tree
            else:
                for value in tree.values():
                    yield from pointers(value)
        elif isinstance(tree, (list, tuple)):
            for value in tree:
                yield from pointers(value)

    def resolve(name, ptr):
        if not ptr['m_PathID']:
            return None
        if not ptr['m_FileID']:
            return name, ptr['m_PathID']
        return file(name).externals[ptr['m_FileID'] - 1].name, ptr['m_PathID']

    def collect(key):
        if key in ids:
            return
        name, path_id = key
        obj = file(name).objects[path_id]
        if obj.type.name not in allowed:
            raise ValueError(f'Unexpected dependency {key}: {obj.type.name}')
        ids[key] = len(ids) + 2  # Path ID 1 is the AssetBundle index.
        tree = obj.read_typetree()
        if key == ('level7', 342491):
            tree['m_Father'] = {'m_FileID': 0, 'm_PathID': 0}
        if obj.type.name == 'Texture2D':
            stream = tree['m_StreamData']
            if stream['size']:
                source = data_root / stream['path']
                if source.parent.resolve() != data_root.resolve():
                    raise ValueError('Unexpected external texture path')
                with source.open('rb') as handle:
                    handle.seek(stream['offset'])
                    image = handle.read(stream['size'])
                if len(image) != stream['size']:
                    raise ValueError('Incomplete texture stream')
                tree['image data'] = image
                tree['m_StreamData'] = dict(offset=0, size=0, path='')
                payload_hashes[tree['m_Name']] = sha(image)
        trees[key] = tree
        for ptr in pointers(tree):
            dependency = resolve(name, ptr)
            if dependency:
                collect(dependency)

    collect(('level7', 137517))  # Native Alpine Particle System and its exact renderer.
    collect(('sharedassets0.assets', 20))  # Original StormSphere FogConfig.windTexture.
    if file('level7').objects[592736].type.name != 'ParticleSystem':
        raise ValueError('Native particle component moved')
    if trees[('sharedassets4.assets', 171)]['m_Name'] != 'M_VFX_Snow':
        raise ValueError('Original snow material moved')

    # Start from the exact game serialization version; retain no scene objects/types.
    output_file = copy.copy(file('level7'))
    output_file.name = 'CAB-continued-native-alpine-snow-24c'
    output_file.objects = {}
    output_file.types = []
    output_file.script_types = []
    output_file.externals = []
    output_file.ref_types = []
    output_file._enable_type_tree = True
    output_file.flags = 4
    type_ids = {}

    def append_object(path_id, class_id, source_type, tree):
        if class_id not in type_ids:
            typ = copy.copy(source_type)
            typ.class_id = class_id
            typ.script_type_index = -1
            typ.is_stripped_type = False
            typ.script_id = None
            template = get_typetree_node(class_id, output_file.version)
            typ.node = type(template).from_list([
                {k: getattr(n, k) for k in ('m_Level', 'm_Type', 'm_Name', 'm_ByteSize',
                 'm_Version', 'm_Index', 'm_MetaFlag') if getattr(n, k) is not None}
                for n in template.traverse()])
            for node in typ.node.traverse():
                if node.m_TypeFlags is None:
                    node.m_TypeFlags = 1 if node.m_Type == 'Array' else 0
                if node.m_RefTypeHash is None:
                    node.m_RefTypeHash = 0
            typ.type_dependencies = ()
            if class_id == 142:
                typ.old_type_hash = bytes(16)
            type_ids[class_id] = len(output_file.types)
            output_file.types.append(typ)
        index = type_ids[class_id]
        obj = ObjectReader(output_file, output_file.reader, path_id, index,
                           output_file.types[index], class_id, ClassIDType(class_id),
                           0, 0, None, None)
        obj.save_typetree(tree)
        output_file.objects[path_id] = obj

    inventory = []
    expected_trees = {}
    for key, path_id in ids.items():
        name, original_id = key
        original = file(name).objects[original_id]
        tree = copy.deepcopy(trees[key])
        for ptr in pointers(tree):
            dependency = resolve(name, ptr)
            ptr.update(m_FileID=0, m_PathID=ids[dependency] if dependency else 0)
        append_object(path_id, original.class_id, original.serialized_type, tree)
        expected_trees[path_id] = tree
        inventory.append(dict(file=name, pathId=original_id, type=original.type.name,
                              name=tree.get('m_Name', ''), bundledPathId=path_id))

    preload = [dict(m_FileID=0, m_PathID=pid) for pid in ids.values()]
    entries = [('assets/continued/native-alpine-snow.prefab', ('level7', 137517)),
               ('assets/continued/native-alpine-fog.texture', ('sharedassets0.assets', 20))]
    bundle_tree = dict(m_Name='continued-native-alpine-snow-24c', m_PreloadTable=preload,
        m_Container=[(name, dict(preloadIndex=0, preloadSize=len(preload),
                                asset=dict(m_FileID=0, m_PathID=ids[key]))) for name, key in entries],
        m_MainAsset=dict(preloadIndex=0, preloadSize=0, asset=dict(m_FileID=0, m_PathID=0)),
        m_RuntimeCompatibility=1, m_AssetBundleName='continued-native-alpine-snow-24c',
        m_Dependencies=[], m_IsStreamedSceneAssetBundle=False, m_ExplicitDataLayout=0,
        m_PathFlags=7, m_SceneHashes=[])
    append_object(1, 142, file('level7').types[0], bundle_tree)
    bundle = object.__new__(BundleFile)
    bundle.signature = 'UnityFS'
    bundle.version = 8
    bundle.version_player = '5.x.x'
    bundle.version_engine = output_file.unity_version
    bundle.dataflags = ArchiveFlags(64)
    bundle._uses_block_alignment = True
    bundle.files = {output_file.name: output_file}
    output_file.parent = bundle
    result = bundle.save('lz4')

    # Re-read the artifact and compare all particle module values with the original.
    checked = UnityPy.load(result)
    checked_objects = {obj.path_id: obj for obj in checked.objects}
    native_particle = copy.deepcopy(trees[('level7', 592736)])
    for ptr in pointers(native_particle):
        dep = resolve('level7', ptr)
        ptr.update(m_FileID=0, m_PathID=ids[dep] if dep else 0)
    if checked_objects[ids[('level7', 592736)]].read_typetree() != native_particle:
        raise ValueError('Particle round-trip changed native values')
    for key, pid in ids.items():
        original = trees[key]
        check = checked_objects[pid].read_typetree()
        if check != expected_trees[pid]:
            raise ValueError(f'Asset round-trip changed fields: {key}')
        if file(key[0]).objects[key[1]].type.name == 'Texture2D':
            if sha(bytes(check['image data'])) != payload_hashes[original['m_Name']]:
                raise ValueError('Texture bytes changed')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(result)
    report = dict(Status='PASSED_NATIVE_ASSET_EXTRACTION', Game='PEAK 2.4.c',
        GameAssemblySHA256=asm_hash, BundleSHA256=sha(result), BundleBytes=len(result),
        UnityVersion=output_file.unity_version, ParticleSettingsMatch=True, AllAssetFieldsMatch=True,
        TexturePayloadSHA256=payload_hashes, Objects=inventory, SceneCount=0,
        MonoBehaviourCount=0, ExternalDependencies=0, RuntimeLoadVerified=False)
    report_path = ROOT / 'artifacts/native-snow-extraction.json'
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--game', type=Path, default=Path(r'D:\Steam\steamapps\common\PEAK'))
    parser.add_argument('--output', type=Path, default=ROOT / 'src/Resources/NativeAlpineSnow.bundle')
    args = parser.parse_args()
    extract(args.game, args.output)
