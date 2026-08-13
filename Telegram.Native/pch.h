#pragma once

#include <Windows.Graphics.DirectX.Direct3D11.interop.h>
#include <Windows.ui.composition.interop.h>
#include <unknwn.h>
#include <winrt/base.h>

#include <algorithm>
#include <mutex>
#include <string_view>

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

namespace Logging
{
    // MSVC expands __FUNCTION__ to the fully qualified name, but the namespace is noise and
    // the class is already given by the file name, so keep only what follows both. Cutting at
    // the last :: instead would reduce a lambda to a bare "operator ()".
    constexpr std::string_view TrimFunction(std::string_view function)
    {
        constexpr std::string_view marker = "implementation::";
        constexpr std::string_view scope = "::";

        auto index = function.rfind(marker);
        if (index == std::string_view::npos)
        {
            return function;
        }

        function.remove_prefix(index + marker.length());

        // A free function in the namespace has no class to drop.
        index = function.find(scope);
        return index == std::string_view::npos
            ? function
            : function.substr(index + scope.length());
    }
}

#define LOGGER_MEMBER winrt::to_hstring(::Logging::TrimFunction(__FUNCTION__))

#define LOGGER_ASSERT(...) \
    NativeUtils::Log(0, hstring(std::format(__VA_ARGS__)), LOGGER_MEMBER, winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_DEBUG(...) \
    NativeUtils::Log(4, hstring(std::format(__VA_ARGS__)), LOGGER_MEMBER, winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_WARNING(...) \
    NativeUtils::Log(2, hstring(std::format(__VA_ARGS__)), LOGGER_MEMBER, winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_ERROR(...) \
    NativeUtils::Log(1, hstring(std::format(__VA_ARGS__)), LOGGER_MEMBER, winrt::to_hstring(__FILE__), __LINE__)
#define LOGGER_INFO(...) \
    NativeUtils::Log(3, hstring(std::format(__VA_ARGS__)), LOGGER_MEMBER, winrt::to_hstring(__FILE__), __LINE__)
