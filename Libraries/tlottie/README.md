# tlottie (prebuilt)

Prebuilt static libraries for [tlottie](https://github.com/dkaraush/tlottie),
Telegram's Rust replacement for rlottie. `LottieAnimation` can render with
either renderer; see `LottieAnimation.UseTLottie`.

Only `x64` and `ARM64` are provided, because those are the platforms Unigram
ships. `Win32` builds without `HAS_TLOTTIE` and uses rlottie unconditionally.

## Regenerating

Needs a nightly toolchain and the `rust-src` component: the `*-uwp-windows-msvc`
targets are tier 3, so there is no prebuilt `std` and it has to be compiled from
source with `-Z build-std`.

The plain `x86_64-pc-windows-msvc` target does **not** work here — its `std`
pulls `NtWriteFile` and `RtlNtStatusToDosError` in through
`std::sys::stdio::windows::write`, which do not resolve against `WindowsApp.lib`
and fail the app-container link.

```bash
rustup toolchain install nightly --profile minimal -c rust-src

# x64
cargo +nightly rustc -Z build-std=std,panic_abort \
  --target x86_64-uwp-windows-msvc --release --features c-api \
  --lib --crate-type staticlib

# ARM64
cargo +nightly rustc -Z build-std=std,panic_abort \
  --target aarch64-uwp-windows-msvc --release --features c-api \
  --lib --crate-type staticlib
```

Copy `target/<triple>/release/tlottie.lib` to `lib/<x64|ARM64>/` and
`include/tlottie.h` from the tlottie checkout.

## Notes

- The renderer is asked for **BGRA** at parse time (`TLOTTIE_CHANNEL_BGRA`), so
  its output byte order matches what rlottie produces and what the
  `B8G8R8A8_UNORM` surfaces expect. No per-frame conversion.
- Both renderers write and read **the same** `.tgfc` cache file, shared with
  video through `Telegram.Native/Cache`. That is only safe because of the point
  above: swap the channel order and channel-swapped frames are persisted to disk
  and then served to the other renderer. Toggling `UseTLottie` therefore does
  not invalidate anything, and there is no per-renderer cache to clear.
