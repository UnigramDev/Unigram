#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <windows.h>

#define __STRINGIFY(x) #x
#define _STRINGIFY(x) __STRINGIFY(x)
#define I_WANT_TO_DIE_IS_THE_NEW_TODO(text) \
	OutputDebugString(L"TODO: I want to die..."); \
	__pragma(message("TODO in " __FUNCTION__  ", line " _STRINGIFY(__LINE__) ", file " _STRINGIFY(__FILE__) ": " text))