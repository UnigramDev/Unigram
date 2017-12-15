#include "pch.h"
#include "Helpers\LibraryHelper.h"
#include "DebugExtensions.h"

static LibraryInstance s_dbghelp(L"dbghelp.dll");

BOOL IMAGEAPI StackWalk64(DWORD MachineType, HANDLE hProcess, HANDLE hThread, LPSTACKFRAME64 StackFrame, PVOID ContextRecord, PREAD_PROCESS_MEMORY_ROUTINE64 ReadMemoryRoutine,
	PFUNCTION_TABLE_ACCESS_ROUTINE64 FunctionTableAccessRoutine, PGET_MODULE_BASE_ROUTINE64 GetModuleBaseRoutine, PTRANSLATE_ADDRESS_ROUTINE64 TranslateAddress)
{
	typedef BOOL(IMAGEAPI *pStackWalk64)(DWORD, __in HANDLE, __in HANDLE, __inout LPSTACKFRAME64, __inout PVOID, __in_opt PREAD_PROCESS_MEMORY_ROUTINE64, __in_opt PFUNCTION_TABLE_ACCESS_ROUTINE64, __in_opt PGET_MODULE_BASE_ROUTINE64, __in_opt PTRANSLATE_ADDRESS_ROUTINE64);
	static const auto procStackWalk64 = s_dbghelp.GetMethod<pStackWalk64>("StackWalk64");

	return procStackWalk64(MachineType, hProcess, hThread, StackFrame, ContextRecord, ReadMemoryRoutine, FunctionTableAccessRoutine, GetModuleBaseRoutine, TranslateAddress);
}

BOOL IMAGEAPI SymInitialize(HANDLE hProcess, LPCSTR UserSearchPath, BOOL fInvadeProcess)
{
	typedef BOOL(IMAGEAPI *pSymInitialize)(_In_ HANDLE, _In_opt_ LPCSTR, _In_ BOOL);
	static const auto procSymInitialize = s_dbghelp.GetMethod<pSymInitialize>("SymInitialize");

	return procSymInitialize(hProcess, UserSearchPath, fInvadeProcess);
}

BOOL IMAGEAPI SymCleanup(_In_ HANDLE hProcess)
{
	typedef BOOL(IMAGEAPI *pSymCleanup)(_In_ HANDLE);
	static const auto procSymCleanup = s_dbghelp.GetMethod<pSymCleanup>("SymCleanup");

	return procSymCleanup(hProcess);
}

DWORD IMAGEAPI SymSetOptions(_In_ DWORD SymOptions)
{
	typedef DWORD(IMAGEAPI *pSymSetOptions)(_In_ DWORD);
	static const auto procSymSetOptions = s_dbghelp.GetMethod<pSymSetOptions>("SymSetOptions");

	return procSymSetOptions(SymOptions);
}

DWORD IMAGEAPI SymGetOptions(VOID)
{
	typedef DWORD(IMAGEAPI *pSymGetOptions)(VOID);
	static const auto procSymGetOptions = s_dbghelp.GetMethod<pSymGetOptions>("SymGetOptions");

	return procSymGetOptions();
}

BOOL IMAGEAPI SymFromAddr(HANDLE hProcess, DWORD64 Address, PDWORD64 Displacement, PSYMBOL_INFO Symbol)
{
	typedef BOOL(IMAGEAPI *pSymFromAddr)(_In_ HANDLE, _In_ DWORD64, _Out_opt_ PDWORD64, _Inout_ PSYMBOL_INFO);
	static const auto procSymFromAddr = s_dbghelp.GetMethod<pSymFromAddr>("SymFromAddr");

	return procSymFromAddr(hProcess, Address, Displacement, Symbol);
}

BOOL IMAGEAPI SymGetLineFromAddr64(HANDLE hProcess, DWORD64 dwAddr, PDWORD pdwDisplacement, PIMAGEHLP_LINE64 Line)
{
	typedef BOOL(IMAGEAPI *pSymGetLineFromAddr64)(_In_ HANDLE, _In_ DWORD64, _Out_ PDWORD, _Out_ PIMAGEHLP_LINE64);
	static const auto procSymGetLineFromAddr64 = s_dbghelp.GetMethod<pSymGetLineFromAddr64>("SymGetLineFromAddr64");

	return procSymGetLineFromAddr64(hProcess, dwAddr, pdwDisplacement, Line);
}

BOOL IMAGEAPI SymGetSymFromAddr64(HANDLE hProcess, DWORD64 Address, PDWORD64 Displacement, PIMAGEHLP_SYMBOL64 Symbol)
{
	typedef BOOL(IMAGEAPI *pSymGetSymFromAddr64)(_In_ HANDLE, _In_ DWORD64, _Out_opt_ PDWORD64, _Inout_ PIMAGEHLP_SYMBOL64);
	static const auto procSymGetSymFromAddr64 = s_dbghelp.GetMethod<pSymGetSymFromAddr64>("SymGetSymFromAddr64");

	return procSymGetSymFromAddr64(hProcess, Address, Displacement, Symbol);
}


VOID WINAPI RtlCaptureContext(PCONTEXT ContextRecord)
{
	typedef VOID(WINAPI *pRtlCaptureContext)(_Out_ PCONTEXT);
	static const auto procRtlCaptureContext = reinterpret_cast<pRtlCaptureContext>(GetProcAddress(GetModuleHandle(L"kernel32.dll"), "RtlCaptureContext"));

	return procRtlCaptureContext(ContextRecord);
}

BOOL WINAPI SetThreadContext(_In_ HANDLE hThread, _In_ const CONTEXT* lpContext)
{
	typedef BOOL(WINAPI *pSetThreadContext)(_In_ HANDLE, _In_ const CONTEXT*);
	static const auto procSetThreadContext = reinterpret_cast<pSetThreadContext>(GetProcAddress(GetModuleHandle(L"kernel32.dll"), "SetThreadContext"));

	return procSetThreadContext(hThread, lpContext);
}