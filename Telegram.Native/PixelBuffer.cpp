#include "pch.h"
#include "PixelBuffer.h"
#if __has_include("PixelBuffer.g.cpp")
#include "PixelBuffer.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    PixelBuffer::PixelBuffer(WriteableBitmap bitmap)
        : m_bitmap(bitmap)
    {
        auto buffer = bitmap.PixelBuffer();
        m_pixels = buffer.data();
        m_capacity = buffer.Capacity();
        m_length = buffer.Length();

        m_bitmapWidth = bitmap.PixelWidth();
        m_bitmapHeight = bitmap.PixelHeight();
    }

    PixelBuffer::~PixelBuffer()
    {
        //m_pixels = nullptr;
        m_bitmap = nullptr;
    }

    uint32_t PixelBuffer::Capacity()
    {
        return m_capacity;
    }

    uint32_t PixelBuffer::Length()
    {
        return m_length;
    }

    void PixelBuffer::Length(uint32_t value)
    {

    }

    HRESULT __stdcall PixelBuffer::Buffer(uint8_t** value)
    {
        *value = m_pixels;
        return S_OK;
    }



    int32_t PixelBuffer::PixelWidth()
    {
        return m_bitmapWidth;
    }

    int32_t PixelBuffer::PixelHeight()
    {
        return m_bitmapHeight;
    }

    WriteableBitmap PixelBuffer::Source()
    {
        return m_bitmap;
    }
}
