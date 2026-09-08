#pragma once

#include "DirectTextLayout.g.h"

#include <algorithm>
#include <vector>

#include <Dwrite_1.h>
#include <D2d1_3.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.Graphics.h>

using namespace winrt::Windows::Foundation::Collections;
using namespace winrt::Windows::UI::Composition;

namespace abi
{
    using namespace ABI::Windows::UI::Composition;
}

namespace winrt::Telegram::Native::implementation
{
    // Only ever held by an InlineObject, and only from the implementation file: kept out of
    // this header so that MicroTeX does not reach everything that includes it.
    struct RichMathSurface;

    // The box an element the text flows around takes: DirectWrite asks for the metrics and,
    // for the usual case, never draws it, because whoever owns the element draws it itself.
    //
    // A formula is the exception. Nothing owns it but the layout, so it is drawn from here,
    // into the target the layout is drawing into and at the origin DirectWrite hands over -
    // which is why there is no element to arrange and nothing to keep in step.
    struct InlineObject : winrt::implements<InlineObject, IDWriteInlineObject>
    {
        InlineObject(float width, float height, float baseline)
            : m_width(width)
            , m_height(height)
            , m_baseline(baseline)
        {
        }

        InlineObject(winrt::com_ptr<RichMathSurface> math, float width, float height, float baseline);

        // Defined where RichMathSurface is complete, which this header is not.
        ~InlineObject();

        // The target to draw into, for the length of one draw: an inline object is handed none
        // of the caller's state, and holding a device context past the draw is holding the
        // device. Nothing to do for a box that only reserves space.
        void BeginDraw(ID2D1RenderTarget* target, Windows::UI::Color color)
        {
            m_target = target;
            m_color = color;
        }

        void EndDraw()
        {
            m_target = nullptr;
        }

        IFACEMETHODIMP Draw(void*, IDWriteTextRenderer*, FLOAT originX, FLOAT originY, BOOL, BOOL, IUnknown*) noexcept override;

        IFACEMETHODIMP GetMetrics(DWRITE_INLINE_OBJECT_METRICS* metrics) noexcept override
        {
            metrics->width = m_width;
            metrics->height = m_height;
            metrics->baseline = m_baseline;
            metrics->supportsSideways = FALSE;
            return S_OK;
        }

        IFACEMETHODIMP GetOverhangMetrics(DWRITE_OVERHANG_METRICS* overhangs) noexcept override
        {
            *overhangs = {};
            return S_OK;
        }

        IFACEMETHODIMP GetBreakConditions(DWRITE_BREAK_CONDITION* before, DWRITE_BREAK_CONDITION* after) noexcept override
        {
            *before = DWRITE_BREAK_CONDITION_NEUTRAL;
            *after = DWRITE_BREAK_CONDITION_NEUTRAL;
            return S_OK;
        }

    private:
        winrt::com_ptr<RichMathSurface> m_math;
        ID2D1RenderTarget* m_target{ nullptr };
        Windows::UI::Color m_color{};

        float m_width;
        float m_height;
        float m_baseline;
    };

    struct DirectTextLayout : DirectTextLayoutT<DirectTextLayout>
    {
        DirectTextLayout() = default;
        DirectTextLayout(CompositionGraphicsDevice device, winrt::com_ptr<IDWriteFactory> factory, winrt::com_ptr<IDWriteFontCollection> fontCollection, winrt::com_ptr<IDWriteFontCollection> systemCollection, hstring monospaceFamily);

        ~DirectTextLayout();

        double FontSize();
        void FontSize(double value);

        hstring FontFamily();
        void FontFamily(hstring value);

        int32_t FontWeight();
        void FontWeight(int32_t value);

        bool Italic();
        void Italic(bool value);

        bool RightToLeft();
        void RightToLeft(bool value);

        int32_t MaxLines();
        void MaxLines(int32_t value);

        Telegram::Native::TextAlignmentMode Alignment();
        void Alignment(Telegram::Native::TextAlignmentMode value);

        static hstring Counters();
        static void ResetCounters();

        bool IsTrimmed();
        int32_t LineCount();

        void SetText(hstring text, IVector<Telegram::Native::TextStylePart> entities);
        void SetParagraphs(IVector<Telegram::Native::TextParagraph> paragraphs);
        void SetColor(int32_t offset, int32_t length, Windows::UI::Color color);
        void ClearColor(int32_t offset, int32_t length);
        void SetInlineObject(int32_t offset, int32_t length, Windows::Foundation::Size size, double baseline);
        bool SetMathObject(int32_t offset, int32_t length, hstring expression);

        Windows::Foundation::Size Measure(double availableWidth);

        com_array<Windows::Foundation::Rect> Ranges(int32_t offset, int32_t length);
        com_array<Windows::Foundation::Rect> Lines(int32_t offset, int32_t length);
        com_array<int32_t> LineLengths();
        int32_t HitTest(Windows::Foundation::Point point, bool& inside);
        int32_t HitTestContent(Windows::Foundation::Point point, bool& inside);
        Windows::Foundation::Point ContentEnd();

        CompositionDrawingSurface Render(Windows::UI::Color color, double rasterizationScale);

        // Deterministic teardown, and the reason this is IClosable: the surface holds a region
        // of the device's atlas, which would otherwise be held until the managed wrapper is
        // collected. MUST be called on the thread the surface was created on.
        void Close();

        Windows::Foundation::Size Bounds();
        Windows::Graphics::SizeInt32 RenderedPixels();

    private:
        struct ColorRange
        {
            int32_t Offset;
            int32_t Length;
            Windows::UI::Color Color;
        };

        struct InlineRange
        {
            int32_t Offset;
            int32_t Length;
            winrt::com_ptr<InlineObject> Object;
        };

        // A paragraph and the layout that draws it, with where it ends up in the stack. Hidden
        // is what MaxLines left out: it keeps its place in the list, as the offsets a caller
        // gives are into the whole text whether it is drawn or not.
        struct Paragraph
        {
            int32_t Offset{ 0 };
            int32_t Length{ 0 };
            bool RightToLeft{ false };
            Telegram::Native::TextAlignmentMode Alignment{ Telegram::Native::TextAlignmentMode::Leading };

            winrt::com_ptr<IDWriteTextLayout> Layout;
            winrt::com_ptr<IDWriteInlineObject> Ellipsis;

            float Y{ 0 };
            float Height{ 0 };
            bool Hidden{ false };
        };

        winrt::com_ptr<IDWriteFactory> m_factory;
        winrt::com_ptr<IDWriteFontCollection> m_fontCollection;
        winrt::com_ptr<IDWriteFontCollection> m_systemCollection;
        hstring m_monospaceFamily;

        std::vector<Paragraph> m_paragraphs;

        // Whether the paragraphs are the one SetText made rather than a division a caller
        // asked for: the implicit one follows RightToLeft and Alignment, which a caller that
        // divides the text says per paragraph instead.
        bool m_implicit{ true };

        // The surface the text is drawn into, and what it was last drawn with: the device can
        // hand the content back at any time, and the redraw has no caller to ask.
        CompositionGraphicsDevice m_device{ nullptr };
        CompositionDrawingSurface m_surface{ nullptr };
        winrt::event_token m_renderingDeviceReplaced{};
        Windows::UI::Color m_color{};
        Windows::Graphics::SizeInt32 m_pixels{};
        double m_scale{ 0 };
        bool m_closed{ false };

        hstring m_text;

        // The text as laid out: the same string unless it has to fit on one line, where the
        // breaks in it become spaces. One character for one character, so every offset a
        // caller gave - a colour, a spoiler, an inline object - still lands where it did.
        std::wstring m_flattened;
        IVector<Telegram::Native::TextStylePart> m_entities{ nullptr };
        std::vector<ColorRange> m_colors;
        std::vector<InlineRange> m_inlines;

        double m_fontSize{ 14 };
        hstring m_fontFamily;
        int32_t m_fontWeight{ 400 };
        bool m_italic{ false };
        double m_width{ 0 };
        bool m_rtl{ false };
        int32_t m_maxLines{ 0 };
        Telegram::Native::TextAlignmentMode m_alignment{ Telegram::Native::TextAlignmentMode::Leading };

        // Whether the last build had to leave anything out for MaxLines.
        bool m_trimmed{ false };

        // The layout is rebuilt from the state above whenever any of it moves. Only the width
        // is cheaper than that: DirectWrite reflows in place for it.
        bool m_invalid{ true };

        void SetObject(int32_t offset, int32_t length, winrt::com_ptr<InlineObject> object);

        HRESULT Build();
        HRESULT BuildParagraph(Paragraph& paragraph);
        HRESULT Stack();
        HRESULT Reflow(double availableWidth);

        const Paragraph* Find(double y) const;

        bool IsLeading() const;

        bool HitTestPoint(Windows::Foundation::Point point, bool& inside, const Paragraph*& paragraph, DWRITE_HIT_TEST_METRICS& metrics, BOOL& isTrailingHit);

        Windows::Foundation::Size Extent();

        // Drawing. Not projected: nothing outside this library has a device context to hand.
        HRESULT Draw(ID2D1DeviceContext* context, ID2D1Brush* brush);
        HRESULT DrawSurface();

        void OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&);
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct DirectTextLayout : DirectTextLayoutT<DirectTextLayout, implementation::DirectTextLayout>
    {
    };
}
