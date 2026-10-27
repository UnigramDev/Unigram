#pragma once

#ifdef WINUI3
#include <appmodel.h>
#include <shlobj_core.h>
#endif

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>

namespace winrt::Telegram::Native
{
    /// <summary>
    /// Where the native layer lives, answered without package identity.
    ///
    /// Package::Current and ApplicationData::Current both throw APPMODEL_ERROR_NO_PACKAGE
    /// (0x80073D54) in a process that was never registered, and the six call sites behind these
    /// three functions are the only places this component asks. Their unpackaged answers have to
    /// match Telegram\Shims\ApplicationDataEx.cs and PackageEx.cs exactly - the managed and the
    /// native halves read and write the same folders.
    ///
    /// Only the desktop build of this component has the fallbacks. The app container build cannot
    /// load without its package anyway, since it links the Store CRT, and the shell functions the
    /// fallbacks need are not in its API set.
    /// </summary>
    namespace AppModel
    {
#ifdef WINUI3
        inline bool HasPackageIdentity()
        {
            static const bool packaged = []
                {
                    uint32_t length = 0;
                    return GetCurrentPackageFullName(&length, nullptr) != APPMODEL_ERROR_NO_PACKAGE;
                }();

            return packaged;
        }

        namespace details
        {
            /// <summary>
            /// Unpackaged, the payload is simply the folder the process was started from, which is
            /// where Assets\ is laid down either way.
            /// </summary>
            inline hstring ModuleFolder()
            {
                wchar_t buffer[MAX_PATH]{};
                auto length = GetModuleFileName(nullptr, buffer, ARRAYSIZE(buffer));

                while (length > 0 && buffer[length - 1] != L'\\')
                {
                    length--;
                }

                // Without the trailing separator, as InstalledLocation().Path() is: every caller
                // concatenates a "\\Assets\\..." of its own.
                return hstring(buffer, length > 0 ? length - 1 : 0);
            }

            inline hstring ContainerFolder(std::wstring_view name)
            {
                wchar_t* local;
                check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local));

                std::wstring path(local);
                CoTaskMemFree(local);

                path.append(L"\\Unigram\\").append(name);

                // The managed half creates these on the way up and gets there first, but this
                // must not depend on the order the two halves happen to start in.
                SHCreateDirectoryEx(nullptr, path.c_str(), nullptr);

                return hstring(path);
            }
        }
#endif

        inline hstring InstalledPath()
        {
#ifdef WINUI3
            if (!HasPackageIdentity())
            {
                return details::ModuleFolder();
            }
#endif

            return Windows::ApplicationModel::Package::Current().InstalledLocation().Path();
        }

        inline hstring LocalPath()
        {
#ifdef WINUI3
            if (!HasPackageIdentity())
            {
                return details::ContainerFolder(L"LocalState");
            }
#endif

            return Windows::Storage::ApplicationData::Current().LocalFolder().Path();
        }

        inline hstring TemporaryPath()
        {
#ifdef WINUI3
            if (!HasPackageIdentity())
            {
                return details::ContainerFolder(L"TempState");
            }
#endif

            return Windows::Storage::ApplicationData::Current().TemporaryFolder().Path();
        }
    }
}
