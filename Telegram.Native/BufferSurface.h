#pragma once

#include "BufferSurface.g.h"
#include <winrt/Windows.Storage.Streams.h>

using namespace winrt::Windows::Storage::Streams;

namespace winrt::Telegram::Native::implementation
{
    struct __declspec(uuid("905a0fef-bc53-11df-8c49-001e4fc686da")) IBufferByteAccess : ::IUnknown
    {
        virtual HRESULT __stdcall Buffer(uint8_t** value) = 0;
    };

    struct BufferSurface : implements<BufferSurface, IBuffer, IBufferByteAccess>
    {
    public:
        std::vector<uint8_t> m_buffer;
        uint32_t m_length{};

        BufferSurface(uint32_t size) :
            m_buffer(size),
            m_length(size)
        {
        }

        static IBuffer Create(uint32_t size)
        {
            auto info = winrt::make_self<winrt::Telegram::Native::implementation::BufferSurface>(size);
            return info.as<IBuffer>();
        }

        static void Copy(IBuffer source, IBuffer destination)
        {
            memcpy(destination.data(), source.data(), source.Length());
        }

        uint32_t Capacity() const
        {
            return m_buffer.size();
        }

        uint32_t Length() const
        {
            return m_length;
        }

        void Length(uint32_t value)
        {
            if (value > m_buffer.size())
            {
                throw hresult_invalid_argument();
            }

            m_length = value;
        }

        HRESULT __stdcall Buffer(uint8_t** value) final
        {
            *value = m_buffer.data();
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
