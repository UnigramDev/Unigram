#pragma once
#include <Windows.h>

//inline HMODULE GetKernelModule()
//{
//	static HMODULE kernelModule;
//	if (kernelModule == nullptr)
//	{
//		MEMORY_BASIC_INFORMATION mbi;
//		if (VirtualQuery(VirtualQuery, &mbi, sizeof(MEMORY_BASIC_INFORMATION)))
//		{
//			kernelModule = reinterpret_cast<HMODULE>(mbi.AllocationBase);
//		}
//	}
//
//	return kernelModule;
//}
//
//HMODULE GetModuleHandle(_In_ LPCTSTR libFileName)
//{
//	typedef HMODULE(WINAPI *pGetModuleHandle)(_In_opt_ LPCTSTR);
//	static const auto procGetModuleHandle = reinterpret_cast<pGetModuleHandle>(GetProcAddress(GetKernelModule(), "GetModuleHandleW"));
//
//	return procGetModuleHandle(libFileName);
//}
//
//HMODULE LoadLibrary(_In_ LPCTSTR lpFileName)
//{
//	typedef HMODULE(WINAPI *pLoadLibrary)(_In_ LPCTSTR);
//	static const auto procLoadLibrary = reinterpret_cast<pLoadLibrary>(GetProcAddress(GetKernelModule(), "LoadLibraryW"));
//
//	return procLoadLibrary(lpFileName);
//}

HMODULE GetKernelModule();
HMODULE GetModuleHandle(_In_ LPCTSTR libFileName);
HMODULE LoadLibrary(_In_ LPCTSTR lpFileName);

struct LibraryInstance
{
public:
	LibraryInstance(LPCTSTR libFileName)
	{
		m_module = LoadLibrary(libFileName);
	}

	~LibraryInstance()
	{
		FreeLibrary(m_module);
	}

	inline bool IsValid() const
	{
		return m_module != nullptr;
	}

	inline HMODULE GetHandle() const
	{
		return m_module;
	}

	template<typename T>
	T GetMethod(LPCSTR methodName)
	{
		return reinterpret_cast<T>(GetProcAddress(m_module, methodName));
	}

	template<typename T>
	T GetMethod(DWORD offset)
	{
		return reinterpret_cast<T>(m_module + offset);
	}

private:
	HMODULE m_module;
};