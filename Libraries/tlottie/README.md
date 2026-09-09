# tlottie (prebuilt)

Prebuilt static libraries for [tlottie](https://github.com/dkaraush/tlottie),
Telegram's Rust replacement for rlottie. It is the only Lottie renderer the app
has; `LottieAnimation` drives it.

Only `x64` and `ARM64` are provided, because those are the platforms Unigram
ships and the only ones the projects configure.

## Regenerating

Built from the `main` branch, currently `4b940c7`.

The build is `no_std`, and that is what lets it use the tier-1
`*-pc-windows-msvc` targets on the stable toolchain: `std` reaches
`NtWriteFile` and `RtlNtStatusToDosError` through
`std::sys::stdio::windows::write`, neither of which resolves against
`WindowsApp.lib`, so a `std` build fails the app-container link. Without `std`
there is nothing to resolve, and no nightly toolchain or `-Z build-std` is
needed.

```bash
rustup target add aarch64-pc-windows-msvc

cargo rustc --profile release-nostd --no-default-features --features cpu,c-api,no-std --target x86_64-pc-windows-msvc --lib --crate-type staticlib
cargo rustc --profile release-nostd --no-default-features --features cpu,c-api,no-std --target aarch64-pc-windows-msvc --lib --crate-type staticlib
```

Copy `target/<triple>/release-nostd/tlottie.lib` to `lib/<x64|ARM64>/` and
`include/tlottie.h` from the tlottie checkout.

## Notes

- The renderer is asked for **BGRA** at parse time (`TLOTTIE_CHANNEL_BGRA`), so
  its output byte order is what the `B8G8R8A8_UNORM` surfaces expect. No
  per-frame conversion.
- Frames land in the `.tgfc` cache file shared with video through
  `Telegram.Native/Cache`, so the channel order above outlives the run that wrote
  them: swap it and channel-swapped frames are served from disk afterwards.
