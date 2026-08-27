#include "pch.h"
#include "NativeUtils.h"
#if __has_include("NativeUtils.g.cpp")
#include "NativeUtils.g.cpp"
#endif

#include "Helpers/COMHelper.h"
#include "Helpers/LibraryHelper.h"
#include "InternalsRT/CoreWindowHelpers.h"

#include "FatalError.h"

#include <atomic>
#include <roerrorapi.h>
#include <detours.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>

typedef
BOOL
(APIENTRY* pGetKeyboardLayoutNameW)(
    _Out_ LPWSTR pwszKLID
    );

using namespace winrt::Windows::UI::Notifications;
using namespace winrt::Windows::ApplicationModel::Core;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::implementation
{
    FatalErrorCallback NativeUtils::Callback;
    LogCallback NativeUtils::s_logCallback;

    void NativeUtils::SetFatalErrorCallback(FatalErrorCallback callback)
    {
        // TODO: td_set_log_message_callback
        //Client::SetLogMessageCallback(0, &NativeUtils::LogMessageCallback);
        Callback = callback;

        InstallFailFastHooks();

        // WatchDog.Initialize runs from the App constructor, before LifetimeService brings TDLib
        // up, and tdjson.dll is only loaded by the first P/Invoke into it - so GetModuleHandle
        // alone finds nothing and TDLib's fatal messages never reach the callback at all.
        auto tdjson = GetModuleHandle(L"tdjson.dll");
        if (!tdjson) tdjson = LoadLibrary(L"tdjson.dll");

        if (tdjson)
        {
            auto td_set_log_message_callback = reinterpret_cast<PFN_td_set_log_message_callback>(GetProcAddress(tdjson, "td_set_log_message_callback"));
            if (td_set_log_message_callback)
            {
                td_set_log_message_callback(0, &NativeUtils::LogMessageCallback);
            }
        }
    }

    void NativeUtils::SetLogCallback(LogCallback callback)
    {
        s_logCallback = callback;
    }

    void NativeUtils::Log(int32_t level, hstring message, hstring member, hstring filePath, int32_t line)
    {
        if (s_logCallback)
        {
            s_logCallback(level, message, member, filePath, line);
        }
    }

    inline bool Contains(const hstring& message, std::wstring_view text)
    {
        return std::wstring_view{ message }.find(text) != std::wstring_view::npos;
    }

    inline bool Contains(const std::wstring& message, std::wstring_view text)
    {
        return message.find(text) != std::wstring::npos;
    }

    inline bool Contains(const std::string& message, std::string_view text)
    {
        return message.find(text) != std::string::npos;
    }

    inline bool IsDatabaseBrokenError(const std::string& message)
    {
        return Contains(message, "Wrong key or database is corrupted")
            || Contains(message, "SQL logic error or missing database")
            || Contains(message, "database disk image is malformed")
            || Contains(message, "file is encrypted or is not a database")
            || Contains(message, "unsupported file format")
            || Contains(message, "attempt to write a readonly database for database")
            || Contains(message, "file is not a database for database")
            || Contains(message, "Can't open database");
    }

    inline bool IsDiskFullError(const std::string& message)
    {
        return Contains(message, "There is not enough space on the disk")
            || Contains(message, ": 112 :")
            || Contains(message, "database or disk is full")
            || Contains(message, "out of memory for database");
    }

    inline bool IsDiskError(const std::string& message)
    {
        return Contains(message, "I/O error")
            || Contains(message, "Structure needs cleaning");
    }

    inline bool IsBinlogError(const std::string& message)
    {
        return Contains(message, "Failed to rename binlog")
            || Contains(message, "Can't rename")
            || Contains(message, "Failed to unlink old binlog")
            || Contains(message, "td.binlog")
            || Contains(message, ": 8 :")
            || Contains(message, ": 1392 :");
    }

    inline bool IsOutOfMemoryError(const std::string& message)
    {
        return Contains(message, "zlib deflate init failed")
            || Contains(message, "zlib inflate init failed")
            || Contains(message, "out of memory")
            || Contains(message, ": 1450 :");
    }

    void NativeUtils::LogMessageCallback(int verbosity_level, const char* msg)
    {
        std::string message = msg;
        if (NativeUtils::Callback)
        {
            if (IsDatabaseBrokenError(message))
            {
                return;
            }
            else if (IsDiskFullError(message))
            {
                return;
            }
            else if (IsDiskError(message))
            {
                return;
            }
            else if (IsBinlogError(message))
            {
                return;
            }
            else if (IsOutOfMemoryError(message))
            {
                return;
            }

            int bracketCount = 0;
            size_t start = std::string::npos, end = std::string::npos;

            for (size_t i = 0; i < message.length(); ++i)
            {
                if (message[i] == '[')
                {
                    bracketCount++;
                    if (bracketCount == 3)
                    {
                        start = i;
                    }
                }
                if (message[i] == ']' && bracketCount == 3)
                {
                    end = i;
                    break;
                }
            }

            if (start != std::string::npos && end != std::string::npos)
            {
                message.erase(start, end - start + 1);
            }

            NativeUtils::Callback(NativeUtils::GetBackTrace(L"TdException", winrt::to_hstring(message)));
        }
    }

    IXamlDirectObject NativeUtils::AddRunToCollection(XamlDirect direct, IXamlDirectObject inlines, hstring text, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize)
    {
        auto run = direct.CreateInstance(XamlTypeIndex::Run);
        direct.SetStringProperty(run, XamlPropertyIndex::Run_Text, text);
        direct.SetEnumProperty(run, XamlPropertyIndex::Run_FlowDirection, (uint32_t)direction);

        if ((style & TextStyle::Bold) != TextStyle::None)
        {
            direct.SetObjectProperty(run, XamlPropertyIndex::TextElement_FontWeight, winrt::box_value(FontWeights::SemiBold()));
        }

        if ((style & TextStyle::Italic) != TextStyle::None)
        {
            direct.SetEnumProperty(run, XamlPropertyIndex::TextElement_FontStyle, (uint32_t)FontStyle::Italic);
        }

        auto decorations = TextDecorations::None;
        if ((style & TextStyle::Underline) != TextStyle::None)
        {
            decorations |= TextDecorations::Underline;
        }
        if ((style & TextStyle::Strikethrough) != TextStyle::None)
        {
            decorations |= TextDecorations::Strikethrough;
        }

        if (decorations != TextDecorations::None)
        {
            direct.SetEnumProperty(run, XamlPropertyIndex::TextElement_TextDecorations, (uint32_t)decorations);
        }

        if (fontFamily)
        {
            direct.SetObjectProperty(run, XamlPropertyIndex::TextElement_FontFamily, fontFamily);
        }

        if (fontSize > 0)
        {
            direct.SetDoubleProperty(run, XamlPropertyIndex::TextElement_FontSize, fontSize);
        }

        direct.AddToCollection(inlines, run);
        return run;
    }

    IXamlDirectObject NativeUtils::AddRunToCollection(XamlDirect direct, IXamlDirectObject inlines, hstring text, int32_t offset, int32_t length, FlowDirection direction, TextStyle style, FontFamily fontFamily, double fontSize)
    {
        // The slice straight out of the source: a std::wstring of the whole paragraph, then a
        // second copy for substr, was two allocations per run of a message that can be thousands
        // of characters long.
        auto run = direct.CreateInstance(XamlTypeIndex::Run);
        direct.SetStringProperty(run, XamlPropertyIndex::Run_Text, hstring(text.c_str() + offset, length));
        direct.SetEnumProperty(run, XamlPropertyIndex::Run_FlowDirection, (uint32_t)direction);

        if ((style & TextStyle::Bold) != TextStyle::None)
        {
            direct.SetObjectProperty(run, XamlPropertyIndex::TextElement_FontWeight, winrt::box_value(FontWeights::SemiBold()));
        }

        if ((style & TextStyle::Italic) != TextStyle::None)
        {
            direct.SetEnumProperty(run, XamlPropertyIndex::TextElement_FontStyle, (uint32_t)FontStyle::Italic);
        }

        auto decorations = TextDecorations::None;
        if ((style & TextStyle::Underline) != TextStyle::None)
        {
            decorations |= TextDecorations::Underline;
        }
        if ((style & TextStyle::Strikethrough) != TextStyle::None)
        {
            decorations |= TextDecorations::Strikethrough;
        }

        if (decorations != TextDecorations::None)
        {
            direct.SetEnumProperty(run, XamlPropertyIndex::TextElement_TextDecorations, (uint32_t)decorations);
        }

        if (fontFamily)
        {
            direct.SetObjectProperty(run, XamlPropertyIndex::TextElement_FontFamily, fontFamily);
        }

        if (fontSize > 0)
        {
            direct.SetDoubleProperty(run, XamlPropertyIndex::TextElement_FontSize, fontSize);
        }

        direct.AddToCollection(inlines, run);
        return run;
    }

    // combase declares neither entry point in a public header, so these are the signatures its
    // exports carry. Both are __cdecl/__stdcall as declared, which only matters on x86.
    using PFN_RoFailFastWithErrorContextInternal2 = void(__cdecl*)(HRESULT, ULONG, STOWED_EXCEPTION_INFORMATION_V2**);
    using PFN_RaiseFailFastException = void(WINAPI*)(PEXCEPTION_RECORD, PCONTEXT, DWORD);

    static PFN_RoFailFastWithErrorContextInternal2 s_RoFailFastWithErrorContextInternal2 = nullptr;
    static PFN_RaiseFailFastException s_RaiseFailFastException = nullptr;

    // Set on the way into the first fail-fast and never cleared: one of these calls always
    // reaches the other, so without it every WinRT fail-fast would be reported twice - the
    // second time with only a native backtrace - and a fail-fast raised while reporting would
    // recurse. Nothing here has to survive, the process is already going down.
    static std::atomic<bool> s_failFasting = false;

    void NativeUtils::InstallFailFastHooks()
    {
        static bool s_installed = false;
        if (s_installed)
        {
            return;
        }

        s_installed = true;

        if (auto combase = GetModuleHandle(L"combase.dll"))
        {
            s_RoFailFastWithErrorContextInternal2 = reinterpret_cast<PFN_RoFailFastWithErrorContextInternal2>(
                GetProcAddress(combase, "RoFailFastWithErrorContextInternal2"));
        }

        if (auto kernelbase = GetModuleHandle(L"KernelBase.dll"))
        {
            s_RaiseFailFastException = reinterpret_cast<PFN_RaiseFailFastException>(
                GetProcAddress(kernelbase, "RaiseFailFastException"));
        }

        DetourTransactionBegin();
        DetourUpdateThread(GetCurrentThread());

        if (s_RoFailFastWithErrorContextInternal2 != nullptr)
        {
            DetourAttach(reinterpret_cast<PVOID*>(&s_RoFailFastWithErrorContextInternal2), RoFailFastWithErrorContextInternal2Hook);
        }

        if (s_RaiseFailFastException != nullptr)
        {
            DetourAttach(reinterpret_cast<PVOID*>(&s_RaiseFailFastException), RaiseFailFastExceptionHook);
        }

        DetourTransactionCommit();
    }

    /// <summary>
    /// XAML fails fast through this whenever an error reaches ReportUnhandledError, and it does so
    /// even when the app marked the exception handled - ShouldForceFailFast overrules that flag.
    /// The stowed records passed in are the only copy of the originating exception: by the time
    /// UnhandledErrorDetected runs, Propagate has flattened it to a bare HRESULT, and a fail-fast
    /// leaves nothing to unwind afterwards.
    /// </summary>
    void __cdecl NativeUtils::RoFailFastWithErrorContextInternal2Hook(HRESULT result, ULONG count, STOWED_EXCEPTION_INFORMATION_V2** stowed)
    {
        ReportFailFast(GetFailFastException(result, count, stowed));

        s_RoFailFastWithErrorContextInternal2(result, count, stowed);
    }

    /// <summary>
    /// The catch-all. Every fail-fast that is not a WinRT one - the runtime's, the CRT's, the V1
    /// and plain RoFailFastWithErrorContext overloads - arrives here with no stowed records, so
    /// all that can be recovered is the code and the stack it was raised from.
    /// </summary>
    void WINAPI NativeUtils::RaiseFailFastExceptionHook(PEXCEPTION_RECORD exceptionRecord, PCONTEXT contextRecord, DWORD flags)
    {
        if (Callback != nullptr && !s_failFasting)
        {
            auto code = exceptionRecord != nullptr ? exceptionRecord->ExceptionCode : 0;
            ReportFailFast(GetBackTrace(L"FailFastException", hstring(wstrprintf(L"Fail-fast 0x%08X", (ULONG)code))));
        }

        s_RaiseFailFastException(exceptionRecord, contextRecord, flags);
    }

    winrt::Telegram::Native::FatalError NativeUtils::GetFailFastException(HRESULT result, ULONG count, STOWED_EXCEPTION_INFORMATION_V2** stowed)
    {
        winrt::Telegram::Native::FatalError root{ nullptr };
        winrt::Telegram::Native::FatalError last{ nullptr };

        // Capped because a bad count would have us walking off the end of the array, and on
        // this path a wild read turns a legible crash into a confusing one. XAML stows a
        // handful; the dumps that prompted this carried four and six.
        if (count > 64)
        {
            count = 64;
        }

        for (ULONG i = 0; stowed != nullptr && i < count; i++)
        {
            auto error = GetStowedException2(stowed[i]);
            if (error == nullptr)
            {
                continue;
            }

            if (root == nullptr)
            {
                root = error;
            }
            else
            {
                // GetStowedException2 fills InnerException from the record's own nested chain,
                // so walk to the end of it rather than overwriting what it found.
                while (last.InnerException() != nullptr)
                {
                    last = last.InnerException();
                }

                last.InnerException(error);
            }

            last = error;
        }

        // The records carry the frames, the thread's restricted error info carries the
        // description, and one turns up without the other often enough to try both.
        if (root == nullptr)
        {
            root = GetStowedException();
        }

        if (root == nullptr)
        {
            root = winrt::Telegram::Native::FatalError(L"", L"", L"", winrt::single_threaded_vector<FatalErrorFrame>());
        }

        if (root.Type().empty())
        {
            root.Type(L"FailFastException");
        }

        if (root.Message().empty())
        {
            root.Message(hstring(wstrprintf(L"Fail-fast with HRESULT 0x%08X", (ULONG)result)));
        }

        return root;
    }

    void NativeUtils::ReportFailFast(winrt::Telegram::Native::FatalError error)
    {
        if (error == nullptr || Callback == nullptr || s_failFasting.exchange(true))
        {
            return;
        }

        try
        {
            Callback(error);
        }
        catch (...)
        {
            // The process is failing fast either way; losing the report is the only thing left
            // to lose, and throwing from here would take the original crash's identity with it.
        }
    }

    winrt::Telegram::Native::FatalError NativeUtils::GetStowedException()
    {
        HRESULT result;

        winrt::com_ptr<IRestrictedErrorInfo> info;
        //winrt::com_ptr<ILanguageExceptionErrorInfo2> info2;
        //winrt::com_ptr<IUnknown> language;
        winrt::com_ptr<IRestrictedErrorInfoContext> context;
        STOWED_EXCEPTION_INFORMATION_V2* stowed = nullptr;

        // Declared up here because CleanupIfFailed is a goto and cannot jump over an initialization.
        hstring description;

        CleanupIfFailed(result, GetRestrictedErrorInfo(info.put()));
        //CleanupIfFailed(result, info->QueryInterface(info2.put()));
        //CleanupIfFailed(result, info2->GetLanguageException(language.put()));

        //if (language != nullptr && onlyNative)
        //{
        //    // Language exceptions are from CoreCLR
        //    return nullptr;
        //}

        if (info == nullptr)
        {
            return nullptr;
        }

        CleanupIfFailed(result, SetRestrictedErrorInfo(info.get()));

        // For a large family of failures the propagated managed exception is a bare E_FAIL with
        // no message and a stack that only shows the watchdog rethrowing it, so this is the one
        // place the originating description survives. "restrictedDescription" carries the message
        // and its trace, "description" only the message.
        {
            HRESULT error;
            BSTR bstrDescription = nullptr;
            BSTR bstrRestricted = nullptr;
            BSTR bstrCapabilitySid = nullptr;

            if (SUCCEEDED(info->GetErrorDetails(&bstrDescription, &error, &bstrRestricted, &bstrCapabilitySid)))
            {
                if (SysStringLen(bstrRestricted) > 0)
                {
                    description = hstring(bstrRestricted, SysStringLen(bstrRestricted));
                }
                else if (SysStringLen(bstrDescription) > 0)
                {
                    description = hstring(bstrDescription, SysStringLen(bstrDescription));
                }
            }

            SysFreeString(bstrDescription);
            SysFreeString(bstrRestricted);
            SysFreeString(bstrCapabilitySid);
        }

        // Deliberately not CleanupIfFailed: the frames are the better signal, but losing them is
        // no reason to throw the description away too.
        if (SUCCEEDED(info->QueryInterface(context.put())) && context != nullptr
            && SUCCEEDED(context->GetContext(&stowed)))
        {
            auto error = GetStowedException2(stowed);
            if (error != nullptr)
            {
                if (!description.empty())
                {
                    // GetStowedException2 already put the record's own details here.
                    std::wstring detail{ std::wstring_view(error.StackTrace()) };
                    if (!detail.empty())
                    {
                        detail += L"\n";
                    }

                    detail += std::wstring_view(description);
                    error.StackTrace(hstring(detail));
                }

                return error;
            }
        }

        if (description.empty())
        {
            return nullptr;
        }

        return winrt::Telegram::Native::FatalError(L"", L"", description, winrt::single_threaded_vector<FatalErrorFrame>());

    Cleanup:
        return nullptr;
    }

    winrt::Telegram::Native::FatalError NativeUtils::GetStowedException2(STOWED_EXCEPTION_INFORMATION_V2* stowed)
    {
        if (stowed == nullptr || stowed->Header.Signature != 'SE02')
        {
            return nullptr;
        }

        auto frames = winrt::single_threaded_vector<FatalErrorFrame>();

        // ResultCode is the HRESULT the error was stowed with, before propagation flattened it to
        // E_FAIL, and ThreadId is the thread it came from - not necessarily the one whose stack
        // ends up in the report.
        std::wstring detail = wstrprintf(L"Stowed HRESULT 0x%08X on thread %u",
            (ULONG)stowed->ResultCode, (ULONG)stowed->ThreadId);

        if (stowed->ExceptionForm == 1)
        {
            for (int i = 0; i < stowed->StackTraceWords; ++i)
            {
                PVOID pointer;
                if (stowed->StackTraceWordSize == 4)
                {
                    auto addresses = (UINT32**)stowed->StackTrace;
                    pointer = *(addresses + i);
                }
                else if (stowed->StackTraceWordSize == 8)
                {
                    auto addresses = (UINT64**)stowed->StackTrace;
                    pointer = *(addresses + i);
                }
                else
                {
                    continue;
                }

                void* moduleBaseVoid = nullptr;
                RtlPcToFileHeader(pointer, &moduleBaseVoid);

                auto moduleBase = (const unsigned char*)moduleBaseVoid;
                if (moduleBase != nullptr)
                {
                    frames.Append({ (intptr_t)pointer, (intptr_t)moduleBase });
                }
                else
                {
                    //trace += wstrprintf(L"   at %s+0x%016llx\n", L"unknown", (uint64_t)pointer);
                }
            }
        }
        else if (stowed->ExceptionForm == 2 && stowed->ErrorText != nullptr)
        {
            // The text form carries no stack at all, so requiring form 1 discarded it whole.
            detail += L"\n";
            detail += stowed->ErrorText;
        }

        // Returned even with no frames: ResultCode, ThreadId and the nested record are worth
        // keeping on their own, and used to be dropped along with them.
        auto error = winrt::Telegram::Native::FatalError(L"", L"", hstring(detail), frames);

        // Also a property, not only text in the trace: the caller has to be able to tell a record
        // stowed on this thread from one that came back over RPC, which reports zero.
        error.ThreadId(stowed->ThreadId);

        if (stowed->NestedExceptionType == STOWED_EXCEPTION_NESTED_TYPE_STOWED)
        {
            error.InnerException(GetStowedException2((STOWED_EXCEPTION_INFORMATION_V2*)stowed->NestedException));
        }

        return error;
    }

    // From http://davidpritchard.org/archives/907
    winrt::Telegram::Native::FatalError NativeUtils::GetBackTrace(hstring type, hstring message)
    {
        constexpr uint32_t TRACE_MAX_STACK_FRAMES = 99;
        void* stack[TRACE_MAX_STACK_FRAMES];

        ULONG hash;
        const int numFrames = CaptureStackBackTrace(1, TRACE_MAX_STACK_FRAMES, stack, &hash);
        auto frames = winrt::single_threaded_vector<FatalErrorFrame>();

        std::wstring trace;
        bool skipping = false;

        for (int i = 0; i < numFrames; ++i)
        {
            PVOID pointer = (unsigned char*)stack[i];

            void* moduleBaseVoid = nullptr;
            RtlPcToFileHeader(stack[i], &moduleBaseVoid);

            auto moduleBase = (const unsigned char*)moduleBaseVoid;
            wchar_t modulePath[MAX_PATH];

            if (moduleBase != nullptr)
            {
                GetModuleFileName((HMODULE)moduleBase, modulePath, MAX_PATH);

                auto moduleFilename = std::wstring(modulePath);

                int moduleFilenamePos = moduleFilename.find_last_of(L"\\");
                if (moduleFilenamePos >= 0)
                {
                    moduleFilename = moduleFilename.substr(moduleFilenamePos + 1);
                }

                trace += wstrprintf(L"   at %s+0x%08lx\n", moduleFilename.c_str(), (uint32_t)((unsigned char*)pointer - moduleBase));
                frames.Append({ (intptr_t)pointer, (intptr_t)moduleBase });
            }
        }

        if (type.empty())
        {
            if (Contains(trace, L"libvlc.dll") || Contains(trace, L"libvlccore.dll"))
            {
                type = L"VLCException";
            }
            else if (Contains(trace, L"Telegram.Native.Calls.dll"))
            {
                type = L"VoipException";
            }
            else if (Contains(trace, L"Telegram.Td.dll"))
            {
                type = L"TdException";
            }
            else
            {
                type = L"NativeException";
            }
        }

        auto error = winrt::make_self<FatalError>(type, message, hstring(trace), frames);

        // A backtrace is always this thread's, by construction. Filling it in anyway keeps the
        // property meaningful whichever of the two sources a record came from.
        error->ThreadId(::GetCurrentThreadId());

        return error.as<winrt::Telegram::Native::FatalError>();
    }

    winrt::Telegram::Native::FatalError NativeUtils::CreateError(hstring type, hstring message, hstring stackTrace)
    {
        auto error = winrt::make_self<FatalError>(type, message, stackTrace, winrt::single_threaded_vector<FatalErrorFrame>());
        error->ThreadId(::GetCurrentThreadId());

        return error.as<winrt::Telegram::Native::FatalError>();
    }

    uint32_t NativeUtils::GetCurrentThreadId()
    {
        return ::GetCurrentThreadId();
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

    uint32_t NativeUtils::GetLastInputTime()
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

    inline static hstring GetDateFormatEx(CONST SYSTEMTIME* lpDate, hstring format)
    {
        DWORD flags = NULL;
        LPCWSTR formatData = NULL;

        if (format == L"DATE_LONGDATE")
        {
            flags = DATE_LONGDATE;
        }
        else if (format == L"DATE_SHORTDATE")
        {
            flags = DATE_SHORTDATE;
        }
        else
        {
            formatData = format.data();
        }

        TCHAR dateString[256];
        if (GetDateFormatEx(LOCALE_NAME_USER_DEFAULT, flags, lpDate, formatData, dateString, 256, NULL))
        {
            return hstring(dateString);
        }

        return hstring();
    }

    hstring NativeUtils::FormatDate(winrt::Windows::Foundation::DateTime value, hstring format)
    {
        FILETIME fileTime = winrt::clock::to_file_time(value);
        SYSTEMTIME systemTime;
        if (FileTimeToSystemTime(&fileTime, &systemTime))
        {
            SYSTEMTIME localSystemTime;
            if (SystemTimeToTzSpecificLocalTime(NULL, &systemTime, &localSystemTime))
            {
                return GetDateFormatEx(&localSystemTime, format);
            }
        }

        return hstring();
    }

    hstring NativeUtils::FormatDate(int year, int month, int day, hstring format)
    {
        SYSTEMTIME systemTime;
        systemTime.wYear = year;
        systemTime.wMonth = month;
        systemTime.wDay = day;
        systemTime.wHour = 12;

        return GetDateFormatEx(&systemTime, format);
    }

    hstring NativeUtils::FormatTime(int value)
    {
        FILETIME fileTime;
        ULARGE_INTEGER uli;
        uli.QuadPart = (static_cast<ULONGLONG>(value) + 11644473600LL) * 10000000LL;
        fileTime.dwLowDateTime = uli.LowPart;
        fileTime.dwHighDateTime = uli.HighPart;

        SYSTEMTIME systemTime;
        if (FileTimeToSystemTime(&fileTime, &systemTime))
        {
            SYSTEMTIME localSystemTime;
            if (SystemTimeToTzSpecificLocalTime(NULL, &systemTime, &localSystemTime))
            {
                TCHAR timeString[128];
                if (GetTimeFormatEx(LOCALE_NAME_USER_DEFAULT, TIME_NOSECONDS, &localSystemTime, nullptr, timeString, 128))
                {
                    return hstring(timeString);
                }
            }
        }

        return hstring();
    }

    hstring NativeUtils::FormatTime(winrt::Windows::Foundation::DateTime value)
    {
        FILETIME fileTime = winrt::clock::to_file_time(value);
        SYSTEMTIME systemTime;
        if (FileTimeToSystemTime(&fileTime, &systemTime))
        {
            SYSTEMTIME localSystemTime;
            if (SystemTimeToTzSpecificLocalTime(NULL, &systemTime, &localSystemTime))
            {
                TCHAR timeString[128];
                if (GetTimeFormatEx(LOCALE_NAME_USER_DEFAULT, TIME_NOSECONDS, &localSystemTime, nullptr, timeString, 128))
                {
                    return hstring(timeString);
                }

                //switch (GetLastError())
                //{
                //case ERROR_INSUFFICIENT_BUFFER:
                //    return L"E_INSUFFICIENT_BUFFER";
                //case ERROR_INVALID_FLAGS:
                //    return L"E_INVALID_FLAGS";
                //case ERROR_INVALID_PARAMETER:
                //    return L"E_INVALID_PARAMETER";
                //case ERROR_OUTOFMEMORY:
                //    return L"E_OUTOFMEMORY";
                //default:
                //    return L"E_UNKNOWN";
                //}
            }
        }

        return hstring();
    }

    hstring NativeUtils::FormatDate(int value, hstring format)
    {
        FILETIME fileTime;
        ULARGE_INTEGER uli;
        uli.QuadPart = (static_cast<ULONGLONG>(value) + 11644473600LL) * 10000000LL;
        fileTime.dwLowDateTime = uli.LowPart;
        fileTime.dwHighDateTime = uli.HighPart;

        SYSTEMTIME systemTime;
        if (FileTimeToSystemTime(&fileTime, &systemTime))
        {
            SYSTEMTIME localSystemTime;
            if (SystemTimeToTzSpecificLocalTime(NULL, &systemTime, &localSystemTime))
            {
                return GetDateFormatEx(&localSystemTime, format);
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

    void NativeUtils::OverrideScaleForCurrentView(int32_t value)
    {
        InternalsRT::Core::Windowing::CoreWindowHelpers::OverrideDpiForCurrentThread(value * 96.0f / 100.0f);
    }

    int32_t NativeUtils::GetScaleForCurrentView()
    {
        return InternalsRT::Core::Windowing::CoreWindowHelpers::GetDpiForCurrentThread() / 96.0f * 100.0f;
    }

    void NativeUtils::Crash()
    {
        std::thread([]() {
            // Both volatile on purpose. Release folded the constant division this used to do
            // away entirely - the button did nothing at all - and without the second volatile
            // the optimiser can prove the pointer null and emit something that is not an
            // access violation.
            volatile int* volatile address = nullptr;
            *address = 42;
            }).detach();
    }

    hstring NativeUtils::GetLogMessage(int64_t format, int64_t args)
    {
        int byteLength = vsnprintf(NULL, NULL, (char*)format, (va_list)args) + 1;
        if (byteLength <= 1)
            return L"";

        char* buffer = new char[byteLength];
        vsprintf(buffer, (char*)format, (va_list)args);
        hstring result = winrt::to_hstring(std::string(buffer, byteLength - 1));
        delete[] buffer;
        return result;
    }

} // namespace winrt::Telegram::Native::implementation
