#include "pch.h"
#include "FileStreamFromApp.h"
#if __has_include("FileStreamFromApp.g.cpp")
#include "FileStreamFromApp.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    FileStreamFromApp::FileStreamFromApp(hstring path)
    {
        DWORD desired_access = GENERIC_READ;

        // TODO: share mode
        DWORD share_mode = FILE_SHARE_READ | FILE_SHARE_DELETE | FILE_SHARE_WRITE;

        DWORD creation_disposition = OPEN_ALWAYS;

        DWORD native_flags = FILE_FLAG_BACKUP_SEMANTICS;
        //if (flags & Direct) {
        //	native_flags |= FILE_FLAG_WRITE_THROUGH | FILE_FLAG_NO_BUFFERING;
        //}
        //if (flags & WinStat) {
        //	native_flags |= FILE_FLAG_BACKUP_SEMANTICS;
        //}
        CREATEFILE2_EXTENDED_PARAMETERS extended_parameters;
        std::memset(&extended_parameters, 0, sizeof(extended_parameters));
        extended_parameters.dwSize = sizeof(extended_parameters);
        extended_parameters.dwFileAttributes = FILE_ATTRIBUTE_NORMAL;
        extended_parameters.dwFileFlags = native_flags;
        auto handle = CreateFile2FromAppW(path.c_str(), desired_access, share_mode, creation_disposition, &extended_parameters);

        m_handle = handle;
    }

    bool FileStreamFromApp::IsValid()
    {
        slim_lock_guard const guard(m_lock);
        return m_handle != INVALID_HANDLE_VALUE;
    }

    bool FileStreamFromApp::Seek(int64_t offset)
    {
        slim_lock_guard const guard(m_lock);

        if (m_handle != INVALID_HANDLE_VALUE)
        {
            LARGE_INTEGER distancetoMove{};
            distancetoMove.QuadPart = offset;

            return SetFilePointerEx(m_handle, distancetoMove, NULL, FILE_BEGIN);
        }

        return false;
    }

    int32_t FileStreamFromApp::Read(int64_t pointer, uint32_t length)
    {
        slim_lock_guard const guard(m_lock);

        if (m_handle != INVALID_HANDLE_VALUE)
        {
            DWORD numberOfBytesRead;
            if (ReadFile(m_handle, (LPVOID)pointer, length, &numberOfBytesRead, NULL))
            {
                return numberOfBytesRead;
            }
        }

        return -1;
    }

    void FileStreamFromApp::Close()
    {
        slim_lock_guard const guard(m_lock);

        if (m_handle != INVALID_HANDLE_VALUE)
        {
            CloseHandle(m_handle);
        }

        m_handle = INVALID_HANDLE_VALUE;
    }
}
