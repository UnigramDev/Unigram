#pragma once

// The UI framework this component is built against: Windows.UI for the UWP app, Microsoft.UI for
// the WinUI 3 one. The two share their type names and nothing else - a Microsoft.UI.Composition
// Visual cannot be handed to a Windows.UI.Composition API - so the component is built once for
// each app, and code names these aliases rather than either namespace. The IDL does the same with
// the UIComposition and UIXaml macros, which the project defines.
#ifdef WINUI3
#include <winrt/Microsoft.Graphics.DirectX.h>
#include <winrt/Microsoft.UI.Composition.h>
#include <winrt/Microsoft.UI.Composition.Interop.h>
#include <winrt/Microsoft.UI.Dispatching.h>
#else
#include <windows.ui.composition.interop.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#endif

// The XAML headers are left to Telegram.Native's precompiled header: Telegram.Native.Calls has no
// use for them, so here the namespace is only declared, for the alias.
#ifdef WINUI3
namespace winrt::Microsoft::UI::Xaml {}
#else
namespace winrt::Windows::UI::Xaml {}
#endif

namespace winrt
{
#ifdef WINUI3
    namespace UIComposition = Microsoft::UI::Composition;
    // The Windows App SDK declares the interop interfaces in the projected namespace.
    namespace UICompositionInterop = Microsoft::UI::Composition;
    namespace UIDispatching = Microsoft::UI::Dispatching;
    namespace UIXaml = Microsoft::UI::Xaml;
    // Not Windows.Graphics.DirectX: Microsoft.UI.Composition takes pixel formats of its own, which
    // are different types with the same names.
    namespace GraphicsDirectX = Microsoft::Graphics::DirectX;
#else
    namespace UIComposition = Windows::UI::Composition;
    namespace UICompositionInterop = ::ABI::Windows::UI::Composition;
    namespace UIDispatching = Windows::System;
    namespace UIXaml = Windows::UI::Xaml;
    namespace GraphicsDirectX = Windows::Graphics::DirectX;
#endif
}
