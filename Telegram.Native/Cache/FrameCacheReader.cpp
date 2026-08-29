#include "pch.h"

#include "FrameCacheReader.h"
#include "FrameCodec.h"

#include <fileapifromapp.h>

namespace winrt::Telegram::Native::Cache
{
    namespace
    {
        bool ReadExact(HANDLE handle, void* buffer, DWORD size) noexcept
        {
            DWORD read = 0;
            return ReadFile(handle, buffer, size, &read, nullptr) && read == size;
        }

        bool SeekTo(HANDLE handle, uint64_t offset) noexcept
        {
            LARGE_INTEGER position{};
            position.QuadPart = static_cast<LONGLONG>(offset);

            return SetFilePointerEx(handle, position, nullptr, FILE_BEGIN) != FALSE;
        }
    }

    FrameCacheReader::~FrameCacheReader()
    {
        Close();
    }

    bool FrameCacheReader::Open(const std::wstring& path, uint32_t width, uint32_t height) noexcept
    {
        Close();

        // Shared for write as well as read: a writer building a *different* size of the same
        // animation must not be blocked by us, and the atomic rename never touches this file.
        auto handle = CreateFile2FromAppW(
            path.c_str(),
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            OPEN_EXISTING,
            nullptr);

        if (handle == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        FrameCacheHeader header{};
        if (!ReadExact(handle, &header, sizeof(header)))
        {
            CloseHandle(handle);
            return false;
        }

        // A stale file with the right name is the one thing the format cannot rule out, so the
        // dimensions are checked rather than trusted.
        auto valid = header.magic == FrameCacheMagic
            && header.version == FrameCacheVersion
            && header.codec == static_cast<uint16_t>(FrameCodec::Id)
            && (width == 0 || header.width == width)
            && (height == 0 || header.height == height)
            && header.width > 0
            && header.height > 0
            && header.frameCount > 0
            && header.maxCompressedSize > 0;

        if (!valid)
        {
            CloseHandle(handle);
            return false;
        }

        std::vector<FrameCacheEntry> index(header.frameCount);

        auto indexBytes = static_cast<uint64_t>(header.frameCount) * sizeof(FrameCacheEntry);
        if (indexBytes > MAXDWORD
            || !SeekTo(handle, header.indexOffset)
            || !ReadExact(handle, index.data(), static_cast<DWORD>(indexBytes)))
        {
            CloseHandle(handle);
            return false;
        }

        m_handle = handle;
        m_header = header;
        m_index = std::move(index);

        return true;
    }

    void FrameCacheReader::Close() noexcept
    {
        if (m_handle != nullptr)
        {
            CloseHandle(m_handle);
            m_handle = nullptr;
        }

        m_index.clear();
        m_header = {};
    }

    float FrameCacheReader::Timestamp(uint32_t frame) const noexcept
    {
        if (frame >= m_index.size())
        {
            return 0;
        }

        auto timestamp = m_index[frame].timestamp;
        if (timestamp > 0)
        {
            return timestamp;
        }

        // A vector animation writes no timestamps: its frames are evenly spaced, so the rate is
        // all the information there is.
        return m_header.frameRate > 0 ? frame / m_header.frameRate : 0;
    }

    bool FrameCacheReader::ReadFrame(uint32_t frame, uint8_t* pixels, size_t pixelsCapacity, uint8_t* scratch, size_t scratchCapacity) noexcept
    {
        if (m_handle == nullptr || frame >= m_index.size() || pixels == nullptr || scratch == nullptr)
        {
            return false;
        }

        const auto& entry = m_index[frame];

        if (entry.size > scratchCapacity || entry.size == 0)
        {
            return false;
        }

        if (!SeekTo(m_handle, entry.offset) || !ReadExact(m_handle, scratch, entry.size))
        {
            return false;
        }

        return FrameCodec::Decompress(scratch, entry.size, pixels, pixelsCapacity);
    }
}
