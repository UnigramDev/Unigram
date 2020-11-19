#pragma once
#include "pch.h"
#include "LibraryHelper.h"

HMODULE GetKernelModule()
{
	static HMODULE kernelModule;
	if (kernelModule == nullptr)
	{
		MEMORY_BASIC_INFORMATION mbi;
		if (VirtualQuery(VirtualQuery, &mbi, sizeof(MEMORY_BASIC_INFORMATION)))
		{
			kernelModule = reinterpret_cast<HMODULE>(mbi.AllocationBase);
		}
	}

	return kernelModule;
}

HMODULE GetModuleHandle(LPCTSTR libFileName)
{
	typedef HMODULE(WINAPI *pGetModuleHandle)(_In_opt_ LPCTSTR);
	static const auto procGetModuleHandle = reinterpret_cast<pGetModuleHandle>(GetProcAddress(GetKernelModule(), "GetModuleHandleW"));

	return procGetModuleHandle(libFileName);
}

HMODULE LoadLibrary(LPCTSTR lpFileName)
{
	typedef HMODULE(WINAPI *pLoadLibrary)(_In_ LPCTSTR);
	static const auto procLoadLibrary = reinterpret_cast<pLoadLibrary>(GetProcAddress(GetKernelModule(), "LoadLibraryW"));

	return procLoadLibrary(lpFileName);
}

HMODULE LoadLibraryEx(_In_ LPCTSTR lpFileName, _Reserved_ HANDLE hFile, _In_ DWORD flags)
{
	typedef HMODULE(WINAPI *pLoadLibraryEx)(_In_ LPCTSTR, _Reserved_ HANDLE, _In_ DWORD);
	static const auto procLoadLibraryEx = reinterpret_cast<pLoadLibraryEx>(GetProcAddress(GetKernelModule(), "LoadLibraryExW"));

	return procLoadLibraryEx(lpFileName, hFile, flags);
}