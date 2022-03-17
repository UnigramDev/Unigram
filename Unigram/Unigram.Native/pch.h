#pragma once

#include <unknwn.h>
#include <winrt/base.h>

#include <algorithm>

#include <robuffer.h>

#include <mfapi.h>

#include <winerror.h>
#include <dwrite.h>
#include <wincodec.h>
#include <d3d11_1.h>
#include <d2d1_1.h>
#include <d2d1effects.h>
#include <dwrite_1.h>

#undef small

// Disable debug string output on non-debug build
#if !_DEBUG
#define DebugMessage(x)
#else
#define DebugMessage(x) OutputDebugString(x)
#endif