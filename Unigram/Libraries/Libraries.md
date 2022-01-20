### ffmpeg
ffmpeg is built using vcpkg, but applying some changes to the default portfile:
- Navigate to `vcpkg\ports\ffmpeg`
- Open `portfile.cmake`
- Locate `--enable-libvpx` and add `--enable-decoder=libvpx_vp8 --enable-decoder=libvpx_vp9 --enable-demuxer=matroska` right after it
- Install ffmpeg using the command `vcpkg install ffmpeg[vpx]:arch-uwp`
