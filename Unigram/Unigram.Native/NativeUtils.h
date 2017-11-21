#pragma once

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>
#include <windows.h>
#include "Shlwapi.h"

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

		private:
			static uint64_t GetDirectorySizeInternal(const std::wstring &path, const std::wstring &filter, uint64_t size);
			static void CleanDirectoryInternal(const std::wstring &path, int days);
			static bool IsBrowsePath(const std::wstring& path);
			static ULONGLONG FileTimeToSeconds(FILETIME& ft);
		};
	}
}