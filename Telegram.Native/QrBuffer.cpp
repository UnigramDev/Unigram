#include "pch.h"
#include "QrBuffer.h"
#if __has_include("QrBuffer.g.cpp")
#include "QrBuffer.g.cpp"
#endif

#include "Qr/QrCode.hpp"
#include "StringUtils.h"

using namespace qrcodegen;

namespace winrt::Telegram::Native::implementation
{
    inline int ReplaceElements(const QrData& data) {
        const auto elements = (data.size / 4);
        const auto shift = (data.size - elements) % 2;
        return (elements - shift);
    }

    inline int ReplaceSize(const QrData& data, int pixel) {
        return ReplaceElements(data) * pixel;
    }

    winrt::Telegram::Native::QrBuffer QrBuffer::FromString(hstring text, int minVersion, int maxVersion) {
        auto data = QrData();
        const auto utf8 = winrt::to_string(text);
        const auto segs = QrSegment::makeSegments(utf8.c_str());
        const auto qr = QrCode::encodeSegments(segs, QrCode::Ecc::MEDIUM, minVersion, maxVersion);
        data.size = qr.getSize();

        data.values.reserve(data.size * data.size);
        for (auto row = 0; row != data.size; ++row) {
            for (auto column = 0; column != data.size; ++column) {
                data.values.push_back(qr.getModule(row, column));
            }
        }

        const auto replaceElements = ReplaceElements(data);
        const auto replaceFrom = (data.size - replaceElements) / 2;
        const auto replaceTill = (data.size - replaceFrom);

        auto values = winrt::single_threaded_vector<bool>(std::move(data.values));
        auto args = winrt::make_self<QrBuffer>(data.size, values, replaceFrom, replaceTill);

        return args.as<winrt::Telegram::Native::QrBuffer>();
    }
}
