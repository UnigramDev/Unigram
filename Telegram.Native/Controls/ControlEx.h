#pragma once

#include "Controls/ControlEx.g.h"
#include "Controls/GridEx.g.h"
#include "Controls/HyperlinkButtonEx.g.h"
#include "Controls/ToggleButtonEx.g.h"

#include "FrameworkElementEx.h"

namespace winrt::Telegram::Native::Controls::implementation
{
    struct ControlEx : FrameworkElementEx<ControlEx, ControlExT<ControlEx>>
    {
        ControlEx() = default;
    };

    struct GridEx : FrameworkElementEx<GridEx, GridExT<GridEx>>
    {
        GridEx() = default;
    };

    struct HyperlinkButtonEx : FrameworkElementEx<HyperlinkButtonEx, HyperlinkButtonExT<HyperlinkButtonEx>>
    {
        HyperlinkButtonEx() = default;
    };

    struct ToggleButtonEx : FrameworkElementEx<ToggleButtonEx, ToggleButtonExT<ToggleButtonEx>>
    {
        ToggleButtonEx() = default;
    };
}

namespace winrt::Telegram::Native::Controls::factory_implementation
{
    struct ControlEx : ControlExT<ControlEx, implementation::ControlEx>
    {
    };

    struct GridEx : GridExT<GridEx, implementation::GridEx>
    {
    };

    struct HyperlinkButtonEx : HyperlinkButtonExT<HyperlinkButtonEx, implementation::HyperlinkButtonEx>
    {
    };

    struct ToggleButtonEx : ToggleButtonExT<ToggleButtonEx, implementation::ToggleButtonEx>
    {
    };
}
