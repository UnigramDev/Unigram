#pragma once

#include <string>
#include <vector>

#include "FrameCacheFormat.h"

namespace winrt::Telegram::Native::Cache
{
    // Reads frames back out of a completed cache file. There is no "is it finished" question to
    // answer: the writer renames into place only once the index is on disk, so a file that opens
    // is a file that is done.
    //
    // Deliberately takes no lock. The writer holds one, but only against other writers of the same
    // key; a reader that had to wait for a build would block whichever shared queue it is running
    // on, and stall every animation behind it.
    class FrameCacheReader
    {
    public:
        FrameCacheReader() = default;
        ~FrameCacheReader();

        FrameCacheReader(const FrameCacheReader&) = delete;
        FrameCacheReader& operator=(const FrameCacheReader&) = delete;

        /// <summary>
        /// Opens and validates. The dimensions are checked against what the caller expects rather
        /// than trusted, because the key is a file name and a stale file with the right name is
        /// the one thing the format cannot rule out.
        /// </summary>
        /// <remarks>
        /// Zero means the caller has no expectation to check against and adopts what the file
        /// says - which is the point of storing it. A video keyed on its *requested* size only
        /// learns its real one by opening a decoder, and doing that on a cache hit is exactly the
        /// work the cache exists to avoid.
        /// </remarks>
        bool Open(const std::wstring& path, uint32_t width = 0, uint32_t height = 0) noexcept;

        void Close() noexcept;

        bool IsOpen() const noexcept
        {
            return m_handle != nullptr;
        }

        uint32_t FrameCount() const noexcept
        {
            return static_cast<uint32_t>(m_index.size());
        }

        float FrameRate() const noexcept
        {
            return m_header.frameRate;
        }

        uint32_t Width() const noexcept
        {
            return m_header.width;
        }

        uint32_t Height() const noexcept
        {
            return m_header.height;
        }

        int32_t Rotation() const noexcept
        {
            return m_header.rotation;
        }

        /// <summary>The scratch a caller needs to hold one compressed frame.</summary>
        uint32_t MaxCompressedSize() const noexcept
        {
            return m_header.maxCompressedSize;
        }

        float Timestamp(uint32_t frame) const noexcept;

        /// <summary>
        /// Decompresses one frame straight into the caller's pixels. <paramref name="scratch"/> is
        /// the caller's, sized at least <see cref="MaxCompressedSize"/>, so that a reader shared
        /// between animations allocates nothing per frame.
        /// </summary>
        bool ReadFrame(uint32_t frame, uint8_t* pixels, size_t pixelsCapacity, uint8_t* scratch, size_t scratchCapacity) noexcept;

    private:
        void* m_handle{ nullptr };
        FrameCacheHeader m_header{};
        std::vector<FrameCacheEntry> m_index;
    };
}
