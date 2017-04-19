#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <windows.h>

#define I_WANT_TO_DIE_IS_THE_NEW_TODO OutputDebugString(L"TODO: I want to die...");