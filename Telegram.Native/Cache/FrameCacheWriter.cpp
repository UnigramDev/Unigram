#include "pch.h"

#include "FrameCacheWriter.h"
#include "FrameCodec.h"

#include <fileapifromapp.h>

namespace winrt::Telegram::Native::Cache
{
    namespace
    {
        bool WriteExact(HANDLE handle, const void* buffer, DWORD size) noexcept
        {
            DWORD written = 0;
            return WriteFile(handle, buffer, size, &written, nullptr) && written == size;
        }

        bool SeekTo(HANDLE handle, uint64_t offset) noexcept
        {
            LARGE_INTEGER position{};
            position.QuadPart = static_cast<LONGLONG>(offset);

            return SetFilePointerEx(handle, position, nullptr, FILE_BEGIN) != FALSE;
        }
    }

    FrameCacheWriter::~FrameCacheWriter()
    {
        Cancel();
    }

    size_t FrameCacheWriter::ScratchSize(uint32_t width, uint32_t height) noexcept
    {
        return FrameCodec::Bound(static_cast<size_t>(width) * height * 4);
    }

    bool FrameCacheWriter::Begin(const std::wstring& path, uint32_t width, uint32_t height, float frameRate, int32_t rotation) noexcept
    {
        Cancel();

        if (width == 0 || height == 0)
        {
            return false;
        }

        // The temp name carries the thread id so two builds of the same key - which the generation
        // service is meant to prevent, but which a second process would not know about - cannot
        // collide on it.
        m_temporary = path + L"." + std::to_wstring(GetCurrentThreadId()) + L".tmp";

        auto handle = CreateFile2FromAppW(
            m_temporary.c_str(),
            GENERIC_READ | GENERIC_WRITE,
            0,
            CREATE_ALWAYS,
            nullptr);

        if (handle == INVALID_HANDLE_VALUE)
        {
            m_temporary.clear();
            return false;
        }

        m_header = {};
        m_header.magic = FrameCacheMagic;
        m_header.version = FrameCacheVersion;
        m_header.codec = static_cast<uint16_t>(FrameCodec::Id);
        m_header.width = width;
        m_header.height = height;
        m_header.frameRate = frameRate;
        m_header.rotation = rotation;

        // A placeholder: the real one goes back over it in Commit, once the counts are known.
        if (!WriteExact(handle, &m_header, sizeof(m_header)))
        {
            CloseHandle(handle);
            DeleteFileFromAppW(m_temporary.c_str());
            m_temporary.clear();
            return false;
        }

        m_handle = handle;
        m_path = path;
        m_position = sizeof(m_header);
        m_index.clear();

        return true;
    }

    bool FrameCacheWriter::Write(const uint8_t* pixels, size_t pixelsSize, float timestamp, uint8_t* scratch, size_t scratchCapacity) noexcept
    {
        if (m_handle == nullptr || pixels == nullptr || scratch == nullptr)
        {
            return false;
        }

        auto compressed = FrameCodec::Compress(pixels, pixelsSize, scratch, scratchCapacity);
        if (compressed == 0 || compressed > MAXDWORD)
        {
            return false;
        }

        if (!WriteExact(m_handle, scratch, static_cast<DWORD>(compressed)))
        {
            return false;
        }

        FrameCacheEntry entry{};
        entry.offset = m_position;
        entry.size = static_cast<uint32_t>(compressed);
        entry.timestamp = timestamp;

        m_index.push_back(entry);
        m_position += compressed;

        if (entry.size > m_header.maxCompressedSize)
        {
            m_header.maxCompressedSize = entry.size;
        }

        return true;
    }

    bool FrameCacheWriter::Commit() noexcept
    {
        if (m_handle == nullptr || m_index.empty())
        {
            Cancel();
            return false;
        }

        auto indexBytes = m_index.size() * sizeof(FrameCacheEntry);
        if (indexBytes > MAXDWORD)
        {
            Cancel();
            return false;
        }

        m_header.frameCount = static_cast<uint32_t>(m_index.size());
        m_header.indexOffset = m_position;

        if (!WriteExact(m_handle, m_index.data(), static_cast<DWORD>(indexBytes))
            || !SeekTo(m_handle, 0)
            || !WriteExact(m_handle, &m_header, sizeof(m_header))
            || !FlushFileBuffers(m_handle))
        {
            Cancel();
            return false;
        }

        CloseHandle(m_handle);
        m_handle = nullptr;

        // No replace flag, deliberately: if another builder got there first their file stands and
        // ours is dropped. Either way what is left on disk is a complete file.
        auto renamed = MoveFileFromAppW(m_temporary.c_str(), m_path.c_str()) != FALSE;
        if (!renamed)
        {
            DeleteFileFromAppW(m_temporary.c_str());
        }

        m_temporary.clear();
        m_index.clear();
        m_position = 0;

        return renamed;
    }

    void FrameCacheWriter::Cancel() noexcept
    {
        if (m_handle != nullptr)
        {
            CloseHandle(m_handle);
            m_handle = nullptr;
        }

        if (!m_temporary.empty())
        {
            DeleteFileFromAppW(m_temporary.c_str());
            m_temporary.clear();
        }

        m_index.clear();
        m_position = 0;
    }
}
