#include "pch.h"
#include "NativeUtils.h"

using namespace Unigram::Native;
using namespace Platform;

int64 NativeUtils::GetDirectorySize(String^ path)
{
	return GetDirectorySize(path, L"\\*");
}

int64 NativeUtils::GetDirectorySize(String^ path, String^ filter)
{
	return GetDirectorySizeInternal(path->Data(), filter->Data(), 0);
}

void NativeUtils::CleanDirectory(String^ path, const Array<String^>^ filters)
{
	std::vector<std::wstring> array;
	for each (auto var in filters)
	{
		array.push_back(var->Data());
	}

	CleanDirectoryInternal(path->Data(), array);
}

void NativeUtils::Delete(String^ path)
{
	DeleteFile(path->Data());
}

void NativeUtils::CleanDirectoryInternal(const std::wstring &path, std::vector<std::wstring> filters)
{
	WIN32_FIND_DATA data;
	HANDLE sh = NULL;
	sh = FindFirstFile((path + L"\\*").c_str(), &data);

	if (sh == INVALID_HANDLE_VALUE)
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
			CleanDirectoryInternal(path + L"\\" + data.cFileName, filters);
		}
		else
		{
			if (std::find(filters.begin(), filters.end(), data.cFileName) != filters.end())
			{
				continue;
			}

			DeleteFile((path + L"\\" + data.cFileName).c_str());
		}

	} while (FindNextFile(sh, &data)); // do

	FindClose(sh);
}

uint64_t NativeUtils::GetDirectorySizeInternal(const std::wstring &path, const std::wstring &filter, uint64_t size)
{
	WIN32_FIND_DATA data;
	HANDLE sh = NULL;
	sh = FindFirstFile((path + filter).c_str(), &data);

	if (sh == INVALID_HANDLE_VALUE)
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

	} while (FindNextFile(sh, &data)); // do

	FindClose(sh);

	return size;
}

bool NativeUtils::IsBrowsePath(const std::wstring& path)
{
	return (path.find(L".") == 0 || path.find(L"..") == 0);
}