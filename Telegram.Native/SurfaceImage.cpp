#include "pch.h"
#include "SurfaceImage.h"
#if __has_include("SurfaceImage.g.cpp")
#include "SurfaceImage.g.cpp"
#endif

#include <Helpers/COMHelper.h>

namespace winrt::Telegram::Native::implementation
{
    SurfaceImage::SurfaceImage(ID2D1Device* d2d1Device, int32_t pixelWidth, int32_t pixelHeight)
        : m_source(SurfaceImageSource(pixelWidth, pixelHeight))
        , m_native(m_source.as<ISurfaceImageSourceNativeWithD2D>())
        , m_pixelWidth(pixelWidth)
        , m_pixelHeight(pixelHeight)
    {
        CreateDeviceResources(d2d1Device);
    }

    HRESULT SurfaceImage::CreateDeviceResources(ID2D1Device* d2d1Device)
    {
        HRESULT result;
        ReturnIfFailed(result, m_native->SetDevice(d2d1Device));
    }

    int32_t SurfaceImage::PixelWidth() noexcept
    {
        return m_pixelWidth;
    }

    int32_t SurfaceImage::PixelHeight() noexcept
    {
        return m_pixelHeight;
    }

    SurfaceImageSource SurfaceImage::Source() noexcept
    {
        return m_source;
    }
}
