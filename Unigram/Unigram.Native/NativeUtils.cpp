#include "pch.h"
#include "NativeUtils.h"

using namespace Unigram::Native;
using namespace Platform;

int64 NativeUtils::GetDirectorySize(String^ path)
{
	return CalculateDirSize(path->Data(), 0);
}

uint64_t NativeUtils::CalculateDirSize(const std::wstring &path, uint64_t size)
{
	WIN32_FIND_DATA data;
	HANDLE sh = NULL;
	sh = FindFirstFile((path + L"\\*").c_str(), &data);

	if (sh == INVALID_HANDLE_VALUE)
	{
		return size;
	}

	do
	{
		// skip current and parent
		if (!IsBrowsePath(data.cFileName))
		{
			// if found object is ...
			if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY)
			{
				// directory, then search it recursievly
				size = CalculateDirSize(path + L"\\" + data.cFileName, size);
			}
			else 
			{
				// otherwise get object size and add it to directory size
				size += (uint64_t)(data.nFileSizeHigh * (MAXDWORD)+data.nFileSizeLow);
			}
		}

	} while (FindNextFile(sh, &data)); // do

	FindClose(sh);

	return size;
}

bool NativeUtils::IsBrowsePath(const std::wstring& path)
{
	return (path.find(L".") == 0 || path.find(L"..") == 0);
}