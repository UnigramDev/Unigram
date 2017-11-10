#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#if _DEBUG
#ifndef TRACE
#define TRACE
#endif
#endif

#include <windows.h>

#define MALLOC(x) HeapAlloc(GetProcessHeap(), 0, (x)) 
#define FREE(x) HeapFree(GetProcessHeap(), 0, (x))
#define __STRINGIFY(x) #x
#define _STRINGIFY(x) __STRINGIFY(x)
#define __STRINGIFY_W(x) L##x
#define _STRINGIFY_W(x) __STRINGIFY_W(x)
//#define I_WANT_TO_DIE_IS_THE_NEW_TODO(text) \
//	OutputDebugString(_STRINGIFY_W("TODO in " __FUNCTION__  ", line " _STRINGIFY(__LINE__) ", file " _STRINGIFY(__FILE__) ": " text "\n")); \
//	__pragma(message("TODO in " __FUNCTION__  ", line " _STRINGIFY(__LINE__) ", file " _STRINGIFY(__FILE__) ": " text))

#define I_WANT_TO_DIE_IS_THE_NEW_TODO(text) \
	__pragma(message("TODO in " __FUNCTION__  ", line " _STRINGIFY(__LINE__) ", file " _STRINGIFY(__FILE__) ": " text))