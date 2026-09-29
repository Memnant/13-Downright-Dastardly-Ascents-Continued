"""Read selected files from PEAK's UnityFS archive without unpacking the whole game.

Uses UnityPy's compression/reader implementation. Only the audited UnityFS 8
layout is supported; the game file is always opened read-only.
"""
from pathlib import Path
from UnityPy.streams import EndianBinaryReader
from UnityPy.helpers import CompressionHelper
from UnityPy.enums import CompressionFlags


class UnityArchive:
    def __init__(self, path: Path):
        self.path = path
        with path.open('rb') as handle:
            reader = EndianBinaryReader(handle)
            if reader.read_string_to_null() != 'UnityFS' or reader.read_u_int() != 8:
                raise ValueError('Expected UnityFS 8 archive')
            reader.read_string_to_null()  # player version
            self.unity_version = reader.read_string_to_null()
            size = reader.read_long()
            compressed, uncompressed, flags = (reader.read_u_int() for _ in range(3))
            if size != path.stat().st_size or flags & ~0x2ff:
                raise ValueError('Unsupported archive header')
            reader.align_stream(16)
            start = reader.Position
            if flags & 0x80:
                reader.Position = size - compressed
            info = self.decompress(reader.read_bytes(compressed), uncompressed, flags)
            if flags & 0x80:
                reader.Position = start
            if flags & 0x200:
                reader.align_stream(16)
            data_start = reader.Position
        info = EndianBinaryReader(info)
        info.read_bytes(16)
        count = info.read_int()
        if not 0 < count < 1_000_000:
            raise ValueError('Invalid archive block count')
        self.blocks = []
        raw_offset, disk_offset = 0, data_start
        for _ in range(count):
            raw_size, disk_size, block_flags = info.read_u_int(), info.read_u_int(), info.read_u_short()
            self.blocks.append((raw_offset, raw_size, disk_offset, disk_size, block_flags))
            raw_offset += raw_size
            disk_offset += disk_size
        self.entries = {}
        for _ in range(info.read_int()):
            offset, length, entry_flags = info.read_long(), info.read_long(), info.read_u_int()
            name = info.read_string_to_null()
            if name in self.entries or offset < 0 or length < 0 or offset + length > raw_offset:
                raise ValueError('Invalid archive entry')
            self.entries[name] = (offset, length, entry_flags)

    @staticmethod
    def decompress(data, size, flags):
        if flags & ~0x3ff:
            raise ValueError('Unsupported block flags')
        result = CompressionHelper.DECOMPRESSION_MAP[CompressionFlags(flags & 0x3f)](data, size)
        if len(result) != size:
            raise ValueError('Incomplete archive block')
        return result

    def read(self, name, start=0, count=None):
        offset, length, _ = self.entries[name]
        if count is None:
            count = length - start
        if start < 0 or count < 0 or start + count > length:
            raise ValueError('Invalid entry range')
        offset += start
        length = count
        result = bytearray()
        with self.path.open('rb') as handle:
            for raw_offset, raw_size, disk_offset, disk_size, flags in self.blocks:
                if raw_offset >= offset + length:
                    break
                if raw_offset + raw_size <= offset:
                    continue
                handle.seek(disk_offset)
                block = self.decompress(handle.read(disk_size), raw_size, flags)
                result.extend(block[max(0, offset - raw_offset):min(raw_size, offset + length - raw_offset)])
        if len(result) != length:
            raise ValueError('Incomplete archive entry')
        return bytes(result)
