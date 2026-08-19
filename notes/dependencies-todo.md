# Native dependencies — state and what is left

Written 2026-08-14. Resume point for the work that moved the unmanaged dependencies onto vcpkg
manifest mode and turned libvlc and webrtc into prebuilt downloads.

## Where things are

**vcpkg is pinned to `c3867e714dd3a51c272826eea77267876517ed99`** (tag `2026.03.18`) in
`vcpkg.json`. That commit is deliberate: it is the newest release tag that still carries zlib
1.3.1. From `2026.04.27` the zlib port renames its output to `z.dll`, which would break the
tdjson export filter silently and the csproj copy loudly.

Setup is now: a full vcpkg clone, either beside the repository or the copy that ships with
Visual Studio's vcpkg component. No `VCPKG_ROOT` needed, no `vcpkg integrate install`, no port
edited by hand. `Directory.Build.props` finds it, `Directory.Build.targets` checks it is not
older than the pin and prints the fix if it is.

| dependency | how it arrives |
|---|---|
| ffmpeg 7.1.2 | overlay port, `Libraries/vcpkg-ports/ffmpeg`, carrying the `--enable-*` flag set |
| libvlc 3.0.23 | overlay port downloading `libvlc-3.0.23-2` from UnigramDev/deps |
| webrtc m123 | overlay port downloading `webrtc-2026-08-19-1` from UnigramDev/deps |
| everything else | stock ports at the pinned baseline |

**UnigramDev/deps** holds the build and packaging scripts, the webrtc patches, and the release
archives. `libvlc/` and `webrtc/` each expose `build.ps1` and `pack.ps1`. Its README documents
publishing a new archive.

**The forks**: [UnigramDev/vlc](https://github.com/UnigramDev/vlc) `unigram-12.7.5` at
`739b198e18`, [UnigramDev/webrtc-uwp](https://github.com/UnigramDev/webrtc-uwp) `m123` at
`801b013618`. Both are pushed and both releases name their commit. The VLC checkout that used to
be a submodule is an ordinary clone at `C:\Source\vlc`.

`Libraries/vlc` and `Libraries/webrtc` are gone from this repository.

## Verified

- Both native projects build against the ports, and a Release x64 msixbundle packages.
- The ports install, and the libvlc archive repacks byte-identically from the relocated checkout.
- The manifest resolves at the pinned commit with the overlay taking precedence.
- tdjson builds for `arm64-uwp` against vcpkg at the latest baseline (a separate question, asked
  because TDLib was considering the move).
- `deps/webrtc/build.ps1 -SkipAcquire` and `pack.ps1`, run 2026-08-19: all four configurations
  built clean under Visual Studio 18 and were published as `webrtc-2026-08-19-1`. The acquire half
  — fetch, sync, patch — is still unexercised.

## Not verified

- **A build that downloads from the releases.** Every build so far resolved from archives seeded
  into `C:\Source\vcpkg\downloads`. Clearing those seven files and rebuilding is the test that a
  contributor can do this at all, and it takes minutes.
- **The packaging wizard.** The two experimental settings in `8aebeaa71` were only exercised from
  the command line.

## Next

1. Commit and push the tgcalls changes. `UwpScreenCapturer.cpp` carries the try/catch from the
   fork review and the libyuv include fix. The second one is load-bearing: removing the hardcoded
   webrtc include path is what made a plain `<libyuv.h>` ambiguous. Until it is on the fork the
   submodule pin cannot move and a fresh clone will not compile Telegram.Native.Calls. **This is
   the only thing blocking someone else from building.**
2. The download test above.
3. `Build.ps1` is broken twice over: it calls `msbuild Telegram.sln`, which was renamed to
   `.slnx`, and passes `PackageCertificateThumbprint=60FFAEE6...` for a certificate that expired
   and was never renewed. Everything is signed with `Telegram.Msix_TemporaryKey.pfx` now, so the
   thumbprint branch can go.
4. Decide on the experimental settings. The control run for the double build is: revert
   `ShouldUnsetParentConfigurationAndPlatform` in the wapproj, rebuild, and look for two
   `Telegram -> ...Telegram.exe` lines and two `Generating native code` passes instead of one.
   `release.binlog` in the repository root is the "after" half.
5. `Libraries/tdjson` still has no answer for the confidential prerelease drops. The design was
   `$(TdjsonDir)` pointing outside the repository, with the drops never inside the working tree.
   `td_api.bak.tl` is currently untracked **and unignored**.
6. The TDLib ABI patch to `td_json_client` exists only in the working tree of the submodule.
   Same class of problem as the wasapi change and the unpushed webrtc branch, both fixed today.

## Traps worth not rediscovering

- **vcpkg reads its version database from the checked-out working tree**, not from the commit
  named by `builtin-baseline`. A checkout older than the pin fails with "no version database
  entry for `<port>` at `<date>`", which does not hint at the cause. `git fetch` is not enough;
  you must check out the pin and re-bootstrap.
- **Autolink is off** (`VcpkgAutoLink=false`). It put every `.lib` in the installed tree on every
  link line, including a 339 MB webrtc static library on projects with no reference into it. Each
  project names what it links in `Directory.Build.targets`; the lists came from what the built
  binaries actually import. Statics leave no import trace, so `ZXing.lib` was inferred.
- **webrtc carries a patched libyuv** whose `ConvertToI420` takes three extra parameters. The
  webrtc port deliberately does not install `include/libyuv.h`; the compiled files include it by
  full path. See `duplicated-libraries.md`.
- **applocal does not populate the app's own output folder**, and does not run at all when the
  linker is skipped or a project is only queried for packaging outputs. The DLLs are declared
  explicitly instead, which is what let `Libraries/vcpkg.patch` be deleted.
- **libvlc plugins cannot be flattened.** Their relative paths are recorded in the generated
  `plugins.dat`, so they reach the package through `Content` items with `Link` metadata.
- **`*.patch` must stay LF.** `git apply` rejects a patch whose line endings were converted; this
  is why `Libraries/vcpkg.patch` had stopped applying. Pinned in `.gitattributes` in both repos.
- **`Telegram.Stub`'s native AOT needs `vswhere` on PATH** — `findvcvarsall.bat` calls it bare, so
  a build from a plain shell fails where Visual Studio or a developer prompt succeeds.
