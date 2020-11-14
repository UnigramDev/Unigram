#pragma once

#include <algorithm>
#include <collection.h>
#include <ppltasks.h>

#include <wrl.h>
#include <robuffer.h>

#include <mfapi.h>

#include <winerror.h>
#include <dwrite.h>
#include <wincodec.h>
#include <shcore.h>
#include <d3d11_1.h>
#include <d2d1_1.h>
#include <d2d1effects.h>
#include <dwrite_1.h>

#include "Helpers\COMHelper.h"

// Disable debug string output on non-debug build
#if !_DEBUG
#define DebugMessage(x)
#else
#define DebugMessage(x) OutputDebugString(x)
#endif