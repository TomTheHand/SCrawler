"""Fork tooling: check or refresh lib/'s PersonalUtilities DLLs against an upstream release zip.

Reads only the zip's central directory and the three DLL entries via HTTP Range requests, so a
360 MB release costs a few MB. Every extracted file is verified against the CRC-32 in the zip's
directory before it is written.

    py -3 Tools/release_dlls.py 2026.9.21.0             compare lib/ with the release
    py -3 Tools/release_dlls.py 2026.9.21.0 --extract   write the release's DLLs into lib/
"""
import struct
import sys
import urllib.request
import zlib
from pathlib import Path

if len(sys.argv) < 2 or sys.argv[1].startswith("-"):
    raise SystemExit(__doc__)
TAG = sys.argv[1]
EXTRACT = "--extract" in sys.argv[2:]
URL = f"https://github.com/AAndyProgram/SCrawler/releases/download/{TAG}/SCrawler_{TAG}_x64.zip"
LIB = Path(__file__).resolve().parent.parent / "lib"


def fetch(start, end=None):
    rng = f"bytes={start}-" if end is None else f"bytes={start}-{end}"
    req = urllib.request.Request(URL, headers={"User-Agent": "scrawler-fork-tools", "Range": rng})
    with urllib.request.urlopen(req) as r:
        if r.status != 206:
            raise SystemExit(f"server ignored the Range request (HTTP {r.status})")
        return r.read(), int(r.headers["Content-Range"].split("/")[1])


# The end-of-central-directory record sits in the last 64 KiB + 22 bytes of the archive.
_, total = fetch(0, 0)
tail, _ = fetch(max(0, total - (65536 + 22)))
eocd = tail.rfind(b"PK\x05\x06")
if eocd < 0:
    raise SystemExit("end-of-central-directory record not found")
_, cd_size, cd_offset = struct.unpack_from("<HII", tail, eocd + 10)
if cd_offset == 0xFFFFFFFF:
    raise SystemExit("ZIP64 archive - not handled")
cd, _ = fetch(cd_offset, cd_offset + cd_size - 1)

entries = {}
p = 0
while p < len(cd) and cd[p:p + 4] == b"PK\x01\x02":
    method, = struct.unpack_from("<H", cd, p + 10)
    crc, csize, usize, nlen, xlen, clen = struct.unpack_from("<IIIHHH", cd, p + 16)
    local_header, = struct.unpack_from("<I", cd, p + 42)
    name = cd[p + 46:p + 46 + nlen].decode("utf-8", "replace")
    entries[name] = (crc, usize, csize, method, local_header)
    p += 46 + nlen + xlen + clen

# Root-level copies only; the Updater/ folder carries a duplicate of PersonalUtilities.dll.
dlls = sorted(n for n in entries if "/" not in n and n.lower().startswith("personalutilities") and n.lower().endswith(".dll"))
print(f"{TAG}: {total / 2**20:.1f} MiB archive, {len(entries)} entries, {len(dlls)} PersonalUtilities DLL(s)")

for name in dlls:
    crc, usize, csize, method, local_header = entries[name]
    local = LIB / name
    if EXTRACT:
        hdr, _ = fetch(local_header, local_header + 29)
        nlen, xlen = struct.unpack_from("<HH", hdr, 26)
        start = local_header + 30 + nlen + xlen
        raw, _ = fetch(start, start + csize - 1)
        data = zlib.decompress(raw, -15) if method == 8 else raw if method == 0 else None
        if data is None or len(data) != usize or (zlib.crc32(data) & 0xFFFFFFFF) != crc:
            raise SystemExit(f"verification failed for {name}; nothing written for it")
        local.write_bytes(data)
        print(f"WROTE   {name}  {usize:>9,} B  crc {crc:08x} (verified)")
    elif local.exists():
        data = local.read_bytes()
        lcrc = zlib.crc32(data) & 0xFFFFFFFF
        state = "SAME   " if lcrc == crc and len(data) == usize else "CHANGED"
        print(f"{state} {name}  release {usize:>9,} B crc {crc:08x} | lib {len(data):>9,} B crc {lcrc:08x}")
    else:
        print(f"NEW     {name}  release {usize:>9,} B (not in lib/)")
