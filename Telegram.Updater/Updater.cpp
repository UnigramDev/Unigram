// Applies an update the unpackaged app has staged, after the app has exited.
//
//   %LOCALAPPDATA%\TelegramFZ-LLC.Windows\
//     Updater.exe     this program, copied here by the app before each run
//     app\            the installed version
//     app.new\        the files that changed; complete once app.new\.update exists, and
//                     app.new\.delete lists what the new version no longer ships
//     app.old\        the originals of the files replaced, deleted by the app once the new one is up
//
// Each staged file is copied over the installed one, after a copy of the original goes to
// app.old\, so a cancelled update can put back everything it replaced. Nothing is renamed: a
// folder cannot be renamed while another process has it as its current directory or any file in
// it open, whereas copying a file is blocked only by a handle on that file.
//
// Statically linked against the CRT: it runs from the root, and the CRT DLLs are in app\, which
// it overwrites.

#define NOMINMAX
#include <windows.h>
#include <shellapi.h>

#include <algorithm>
#include <cstdarg>
#include <cstdio>
#include <string>
#include <vector>

namespace
{
    // The {AppId}_is1 key Inno Setup writes for a per-user install. The AppId is the one in
    // Telegram.Installer\Telegram.iss and must never change.
    constexpr wchar_t kUninstallKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{A3996223-6B78-4784-9615-A466F009E4C6}_is1";

    constexpr DWORD kExitTimeout = 30000;

    // Antivirus, the indexer or a process still exiting can hold a file briefly. Past that only
    // the user can close whatever holds it, so they are asked.
    constexpr int kCopyAttempts = 100;
    constexpr DWORD kCopyDelay = 100;

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

    void CreateParents(const std::wstring& path)
    {
        for (size_t i = g_root.size() + 1; (i = path.find(L'\\', i)) != std::wstring::npos; i++)
        {
            CreateDirectoryW(path.substr(0, i).c_str(), nullptr);
        }
    }

    // Relative to the folder. Links are not followed: what they point at is not ours.
    void ListFiles(const std::wstring& folder, const std::wstring& relative, std::vector<std::wstring>& files)
    {
        WIN32_FIND_DATAW data;
        HANDLE find = FindFirstFileW((relative.empty() ? folder + L"\\*" : folder + L'\\' + relative + L"\\*").c_str(), &data);

        if (find == INVALID_HANDLE_VALUE)
        {
            return;
        }

        do
        {
            if (wcscmp(data.cFileName, L".") == 0 || wcscmp(data.cFileName, L"..") == 0 || (data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            {
                continue;
            }

            std::wstring child = relative.empty() ? std::wstring(data.cFileName) : relative + L'\\' + data.cFileName;

            if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
            {
                ListFiles(folder, child, files);
            }
            else
            {
                files.push_back(child);
            }
        } while (FindNextFileW(find, &data));

        FindClose(find);
    }

    bool AskToRetry(const std::wstring& path, DWORD error)
    {
        wchar_t* reason = nullptr;
        FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr, error, 0, reinterpret_cast<LPWSTR>(&reason), 0, nullptr);

        std::wstring text = L"Telegram could not be updated, because this file could not be replaced:\n\n" + path + L"\n\n";
        if (reason != nullptr)
        {
            text += reason;
            LocalFree(reason);
        }

        text += L"\nClose any program that may be using it and retry, or cancel to keep the current version.";

        return MessageBoxW(nullptr, text.c_str(), L"Telegram", MB_ICONERROR | MB_RETRYCANCEL | MB_SETFOREGROUND) == IDRETRY;
    }

    // False once the user gives up, who is shown the installed file whichever end of the copy it
    // is. With keepExisting, a target already there counts as copied.
    bool Copy(const std::wstring& from, const std::wstring& to, bool keepExisting, const std::wstring& installed)
    {
        for (;;)
        {
            DWORD error = 0;

            for (int attempt = 0; attempt < kCopyAttempts; attempt++)
            {
                if (CopyFileW(from.c_str(), to.c_str(), keepExisting))
                {
                    return true;
                }

                error = GetLastError();
                if (keepExisting && error == ERROR_FILE_EXISTS)
                {
                    return true;
                }

                Sleep(kCopyDelay);
            }

            Log(L"Copy %s -> %s failed: %lu", from.c_str(), to.c_str(), error);

            if (!AskToRetry(installed, error))
            {
                return false;
            }
        }
    }

    struct Replaced
    {
        std::wstring installed;

        // Empty for a file the update added.
        std::wstring original;
    };

    // Newest first. A file that cannot be put back is logged and skipped: stopping there would
    // leave more of the update in place, not less.
    void Restore(const std::vector<Replaced>& replaced)
    {
        for (auto it = replaced.rbegin(); it != replaced.rend(); ++it)
        {
            bool restored = it->original.empty()
                ? DeleteFileW(it->installed.c_str()) || GetLastError() == ERROR_FILE_NOT_FOUND
                : CopyFileW(it->original.c_str(), it->installed.c_str(), FALSE);

            if (!restored)
            {
                Log(L"Could not restore %s: %lu", it->installed.c_str(), GetLastError());
            }
        }
    }

    std::wstring ReadText(const std::wstring& path)
    {
        std::string bytes;

        FILE* file = nullptr;
        if (_wfopen_s(&file, path.c_str(), L"rb") == 0 && file != nullptr)
        {
            char buffer[4096];
            size_t read;
            while ((read = fread(buffer, 1, sizeof(buffer), file)) > 0)
            {
                bytes.append(buffer, read);
            }

            fclose(file);
        }

        std::wstring text(MultiByteToWideChar(CP_UTF8, 0, bytes.data(), int(bytes.size()), nullptr, 0), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, bytes.data(), int(bytes.size()), text.data(), int(text.size()));
        return text;
    }

    // The marker holds the staged version as one line of ASCII, written by the app last.
    std::wstring ReadVersion(const std::wstring& marker)
    {
        std::wstring text = ReadText(marker);
        return text.substr(0, std::min(text.find_first_of(L"\r\n"), size_t(32)));
    }

    // Written by the app from its own listing of app\; this only keeps a damaged list inside it.
    bool IsInside(const std::wstring& path)
    {
        return !path.empty() && path[0] != L'\\' && path.find(L':') == std::wstring::npos && path.find(L"..") == std::wstring::npos;
    }

    // A file the new version does not ship is only clutter, so one that cannot be deleted is
    // logged and left.
    void DeleteRemoved(const std::wstring& installed, const std::wstring& list)
    {
        std::wstring text = ReadText(list);

        for (size_t start = 0; start < text.size();)
        {
            size_t end = std::min(text.find_first_of(L"\r\n", start), text.size());
            std::wstring path = text.substr(start, end - start);
            start = end + 1;

            if (path.empty())
            {
                continue;
            }

            if (!IsInside(path))
            {
                Log(L"Not deleting %s", path.c_str());
                continue;
            }

            std::wstring target = installed + L'\\' + path;
            if (!DeleteFileW(target.c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
            {
                Log(L"Could not delete %s: %lu", target.c_str(), GetLastError());
            }
        }
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
        std::wstring removed = staged + L"\\.delete";

        if (!Exists(marker))
        {
            Log(L"Nothing staged");
            Launch();
            return 0;
        }

        std::wstring version = ReadVersion(marker);
        Log(L"Applying %s", version.c_str());

        std::vector<std::wstring> files;
        ListFiles(staged, std::wstring(), files);

        files.erase(std::remove_if(files.begin(), files.end(), [](const std::wstring& file)
        {
            return file == L".update" || file == L".delete";
        }), files.end());

        // Last: until it is replaced, the installed Telegram.exe is the old version, which starts
        // this updater again whenever a newer one is staged. An update cut short by a crash or a
        // shutdown is picked up at the next launch.
        auto exe = std::find_if(files.begin(), files.end(), [](const std::wstring& file)
        {
            return _wcsicmp(file.c_str(), L"Telegram.exe") == 0;
        });

        if (exe != files.end())
        {
            std::rotate(exe, exe + 1, files.end());
        }

        std::vector<Replaced> replaced;
        bool cancelled = false;

        for (const std::wstring& file : files)
        {
            std::wstring source = staged + L'\\' + file;
            std::wstring target = installed + L'\\' + file;
            std::wstring original;

            if (Exists(target))
            {
                original = previous + L'\\' + file;
                CreateParents(original);

                // An original already there is from this same update, cut short before: the
                // installed file may be the new one by now.
                if (!Copy(target, original, true, target))
                {
                    cancelled = true;
                    break;
                }
            }
            else
            {
                CreateParents(target);
            }

            // Before the copy, which can leave the target half written when it fails.
            replaced.push_back({ target, original });

            if (!Copy(source, target, false, target))
            {
                cancelled = true;
                break;
            }
        }

        // The app starts this updater whenever it finds the marker, so a failure that left it in
        // place would have the relaunch below start it again, and again. Without it the app stages
        // the update afresh, and the next restart is the retry.
        DeleteFileW(marker.c_str());

        if (cancelled)
        {
            Restore(replaced);
            Log(L"Cancelled %s, %zu files restored", version.c_str(), replaced.size());

            Launch();
            return 1;
        }

        DeleteRemoved(installed, removed);
        UpdateUninstallEntry(version);

        Log(L"Applied %s, %zu files replaced", version.c_str(), replaced.size());
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
