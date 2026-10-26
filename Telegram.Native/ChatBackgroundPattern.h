#pragma once

#include "ChatBackgroundPattern.g.h"

#include <memory>
#include <vector>

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::UIComposition;

namespace winrt::Telegram::Native::implementation
{
    struct SvgShape
    {
        winrt::com_ptr<ID2D1PathGeometry1> Geometry;
        winrt::com_ptr<ID2D1StrokeStyle1> StrokeStyle; // null when the shape is not stroked
        float Opacity;
        float StrokeWidth;
        bool Fill;
    };

    // In SVG units, so one parse serves every scale the pattern is drawn at. The geometries belong
    // to the factory that parsed them, and Direct2D draws them only on a device of that factory.
    struct SvgPattern
    {
        float Width;
        float Height;
        winrt::com_ptr<ID2D1GeometryGroup> Solid; // every opaque fill, drawn in one call
        std::vector<SvgShape> Shapes;
        std::vector<ChatBackgroundSymbol> Symbols;
    };

    inline static float2 ComputeRenderSize(float width, float height, float rasterizationScale)
    {
        auto scale = (int)(rasterizationScale * 100);
        auto dpi = 0.25f * rasterizationScale;
        auto factor = 1.0f / rasterizationScale;

        return float2(width * dpi * factor, height * dpi * factor);
    }

    inline static float2 ComputeRenderPhysicalSize(float width, float height, float rasterizationScale)
    {
        auto scale = (int)(rasterizationScale * 100);
        auto dpi = 0.25f * rasterizationScale;

        return float2(width * dpi, height * dpi);
    }

    struct ChatBackgroundPattern : ChatBackgroundPatternT<ChatBackgroundPattern>
    {
        ChatBackgroundPattern(ICompositionSurface surface)
            : m_surface(surface)
            , m_renderSize(0, 0)
            , m_renderPhysicalSize(0, 0)
            , m_naturalSize(0, 0)
            , m_symbols(winrt::single_threaded_vector<ChatBackgroundSymbol>())
        {

        }

        // Must be constructed on the view's thread, which is where the redraw after a device
        // replacement is posted.
        ChatBackgroundPattern(CompositionGraphicsDevice device, CompositionDrawingSurface surface, std::shared_ptr<const SvgPattern> svg, float rasterizationScale, float dpi, float intensity, bool negative, IVector<ChatBackgroundSymbol> patterns);

        ICompositionSurface Surface();
        float2 RenderSize();
        float2 RenderPhysicalSize();
        float2 NaturalSize();
        IVector<ChatBackgroundSymbol> Symbols();

        HRESULT Invalidate();

        // Must be called on the view's thread. The surface is kept: a brush that is still fading
        // out may show it.
        void Close();

    private:
        bool m_closed = false;
        ICompositionSurface m_surface;
        float2 m_renderSize;
        float2 m_renderPhysicalSize;
        float2 m_naturalSize;
        IVector<ChatBackgroundSymbol> m_symbols;

        CompositionGraphicsDevice m_compositionDevice{ nullptr };
        winrt::UIDispatching::DispatcherQueue m_dispatcherQueue{ nullptr };
        std::shared_ptr<const SvgPattern> m_svg;
        float m_dpi = 0;
        float m_intensity = 0;
        bool m_negative = false;

        winrt::event_token m_renderingDeviceReplaced;

        void OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&);
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct ChatBackgroundPattern : ChatBackgroundPatternT<ChatBackgroundPattern, implementation::ChatBackgroundPattern>
    {
    };
}
