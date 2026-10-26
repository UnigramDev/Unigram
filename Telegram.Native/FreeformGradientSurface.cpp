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
using namespace winrt::GraphicsDirectX;

namespace winrt::Telegram::Native::implementation
{
    FreeformGradientSurface::FreeformGradientSurface(CompositionGraphicsDevice device, winrt::com_ptr<ID2D1Factory1> d2dFactory, Compositor compositor, CompositionDrawingSurface surface, IVectorView<int32_t> colors)
        : m_compositionDevice(device)
        , m_dispatcherQueue(DispatcherQueue::GetForCurrentThread())
        , m_multithread(d2dFactory.try_as<ID2D1Multithread>())
        , m_surface(surface.as<abi::ICompositionDrawingSurfaceInterop>())
        , m_colors(colors)
        , m_brush(compositor.CreateSurfaceBrush())
    {
        m_brush.Surface(surface);
        m_brush.Stretch(CompositionStretch::Fill);

        m_timer.Interval(std::chrono::milliseconds(500 / 30));
        m_tick = m_timer.Tick({ get_weak(), &FreeformGradientSurface::OnTick });

        m_easing = GetKeyFrames();
        m_pixels.resize(s_width * s_height * 4);

        SetColors(colors);
        Invalidate();

        m_phase = 0;
        m_renderingDeviceReplaced = m_compositionDevice.RenderingDeviceReplaced({ get_weak(), &FreeformGradientSurface::OnRenderingDeviceReplaced });
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
        m_multithread = nullptr;
        m_dispatcherQueue = nullptr;
        m_colors = nullptr;
    }

    FreeformGradientSurface::~FreeformGradientSurface()
    {
        // All UI-thread-affinitized resources are released by Close(), which the owner must call on the
        // UI thread. If Close() ran, the members are already null and destruction here is a no-op.
    }

    void FreeformGradientSurface::OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
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

    static constexpr int s_pixelWidth = static_cast<int>(FreeformGradientSurface::s_width);
    static constexpr int s_pixelHeight = static_cast<int>(FreeformGradientSurface::s_height);

    // The swirl moves each pixel by an amount that depends on nothing but where the pixel is, so it
    // is worked out once for every surface and frame instead of a sqrt, sin and cos per pixel each time.
    static const std::array<float2, s_pixelWidth * s_pixelHeight>& SwirlTable()
    {
        static const auto table = []
        {
            std::array<float2, s_pixelWidth * s_pixelHeight> result;

            for (int y = 0; y < s_pixelHeight; y++)
            {
                auto centerDistanceY = y / (float)s_pixelHeight - 0.5f;
                auto centerDistanceY2 = centerDistanceY * centerDistanceY;

                for (int x = 0; x < s_pixelWidth; x++)
                {
                    auto centerDistanceX = x / (float)s_pixelWidth - 0.5f;
                    auto centerDistance = sqrtf(centerDistanceX * centerDistanceX + centerDistanceY2);

                    auto swirlFactor = 0.35f * centerDistance;
                    auto theta = swirlFactor * swirlFactor * 0.8f * 8.0f;
                    auto sinTheta = sinf(theta);
                    auto cosTheta = cosf(theta);

                    result[y * s_pixelWidth + x] = float2(
                        fmaxf(0.0f, fminf(1.0f, 0.5f + centerDistanceX * cosTheta - centerDistanceY * sinTheta)),
                        fmaxf(0.0f, fminf(1.0f, 0.5f + centerDistanceX * sinTheta + centerDistanceY * cosTheta)));
                }
            }

            return result;
        }();

        return table;
    }

    static void GenerateGradient(uint32_t* pixels, const float3* colors, uint32_t count, QuadPoints const& positions)
    {
        auto const& swirl = SwirlTable();

        for (int i = 0; i < s_pixelWidth * s_pixelHeight; i++)
        {
            auto pixel = swirl[i];

            auto distanceSum = 0.0f;
            auto r = 0.0f;
            auto g = 0.0f;
            auto b = 0.0f;

            for (uint32_t j = 0; j < count; j++)
            {
                auto distanceX = pixel.x - positions[j].x;
                auto distanceY = pixel.y - positions[j].y;

                auto distance = fmaxf(0.0f, 0.9f - sqrtf(distanceX * distanceX + distanceY * distanceY));
                distance = distance * distance * distance * distance;
                distanceSum += distance;

                r += distance * colors[j].x;
                g += distance * colors[j].y;
                b += distance * colors[j].z;
            }

            auto scale = 255.0f / distanceSum;

            // BGRA in memory, so 0xAARRGGBB as a little-endian word.
            pixels[i] = 0xff000000
                | static_cast<uint32_t>(static_cast<uint8_t>(r * scale)) << 16
                | static_cast<uint32_t>(static_cast<uint8_t>(g * scale)) << 8
                | static_cast<uint32_t>(static_cast<uint8_t>(b * scale));
        }
    }

    HRESULT FreeformGradientSurface::Invalidate()
    {
        if (!m_surface) return E_FAIL;

        // Before BeginDraw and outside the factory lock, neither of which it needs.
        GenerateGradient(reinterpret_cast<uint32_t*>(m_pixels.data()), m_colorChannels.data(), m_colorCount, m_easing[m_index % m_easing.size()]);

        winrt::com_ptr<IDXGISurface> dxgiSurface;
        POINT offset;

        if (FAILED(m_surface->BeginDraw(nullptr, __uuidof(IDXGISurface), dxgiSurface.put_void(), &offset)))
        {
            return E_FAIL;
        }

        HRESULT result = E_NOINTERFACE;

        if (auto texture = dxgiSurface.try_as<ID3D11Texture2D>())
        {
            winrt::com_ptr<ID3D11Device> device;
            texture->GetDevice(device.put());

            winrt::com_ptr<ID3D11DeviceContext> context;
            device->GetImmediateContext(context.put());

            D3D11_BOX box{ static_cast<UINT>(offset.x), static_cast<UINT>(offset.y), 0, static_cast<UINT>(offset.x + s_width), static_cast<UINT>(offset.y + s_height), 1 };

            if (m_multithread)
            {
                m_multithread->Enter();
            }

            context->UpdateSubresource(texture.get(), 0, &box, m_pixels.data(), s_width * 4, 0);

            if (m_multithread)
            {
                m_multithread->Leave();
            }

            result = S_OK;
        }

        if (FAILED(m_surface->EndDraw()) || FAILED(result))
        {
            return E_FAIL;
        }

        return S_OK;
    }

    IVectorView<int32_t> FreeformGradientSurface::Colors()
    {
        return m_colors;
    }

    void FreeformGradientSurface::Colors(IVectorView<int32_t> value)
    {
        if (SetColors(value))
        {
            Invalidate();
        }
    }

    // False when the colors are the ones already drawn. Compared by content: the same fill from
    // TDLib arrives as a different array.
    bool FreeformGradientSurface::SetColors(IVectorView<int32_t> const& value)
    {
        std::array<int32_t, 4> values{};
        uint32_t count = value ? value.GetMany(0, values) : 0;

        m_colors = value;

        if (count == m_colorCount && values == m_colorValues)
        {
            return false;
        }

        m_colorValues = values;
        m_colorCount = count;

        for (uint32_t i = 0; i < count; i++)
        {
            // 0x00RRGGBB, straight from TDLib: unpacked here rather than widened to
            // Windows.UI.Color on the managed side, which cost a conversion and a copy
            // per background change and could not cross the ABI as an array at all.
            auto color = values[i];

            m_colorChannels[i] = float3(((color >> 16) & 0xff) / 255.f, ((color >> 8) & 0xff) / 255.f, (color & 0xff) / 255.f);
        }

        return true;
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
