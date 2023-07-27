#pragma once

#include "NativeUtils.g.h"

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>

using namespace winrt::Windows::Foundation;

namespace winrt::Telegram::Native::implementation
{
    struct NativeUtils : NativeUtilsT<NativeUtils>
    {
    public:
        static bool FileExists(hstring path);

        static int64_t GetDirectorySize(hstring path);
        static int64_t GetDirectorySize(hstring path, hstring filter);
        static void CleanDirectory(hstring path, int days);
        static void Delete(hstring path);

        static int32_t GetLastInputTime();

        //[DefaultOverload]
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value);
        //static int32_t GetDirectionality(char16 value);

        static hstring GetCurrentCulture();
        static hstring GetKeyboardCulture();

        static bool IsFileReadable(hstring path);
        static bool IsFileReadable(hstring path, int64_t& fileSize, int64_t& fileTime);

        static bool IsMediaSupported();

        static void SetFatalErrorCallback(FatalErrorCallback action);

        static hstring GetBacktrace();

        static void Crash();

        static FatalErrorCallback Callback;

    private:
        static uint64_t GetDirectorySizeInternal(const std::wstring& path, const std::wstring& filter, uint64_t size);
        static void CleanDirectoryInternal(const std::wstring& path, int days);
        static bool IsBrowsePath(const std::wstring& path);
        static ULONGLONG FileTimeToSeconds(FILETIME& ft);
        static bool IsFileReadableInternal(hstring path, int64_t* fileSize, int64_t* fileTime);
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct NativeUtils : NativeUtilsT<NativeUtils, implementation::NativeUtils>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
