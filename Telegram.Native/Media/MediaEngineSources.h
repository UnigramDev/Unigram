#pragma once

#include "Media/MediaEngine.h"

#include <winrt/Telegram.Native.Media.h>

namespace winrt::Telegram::Native::Media::implementation
{
    // The bytes of an IAsyncMediaPlayerSource for the engine. ReadCallback only waits until the
    // bytes are on disk, and they are read from the file it downloads into.
    class EngineSource : public IMediaByteSource
    {
    public:
        explicit EngineSource(IAsyncMediaPlayerSource source)
            : m_source(std::move(source))
        {
        }

        ~EngineSource() override
        {
            if (m_file != INVALID_HANDLE_VALUE)
            {
                CloseHandle(m_file);
            }
        }

        void Open() override
        {
            m_source.Open();
        }

        void Close() override
        {
            m_source.Close();
        }

        int64_t Size() override
        {
            return m_source.FileSize();
        }

        int64_t Read(int64_t offset, uint8_t* buffer, int64_t count, int64_t window) override
        {
            if (m_source.Offset() != offset)
            {
                m_source.SeekCallback(offset);
            }

            int64_t available = 0;
            m_source.ReadCallback(count, window, available);

            if (available < 0)
            {
                return ReadStalled;
            }
            else if (available == 0)
            {
                return 0;
            }

            if (m_file == INVALID_HANDLE_VALUE)
            {
                // Opened once and kept: TDLib moves the file when the download completes, and an
                // open handle follows it.
                m_file = CreateFile2FromAppW(m_source.FilePath().c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);

                if (m_file == INVALID_HANDLE_VALUE)
                {
                    return ReadFailed;
                }
            }

            LARGE_INTEGER position{};
            position.QuadPart = offset;

            DWORD read = 0;
            const DWORD wanted = static_cast<DWORD>(std::min<int64_t>(count, available));

            if (!SetFilePointerEx(m_file, position, nullptr, FILE_BEGIN) || !ReadFile(m_file, buffer, wanted, &read, nullptr) || read == 0)
            {
                return ReadFailed;
            }

            m_source.SeekCallback(offset + read);
            return read;
        }

    private:
        IAsyncMediaPlayerSource m_source;
        HANDLE m_file{ INVALID_HANDLE_VALUE };
    };

    class EngineRendition : public IMediaRendition
    {
    public:
        explicit EngineRendition(IAsyncMediaPlayerRendition rendition)
            : m_rendition(std::move(rendition))
        {
        }

        int32_t Width() override
        {
            return m_rendition.Width();
        }

        int32_t Height() override
        {
            return m_rendition.Height();
        }

        int64_t Size() override
        {
            return m_rendition.Size();
        }

        bool HasPlaylist() override
        {
            return m_rendition.HasPlaylist();
        }

        std::string LoadPlaylist() override
        {
            return winrt::to_string(m_rendition.LoadPlaylist());
        }

        void CancelPlaylist() override
        {
            m_rendition.CancelPlaylist();
        }

        std::shared_ptr<IMediaByteSource> CreateSource() override
        {
            auto source = m_rendition.CreateSource();
            return source ? std::make_shared<EngineSource>(source) : nullptr;
        }

        double DownloadSpeed() override
        {
            return m_rendition.DownloadSpeed();
        }

    private:
        IAsyncMediaPlayerRendition m_rendition;
    };
}
