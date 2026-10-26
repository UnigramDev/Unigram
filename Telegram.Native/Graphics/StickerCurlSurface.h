#pragma once

#include "Graphics/StickerCurlSurface.g.h"

#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.Storage.Streams.h>
#include <windows.ui.composition.interop.h>

#include <vector>

namespace winrt::Telegram::Native::Graphics::implementation
{
    struct StickerCurlSurface : StickerCurlSurfaceT<StickerCurlSurface>
    {
        StickerCurlSurface(winrt::UIComposition::CompositionGraphicsDevice const& device, int32_t side);

        static float Margin();

        winrt::UIComposition::CompositionDrawingSurface Surface();

        void SetImage(winrt::Windows::Storage::Streams::IBuffer const& pixels, int32_t pixelWidth, int32_t pixelHeight, float dieCut);

        bool Draw(float curl, winrt::Windows::Foundation::Numerics::float2 const& direction, float bend);

        void Trim();

        void Close();

    private:
        bool EnsureResources(ID2D1DeviceContext* context);
        void ReleaseResources();

        winrt::UIComposition::CompositionDrawingSurface m_surface{ nullptr };
        winrt::com_ptr<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop> m_interop;

        int32_t m_side;
        int32_t m_extent;

        std::vector<uint8_t> m_pixels;
        int32_t m_pixelWidth = 0;
        int32_t m_pixelHeight = 0;
        float m_dieCut = 0;

        // All made on m_device and remade when the session's device is another one, which is how
        // a device loss shows up here: the surface survives it, these do not.
        winrt::com_ptr<ID2D1Device> m_device;
        winrt::com_ptr<ID2D1Bitmap1> m_art;
        winrt::com_ptr<ID2D1Bitmap1> m_sheet;
        winrt::com_ptr<ID2D1Bitmap1> m_output;
        winrt::com_ptr<ID2D1Effect> m_coverage;
        winrt::com_ptr<ID2D1Effect> m_blur;
        winrt::com_ptr<ID2D1Effect> m_edge;
        winrt::com_ptr<ID2D1Effect> m_composite;
        winrt::com_ptr<ID2D1Effect> m_curl;

        // The die-cut sticker in m_sheet is drawn once per image, the curl every frame.
        bool m_sheetValid = false;

        // m_pixels changed since m_art was filled from them.
        bool m_artDirty = false;
        bool m_closed = false;
    };
}

namespace winrt::Telegram::Native::Graphics::factory_implementation
{
    struct StickerCurlSurface : StickerCurlSurfaceT<StickerCurlSurface, implementation::StickerCurlSurface>
    {
    };
}
