#pragma once

#include <cstdint>
#include <lz4.h>

#include "FrameCacheFormat.h"

namespace winrt::Telegram::Native::Cache
{
    // The seam the compression sits behind. It exists so the choice can be revisited without the
    // cache layer knowing: compress a frame, decompress a frame, and say how much room a
    // compressed frame might need. Nothing else about LZ4 leaks past this file.
    //
    // The codec id travels in the header, so introducing a second one does not invalidate the
    // files written by the first.
    struct FrameCodec
    {
        static constexpr FrameCacheCodec Id = FrameCacheCodec::Lz4Raw;

        static size_t Bound(size_t rawSize) noexcept
        {
            if (rawSize == 0 || rawSize > LZ4_MAX_INPUT_SIZE)
            {
                return 0;
            }

            return static_cast<size_t>(LZ4_compressBound(static_cast<int>(rawSize)));
        }

        /// <returns>Compressed size, or 0 on failure.</returns>
        static size_t Compress(const uint8_t* source, size_t sourceSize, uint8_t* destination, size_t destinationCapacity) noexcept
        {
            if (source == nullptr || destination == nullptr || sourceSize == 0)
            {
                return 0;
            }

            auto written = LZ4_compress_default(
                reinterpret_cast<const char*>(source),
                reinterpret_cast<char*>(destination),
                static_cast<int>(sourceSize),
                static_cast<int>(destinationCapacity));

            return written > 0 ? static_cast<size_t>(written) : 0;
        }

        /// <remarks>
        /// _safe rather than _fast: the input is a file that another process, or a half-written
        /// build from an older version, could have left malformed. The bounds check is worth more
        /// than the few percent.
        /// </remarks>
        static bool Decompress(const uint8_t* source, size_t sourceSize, uint8_t* destination, size_t destinationCapacity) noexcept
        {
            if (source == nullptr || destination == nullptr || sourceSize == 0)
            {
                return false;
            }

            auto written = LZ4_decompress_safe(
                reinterpret_cast<const char*>(source),
                reinterpret_cast<char*>(destination),
                static_cast<int>(sourceSize),
                static_cast<int>(destinationCapacity));

            return written > 0 && static_cast<size_t>(written) == destinationCapacity;
        }
    };
}
