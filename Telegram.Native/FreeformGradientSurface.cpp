#include "pch.h"
#include "FreeformGradientSurface.h"
#if __has_include("FreeformGradientSurface.g.cpp")
#include "FreeformGradientSurface.g.cpp"
#endif

#include "Helpers\COMHelper.h"

#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>

using namespace D2D1;
using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::UI;
using namespace winrt::Windows::Graphics::DirectX;

namespace winrt::Telegram::Native::implementation
{
    FreeformGradientSurface::FreeformGradientSurface(CompositionGraphicsDevice device, winrt::com_ptr<ID2D1Factory1> d2dFactory, Compositor compositor, CompositionDrawingSurface surface, IVectorView<int32_t> colors)
        : m_compositionDevice(device)
        , m_d2dFactory(d2dFactory)
        , m_compositor(compositor)
        , m_surface(surface.as<abi::ICompositionDrawingSurfaceInterop>())
        , m_colors(colors)
        , m_brush(m_compositor.CreateSurfaceBrush())
    {
        m_brush.Surface(surface);
        m_brush.Stretch(CompositionStretch::Fill);

        m_timer.Interval(std::chrono::milliseconds(500 / 30));
        m_tick = m_timer.Tick({ get_weak(), &FreeformGradientSurface::OnTick});

        m_easing = GetKeyFrames();
        m_pixels.reserve(s_width * s_height * 4);

        Invalidate();

        m_phase = 0;
        m_renderingDeviceReplaced = m_compositionDevice.RenderingDeviceReplaced({ get_weak(), &FreeformGradientSurface::OnRenderingDeviceReplaced});
    }

    void FreeformGradientSurface::Close()
    {
        // Must run on the UI thread. Stops the timer, detaches both event handlers, and releases every
        // UI-thread-affinitized member here so that finalization of the managed wrapper (which can run
        // on the GC thread) has nothing affinitized left to release.
        if (m_closed)
        {
            return;
        }

        m_closed = true;

        if (m_timer != nullptr)
        {
            m_timer.Stop();
            m_timer.Tick(m_tick);
            m_timer = nullptr;
        }

        if (m_compositionDevice != nullptr)
        {
            m_compositionDevice.RenderingDeviceReplaced(m_renderingDeviceReplaced);
            m_compositionDevice = nullptr;
        }

        m_brush = nullptr;
        m_surface = nullptr;
        m_bitmap = nullptr;
        m_d2dFactory = nullptr;
        m_compositor = nullptr;
        m_colors = nullptr;
    }

    FreeformGradientSurface::~FreeformGradientSurface()
    {
        // All UI-thread-affinitized resources are released by Close(), which the owner must call on the
        // UI thread. If Close() ran, the members are already null and destruction here is a no-op.
    }

    void FreeformGradientSurface::OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
    {
        m_bitmap = nullptr;
        Invalidate();
    }

    void FreeformGradientSurface::OnTick(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::Foundation::IInspectable const&)
    {
        HRESULT result = Invalidate();
        m_index++;

        if (FAILED(result) || m_index == m_easing.size())
        {
            m_index--;
            m_timer.Stop();
        }
    }

    inline void GenerateGradient(uint8_t* imageBytes, IVectorView<int32_t> colors, QuadPoints positions)
    {
        auto width = 50;
        auto height = 50;

        for (int y = 0; y < height; y++)
        {
            auto directPixelY = y / (float)height;
            auto centerDistanceY = directPixelY - 0.5f;
            auto centerDistanceY2 = centerDistanceY * centerDistanceY;

            auto lineBytes = imageBytes + width * 4 * y;
            for (int x = 0; x < width; x++)
            {
                auto directPixelX = x / (float)width;

                auto centerDistanceX = directPixelX - 0.5f;
                auto centerDistance = sqrtf(centerDistanceX * centerDistanceX + centerDistanceY2);

                auto swirlFactor = 0.35f * centerDistance;
                auto theta = swirlFactor * swirlFactor * 0.8f * 8.0f;
                auto sinTheta = sinf(theta);
                auto cosTheta = cosf(theta);

                auto pixelX = fmaxf(0.0f, fminf(1.0f, 0.5f + centerDistanceX * cosTheta - centerDistanceY * sinTheta));
                auto pixelY = fmaxf(0.0f, fminf(1.0f, 0.5f + centerDistanceX * sinTheta + centerDistanceY * cosTheta));

                auto distanceSum = 0.0f;

                auto r = 0.0f;
                auto g = 0.0f;
                auto b = 0.0f;

                for (int i = 0; i < colors.Size(); i++)
                {
                    auto colorX = positions[i].x;
                    auto colorY = positions[i].y;

                    auto distanceX = pixelX - colorX;
                    auto distanceY = pixelY - colorY;

                    auto distance = fmaxf(0.0f, 0.9f - sqrtf(distanceX * distanceX + distanceY * distanceY));
                    distance = distance * distance * distance * distance;
                    distanceSum += distance;

                    // 0x00RRGGBB, straight from TDLib: unpacked here rather than widened to
                    // Windows.UI.Color on the managed side, which cost a conversion and a copy
                    // per background change and could not cross the ABI as an array at all.
                    auto color = colors.GetAt(i);

                    r += distance * ((color >> 16) & 0xff) / 255.f;
                    g += distance * ((color >> 8) & 0xff) / 255.f;
                    b += distance * (color & 0xff) / 255.f;
                }

                auto pixelBytes = lineBytes + x * 4;
                pixelBytes[0] = (byte)(b / distanceSum * 255.0f);
                pixelBytes[1] = (byte)(g / distanceSum * 255.0f);
                pixelBytes[2] = (byte)(r / distanceSum * 255.0f);
                pixelBytes[3] = 0xff;
            }
        }
    }

    HRESULT FreeformGradientSurface::Invalidate()
    {
        if (!m_surface) return E_FAIL;

        //std::lock_guard const guard(m_criticalSection);
        HRESULT result;

        winrt::com_ptr<ID2D1DeviceContext> d2dContext;
        POINT offset;

        // BeginDraw can return DXGI_ERROR_DEVICE_REMOVED, if it happens we just return.
        // Direct2DDevice will be handling this for us, raising RenderingDeviceReplaced.
        ReturnIfFailed(result, m_surface->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), d2dContext.put_void(), &offset));

        if (m_bitmap == nullptr)
        {
            D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_NONE, 0 };
            ReturnIfFailed(result, d2dContext->CreateBitmap(D2D1::SizeU(s_width, s_height), nullptr, 0, properties, m_bitmap.put()));
        }

        GenerateGradient(m_pixels.data(), m_colors, m_easing[m_index % m_easing.size()]);

        uint32_t pitch = s_width * 4;
        D2D1_RECT_U destRect = D2D1::RectU(0, 0, s_width, s_height);

        m_bitmap->CopyFromMemory(&destRect, m_pixels.data(), pitch);

        d2dContext->Clear(D2D1::ColorF(0, 0, 0, 1));
        d2dContext->SetTransform(D2D1::Matrix3x2F::Translation(offset.x, offset.y));

        d2dContext->DrawBitmap(m_bitmap.get());

    Cleanup:
        return m_surface->EndDraw();
    }

    IVectorView<int32_t> FreeformGradientSurface::Colors()
    {
        return m_colors;
    }

    void FreeformGradientSurface::Colors(IVectorView<int32_t> value)
    {
        m_colors = value;
        Invalidate();
    }

    CompositionSurfaceBrush FreeformGradientSurface::Brush()
    {
        return m_brush;
    }

    void FreeformGradientSurface::Next()
    {
        m_timer.Stop();
        m_easing = GetKeyFrames();
        m_index = 0;
        m_timer.Start();
    }
}
