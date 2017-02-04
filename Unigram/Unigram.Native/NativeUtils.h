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

		private:
			static uint64_t CalculateDirSize(const std::wstring &path, uint64_t size);
			static bool IsBrowsePath(const std::wstring& path);
		};
	}
}