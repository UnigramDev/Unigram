#include "pch.h"
#include "TextRecognizerDefault.h"

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::AI::implementation
{
    TextRecognizerDefault::TextRecognizerDefault(winrt::Windows::Media::Ocr::OcrEngine engine)
        : m_engine(engine)
    {

    }

    inline static Rect UnionRect(const Rect& a, const Rect& b)
    {
        float x1 = std::min(a.X, b.X);
        float x2 = std::max(a.X + a.Width, b.X + b.Width);
        float y1 = std::min(a.Y, b.Y);
        float y2 = std::max(a.Y + a.Height, b.Y + b.Height);
        return Rect(x1, y1, x2 - x1, y2 - y1);
    }

    inline static Rect UnionRects(const IVectorView<winrt::Windows::Media::Ocr::OcrWord>& rects)
    {
        if (rects.Size() == 0) return Rect();
        Rect u = rects.GetAt(0).BoundingRect();
        for (uint32_t i = 1; i < rects.Size(); i++)
            u = UnionRect(u, rects.GetAt(i).BoundingRect());
        return u;
    }

    inline static RecognizedTextBoundingBox ApplyTextAngleToRect(const Rect& boundingRect, IReference<double> textAngleRef, double imageWidth, double imageHeight)
    {
        double textAngle = textAngleRef ? textAngleRef.Value() : 0.0;
        if (textAngle == 0.0)
        {
            return RecognizedTextBoundingBox{
                float2(boundingRect.X, boundingRect.Y),
                float2(boundingRect.X + boundingRect.Width, boundingRect.Y),
                float2(boundingRect.X + boundingRect.Width, boundingRect.Y + boundingRect.Height),
                float2(boundingRect.X, boundingRect.Y + boundingRect.Height)
            };
        }

        double radians = textAngle * M_PI / 180.0;
        double cosA = std::cos(radians);
        double sinA = std::sin(radians);
        double cx = imageWidth / 2.0;
        double cy = imageHeight / 2.0;

        double cornersX[4] = { boundingRect.X, boundingRect.X + boundingRect.Width, boundingRect.X + boundingRect.Width, boundingRect.X };
        double cornersY[4] = { boundingRect.Y, boundingRect.Y, boundingRect.Y + boundingRect.Height, boundingRect.Y + boundingRect.Height };
        float rotatedX[4];
        float rotatedY[4];

        for (int i = 0; i < 4; ++i)
        {
            double tx = cornersX[i] - cx;
            double ty = cornersY[i] - cy;
            rotatedX[i] = (float)(tx * cosA - ty * sinA + cx);
            rotatedY[i] = (float)(tx * sinA + ty * cosA + cy);
        }

        return RecognizedTextBoundingBox{
            float2(rotatedX[0], rotatedY[0]),
            float2(rotatedX[1], rotatedY[1]),
            float2(rotatedX[2], rotatedY[2]),
            float2(rotatedX[3], rotatedY[3])
        };
    }

    IAsyncOperation<RecognizedText> TextRecognizerDefault::RecognizeAsync(SoftwareBitmap bitmap)
    {
        co_await winrt::resume_background();

        auto result = co_await m_engine.RecognizeAsync(bitmap);
        auto lines = winrt::single_threaded_vector<RecognizedLine>();

        for (auto const& linex : result.Lines())
        {
            auto words = winrt::single_threaded_vector<RecognizedWord>();
            for (auto const& wordx : linex.Words())
            {
                RecognizedTextBoundingBox bbox = ApplyTextAngleToRect(wordx.BoundingRect(), result.TextAngle(), bitmap.PixelWidth(), bitmap.PixelHeight());
                words.Append(RecognizedWord(wordx.Text(), bbox));
            }

            Rect unionRect = UnionRects(linex.Words());
            RecognizedTextBoundingBox lineBox = ApplyTextAngleToRect(unionRect, result.TextAngle(), bitmap.PixelWidth(), bitmap.PixelHeight());

            lines.Append(RecognizedLine(linex.Text(), lineBox, words));
        }

        auto textAngleRef = result.TextAngle();
        double textAngle = textAngleRef ? textAngleRef.Value() : 0.0;
        co_return RecognizedText(lines, textAngle);
    }
}