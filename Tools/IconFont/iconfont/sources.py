"""Where an icon's artwork comes from.

Two kinds of source exist. A local one is a file in this folder, checked in
beside the manifest, and is the whole story for artwork the designer drew. A
remote one is pinned to a version and fetched on demand, so `update` can pull
today's copy of a Microsoft icon instead of the one that was current whenever
somebody last opened IcoMoon.

Microsoft's icons come from npm rather than the GitHub repo on purpose: the repo
has over a hundred thousand entries, so the git tree API truncates and the
folder name for an icon cannot be derived from its name. The npm package is one
request, is versioned, and lays every icon out flat as icons/<name>.svg.
"""

import io
import json
import os
import ssl
import tarfile
import urllib.request

CACHE_DIR = ".cache"
USER_AGENT = "unigram-iconfont/1.0"


class SourceError(Exception):
    pass


class LocalSource:
    kind = "local"

    def __init__(self, root):
        self.root = root

    def read(self, ident):
        path = os.path.join(self.root, ident.replace("/", os.sep))
        if not os.path.exists(path):
            raise SourceError("%s does not exist" % ident)
        with open(path, "r", encoding="utf-8-sig") as fp:
            return fp.read()

    def describe(self):
        return "local files"


class NpmSource:
    """A pinned npm package, unpacked once into the cache."""

    kind = "npm"

    def __init__(self, root, config):
        self.package = config["package"]
        self.version = config["version"]
        self.prefix = config.get("prefix", "icons/")
        self.cache = os.path.join(root, CACHE_DIR)
        self._files = None

    @property
    def stamp(self):
        return "%s@%s" % (self.package, self.version)

    def _tarball_path(self):
        safe = self.stamp.replace("/", "-").replace("@", "-").strip("-")
        return os.path.join(self.cache, safe + ".tgz")

    def _fetch(self):
        path = self._tarball_path()
        if os.path.exists(path):
            return path
        name = self.package.rsplit("/", 1)[-1]
        url = "https://registry.npmjs.org/%s/-/%s-%s.tgz" % (
            self.package, name, self.version)
        if not os.path.isdir(self.cache):
            os.makedirs(self.cache)
        data = _get(url)
        # Written via a temporary name so an interrupted download can never be
        # mistaken for a complete one on the next run.
        tmp = path + ".part"
        with open(tmp, "wb") as fp:
            fp.write(data)
        os.replace(tmp, path)
        return path

    def _load(self):
        if self._files is not None:
            return self._files
        self._files = {}
        with tarfile.open(self._fetch()) as tar:
            for member in tar:
                if not member.isfile() or not member.name.endswith(".svg"):
                    continue
                rel = member.name.split("/", 1)[1] if "/" in member.name else member.name
                if rel.startswith(self.prefix):
                    key = rel[len(self.prefix):]
                    self._files[key[:-4]] = tar.extractfile(member).read().decode("utf-8")
        return self._files

    def read(self, ident):
        files = self._load()
        if ident not in files:
            raise SourceError("%s has no icon named %r" % (self.stamp, ident))
        return files[ident]

    def contains(self, ident):
        return ident in self._load()

    def names(self):
        return sorted(self._load())

    def latest_version(self):
        meta = json.loads(_get("https://registry.npmjs.org/%s" % self.package).decode("utf-8"))
        return meta["dist-tags"]["latest"]

    def describe(self):
        return self.stamp


def _get(url):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    try:
        # Corporate proxies and old certificate stores are a common cause of
        # failure here; say so rather than surfacing a bare SSL traceback.
        with urllib.request.urlopen(request, timeout=60) as response:
            return response.read()
    except ssl.SSLError as e:
        raise SourceError("TLS failure fetching %s (%s)" % (url, e))
    except Exception as e:
        raise SourceError("could not fetch %s (%s)" % (url, e))


def build(manifest):
    """Instantiate every source the manifest declares, plus the local one."""
    sources = {"local": LocalSource(manifest.root)}
    for name, config in manifest.sources.items():
        kind = config.get("type")
        if kind == "npm":
            sources[name] = NpmSource(manifest.root, config)
        else:
            raise SourceError("unknown source type %r for %r" % (kind, name))
    return sources


def read(icon, sources):
    source = sources.get(icon.source_kind)
    if source is None:
        raise SourceError("%s refers to unknown source %r" % (icon.name, icon.source_kind))
    return source.read(icon.source_id)
