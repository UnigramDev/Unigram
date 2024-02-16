#pragma once

#include "FileStreamFromApp.g.h"

namespace winrt::Telegram::Native::implementation
{
    struct FileStreamFromApp : FileStreamFromAppT<FileStreamFromApp>
    {
        FileStreamFromApp(hstring path);

        bool IsValid();

        bool Seek(int64_t offset);
        int32_t Read(int64_t pointer, uint32_t length);

        void Close();

    private:        
        std::mutex m_lock;
        HANDLE m_handle;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct FileStreamFromApp : FileStreamFromAppT<FileStreamFromApp, implementation::FileStreamFromApp>
    {
    };
}
