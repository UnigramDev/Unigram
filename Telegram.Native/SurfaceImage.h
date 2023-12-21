#pragma once

#include "SurfaceImage.g.h"

#include <D2d1_3.h>

#include <winrt/Windows.UI.Xaml.Media.Imaging.h>
#include <windows.ui.xaml.media.dxinterop.h>

using namespace winrt::Windows::UI::Xaml::Media::Imaging;

namespace winrt::Telegram::Native::implementation
{
    struct SurfaceImage : SurfaceImageT<SurfaceImage>
    {
        SurfaceImage(ID2D1Device* d2d1Device, int32_t pixelWidth, int32_t pixelHeight);

        int32_t PixelWidth() noexcept;
        int32_t PixelHeight() noexcept;

        SurfaceImageSource Source() noexcept;

        HRESULT CreateDeviceResources(ID2D1Device* d2d1Device);

    //private:
        int32_t m_pixelWidth;
        int32_t m_pixelHeight;

        SurfaceImageSource m_source;
        winrt::com_ptr<ISurfaceImageSourceNativeWithD2D> m_native;
    };
}

//namespace winrt::Telegram::Native::factory_implementation
//{
//    struct SurfaceImage : SurfaceImageT<SurfaceImage, implementation::SurfaceImage>
//    {
//    };
//}
