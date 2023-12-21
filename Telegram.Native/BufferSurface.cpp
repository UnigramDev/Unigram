#include "pch.h"
#include "BufferSurface.h"
#if __has_include("BufferSurface.g.cpp")
#include "BufferSurface.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    size_t BufferSurface::_counter = 0;

    //uint8_t* LottieSurface::Pixels()
    //{
    //    return m_pixels;
    //}

    //void LottieSurface::Pixels(uint8_t* value)
    //{
    //    m_pixels = value;
    //}
}
