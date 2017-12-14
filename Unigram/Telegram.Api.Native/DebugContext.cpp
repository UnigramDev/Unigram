#include "pch.h"
#include "DebugExtensions.h"
#include "Helpers\DebugHelper.h"
#include "DebugContext.h"

using namespace Telegram::Api::Native::Diagnostics;

DebugContext::DebugContext()
{
	SymSetOptions(SymGetOptions() | SYMOPT_DEBUG | SYMOPT_LOAD_LINES | SYMOPT_FAIL_CRITICAL_ERRORS);
	if (!SymInitialize(GetCurrentProcess(), NULL, TRUE))
	{
		OutputDebugStringFormat(L"SymInitialize returned error : 0x%08x\n", GetLastError());
	}
}

DebugContext::~DebugContext()
{
	SymCleanup(GetCurrentProcess());
}
