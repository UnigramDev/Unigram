#pragma once

#include "PixelBuffer.g.h"

#include <winrt/Windows.UI.Xaml.Media.Imaging.h>

using namespace winrt::Windows::UI::Xaml::Media::Imaging;
using namespace winrt::Windows::Storage::Streams;

namespace winrt::Telegram::Native::implementation
{
    struct __declspec(uuid("905a0fef-bc53-11df-8c49-001e4fc686da")) IBufferByteAccess : ::IUnknown
    {
        virtual HRESULT __stdcall Buffer(uint8_t** value) = 0;
    };

    struct PixelBuffer : PixelBufferT<PixelBuffer, IBufferByteAccess>
    {
        PixelBuffer(WriteableBitmap bitmap);

        uint32_t Capacity();
        uint32_t Length();
        void Length(uint32_t value);

        HRESULT __stdcall Buffer(uint8_t** value);

        int32_t PixelWidth();
        int32_t PixelHeight();

        WriteableBitmap Source();

    private:
        WriteableBitmap m_bitmap;
        uint8_t* m_pixels;
        int32_t m_bitmapWidth;
        int32_t m_bitmapHeight;

        uint32_t m_capacity;
        uint32_t m_length;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct PixelBuffer : PixelBufferT<PixelBuffer, implementation::PixelBuffer>
    {
    };
}
