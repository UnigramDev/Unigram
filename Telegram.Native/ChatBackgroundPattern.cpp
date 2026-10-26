#include "pch.h"
#include "ChatBackgroundPattern.h"
#if __has_include("ChatBackgroundPattern.g.cpp")
#include "ChatBackgroundPattern.g.cpp"
#endif

#include "Helpers\COMHelper.h"

using namespace winrt::UIDispatching;
using namespace winrt::UIXaml::Media;

namespace winrt::Telegram::Native::implementation
{
    ChatBackgroundPattern::ChatBackgroundPattern(CompositionGraphicsDevice device, CompositionDrawingSurface surface, std::shared_ptr<const SvgPattern> svg, float rasterizationScale, float dpi, float intensity, bool negative, IVector<ChatBackgroundSymbol> patterns)
        : m_surface(surface)
        , m_renderSize(ComputeRenderSize(svg->Width, svg->Height, rasterizationScale))
        , m_renderPhysicalSize(ComputeRenderPhysicalSize(svg->Width, svg->Height, rasterizationScale))
        , m_naturalSize(svg->Width, svg->Height)
        , m_symbols(patterns)
        , m_compositionDevice(device)
        , m_dispatcherQueue(DispatcherQueue::GetForCurrentThread())
        , m_svg(std::move(svg))
        , m_dpi(dpi)
        , m_intensity(intensity)
        , m_negative(negative)
    {
        m_renderingDeviceReplaced = m_compositionDevice.RenderingDeviceReplaced({ get_weak(), &ChatBackgroundPattern::OnRenderingDeviceReplaced });
    }

    void ChatBackgroundPattern::Close()
    {
        if (m_closed)
        {
            return;
        }

        m_closed = true;

        if (m_compositionDevice != nullptr)
        {
            m_compositionDevice.RenderingDeviceReplaced(m_renderingDeviceReplaced);
            m_compositionDevice = nullptr;
        }

        m_svg = nullptr;
    }

    void ChatBackgroundPattern::OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
    {
        // Posted rather than drawn here: nothing says which thread raises the event, and a drawing
        // session on this device fails if it overlaps one the view's thread has open.
        if (m_dispatcherQueue)
        {
            m_dispatcherQueue.TryEnqueue([weak = get_weak()]
                {
                    if (auto self = weak.get())
                    {
                        self->Invalidate();
                    }
                });
        }
    }

    HRESULT ChatBackgroundPattern::Invalidate()
    {
        if (m_closed || m_svg == nullptr)
        {
            return E_FAIL;
        }

        auto surfaceInterop = m_surface.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();

        HRESULT result;
        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        ReturnIfFailed(result, surfaceInterop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), context.put_void(), &offset));

        // Only one surface may be open for drawing on a device at a time, so everything from here
        // reaches EndDraw whatever it returns. The context is the device's, handed to every other
        // surface drawn from it, so the blend and transform set here are put back.
        winrt::com_ptr<ID2D1SolidColorBrush> blackBrush;

        if (m_negative)
        {
            context->Clear(D2D1::ColorF(0, 0, 0, 1));
            context->SetPrimitiveBlend(D2D1_PRIMITIVE_BLEND_COPY);

            result = context->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, 1 - m_intensity), blackBrush.put());
        }
        else
        {
            context->Clear(D2D1::ColorF(0, 0, 0, 0));

            result = context->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, m_intensity), blackBrush.put());
        }

        if (SUCCEEDED(result))
        {
            // The surface can sit anywhere in the device's atlas, and BeginDraw says where.
            context->SetTransform(D2D1::Matrix3x2F::Scale(m_dpi, m_dpi) * D2D1::Matrix3x2F::Translation((float)offset.x, (float)offset.y));

            if (m_svg->Solid)
            {
                blackBrush->SetOpacity(1);
                context->FillGeometry(m_svg->Solid.get(), blackBrush.get());
            }

            for (auto const& shape : m_svg->Shapes)
            {
                blackBrush->SetOpacity(shape.Opacity);

                if (shape.Fill)
                {
                    context->FillGeometry(shape.Geometry.get(), blackBrush.get());
                }

                if (shape.StrokeStyle)
                {
                    context->DrawGeometry(shape.Geometry.get(), blackBrush.get(), shape.StrokeWidth, shape.StrokeStyle.get());
                }
            }

            context->SetTransform(D2D1::Matrix3x2F::Identity());
        }

        context->SetPrimitiveBlend(D2D1_PRIMITIVE_BLEND_SOURCE_OVER);

        HRESULT ended = surfaceInterop->EndDraw();
        return FAILED(result) ? result : ended;
    }

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
