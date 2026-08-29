#pragma once

#include <cstdint>

namespace winrt::Telegram::Native::Cache
{
    // The on-disk layout, shared by every animation kind. A new format rather than a version bump
    // of the old one: stale cache files are wiped regularly, so the old ones age out on their own
    // and nothing has to migrate. The extension differs too, so both can coexist during the change.
    //
    // The rule the format is built around: a file that exists is complete. Frames are written to a
    // temporary file and renamed into place only once the index is on disk, so a reader never has
    // to ask whether what it opened is finished, and a build that dies leaves nothing to clean up
    // but a temp file. That is what lets readers run without taking the writer's lock.

    constexpr uint32_t FrameCacheMagic = 0x43464754;    // 'TGFC', little-endian

    // Bumping this is the cheap option and always will be: the reader rejects anything that does
    // not match, the file is rebuilt from a source that is still on disk, and the strays age out
    // with the rest of the cache. There is no migration path and there does not need to be one.
    constexpr uint16_t FrameCacheVersion = 1;

    enum class FrameCacheCodec : uint16_t
    {
        // Raw BGRA through LZ4. Chosen for decompression speed rather than size: the binding
        // constraint is a hundred animations decoding concurrently, not disk. See the codec seam
        // in FrameCodec.h before changing this - the id is stored, so a second codec can coexist.
        Lz4Raw = 1,
    };

#pragma pack(push, 1)

    struct FrameCacheHeader
    {
        uint32_t magic;
        uint16_t version;
        uint16_t codec;

        uint32_t width;
        uint32_t height;
        uint32_t frameCount;

        // The largest compressed frame, so a reader can size its scratch buffer once instead of
        // growing it as it goes.
        uint32_t maxCompressedSize;

        float frameRate;

        // Applied when the frame is drawn, not when it is written, so it has to survive in the
        // file: a cached animation never opens the video it came from and has nowhere else to
        // learn it. Zero for everything that is not a rotated video.
        int32_t rotation;

        uint64_t indexOffset;
    };

    struct FrameCacheEntry
    {
        uint64_t offset;
        uint32_t size;

        // Seconds from the start. Video carries real timestamps; a vector animation writes 0 and
        // the reader falls back to frameRate, which is why this is per frame rather than implied.
        float timestamp;
    };

#pragma pack(pop)

    static_assert(sizeof(FrameCacheHeader) == 40, "the header is written verbatim");
    static_assert(sizeof(FrameCacheEntry) == 16, "the index is written verbatim");
}
