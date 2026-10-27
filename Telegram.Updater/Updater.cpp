// Applies an update the unpackaged app has staged, after the app has exited.
//
//   %LOCALAPPDATA%\TelegramFZ-LLC.Windows\
//     Updater.exe     this program, copied here by the app before each run
//     app\            the installed version
//     app.new\        the staged one; complete once app.new\.update exists
//     app.old\        the version replaced, deleted by the app once the new one is up
//
// The swap is two directory renames on one volume, so at every point either app\ is a complete
// version or the previous one can be renamed back. Nothing is copied.
//
// Statically linked against the CRT: it runs from the root, and the CRT DLLs are in app\, which
// it renames away.

#include <windows.h>
#include <shellapi.h>

#include <cstdarg>
#include <cstdio>
#include <string>

namespace
{
    // The {AppId}_is1 key Inno Setup writes for a per-user install. The AppId is the one in
    // Telegram.Installer\Telegram.iss and must never change.
    constexpr wchar_t kUninstallKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{A3996223-6B78-4784-9615-A466F009E4C6}_is1";

    constexpr DWORD kExitTimeout = 30000;

    // Antivirus, the indexer or an Explorer window can hold a handle in the folder briefly.
    constexpr int kRenameAttempts = 40;
    constexpr DWORD kRenameDelay = 250;

    std::wstring g_root;
    FILE* g_log = nullptr;

    void Log(const wchar_t* format, ...)
    {
        if (!g_log)
        {
            return;
        }

        SYSTEMTIME now;
        GetLocalTime(&now);
        fwprintf(g_log, L"%04u-%02u-%02u %02u:%02u:%02u.%03u ", now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond, now.wMilliseconds);

        va_list args;
        va_start(args, format);
        vfwprintf(g_log, format, args);
        va_end(args);

        fputwc(L'\n', g_log);
        fflush(g_log);
    }

    std::wstring Path(const wchar_t* name)
    {
        return g_root + L'\\' + name;
    }

    bool Exists(const std::wstring& path)
    {
        return GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES;
    }

    // Returns once the process is gone. A creation time that does not match means the id was
    // reused by something else, so the process being waited for has already exited.
    bool WaitForExit(DWORD pid, ULONGLONG created)
    {
        HANDLE process = OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (process == nullptr)
        {
            return true;
        }

        FILETIME creation, exit, kernel, user;
        if (created != 0 && GetProcessTimes(process, &creation, &exit, &kernel, &user))
        {
            ULONGLONG actual = (ULONGLONG(creation.dwHighDateTime) << 32) | creation.dwLowDateTime;
            if (actual != created)
            {
                CloseHandle(process);
                return true;
            }
        }

        DWORD result = WaitForSingleObject(process, kExitTimeout);
        CloseHandle(process);

        return result == WAIT_OBJECT_0;
    }

    bool Rename(const std::wstring& from, const std::wstring& to)
    {
        for (int attempt = 1;; attempt++)
        {
            if (MoveFileExW(from.c_str(), to.c_str(), 0))
            {
                return true;
            }

            DWORD error = GetLastError();
            if (attempt == kRenameAttempts)
            {
                Log(L"Rename %s -> %s failed: %lu", from.c_str(), to.c_str(), error);
                return false;
            }

            Sleep(kRenameDelay);
        }
    }

    // Junctions are removed, never followed: what they point at is not ours.
    bool DeleteTree(const std::wstring& path)
    {
        WIN32_FIND_DATAW data;
        HANDLE find = FindFirstFileW((path + L"\\*").c_str(), &data);

        if (find != INVALID_HANDLE_VALUE)
        {
            do
            {
                if (wcscmp(data.cFileName, L".") == 0 || wcscmp(data.cFileName, L"..") == 0)
                {
                    continue;
                }

                std::wstring child = path + L'\\' + data.cFileName;

                if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0 && (data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0)
                {
                    DeleteTree(child);
                }
                else
                {
                    if ((data.dwFileAttributes & FILE_ATTRIBUTE_READONLY) != 0)
                    {
                        SetFileAttributesW(child.c_str(), data.dwFileAttributes & ~FILE_ATTRIBUTE_READONLY);
                    }

                    if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                    {
                        RemoveDirectoryW(child.c_str());
                    }
                    else
                    {
                        DeleteFileW(child.c_str());
                    }
                }
            } while (FindNextFileW(find, &data));

            FindClose(find);
        }

        return RemoveDirectoryW(path.c_str()) || GetLastError() == ERROR_FILE_NOT_FOUND;
    }

    // The marker holds the staged version as one line of ASCII, written by the app last.
    std::wstring ReadVersion(const std::wstring& marker)
    {
        std::wstring version;

        FILE* file = nullptr;
        if (_wfopen_s(&file, marker.c_str(), L"rb") == 0 && file != nullptr)
        {
            int c;
            while ((c = fgetc(file)) != EOF && c != '\r' && c != '\n' && version.size() < 32)
            {
                version.push_back(wchar_t(c));
            }

            fclose(file);
        }

        return version;
    }

    // Installed apps reads the version from Inno Setup's uninstall entry, which only the
    // installer writes. Missing when the app was not installed by it, which is fine.
    void UpdateUninstallEntry(const std::wstring& version)
    {
        HKEY key;
        if (RegOpenKeyExW(HKEY_CURRENT_USER, kUninstallKey, 0, KEY_SET_VALUE, &key) != ERROR_SUCCESS)
        {
            Log(L"No uninstall entry to update");
            return;
        }

        if (!version.empty())
        {
            RegSetValueExW(key, L"DisplayVersion", 0, REG_SZ, reinterpret_cast<const BYTE*>(version.c_str()), DWORD((version.size() + 1) * sizeof(wchar_t)));
        }

        SYSTEMTIME now;
        GetLocalTime(&now);

        wchar_t date[9];
        swprintf_s(date, L"%04u%02u%02u", now.wYear, now.wMonth, now.wDay);
        RegSetValueExW(key, L"InstallDate", 0, REG_SZ, reinterpret_cast<const BYTE*>(date), DWORD(sizeof(date)));

        RegCloseKey(key);
    }

    void Launch()
    {
        std::wstring directory = Path(L"app");
        std::wstring command = L"\"" + directory + L"\\Telegram.exe\"";

        STARTUPINFOW startup{ sizeof(startup) };
        PROCESS_INFORMATION process{};

        if (CreateProcessW(nullptr, command.data(), nullptr, nullptr, FALSE, 0, nullptr, directory.c_str(), &startup, &process))
        {
            CloseHandle(process.hThread);
            CloseHandle(process.hProcess);
        }
        else
        {
            Log(L"Launching %s failed: %lu", command.c_str(), GetLastError());
        }
    }

    int Run(DWORD pid, ULONGLONG created)
    {
        if (pid != 0 && !WaitForExit(pid, created))
        {
            // Still running, so there is nothing to relaunch either. The update stays staged.
            Log(L"Process %lu did not exit", pid);
            return 1;
        }

        std::wstring installed = Path(L"app");
        std::wstring staged = Path(L"app.new");
        std::wstring previous = Path(L"app.old");
        std::wstring marker = staged + L"\\.update";

        if (!Exists(marker))
        {
            Log(L"Nothing staged");
            Launch();
            return 0;
        }

        std::wstring version = ReadVersion(marker);
        Log(L"Applying %s", version.c_str());

        // The app starts this updater whenever it finds the marker, so a failure that left it in
        // place would have the relaunch below start it again, and again. Without it the app stages
        // the update afresh, and the next restart is the retry.
        auto fail = [&]()
        {
            DeleteFileW(marker.c_str());
            Launch();
            return 1;
        };

        if (Exists(previous) && !DeleteTree(previous))
        {
            Log(L"Could not remove %s", previous.c_str());
            return fail();
        }

        if (!Rename(installed, previous))
        {
            return fail();
        }

        if (!Rename(staged, installed))
        {
            if (!Rename(previous, installed))
            {
                Log(L"Could not restore %s", installed.c_str());
                return 1;
            }

            return fail();
        }

        DeleteFileW((installed + L"\\.update").c_str());
        UpdateUninstallEntry(version);

        Log(L"Applied %s", version.c_str());
        Launch();
        return 0;
    }
}

// Updater.exe --pid <id> --created <FILETIME as a decimal integer>
int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    wchar_t module[MAX_PATH];
    DWORD length = GetModuleFileNameW(nullptr, module, MAX_PATH);
    if (length == 0 || length == MAX_PATH)
    {
        return 1;
    }

    g_root.assign(module, length);
    g_root.resize(g_root.find_last_of(L'\\'));

    std::wstring logs = Path(L"TempState");
    CreateDirectoryW(logs.c_str(), nullptr);
    _wfopen_s(&g_log, (logs + L"\\updater.log").c_str(), L"a, ccs=UTF-8");

    DWORD pid = 0;
    ULONGLONG created = 0;

    int count;
    LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &count);
    if (args != nullptr)
    {
        for (int i = 1; i + 1 < count; i++)
        {
            if (wcscmp(args[i], L"--pid") == 0)
            {
                pid = wcstoul(args[++i], nullptr, 10);
            }
            else if (wcscmp(args[i], L"--created") == 0)
            {
                created = wcstoull(args[++i], nullptr, 10);
            }
        }

        LocalFree(args);
    }

    int result = Run(pid, created);

    if (g_log)
    {
        fclose(g_log);
    }

    return result;
}
