#pragma once
#include <Windows.h>
#include <fileapifromapp.h>

// The FromApp functions are how the UWP flavour reaches a file outside its container: past the
// container, they act on the access the user granted, through a broker. WinUI runs unpackaged, with
// no container and no grants, and calls Win32 directly.
namespace FileHelper
{
    inline HANDLE CreateFile2(_In_ LPCWSTR fileName, _In_ DWORD desiredAccess, _In_ DWORD shareMode, _In_ DWORD creationDisposition, _In_opt_ LPCREATEFILE2_EXTENDED_PARAMETERS extendedParameters)
    {
#ifdef WINUI3
        return ::CreateFile2(fileName, desiredAccess, shareMode, creationDisposition, extendedParameters);
#else
        return CreateFile2FromAppW(fileName, desiredAccess, shareMode, creationDisposition, extendedParameters);
#endif
    }

    inline HANDLE CreateFileW(_In_ LPCWSTR fileName, _In_ DWORD desiredAccess, _In_ DWORD shareMode, _In_opt_ LPSECURITY_ATTRIBUTES securityAttributes, _In_ DWORD creationDisposition, _In_ DWORD flagsAndAttributes, _In_opt_ HANDLE templateFile)
    {
#ifdef WINUI3
        return ::CreateFileW(fileName, desiredAccess, shareMode, securityAttributes, creationDisposition, flagsAndAttributes, templateFile);
#else
        return CreateFileFromAppW(fileName, desiredAccess, shareMode, securityAttributes, creationDisposition, flagsAndAttributes, templateFile);
#endif
    }

    inline BOOL DeleteFileW(_In_ LPCWSTR fileName)
    {
#ifdef WINUI3
        return ::DeleteFileW(fileName);
#else
        return DeleteFileFromAppW(fileName);
#endif
    }

    // Neither replaces an existing target.
    inline BOOL MoveFileW(_In_ LPCWSTR existingFileName, _In_ LPCWSTR newFileName)
    {
#ifdef WINUI3
        return ::MoveFileW(existingFileName, newFileName);
#else
        return MoveFileFromAppW(existingFileName, newFileName);
#endif
    }

    inline BOOL GetFileAttributesExW(_In_ LPCWSTR fileName, _In_ GET_FILEEX_INFO_LEVELS infoLevelId, _Out_writes_bytes_(sizeof(WIN32_FILE_ATTRIBUTE_DATA)) LPVOID fileInformation)
    {
#ifdef WINUI3
        return ::GetFileAttributesExW(fileName, infoLevelId, fileInformation);
#else
        return GetFileAttributesExFromAppW(fileName, infoLevelId, fileInformation);
#endif
    }
}

#ifdef WINUI3
// Only FileHelper may choose between the two, so the FromApp names do not compile here.
#define CopyFileFromAppW CopyFileFromAppW_is_UWP_only
#define CreateDirectoryFromAppW CreateDirectoryFromAppW_is_UWP_only
#define CreateFile2FromAppW CreateFile2FromAppW_is_UWP_only_use_FileHelper
#define CreateFileFromAppW CreateFileFromAppW_is_UWP_only_use_FileHelper
#define DeleteFileFromAppW DeleteFileFromAppW_is_UWP_only_use_FileHelper
#define FindFirstFileExFromAppW FindFirstFileExFromAppW_is_UWP_only
#define GetFileAttributesExFromAppW GetFileAttributesExFromAppW_is_UWP_only_use_FileHelper
#define MoveFileFromAppW MoveFileFromAppW_is_UWP_only_use_FileHelper
#define RemoveDirectoryFromAppW RemoveDirectoryFromAppW_is_UWP_only
#define ReplaceFileFromAppW ReplaceFileFromAppW_is_UWP_only
#define SetFileAttributesFromAppW SetFileAttributesFromAppW_is_UWP_only
#endif
