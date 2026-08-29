#pragma once

#include <string>
#include <vector>

#include "FrameCacheFormat.h"

namespace winrt::Telegram::Native::Cache
{
    // Builds a cache file. Frames go into a temporary file beside the target and the whole thing is
    // renamed into place once the index is written, which is what lets readers skip validation and
    // skip the writer's lock entirely.
    //
    // The rename is MoveFileFromAppW, which has no replace flag - and that is the behaviour we
    // want. If someone else finished the same file first, the rename fails, we drop the temp, and
    // their copy stands. First writer wins, with no lock held across the build.
    //
    // Abandoning is free: Cancel (or simply destructing) deletes the temp and leaves nothing
    // half-written for anyone to find.
    class FrameCacheWriter
    {
    public:
        FrameCacheWriter() = default;
        ~FrameCacheWriter();

        FrameCacheWriter(const FrameCacheWriter&) = delete;
        FrameCacheWriter& operator=(const FrameCacheWriter&) = delete;

        bool Begin(const std::wstring& path, uint32_t width, uint32_t height, float frameRate, int32_t rotation) noexcept;

        bool IsOpen() const noexcept
        {
            return m_handle != nullptr;
        }

        /// <summary>
        /// Compresses and appends one frame. <paramref name="scratch"/> is the caller's, sized at
        /// least <see cref="ScratchSize"/> - the generation service hands the same buffer to every
        /// build so that concurrency, not frame count, bounds the memory.
        /// </summary>
        bool Write(const uint8_t* pixels, size_t pixelsSize, float timestamp, uint8_t* scratch, size_t scratchCapacity) noexcept;

        /// <summary>Writes the index, closes, and renames into place.</summary>
        bool Commit() noexcept;

        void Cancel() noexcept;

        /// <summary>How much scratch <see cref="Write"/> needs for a frame of this size.</summary>
        static size_t ScratchSize(uint32_t width, uint32_t height) noexcept;

    private:
        void* m_handle{ nullptr };
        std::wstring m_path;
        std::wstring m_temporary;

        FrameCacheHeader m_header{};
        std::vector<FrameCacheEntry> m_index;
        uint64_t m_position{ 0 };
    };
}
