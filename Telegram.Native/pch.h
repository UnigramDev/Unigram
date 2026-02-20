#pragma once

#include <Windows.Graphics.DirectX.Direct3D11.interop.h>
#include <Windows.ui.composition.interop.h>
#include <unknwn.h>
#include <winrt/base.h>

#include <algorithm>
#include <mutex>

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

#define LOGGER_ASSERT(...) \
    NativeUtils::Log(0, hstring(std::format(__VA_ARGS__)), winrt::to_hstring(std::string(__FUNCTION__)), winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_DEBUG(...) \
    NativeUtils::Log(4, hstring(std::format(__VA_ARGS__)), winrt::to_hstring(std::string(__FUNCTION__)), winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_WARNING(...) \
    NativeUtils::Log(2, hstring(std::format(__VA_ARGS__)), winrt::to_hstring(std::string(__FUNCTION__)), winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_ERROR(...) \
    NativeUtils::Log(1, hstring(std::format(__VA_ARGS__)), winrt::to_hstring(std::string(__FUNCTION__)), winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_INFO(...) \
    NativeUtils::Log(3, hstring(std::format(__VA_ARGS__)), winrt::to_hstring(std::string(__FUNCTION__)), winrt::to_hstring(__FILE__), __LINE__)
