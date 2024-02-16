#include "pch.h"
#include "NativeUtils.h"
#if __has_include("NativeUtils.g.cpp")
#include "NativeUtils.g.cpp"
#endif

#include "Helpers\LibraryHelper.h"
#include "DebugUtils.h"

#include <winrt/Windows.Data.Xml.Dom.h>
#include <winrt/Windows.UI.Notifications.h>

typedef
BOOL
(APIENTRY* pGetKeyboardLayoutNameW)(
    _Out_ LPWSTR pwszKLID
    );

using namespace winrt::Windows::Data::Xml::Dom;
using namespace winrt::Windows::UI::Notifications;

namespace winrt::Telegram::Native::implementation
{
    FatalErrorCallback NativeUtils::Callback;

    void NativeUtils::SetFatalErrorCallback(FatalErrorCallback callback)
    {
        Callback = callback;
    }

    bool NativeUtils::FileExists(hstring path)
    {
        WIN32_FILE_ATTRIBUTE_DATA fileInfo;
        return GetFileAttributesExFromAppW(path.data(), GetFileExInfoStandard, (void*)&fileInfo);
    }

    int64_t NativeUtils::GetDirectorySize(hstring path)
    {
        return GetDirectorySize(path, L"\\*");
    }

    int64_t NativeUtils::GetDirectorySize(hstring path, hstring filter)
    {
        return GetDirectorySizeInternal(path.data(), filter.data(), 0);
    }

    void NativeUtils::CleanDirectory(hstring path, int days)
    {
        CleanDirectoryInternal(path.data(), days);
    }

    void NativeUtils::Delete(hstring path)
    {
        DeleteFile(path.data());
    }

    void NativeUtils::CleanDirectoryInternal(const std::wstring& path, int days)
    {
        long diff = 60 * 60 * 1000 * 24 * days;

        FILETIME ft;
        GetSystemTimeAsFileTime(&ft);
        auto currentTime = FileTimeToSeconds(ft);

        WIN32_FIND_DATA data;
        HANDLE handle = FindFirstFile((path + L"\\*").c_str(), &data);

        if (handle == INVALID_HANDLE_VALUE)
        {
            return;
        }

        do
        {
            if (IsBrowsePath(data.cFileName))
            {
                continue;
            }

            if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY)
            {
                CleanDirectoryInternal(path + L"\\" + data.cFileName, days);
            }
            else
            {
                auto lastAccess = FileTimeToSeconds(data.ftLastAccessTime);
                auto lastWrite = FileTimeToSeconds(data.ftLastWriteTime);

                if (days == 0)
                {
                    DeleteFile((path + L"\\" + data.cFileName).c_str());
                }
                else if (lastAccess > lastWrite)
                {
                    if (lastAccess + diff < currentTime)
                    {
                        DeleteFile((path + L"\\" + data.cFileName).c_str());
                    }
                }
                else if (lastWrite + diff < currentTime)
                {
                    DeleteFile((path + L"\\" + data.cFileName).c_str());
                }
            }

        } while (FindNextFile(handle, &data));

        FindClose(handle);
    }

    uint64_t NativeUtils::GetDirectorySizeInternal(const std::wstring& path, const std::wstring& filter, uint64_t size)
    {
        WIN32_FIND_DATA data;
        HANDLE handle = FindFirstFile((path + filter).c_str(), &data);

        if (handle == INVALID_HANDLE_VALUE)
        {
            return size;
        }

        do
        {
            if (IsBrowsePath(data.cFileName))
            {
                continue;
            }

            if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY)
            {
                size = GetDirectorySizeInternal(path + L"\\" + data.cFileName, filter, size);
            }
            else
            {
                size += (uint64_t)(data.nFileSizeHigh * (MAXDWORD)+data.nFileSizeLow);
            }

        } while (FindNextFile(handle, &data));

        FindClose(handle);

        return size;
    }

    bool NativeUtils::IsBrowsePath(const std::wstring& path)
    {
        return (path.find(L".") == 0 || path.find(L"..") == 0);
    }

    ULONGLONG NativeUtils::FileTimeToSeconds(FILETIME& ft)
    {
        ULARGE_INTEGER uli;
        uli.HighPart = ft.dwHighDateTime;
        uli.LowPart = ft.dwLowDateTime;

        return uli.QuadPart / 10000;
    }

    int32_t NativeUtils::GetLastInputTime()
    {
        typedef BOOL(WINAPI* pGetLastInputInfo)(_Out_ PLASTINPUTINFO);

        static const LibraryInstance user32(L"User32.dll", 0x00000001);
        static const auto getLastInputInfo = user32.GetMethod<pGetLastInputInfo>("GetLastInputInfo");

        if (getLastInputInfo == nullptr)
        {
            return 0;
        }

        LASTINPUTINFO lastInput;
        lastInput.cbSize = sizeof(LASTINPUTINFO);

        if (getLastInputInfo(&lastInput))
        {
            return lastInput.dwTime;
        }

        return 0;
    }

    winrt::Telegram::Native::TextDirectionality NativeUtils::GetDirectionality(hstring value)
    {
        return GetDirectionality(value, 0, value.size());
    }

    winrt::Telegram::Native::TextDirectionality NativeUtils::GetDirectionality(hstring value, int32_t offset)
    {
        return GetDirectionality(value, offset, value.size() - offset);
    }

    winrt::Telegram::Native::TextDirectionality NativeUtils::GetDirectionality(hstring value, int32_t offset, int32_t length)
    {
        DWORD prev = C2_OTHERNEUTRAL;
        for (int i = 0; i < length; i++)
        {
            if (IS_HIGH_SURROGATE(value[offset + i]) || IS_LOW_SURROGATE(value[offset + i]))
            {
                continue;
            }

            WORD type;
            GetStringTypeEx(LOCALE_USER_DEFAULT, CT_CTYPE2, value.data() + offset + i, 1, &type);

            // We use the first strong character after a neutral character.
            if (prev >= C2_BLOCKSEPARATOR && prev <= C2_OTHERNEUTRAL)
            {
                if (type == C2_LEFTTORIGHT)
                {
                    return winrt::Telegram::Native::TextDirectionality::LeftToRight;
                }
                else if (type == C2_RIGHTTOLEFT)
                {
                    return winrt::Telegram::Native::TextDirectionality::RightToLeft;
                }
            }

            prev = type;
        }

        return winrt::Telegram::Native::TextDirectionality::Neutral;
    }

    hstring NativeUtils::GetCurrentCulture()
    {
        TCHAR buff[LOCALE_NAME_MAX_LENGTH];
        int result = GetLocaleInfoEx(LOCALE_NAME_USER_DEFAULT, LOCALE_SNAME, buff, LOCALE_NAME_MAX_LENGTH);
        if (result == 0)
        {
            result = GetLocaleInfoEx(LOCALE_NAME_SYSTEM_DEFAULT, LOCALE_SNAME, buff, LOCALE_NAME_MAX_LENGTH);
            if (result == 0)
            {
                return L"en";
            }
        }

        std::wstring str = buff;
        size_t sorting = str.find(L"_");

        if (sorting != std::wstring::npos)
        {
            return str.substr(0, sorting).c_str();
        }

        return buff;
    }

    hstring NativeUtils::GetKeyboardCulture()
    {
        // TODO: I'm not sure about how much expensive this call is.
        // At the moment it isn't used extremely often, but we should
        // consider caching it (problem is how to invalidate the cache)
        static const LibraryInstance user32(L"User32.dll");
        static const auto getKeyboardLayoutName = user32.GetMethod<pGetKeyboardLayoutNameW>("GetKeyboardLayoutNameW");

        WCHAR name[KL_NAMELENGTH];
        if (getKeyboardLayoutName(name))
        {
            // The layout name looks something like this: 00000410
            // Where the first 4 bytes are most likely flags
            // And the second half is actually the LCID as a HEX string
            unsigned int lcid = std::stoul(name + 4, nullptr, 16);

            WCHAR locale[LOCALE_NAME_MAX_LENGTH];
            int length = LCIDToLocaleName(lcid, locale, LOCALE_NAME_MAX_LENGTH, 0);

            if (length > 0)
            {
                // The string is null terminated
                return hstring(locale, length - 1);
            }
        }

        // TODO: probably better this than an empty string.
        return L"en-US";
    }

    hstring NativeUtils::FormatTime(int value)
    {
        FILETIME fileTime;
        ULARGE_INTEGER uli;
        uli.QuadPart = (static_cast<ULONGLONG>(value) + 11644473600LL) * 10000000LL;
        fileTime.dwLowDateTime = uli.LowPart;
        fileTime.dwHighDateTime = uli.HighPart;

        FILETIME localFileTime;
        if (FileTimeToLocalFileTime(&fileTime, &localFileTime))
        {
            SYSTEMTIME systemTime;
            if (FileTimeToSystemTime(&localFileTime, &systemTime))
            {
                TCHAR timeString[128];
                if (GetTimeFormatEx(LOCALE_NAME_USER_DEFAULT, TIME_NOSECONDS, &systemTime, nullptr, timeString, 128))
                {
                    return hstring(timeString);
                }

                switch (GetLastError())
                {
                case ERROR_INSUFFICIENT_BUFFER:
                    return L"E_INSUFFICIENT_BUFFER";
                case ERROR_INVALID_FLAGS:
                    return L"E_INVALID_FLAGS";
                case ERROR_INVALID_PARAMETER:
                    return L"E_INVALID_PARAMETER";
                case ERROR_OUTOFMEMORY:
                    return L"E_OUTOFMEMORY";
                default:
                    return L"E_UNKNOWN";
                }
            }
        }

        return hstring();
    }

    hstring NativeUtils::FormatDate(int value)
    {
        // TODO: DATE_MONTHDAY doesn't seem to work, so we're not using this method.

        FILETIME fileTime;
        ULARGE_INTEGER uli;
        uli.QuadPart = (static_cast<ULONGLONG>(value) + 11644473600LL) * 10000000LL;
        fileTime.dwLowDateTime = uli.LowPart;
        fileTime.dwHighDateTime = uli.HighPart;

        FILETIME localFileTime;
        if (FileTimeToLocalFileTime(&fileTime, &localFileTime))
        {
            SYSTEMTIME systemTime;
            if (FileTimeToSystemTime(&localFileTime, &systemTime))
            {
                SYSTEMTIME todayTime;
                GetSystemTime(&todayTime);

                int difference = abs(systemTime.wMonth - todayTime.wMonth + 12 * (systemTime.wYear - todayTime.wYear));
                DWORD flags = difference >= 11
                    ? DATE_LONGDATE
                    : DATE_MONTHDAY;

                TCHAR dateString[256];
                if (GetDateFormatEx(LOCALE_NAME_USER_DEFAULT, flags, &systemTime, nullptr, dateString, 256, nullptr))
                {
                    return hstring(dateString);
                }

                switch (GetLastError())
                {
                case ERROR_INSUFFICIENT_BUFFER:
                    return L"E_INSUFFICIENT_BUFFER";
                case ERROR_INVALID_FLAGS:
                    return L"E_INVALID_FLAGS";
                case ERROR_INVALID_PARAMETER:
                    return L"E_INVALID_PARAMETER";
                case ERROR_OUTOFMEMORY:
                    return L"E_OUTOFMEMORY";
                default:
                    return L"E_UNKNOWN";
                }
            }
        }

        return hstring();
    }

    bool NativeUtils::IsFileReadable(hstring path)
    {
        return IsFileReadableInternal(path, NULL, NULL);
    }

    bool NativeUtils::IsFileReadable(hstring path, int64_t& fileSize, int64_t& fileTime)
    {
        return IsFileReadableInternal(path, &fileSize, &fileTime);
    }

    bool NativeUtils::IsFileReadableInternal(hstring path, int64_t* fileSize, int64_t* fileTime)
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

        if (handle == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        if (fileSize)
        {
            LARGE_INTEGER pFileSize;
            GetFileSizeEx(handle, &pFileSize);

            *fileSize = static_cast<int64_t>(pFileSize.QuadPart);
        }

        if (fileTime)
        {
            FILETIME pFileTime;
            GetFileTime(handle, NULL, NULL, &pFileTime);

            *fileTime = static_cast<int64_t>(FileTimeToSeconds(pFileTime));
        }

        CloseHandle(handle);
        return true;
    }

    bool NativeUtils::IsMediaSupported()
    {
        HRESULT result;
        result = MFStartup(MF_VERSION);

        if (result == S_OK)
        {
            MFShutdown();
        }

        return result != E_NOTIMPL;
    }

    hstring NativeUtils::GetBacktrace()
    {
        return hstring(::GetBacktrace(0));
    }

    void NativeUtils::Crash()
    {
        int32_t* ciao = nullptr;
        *ciao = 42;
    }

} // namespace winrt::Telegram::Native::implementation
