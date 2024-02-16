#pragma once

#include "BufferSurface.g.h"
#include <winrt/Windows.Storage.Streams.h>

#include "Helpers/DebugHelper.h"

using namespace winrt::Windows::Storage::Streams;

namespace winrt::Telegram::Native::implementation
{
#ifndef IBufferByteAccess_H
#define IBufferByteAccess_H
    struct __declspec(uuid("905a0fef-bc53-11df-8c49-001e4fc686da")) IBufferByteAccess : ::IUnknown
    {
        virtual HRESULT __stdcall Buffer(uint8_t** value) = 0;
    };
#endif

    struct BufferSurface : implements<BufferSurface, IBuffer, IBufferByteAccess>
    {
    public:
        uint8_t* m_buffer;
        uint32_t m_length{};

        static size_t _counter;

        BufferSurface(winrt::array_view<uint8_t const> data) :
            m_buffer((uint8_t*)malloc(data.size())),
            m_length(data.size())
        {
            memcpy(m_buffer, data.data(), data.size());
        }

        BufferSurface(uint32_t size) :
            m_buffer((uint8_t*)malloc(size)),
            m_length(size)
        {
        }

        ~BufferSurface()
        {
            free(m_buffer);
            m_buffer = nullptr;
        }

        static IBuffer Create(uint32_t size)
        {
            auto info = winrt::make_self<winrt::Telegram::Native::implementation::BufferSurface>(size);
            return info.as<IBuffer>();
        }

        static IBuffer Create(winrt::array_view<uint8_t const> data)
        {
            auto info = winrt::make_self<winrt::Telegram::Native::implementation::BufferSurface>(data);
            return info.as<IBuffer>();
        }

        static void Copy(IBuffer source, IBuffer destination)
        {
            memcpy(destination.data(), source.data(), source.Length());
        }

        uint32_t Capacity() const
        {
            return m_length;
        }

        uint32_t Length() const
        {
            return m_length;
        }

        void Length(uint32_t value)
        {
        }

        HRESULT __stdcall Buffer(uint8_t** value) final
        {
            *value = m_buffer;
            return S_OK;
        }
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct BufferSurface : BufferSurfaceT<BufferSurface, implementation::BufferSurface>
    {
    };
}
