#pragma once

#include "DirectTextLayout.g.h"
#include "EditableTextLayout.h"

#include <algorithm>
#include <memory>
#include <mutex>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#include <dwrite_3.h>
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

    // A XAML font family as DirectWrite takes one: the first name in the chain that exists, in
    // its own collection, and the names after it as a fallback ahead of the system's.
    struct ResolvedFontFamily
    {
        std::wstring Family;
        winrt::com_ptr<IDWriteFontCollection> Collection;

        // Null when the chain is one name and there is no monospace face to give it to.
        winrt::com_ptr<IDWriteFontFallback> Fallback;
    };

    // The chains in use on a device, built once each. A handful of distinct strings ever reach
    // it - the theme's families and a few fixed ones - so nothing is evicted, and a resolved
    // family can be held by pointer for as long as this lives.
    class FontChains
    {
    public:
        FontChains(winrt::com_ptr<IDWriteFactory> factory, winrt::com_ptr<IDWriteFontCollection> systemCollection, std::wstring root, std::wstring monospaceFamily);

        // Null when no name in the chain could be found.
        const ResolvedFontFamily* Resolve(std::wstring_view chain) noexcept;

    private:
        HRESULT Build(std::wstring_view chain, ResolvedFontFamily& resolved);
        winrt::com_ptr<IDWriteFontCollection> Packaged(std::wstring_view path);

        std::mutex m_mutex;
        winrt::com_ptr<IDWriteFactory5> m_factory;
        winrt::com_ptr<IDWriteFontCollection> m_systemCollection;
        std::wstring m_root;
        std::wstring m_monospaceFamily;

        std::unordered_map<std::wstring, ResolvedFontFamily> m_chains;
        std::unordered_map<std::wstring, winrt::com_ptr<IDWriteFontCollection>> m_files;
    };

    struct DirectTextLayout : DirectTextLayoutT<DirectTextLayout>
    {
        DirectTextLayout() = default;
        DirectTextLayout(CompositionGraphicsDevice device, winrt::com_ptr<IDWriteFactory> factory, winrt::com_ptr<IDWriteFontCollection> fontCollection, winrt::com_ptr<IDWriteFontCollection> systemCollection, hstring monospaceFamily, std::shared_ptr<FontChains> chains);

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

        bool IsReadOnly();
        void IsReadOnly(bool value);

        bool UseAnalyzer();
        void UseAnalyzer(bool value);

        hstring CompareWithAnalyzer();

        Telegram::Native::TextAlignmentMode Alignment();
        void Alignment(Telegram::Native::TextAlignmentMode value);

        static hstring Counters();
        static void ResetCounters();

        static bool VerifyAnalyzer();
        static void VerifyAnalyzer(bool value);

        bool IsTrimmed();
        int32_t LineCount();

        void SetText(hstring text, IVector<Telegram::Native::TextStylePart> entities);
        void ReplaceText(int32_t offset, int32_t length, hstring text, IVector<Telegram::Native::TextStylePart> entities);
        hstring Text();
        void SetParagraphs(IVector<Telegram::Native::TextParagraph> paragraphs);
        void SetColor(int32_t offset, int32_t length, Windows::UI::Color color);
        void ClearColor(int32_t offset, int32_t length);
        void ClearColors();
        void AddColors(array_view<int32_t const> offsets, array_view<int32_t const> lengths, array_view<Windows::UI::Color const> colors);
        void SetInlineObject(int32_t offset, int32_t length, Windows::Foundation::Size size, double baseline);
        bool SetMathObject(int32_t offset, int32_t length, hstring expression);

        Windows::Foundation::Size Measure(double availableWidth);

        com_array<Windows::Foundation::Rect> Ranges(int32_t offset, int32_t length);
        Windows::Foundation::Rect Caret(int32_t offset);
        com_array<Windows::Foundation::Rect> Lines(int32_t offset, int32_t length);
        com_array<int32_t> LineLengths();
        int32_t HitTest(Windows::Foundation::Point point, bool& inside);
        int32_t HitTestContent(Windows::Foundation::Point point, bool& inside);
        Windows::Foundation::Point ContentEnd();

        CompositionDrawingSurface Render(Windows::UI::Color color, double rasterizationScale);

        void SetViewport(double top, double bottom);
        bool HasVirtualSurface();

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
        std::shared_ptr<FontChains> m_chains;

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
        const ResolvedFontFamily* m_resolvedFamily{ nullptr };
        int32_t m_fontWeight{ 400 };
        bool m_italic{ false };
        double m_width{ 0 };
        bool m_rtl{ false };
        int32_t m_maxLines{ 0 };
        bool m_readOnly{ true };
        bool m_useAnalyzer{ false };
        Telegram::Native::TextAlignmentMode m_alignment{ Telegram::Native::TextAlignmentMode::Leading };

        // The text laid out on the analyzer instead of in m_paragraphs, when it is being edited
        // and UseAnalyzer asks for it. Only the one implicit paragraph, without MaxLines.
        std::unique_ptr<EditableTextLayout> m_editable;

        // The one SetText took out of use, which the next build updates rather than starting
        // over: until then nothing is answered from it, as nothing is from m_paragraphs.
        std::unique_ptr<EditableTextLayout> m_previous;

        // What ReplaceText changed since the last build, all of it as one edit, which that build
        // hands the analyzer instead of having it compare the texts. Known only while every
        // change since was a ReplaceText; SetText is a text that may differ anywhere.
        EditableEdit m_replace{};
        bool m_replaceKnown{ false };
        bool m_replacePending{ false };

        // Where ReplaceText puts the text together, kept for its capacity.
        std::wstring m_replaced;

        // The virtual surface a long edited text is drawn into instead, in tiles of
        // c_tileHeight pixels: which of them are drawn, the run of them kept (the viewport and
        // a margin), and what changed since the last draw - rows in DIPs, the edit, and the
        // colours, scale and colour it was drawn with, to compare against.
        CompositionVirtualDrawingSurface m_virtualSurface{ nullptr };
        bool m_virtual{ false };
        std::vector<bool> m_tiles;
        int32_t m_windowFrom{ 0 };
        int32_t m_windowTo{ 0 };
        double m_viewportTop{ 0 };
        double m_viewportBottom{ 0 };

        bool m_dirtyAll{ true };
        float m_dirtyTop{ 0 };
        float m_dirtyBottom{ 0 };
        int32_t m_edits{ 0 };
        EditableChange m_edit;
        std::vector<ColorRange> m_drawnColors;
        Windows::UI::Color m_drawnColor{};
        double m_drawnScale{ 0 };

        // Whether the last build had to leave anything out for MaxLines.
        bool m_trimmed{ false };

        // The layout is rebuilt from the state above whenever any of it moves. Only the width
        // is cheaper than that: DirectWrite reflows in place for it.
        bool m_invalid{ true };

        void SetObject(int32_t offset, int32_t length, winrt::com_ptr<InlineObject> object);

        Windows::Graphics::SizeInt32 SurfaceSize(Windows::Graphics::SizeInt32 pixels);
        bool KeepSurface(Windows::Graphics::SizeInt32 pixels);

        HRESULT Build();
        HRESULT BuildParagraph(Paragraph& paragraph);

        bool UsesAnalyzer() const;
        HRESULT BuildEditable(EditableTextLayout& layout, const EditableEdit* edit = nullptr);
        void VerifyEditable(const EditableTextLayout& layout);
        HRESULT Stack();
        HRESULT Reflow(double availableWidth);

        const Paragraph* Find(double y) const;

        bool IsLeading() const;

        bool HitTestPoint(Windows::Foundation::Point point, bool& inside, const Paragraph*& paragraph, DWRITE_HIT_TEST_METRICS& metrics, BOOL& isTrailingHit);

        Windows::Foundation::Size Extent();

        // Drawing. Not projected: nothing outside this library has a device context to hand.
        HRESULT Draw(ID2D1DeviceContext* context, ID2D1Brush* brush);
        HRESULT DrawEditable(ID2D1DeviceContext* context, ID2D1Brush* brush, float top, float bottom);

        bool UsesVirtualSurface(Windows::Graphics::SizeInt32 pixels);
        void TakeChange();
        void MarkDirty(float top, float bottom);
        void MarkRangeDirty(int32_t start, int32_t end);
        void DiffColors();
        CompositionDrawingSurface RenderVirtual(Windows::Graphics::SizeInt32 pixels);
        HRESULT DrawVirtual();
        HRESULT DrawRows(int32_t top, int32_t bottom);
        void DrawScript(ID2D1DeviceContext* context, const Paragraph& paragraph, DWRITE_TEXT_RANGE range, bool superscript, ID2D1Brush* brush);
        HRESULT ApplyFallback(IDWriteTextLayout* layout);

        // How big sub- and superscript are drawn, against the text around them.
        static constexpr double ScriptScale = 0.7;
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
