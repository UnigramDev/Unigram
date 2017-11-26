#pragma once

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>
#include <windows.h>
#include "Shlwapi.h"

typedef HMODULE(WINAPI *pLoadLibraryEx)(_In_ LPCTSTR, _Reserved_ HANDLE, _In_ DWORD);
typedef HMODULE(WINAPI *pGetModuleHandle)(__in_opt LPCTSTR lpModuleName);

typedef BOOL(WINAPI *pGetLastInputInfo)(_Out_ PLASTINPUTINFO plii);

using namespace Platform;

namespace Unigram
{
	namespace Native
	{
		public ref class NativeUtils sealed
		{
		public:
			static int64 GetDirectorySize(String^ path);
			static int64 GetDirectorySize(String^ path, String^ filter);
			static void CleanDirectory(String^ path, int days);
			static void Delete(String^ path);

			static int32 GetLastInputTime();

		private:
			static uint64_t GetDirectorySizeInternal(const std::wstring &path, const std::wstring &filter, uint64_t size);
			static void CleanDirectoryInternal(const std::wstring &path, int days);
			static bool IsBrowsePath(const std::wstring& path);
			static ULONGLONG FileTimeToSeconds(FILETIME& ft);

			static HMODULE s_user32;
			static pGetLastInputInfo s_getLastInputInfo;
		};

		HMODULE GetKernelModule()
		{
			MEMORY_BASIC_INFORMATION mbi = { 0 };
			VirtualQuery(VirtualQuery, &mbi, sizeof(mbi));
			return reinterpret_cast<HMODULE>(mbi.AllocationBase);
		}
	}
}