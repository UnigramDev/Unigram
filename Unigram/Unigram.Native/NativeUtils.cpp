#include "pch.h"
#include "NativeUtils.h"

using namespace Unigram::Native;
using namespace Platform;

HMODULE NativeUtils::s_user32 = NULL;
pGetLastInputInfo NativeUtils::s_getLastInputInfo = NULL;

int64 NativeUtils::GetDirectorySize(String^ path)
{
	return GetDirectorySize(path, L"\\*");
}

int64 NativeUtils::GetDirectorySize(String^ path, String^ filter)
{
	return GetDirectorySizeInternal(path->Data(), filter->Data(), 0);
}

void NativeUtils::CleanDirectory(String^ path, int days)
{
	CleanDirectoryInternal(path->Data(), days);
}

void NativeUtils::Delete(String^ path)
{
	DeleteFile(path->Data());
}

void NativeUtils::CleanDirectoryInternal(const std::wstring &path, int days)
{
	long diff = 60 * 60 * 1000 * 24 * days;

	FILETIME ft;
	GetSystemTimeAsFileTime(&ft);
	auto currentTime = FileTimeToSeconds(ft);

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
			CleanDirectoryInternal(path + L"\\" + data.cFileName, days);
		}
		else
		{
			auto lastAccess = FileTimeToSeconds(data.ftLastAccessTime);
			auto lastWrite = FileTimeToSeconds(data.ftLastWriteTime);

			if (days == 0)
			{
				DeleteFile((path + L"\\" + data.cFileName).c_str());
			}
			else if (lastAccess > lastWrite)
			{
				if (lastAccess + diff < currentTime)
				{
					DeleteFile((path + L"\\" + data.cFileName).c_str());
				}
			}
			else if (lastWrite + diff < currentTime)
			{
				DeleteFile((path + L"\\" + data.cFileName).c_str());
			}
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

ULONGLONG NativeUtils::FileTimeToSeconds(FILETIME& ft)
{
	ULARGE_INTEGER uli;
	uli.HighPart = ft.dwHighDateTime;
	uli.LowPart = ft.dwLowDateTime;

	return uli.QuadPart / 10000;
}

int32 NativeUtils::GetLastInputTime()
{
	if (s_getLastInputInfo == NULL)
	{
		pLoadLibraryEx loadLibrary = (pLoadLibraryEx)GetProcAddress(GetKernelModule(), "LoadLibraryExW");

		s_user32 = loadLibrary(L"User32.dll", NULL, 0x00000001);
		s_getLastInputInfo = (pGetLastInputInfo)GetProcAddress(s_user32, "GetLastInputInfo");
	}

	if (s_getLastInputInfo == NULL)
	{
		return 0;
	}

	LASTINPUTINFO last_input;
	last_input.cbSize = sizeof(last_input);

	if (s_getLastInputInfo(&last_input))
	{
		return last_input.dwTime;
	}

	return 0;
}