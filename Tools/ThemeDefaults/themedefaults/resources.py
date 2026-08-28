"""Pulls the compiled theme resources out of a shipped Microsoft.UI.Xaml package.

Only needed if the tables ever have to be rebuilt from a framework rather than from the
tables already here - a WinUI 3 migration, most likely. The resources are not loose files
in the package: they are XBF blobs embedded in resources.pri, and the Generic.xaml that
ships in lib/ is a trimmed stub with about seventy keys in it.

What this gets you is the key list, which is plaintext UTF-16 inside the XBF. Values are
converted into the node stream, so key to colour needs a real XBF2 decoder and this is not
one. To get values, run the framework instead of parsing it: a throwaway app referencing
the same package, walking Application.Current.Resources.ThemeDictionaries and dumping what
it resolves. That also settles the OS resources, which no source file states outright.
"""

import base64
import os
import re
import subprocess
import zipfile

KITS = r"C:\Program Files (x86)\Windows Kits\10\bin"

_NAMED = re.compile(r'<NamedResource name="([^"]+)"[^>]*>(.*?)</NamedResource>', re.S)
_BASE64 = re.compile(r"<Base64Value>\s*(.*?)\s*</Base64Value>", re.S)
_IDENTIFIER = re.compile(r"^[A-Za-z][A-Za-z0-9_]{5,}$")


def find_makepri():
    if not os.path.isdir(KITS):
        raise SystemExit("no Windows Kits at " + KITS)
    found = []
    for version in os.listdir(KITS):
        candidate = os.path.join(KITS, version, "x64", "makepri.exe")
        if os.path.isfile(candidate):
            found.append((version, candidate))
    if not found:
        raise SystemExit("no makepri.exe under " + KITS)
    return sorted(found)[-1][1]


def unpack_package(package, out_dir):
    """Lifts resources.pri out of an .appx or .msix."""
    os.makedirs(out_dir, exist_ok=True)
    with zipfile.ZipFile(package) as archive:
        names = [n for n in archive.namelist() if n.lower().endswith("resources.pri")]
        if not names:
            raise SystemExit("no resources.pri in " + package)
        archive.extract(names[0], out_dir)
        return os.path.join(out_dir, names[0])


def extract(pri, out_dir):
    """Writes every embedded resource in a .pri out as a file. Returns their paths."""
    os.makedirs(out_dir, exist_ok=True)
    dump = os.path.join(out_dir, "dump.xml")

    # makepri wants Windows-style switches; run it with the .pri's folder as the cwd so
    # the paths it echoes stay short.
    subprocess.run([find_makepri(), "dump", "/if", os.path.abspath(pri),
                    "/of", os.path.abspath(dump), "/dt", "detailed"],
                   check=True, stdout=subprocess.DEVNULL)

    with open(dump, "r", encoding="utf-8") as fp:
        text = fp.read()

    written = []
    for match in _NAMED.finditer(text):
        name, body = match.group(1), match.group(2)
        value = _BASE64.search(body)
        if not value:
            continue
        blob = base64.b64decode(re.sub(r"\s+", "", value.group(1)))
        path = os.path.join(out_dir, name)
        with open(path, "wb") as fp:
            fp.write(blob)
        written.append(path)
    return written


def keys(path):
    """Every identifier-shaped UTF-16 run in an XBF, sorted.

    A superset - type and property names are in the same string table as resource keys -
    so treat it as "is this key present", not as an extraction of the key set.
    """
    with open(path, "rb") as fp:
        blob = fp.read()

    # A run of ASCII-in-UTF-16, found at any offset rather than on a two byte stride:
    # the string table is not aligned and a stride would miss half of it.
    found = set()
    for run in re.finditer(rb"(?:[\x20-\x7e]\x00){6,}", blob):
        text = run.group(0).decode("utf-16-le")
        if _IDENTIFIER.match(text):
            found.add(text)
    return sorted(found)
