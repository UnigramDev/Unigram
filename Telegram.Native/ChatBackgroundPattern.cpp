#include "pch.h"
#include "ChatBackgroundPattern.h"
#if __has_include("ChatBackgroundPattern.g.cpp")
#include "ChatBackgroundPattern.g.cpp"
#endif

#include <winrt/Windows.UI.Xaml.Media.h>

using namespace winrt::Windows::UI::Xaml::Media;

namespace winrt::Telegram::Native::implementation
{
    ICompositionSurface ChatBackgroundPattern::Surface()
    {
        return m_surface;
    }

    float2 ChatBackgroundPattern::RenderSize()
    {
        if (auto loadedImageSurface = m_surface.try_as<LoadedImageSurface>())
        {
            return loadedImageSurface.DecodedSize();
        }

        return m_renderSize;
    }

    float2 ChatBackgroundPattern::RenderPhysicalSize()
    {
        if (auto loadedImageSurface = m_surface.try_as<LoadedImageSurface>())
        {
            return loadedImageSurface.DecodedPhysicalSize();
        }

        return m_renderPhysicalSize;
    }

    float2 ChatBackgroundPattern::NaturalSize()
    {
        if (auto loadedImageSurface = m_surface.try_as<LoadedImageSurface>())
        {
            return loadedImageSurface.NaturalSize();
        }

        return m_naturalSize;
    }

    IVector<ChatBackgroundSymbol> ChatBackgroundPattern::Symbols()
    {
        return m_symbols;
    }
}
