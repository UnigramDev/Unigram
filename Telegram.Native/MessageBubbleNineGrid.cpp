#include "pch.h"
#include "MessageBubbleNineGrid.h"
#if __has_include("MessageBubbleNineGrid.g.cpp")
#include "MessageBubbleNineGrid.g.cpp"
#endif

#include "Helpers\COMHelper.h"

#include <winrt/Windows.Graphics.Effects.h>

using namespace D2D1;
using namespace winrt::Windows::Graphics::DirectX;

namespace winrt::Telegram::Native::implementation
{
    MessageBubbleNineGrid::MessageBubbleNineGrid(CompositionGraphicsDevice device, winrt::com_ptr<ID2D1Factory1> d2dFactory, Compositor compositor, XamlRoot xamlRoot, CompositionDrawingSurface surface, CompositionEffectBrush effect, float topLeftRadius, float topRightRadius, float bottomRightRadius, float bottomLeftRadius)
        : m_compositionDevice(device)
        , m_d2dFactory(d2dFactory)
        , m_compositor(compositor)
        , m_xamlRoot(xamlRoot)
        , m_surface(surface.as<abi::ICompositionDrawingSurfaceInterop>())
        , m_topLeftRadius(topLeftRadius)
        , m_topRightRadius(topRightRadius)
        , m_bottomRightRadius(bottomRightRadius)
        , m_bottomLeftRadius(bottomLeftRadius)
        , m_rasterizationScale(xamlRoot.RasterizationScale())
        , m_brush(m_compositor.CreateNineGridBrush())
        , m_effect(effect)
    {
        auto surfaceBrush = m_compositor.CreateSurfaceBrush();
        surfaceBrush.Surface(surface);
        surfaceBrush.Stretch(CompositionStretch::Fill);

        m_brush.Source(surfaceBrush);
        m_brush.SetInsets(s_insets * static_cast<float>(m_rasterizationScale));

        m_effect.SetSourceParameter(L"mask", m_brush);

        Invalidate(m_rasterizationScale);

        m_xamlRootChanged = m_xamlRoot.Changed({ this, &MessageBubbleNineGrid::OnXamlRootChanged });
        m_renderingDeviceReplaced = m_compositionDevice.RenderingDeviceReplaced({ this, &MessageBubbleNineGrid::OnRenderingDeviceReplaced });
    }

    MessageBubbleNineGrid::~MessageBubbleNineGrid()
    {
        m_xamlRoot.Changed(m_xamlRootChanged);
        m_compositionDevice.RenderingDeviceReplaced(m_renderingDeviceReplaced);
    }

    void MessageBubbleNineGrid::OnXamlRootChanged(XamlRoot const& sender, XamlRootChangedEventArgs const& args)
    {
        if (m_rasterizationScale != sender.RasterizationScale())
        {
            Invalidate(sender.RasterizationScale());
        }
    }

    void MessageBubbleNineGrid::OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
    {
        Invalidate(m_xamlRoot.RasterizationScale());
    }

    HRESULT MessageBubbleNineGrid::Invalidate(double rasterizationScale)
    {
        //std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        if (m_rasterizationScale != rasterizationScale)
        {
            SIZE imageSize(std::ceil(s_width * rasterizationScale), std::ceil(s_height * rasterizationScale));
            ReturnIfFailed(result, m_surface->Resize(imageSize));

            m_brush.SetInsets(s_insets * static_cast<float>(rasterizationScale));
            m_rasterizationScale = rasterizationScale;
        }

        winrt::com_ptr<ID2D1SolidColorBrush> blackBrush;
        winrt::com_ptr<ID2D1GeometrySink> d2dGeometrySink;
        winrt::com_ptr<ID2D1PathGeometry1> d2dPathGeometry;

        auto r = static_cast<float>(m_rasterizationScale);
        auto xshift = s_width - 30;
        auto yshift = s_height - 30;

        winrt::com_ptr<ID2D1DeviceContext> d2dContext;
        POINT offset;

        // BeginDraw can return DXGI_ERROR_DEVICE_REMOVED, if it happens we just return.
        // PlaceholderImageHelper will be handling this for us, raising RenderingDeviceReplaced.
        ReturnIfFailed(result, m_surface->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), d2dContext.put_void(), &offset));

        d2dContext->Clear(D2D1::ColorF(0, 0, 0, 0));
        d2dContext->SetTransform(D2D1::Matrix3x2F::Translation(offset.x, offset.y));

        CleanupIfFailed(result, d2dContext->CreateSolidColorBrush(D2D1::ColorF(1, 1, 1, 1), blackBrush.put()));

        CleanupIfFailed(result, m_d2dFactory->CreatePathGeometry(d2dPathGeometry.put()));
        CleanupIfFailed(result, d2dPathGeometry->Open(d2dGeometrySink.put()));

        d2dGeometrySink->BeginFigure({ m_topLeftRadius * r, 0 }, D2D1_FIGURE_BEGIN_FILLED);

        // Top edge
        d2dGeometrySink->AddLine({ (s_width - m_topRightRadius) * r, 0 });

        // Top-right corner
        if (m_topRightRadius > 0)
            d2dGeometrySink->AddArc({ {s_width * r, m_topRightRadius * r}, {m_topRightRadius * r, m_topRightRadius * r}, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });

        // Right edge
        d2dGeometrySink->AddLine({ s_width * r, (s_height - (m_bottomRightRadius > 0 ? m_bottomRightRadius : 15)) * r });

        // Bottom-right corner
        if (m_bottomRightRadius > 0)
        {
            d2dGeometrySink->AddArc({ { (s_width - m_bottomRightRadius) * r, s_height * r }, { m_bottomRightRadius * r, m_bottomRightRadius * r}, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });
        }
        else
        {
            d2dGeometrySink->AddBezier({ { (xshift + 30.f) * r, (yshift + 15.f) * r }, { (xshift + 30.f) * r, (yshift + 18.493f) * r }, { (xshift + 28.796f) * r, (yshift + 21.704f) * r } });
            d2dGeometrySink->AddBezier({ { (xshift + 26.802f) * r, (yshift + 24.259f) * r }, { (xshift + 26.802f) * r, (yshift + 27.222f) * r }, { (xshift + 29.444f) * r, (yshift + 28.889f) * r } });
            d2dGeometrySink->AddBezier({ { (xshift + 29.833f) * r, (yshift + 29.167f) * r }, { (xshift + 30.f) * r, (yshift + 29.444f) * r }, { (xshift + 29.815f) * r, (yshift + 29.815f) * r } });
            d2dGeometrySink->AddBezier({ { (xshift + 29.444f) * r, (yshift + 29.815f) * r }, { (xshift + 25.463f) * r, (yshift + 29.815f) * r }, { (xshift + 24.630f) * r, (yshift + 29.815f) * r } });
            d2dGeometrySink->AddBezier({ { (xshift + 21.667f) * r, (yshift + 28.444f) * r }, { (xshift + 19.630f) * r, (yshift + 29.444f) * r }, { (xshift + 17.407f) * r, (yshift + 30.f) * r } });
        }

        // Bottom edge
        d2dGeometrySink->AddLine({ (m_bottomLeftRadius > 0 ? m_bottomLeftRadius : 15) * r, s_height * r });

        // Bottom-left corner
        if (m_bottomLeftRadius > 0)
        {
            d2dGeometrySink->AddArc({ { 0, (s_height - m_bottomLeftRadius) * r }, { m_bottomLeftRadius * r, m_bottomLeftRadius * r }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });
        }
        else
        {
            d2dGeometrySink->AddBezier({ { 12.593f * r, (yshift + 30.f) * r }, { 10.370f * r, (yshift + 29.444f) * r }, { 8.333f * r, (yshift + 28.444f) * r } });
            d2dGeometrySink->AddBezier({ { 5.370f * r, (yshift + 29.815f) * r }, { 4.537f * r, (yshift + 29.815f) * r }, { 0.556f * r, (yshift + 29.815f) * r } });
            d2dGeometrySink->AddBezier({ { 0.185f * r, (yshift + 29.815f) * r }, { 0.f, (yshift + 29.444f) * r }, { 0.167f * r, (yshift + 29.167f) * r } });
            d2dGeometrySink->AddBezier({ { 0.556f * r, (yshift + 28.889f) * r }, { 3.198f * r, (yshift + 27.222f) * r }, { 3.198f * r, (yshift + 24.259f) * r } });
            d2dGeometrySink->AddBezier({ { 1.204f * r, (yshift + 21.704f) * r }, { 0.f, (yshift + 18.493f) * r }, { 0.f, (yshift + 15.f) * r } });
        }

        // Left edge
        d2dGeometrySink->AddLine({ 0, m_topLeftRadius * r });

        // Top-left corner
        if (m_topLeftRadius > 0)
            d2dGeometrySink->AddArc({ { m_topLeftRadius * r, 0 }, { m_topLeftRadius * r, m_topLeftRadius * r }, 0, D2D1_SWEEP_DIRECTION_CLOCKWISE, D2D1_ARC_SIZE_SMALL });

        d2dGeometrySink->EndFigure(D2D1_FIGURE_END_CLOSED);

        CleanupIfFailed(result, d2dGeometrySink->Close());

        d2dContext->FillGeometry(d2dPathGeometry.get(), blackBrush.get());

    Cleanup:
        return m_surface->EndDraw();
    }

    CompositionEffectBrush MessageBubbleNineGrid::Effect()
    {
        return m_effect;
    }
}
