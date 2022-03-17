#pragma once

#include "QrBuffer.g.h"

#include <winrt/Windows.Foundation.Collections.h>

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::implementation
{
    struct QrData {
        int size = 0;
        std::vector<bool> values; // size x size
    };

    struct QrBuffer : QrBufferT<QrBuffer>
    {
        QrBuffer(int32_t size, IVector<bool> values, int32_t replaceFrom, int32_t replaceTill) :
            m_size(size),
            m_values(values),
            m_replaceFrom(replaceFrom),
            m_replaceTill(replaceTill)
        {
        }

        int32_t Size() {
            return m_size;
        }

        IVector<bool> Values() {
            return m_values;
        }

        int32_t ReplaceFrom() {
            return m_replaceFrom;
        }

        int32_t ReplaceTill() {
            return m_replaceTill;
        }

        static winrt::Unigram::Native::QrBuffer FromString(hstring text);

    private:
        int32_t m_size;
        IVector<bool> m_values{ nullptr };

        int32_t m_replaceFrom;
        int32_t m_replaceTill;
    };
}

namespace winrt::Unigram::Native::factory_implementation
{
    struct QrBuffer : QrBufferT<QrBuffer, implementation::QrBuffer>
    {
    };
}
