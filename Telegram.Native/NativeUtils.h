#pragma once

#include "NativeUtils.g.h"

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>

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
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value, int32_t offset);
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value, int32_t offset, int32_t length);
        //static int32_t GetDirectionality(char16 value);

        static hstring GetCurrentCulture();
        static hstring GetKeyboardCulture();

        static hstring FormatTime(winrt::Windows::Foundation::DateTime value);
        static hstring FormatDate(winrt::Windows::Foundation::DateTime value, hstring format);
        static hstring FormatDate(int year, int month, int day, hstring format);

        static hstring FormatTime(int value);
        static hstring FormatDate(int value, hstring format);

        static bool IsFileReadable(hstring path);
        static bool IsFileReadable(hstring path, int64_t& fileSize, int64_t& fileTime);

        static bool IsMediaSupported();

        static void OverrideScaleForCurrentView(int32_t value);
        static int32_t GetScaleForCurrentView();

        static void SetFatalErrorCallback(FatalErrorCallback action);
        static winrt::Telegram::Native::FatalError GetFatalError(bool onlyNative);
        static winrt::Telegram::Native::FatalError GetBackTrace(DWORD code);

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
