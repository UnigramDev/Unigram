#include "pch.h"
#include "Helpers\LibraryHelper.h"
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
	HANDLE handle = FindFirstFile((path + L"\\*").c_str(), &data);

	if (handle == INVALID_HANDLE_VALUE)
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

	} while (FindNextFile(handle, &data));

	FindClose(handle);
}

uint64_t NativeUtils::GetDirectorySizeInternal(const std::wstring &path, const std::wstring &filter, uint64_t size)
{
	WIN32_FIND_DATA data;
	HANDLE handle = FindFirstFile((path + filter).c_str(), &data);

	if (handle == INVALID_HANDLE_VALUE)
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

	} while (FindNextFile(handle, &data));

	FindClose(handle);

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
	typedef BOOL(WINAPI *pGetLastInputInfo)(_Out_ PLASTINPUTINFO);

	static const LibraryInstance user32(L"User32.dll", 0x00000001);
	static const auto getLastInputInfo = user32.GetMethod<pGetLastInputInfo>("GetLastInputInfo");

	if (getLastInputInfo == nullptr)
	{
		return 0;
	}

	LASTINPUTINFO lastInput;
	lastInput.cbSize = sizeof(LASTINPUTINFO);

	if (getLastInputInfo(&lastInput))
	{
		return lastInput.dwTime;
	}

	return 0;
}

int32 NativeUtils::GetDirectionality(String^ value)
{
	unsigned int length = value->Length();
	WORD* type;
	type = new WORD[length];
	GetStringTypeEx(LOCALE_USER_DEFAULT, CT_CTYPE2, value->Data(), length, type);

	for (int i = 0; i < length; i++)
	{
		/*if (type[i] & C2_LEFTTORIGHT)
		{
			return C2_LEFTTORIGHT;
		}
		else*/ if (type[i] & C2_RIGHTTOLEFT && !(type[i] & C2_LEFTTORIGHT))
		{
			return C2_RIGHTTOLEFT;
		}
	}

	return C2_OTHERNEUTRAL;
}

int32 NativeUtils::GetDirectionality(char16 value)
{
	WORD type = 0;
	GetStringTypeEx(LOCALE_USER_DEFAULT, CT_CTYPE2, &value, 1, &type);

	if (type & C2_LEFTTORIGHT)
	{
		return C2_LEFTTORIGHT;
	}
	else if (type & C2_RIGHTTOLEFT)
	{
		return C2_RIGHTTOLEFT;
	}

	return C2_OTHERNEUTRAL;
}

String^ NativeUtils::GetCurrentCulture()
{
	TCHAR buff[530];
	int result = GetLocaleInfoEx(LOCALE_NAME_USER_DEFAULT, LOCALE_SNAME, buff, 530);
	if (result == 0)
	{
		result = GetLocaleInfoEx(LOCALE_NAME_SYSTEM_DEFAULT, LOCALE_SNAME, buff, 530);
		if (result == 0)
		{
			return nullptr;
		}
	}

	return ref new String(buff);
}

bool NativeUtils::IsMediaSupported()
{
	HRESULT result;
	result = MFStartup(MF_VERSION);

	if (result == S_OK)
	{
		MFShutdown();
	}

	return result != E_NOTIMPL;
}