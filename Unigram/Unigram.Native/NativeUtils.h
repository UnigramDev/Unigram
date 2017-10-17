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
			static int64 GetFileSize(String^ path);
			static void Delete(String^ path);

		private:
			static uint64_t CalculateDirSize(const std::wstring &path, uint64_t size);
			static uint64_t CalculateFileSize(const std::wstring &path);
			static bool IsBrowsePath(const std::wstring& path);
		};
	}
}