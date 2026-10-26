#pragma once

#include "FreeformGradientSurface.g.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <windows.graphics.interop.h>
#include <windows.graphics.effects.interop.h>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::UIComposition;
using namespace winrt::Windows::UI;
using namespace winrt::UIXaml;
using namespace winrt::UIDispatching;

namespace abi
{
    using namespace ABI::Windows::Foundation;
    using namespace winrt::UICompositionInterop;
}

typedef std::array<float2, 4> QuadPoints;
typedef std::array<QuadPoints, 30> AnimationStops;

namespace winrt::Telegram::Native::implementation
{
    // TODO: consider creating a base class, use it for ChatBackgroundPattern as well
    struct FreeformGradientSurface : FreeformGradientSurfaceT<FreeformGradientSurface>
    {
        FreeformGradientSurface(CompositionGraphicsDevice device, winrt::com_ptr<ID2D1Factory1> d2dFactory, Compositor compositor, CompositionDrawingSurface surface, IVectorView<int32_t> colors);

        ~FreeformGradientSurface();

        IVectorView<int32_t> Colors();
        void Colors(IVectorView<int32_t> value);

        CompositionSurfaceBrush Brush();
        void Next();

        // Deterministic teardown. MUST be called on the UI thread (composition/timer are UI-affinitized).
        // After Close() all UI resources are released, so finalization on the GC thread is a no-op.
        void Close();

        static constexpr float s_width = 50.f;
        static constexpr float s_height = 50.f;

    private:
        bool m_closed = false;
        CompositionGraphicsDevice m_compositionDevice{ nullptr };
        DispatcherQueue m_dispatcherQueue{ nullptr };
        winrt::com_ptr<ID2D1Multithread> m_multithread;
        winrt::com_ptr<abi::ICompositionDrawingSurfaceInterop> m_surface;
        CompositionSurfaceBrush m_brush{ nullptr };
        DispatcherTimer m_timer;
        IVectorView<int32_t> m_colors;
        std::vector<uint8_t> m_pixels;

        // Copied out of m_colors on each change: read per pixel, every GetAt crosses the ABI. At most
        // four, one per point of QuadPoints.
        std::array<int32_t, 4> m_colorValues{};
        std::array<float3, 4> m_colorChannels{};
        uint32_t m_colorCount = 0;

        bool SetColors(IVectorView<int32_t> const& value);

        winrt::event_token m_renderingDeviceReplaced;
        winrt::event_token m_tick;

        void OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&);
        void OnTick(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::Foundation::IInspectable const&);

        HRESULT Invalidate();

        static constexpr float s_curve[] =
        {
            0.0f, 0.25f, 0.50f, 0.75f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f,
            13.0f, 14.0f, 15.0f, 16.0f, 17.0f, 18.0f, 18.3f, 18.6f, 18.9f, 19.2f, 19.5f, 19.8f, 20.1f, 20.4f, 20.7f,
            21.0f, 21.3f, 21.6f, 21.9f, 22.2f, 22.5f, 22.8f, 23.1f, 23.4f, 23.7f, 24.0f, 24.3f, 24.6f,
            24.9f, 25.2f, 25.5f, 25.8f, 26.1f, 26.3f, 26.4f, 26.5f, 26.6f, 26.7f, 26.8f, 26.9f, 27.0f
        };

        int m_index = 0;
        AnimationStops m_easing;

        int m_phase = 0;
        std::array<float2, 8> m_positions =
        {
            float2(0.80f, 0.10f),
            float2(0.60f, 0.20f),
            float2(0.35f, 0.25f),
            float2(0.25f, 0.60f),
            float2(0.20f, 0.90f),
            float2(0.40f, 0.80f),
            float2(0.65f, 0.75f),
            float2(0.75f, 0.40f),
        };

        std::array<float2, 4> Gather(int offset)
        {
            std::array<float2, 4> result;
            int n = static_cast<int>(m_positions.size());

            for (int i = 0; i < 4; ++i)
            {
                int originalIndex = i * 2;
                int shiftedIndex = (originalIndex + (offset % n) + n) % n;

                result[i] = m_positions[shiftedIndex];
            }
            return result;
        }

        AnimationStops GetKeyFrames()
        {
            m_phase++;

            auto nextPoints = Gather(m_phase % 8);
            auto prevPoints = Gather((m_phase - 1) % 8);

            const float h = 27.0f;

            std::array<float2, 4> deltas;
            for (int j = 0; j < 4; ++j)
            {
                deltas[j] = (nextPoints[j] - prevPoints[j]) / h;
            }

            AnimationStops stops;
            for (int i = 0; i < 30; ++i)
            {
                float t = s_curve[i * 2];

                for (int j = 0; j < 4; ++j)
                {
                    stops[i][j] = prevPoints[j] + (deltas[j] * t);
                }
            }

            return stops;
        }
    };
}

// No need since the type has no public constructor
//namespace winrt::Telegram::Native::factory_implementation
//{
//    struct FreeformGradientSurface : FreeformGradientSurfaceT<FreeformGradientSurface, implementation::FreeformGradientSurface>
//    {
//    };
//}
