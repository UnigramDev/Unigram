#include "pch.h"
#include "DirectTextLayout.h"
#if __has_include("DirectTextLayout.g.cpp")
#include "DirectTextLayout.g.cpp"
#endif

#include "Helpers\COMHelper.h"
#include "RichMathSurface.h"

#include <cmath>

#include <winrt/Windows.Graphics.DirectX.h>

using namespace winrt::Windows::Graphics::DirectX;

namespace winrt::Telegram::Native::implementation
{
    InlineObject::InlineObject(winrt::com_ptr<RichMathSurface> math, float width, float height, float baseline)
        : m_math(math)
        , m_width(width)
        , m_height(height)
        , m_baseline(baseline)
    {
    }

    InlineObject::~InlineObject() = default;

    // Where DirectWrite says the box landed, in the coordinates the layout was drawn in, which
    // is what the formula is drawn at. A box with nothing of its own draws nothing: whoever
    // owns the element puts it there.
    IFACEMETHODIMP InlineObject::Draw(void*, IDWriteTextRenderer*, FLOAT originX, FLOAT originY, BOOL, BOOL, IUnknown*) noexcept
    {
        if (m_math != nullptr && m_target != nullptr)
        {
            m_math->Draw(m_target, originX, originY, m_color);
        }

        return S_OK;
    }

    DirectTextLayout::DirectTextLayout(CompositionGraphicsDevice device, winrt::com_ptr<IDWriteFactory> factory, winrt::com_ptr<IDWriteFontCollection> fontCollection, winrt::com_ptr<IDWriteFontCollection> systemCollection, hstring monospaceFamily)
        : m_device(device)
        , m_factory(factory)
        , m_fontCollection(fontCollection)
        , m_systemCollection(systemCollection)
        , m_monospaceFamily(monospaceFamily)
    {
    }

    DirectTextLayout::~DirectTextLayout()
    {
        // The owner is expected to Close this on its own thread, and this only covers the one
        // that was dropped without it. The handler holds a weak reference and would no-op, but
        // the registration itself would sit on the device - which lives as long as the view -
        // for every layout that ever drew.
        //
        // Guarded because this can run on the finalizer thread, where a call on a composition
        // object is not allowed: a registration that cannot be removed there is left to the
        // device, and throwing out of a destructor would take the process instead.
        try
        {
            Close();
        }
        catch (...)
        {
        }
    }

    void DirectTextLayout::Close()
    {
        if (m_closed)
        {
            return;
        }

        m_closed = true;

        if (m_device != nullptr)
        {
            if (m_renderingDeviceReplaced)
            {
                m_device.RenderingDeviceReplaced(m_renderingDeviceReplaced);
                m_renderingDeviceReplaced = {};
            }

            m_device = nullptr;
        }

        // The last reference here, but not necessarily the last one: the caller's brush holds
        // the surface too, and the atlas region goes when both are gone.
        m_surface = nullptr;

        m_paragraphs.clear();
        m_entities = nullptr;

        m_colors.clear();
        m_inlines.clear();
    }

    double DirectTextLayout::FontSize()
    {
        return m_fontSize;
    }

    void DirectTextLayout::FontSize(double value)
    {
        if (m_fontSize != value)
        {
            m_fontSize = value;
            m_invalid = true;
        }
    }

    hstring DirectTextLayout::FontFamily()
    {
        return m_fontFamily;
    }

    void DirectTextLayout::FontFamily(hstring value)
    {
        if (m_fontFamily != value)
        {
            m_fontFamily = value;
            m_invalid = true;
        }
    }

    int32_t DirectTextLayout::FontWeight()
    {
        return m_fontWeight;
    }

    void DirectTextLayout::FontWeight(int32_t value)
    {
        if (m_fontWeight != value)
        {
            m_fontWeight = value;
            m_invalid = true;
        }
    }

    bool DirectTextLayout::Italic()
    {
        return m_italic;
    }

    void DirectTextLayout::Italic(bool value)
    {
        if (m_italic != value)
        {
            m_italic = value;
            m_invalid = true;
        }
    }

    bool DirectTextLayout::RightToLeft()
    {
        return m_rtl;
    }

    void DirectTextLayout::RightToLeft(bool value)
    {
        if (m_rtl != value)
        {
            m_rtl = value;
            m_invalid = true;
        }
    }

    Telegram::Native::TextAlignmentMode DirectTextLayout::Alignment()
    {
        return m_alignment;
    }

    void DirectTextLayout::Alignment(Telegram::Native::TextAlignmentMode value)
    {
        if (m_alignment != value)
        {
            m_alignment = value;
            m_invalid = true;
        }
    }

    bool DirectTextLayout::IsTrimmed()
    {
        return m_trimmed;
    }

    int32_t DirectTextLayout::LineCount()
    {
        int32_t count = 0;

        for (const Paragraph& paragraph : m_paragraphs)
        {
            DWRITE_TEXT_METRICS metrics;

            if (paragraph.Layout != nullptr && SUCCEEDED(paragraph.Layout->GetMetrics(&metrics)))
            {
                count += metrics.lineCount;
            }
        }

        return count;
    }

    int32_t DirectTextLayout::MaxLines()
    {
        return m_maxLines;
    }

    void DirectTextLayout::MaxLines(int32_t value)
    {
        if (m_maxLines != value)
        {
            m_maxLines = value;
            m_invalid = true;
        }
    }

    // The colours and the inline objects belong to the text they were set over, so they go
    // with it: a caller sets the text first and describes it afterwards.
    void DirectTextLayout::SetText(hstring text, IVector<Telegram::Native::TextStylePart> entities)
    {
        m_text = text;
        m_entities = entities;

        m_colors.clear();
        m_inlines.clear();

        // One paragraph over the whole of it until a caller says otherwise, reading the way
        // RightToLeft and Alignment say. Built at the next build, so a direction set after this
        // still reaches it.
        m_implicit = true;
        m_paragraphs.clear();

        m_invalid = true;
    }

    // The division of the text. Offsets are into the text, and what lies between one paragraph
    // and the next - the newline that separates them - belongs to neither: it is in no layout,
    // and the line the paragraph before it ends with is where a caller walking by line finds
    // it.
    void DirectTextLayout::SetParagraphs(IVector<Telegram::Native::TextParagraph> paragraphs)
    {
        m_implicit = false;
        m_paragraphs.clear();

        const auto length = (int32_t)m_text.size();

        if (paragraphs != nullptr)
        {
            m_paragraphs.reserve(paragraphs.Size());

            for (const Telegram::Native::TextParagraph& item : paragraphs)
            {
                // Clamped rather than trusted: the text and the division of it are two calls,
                // and a layout built over the end of a string is a crash rather than a defect.
                Paragraph paragraph;
                paragraph.Offset = std::clamp(item.Offset, 0, length);
                paragraph.Length = std::clamp(item.Length, 0, length - paragraph.Offset);
                paragraph.RightToLeft = item.RightToLeft;
                paragraph.Alignment = item.Alignment;

                m_paragraphs.push_back(paragraph);
            }
        }

        if (m_paragraphs.empty())
        {
            m_implicit = true;
        }

        m_invalid = true;
    }

    // Setting the same range again replaces it, as with an inline object: a caller says what
    // a range looks like now, not what it has ever looked like.
    void DirectTextLayout::SetColor(int32_t offset, int32_t length, Windows::UI::Color color)
    {
        for (ColorRange& existing : m_colors)
        {
            if (existing.Offset == offset && existing.Length == length)
            {
                existing.Color = color;
                return;
            }
        }

        m_colors.push_back({ offset, length, color });
    }

    void DirectTextLayout::ClearColor(int32_t offset, int32_t length)
    {
        for (auto it = m_colors.begin(); it != m_colors.end(); ++it)
        {
            if (it->Offset == offset && it->Length == length)
            {
                m_colors.erase(it);
                return;
            }
        }
    }

    // Setting the same range again replaces it: a caller that measures a variable sized object
    // - a button, as wide as its label - says so again whenever that width moves, and the
    // layout has to end up with one box for it, not a pile of them.
    void DirectTextLayout::SetInlineObject(int32_t offset, int32_t length, Windows::Foundation::Size size, double baseline)
    {
        SetObject(offset, length, winrt::make_self<InlineObject>(size.Width, size.Height, baseline > 0 ? (float)baseline : size.Height));
    }

    // A formula, which is the one box the layout both reserves and draws: it is text, so it
    // belongs in the surface with the rest of it rather than in an element of its own that has
    // to be measured, arranged, coloured and kept in step.
    bool DirectTextLayout::SetMathObject(int32_t offset, int32_t length, hstring expression)
    {
        winrt::com_ptr<RichMathSurface> math;

        try
        {
            math = winrt::make_self<RichMathSurface>(expression);
        }
        catch (...)
        {
            // Not an expression this can make sense of. Nothing is reserved and nothing drawn,
            // so what was written stays as the text it already is.
            return false;
        }

        const float width = (float)math->PixelWidth();
        const float height = (float)math->PixelHeight();

        // The baseline of a formula is a fraction of its height, and DirectWrite wants the
        // distance from the top of the box down to it.
        SetObject(offset, length, winrt::make_self<InlineObject>(math, width, height, math->Baseline() * height));
        return true;
    }

    void DirectTextLayout::SetObject(int32_t offset, int32_t length, winrt::com_ptr<InlineObject> object)
    {
        for (InlineRange& existing : m_inlines)
        {
            if (existing.Offset == offset && existing.Length == length)
            {
                existing.Object = object;
                m_invalid = true;
                return;
            }
        }

        m_inlines.push_back({ offset, length, object });
        m_invalid = true;
    }

    // A range of the whole text as a range of one paragraph, or false when none of it is in
    // that paragraph: every offset a caller gives is into the text, and every layout below
    // wants one into itself.
    static bool Intersect(const int32_t start, const int32_t count, int32_t offset, int32_t length, DWRITE_TEXT_RANGE& range)
    {
        const auto from = std::max(offset, start);
        const auto to = std::min(offset + length, start + count);

        if (to <= from)
        {
            return false;
        }

        range = { (UINT32)(from - start), (UINT32)(to - from) };
        return true;
    }

    // One layout per paragraph, stacked. DirectWrite gives a layout a single reading direction
    // and a single alignment, so a text whose paragraphs disagree cannot be one layout - and a
    // message that mixes scripts is ordinary rather than exotic.
    HRESULT DirectTextLayout::Build()
    {
        HRESULT result;

        m_trimmed = false;

        // The division SetText leaves behind: one paragraph over everything, reading the way
        // the properties say. Rebuilt here rather than remembered, so a direction set after the
        // text still reaches it.
        if (m_implicit)
        {
            m_paragraphs.assign(1, Paragraph());
            m_paragraphs[0].Offset = 0;
            m_paragraphs[0].Length = (int32_t)m_text.size();
            m_paragraphs[0].RightToLeft = m_rtl;
            m_paragraphs[0].Alignment = m_alignment;
        }

        if (m_factory == nullptr || m_text.empty())
        {
            m_paragraphs.clear();
            return S_OK;
        }

        for (Paragraph& paragraph : m_paragraphs)
        {
            ReturnIfFailed(result, BuildParagraph(paragraph));
        }

        ReturnIfFailed(result, Stack());

        m_invalid = false;
        return S_OK;
    }

    HRESULT DirectTextLayout::BuildParagraph(Paragraph& paragraph)
    {
        HRESULT result;

        paragraph.Layout = nullptr;
        paragraph.Ellipsis = nullptr;
        paragraph.Height = 0;
        paragraph.Hidden = false;

        // The app's own collection carries the emoji font the default family names, and a
        // caller asking for something else - a page title in a serif - is asking for a system
        // one. Whatever is missing from the family chosen, DirectWrite falls back for.
        const auto family = m_fontFamily.empty();

        winrt::com_ptr<IDWriteTextFormat> format;
        ReturnIfFailed(result, m_factory->CreateTextFormat(
            family ? L"Segoe UI Emoji" : m_fontFamily.c_str(),
            family ? m_fontCollection.get() : m_systemCollection.get(),
            (DWRITE_FONT_WEIGHT)m_fontWeight,
            m_italic ? DWRITE_FONT_STYLE_ITALIC : DWRITE_FONT_STYLE_NORMAL,
            DWRITE_FONT_STRETCH_NORMAL,
            (float)m_fontSize,
            L"",
            format.put()
        ));

        // Left and right are the near and far edge of the box, and which is which depends on
        // the direction: leading leaves that to it, and is what a caller that has said nothing
        // gets.
        const auto alignment = paragraph.Alignment == Telegram::Native::TextAlignmentMode::Center
            ? DWRITE_TEXT_ALIGNMENT_CENTER
            : paragraph.Alignment == Telegram::Native::TextAlignmentMode::Left
            ? (paragraph.RightToLeft ? DWRITE_TEXT_ALIGNMENT_TRAILING : DWRITE_TEXT_ALIGNMENT_LEADING)
            : paragraph.Alignment == Telegram::Native::TextAlignmentMode::Right
            ? (paragraph.RightToLeft ? DWRITE_TEXT_ALIGNMENT_LEADING : DWRITE_TEXT_ALIGNMENT_TRAILING)
            : DWRITE_TEXT_ALIGNMENT_LEADING;

        ReturnIfFailed(result, format->SetTextAlignment(alignment));
        ReturnIfFailed(result, format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
        ReturnIfFailed(result, format->SetReadingDirection(paragraph.RightToLeft ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT));

        // One line is trimmed with an ellipsis rather than wrapped; anything else breaks inside
        // a word that does not fit, which is what TextWrapping.Wrap does.
        ReturnIfFailed(result, format->SetWordWrapping(m_maxLines == 1 ? DWRITE_WORD_WRAPPING_NO_WRAP : DWRITE_WORD_WRAPPING_EMERGENCY_BREAK));

        auto text = m_text.data() + paragraph.Offset;
        auto length = (uint32_t)paragraph.Length;

        // A single line means one line of text, not merely text that does not wrap: the breaks
        // in it have to go, or every one of them is another line however narrow the box is.
        if (m_maxLines == 1)
        {
            m_flattened.assign(text, length);

            for (wchar_t& character : m_flattened)
            {
                if (character == L'\n' || character == L'\r')
                {
                    character = L' ';
                }
            }

            text = m_flattened.c_str();
            length = (uint32_t)m_flattened.size();
        }

        ReturnIfFailed(result, m_factory->CreateTextLayout(text, length, format.get(), (float)std::max(m_width, 0.0), INFINITY, paragraph.Layout.put()));

        if (m_maxLines != 0)
        {
            ReturnIfFailed(result, m_factory->CreateEllipsisTrimmingSign(paragraph.Layout.get(), paragraph.Ellipsis.put()));

            DWRITE_TRIMMING trimming = { DWRITE_TRIMMING_GRANULARITY_CHARACTER, 0, 0 };
            ReturnIfFailed(result, paragraph.Layout->SetTrimming(&trimming, paragraph.Ellipsis.get()));
        }

        // Asked before iterating, and most texts answer zero: walking an IVector builds an
        // iterator on the far side of the ABI, and the empty case used to allocate a vector of
        // its own just to have something to walk.
        if (m_entities != nullptr && m_entities.Size() > 0)
        {
            for (const Telegram::Native::TextStylePart& entity : m_entities)
            {
                DWRITE_TEXT_RANGE range;

                if (!Intersect(paragraph.Offset, paragraph.Length, entity.Offset, entity.Length, range))
                {
                    continue;
                }

                if (entity.Type == TextStyle::Bold)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetFontWeight(DWRITE_FONT_WEIGHT_SEMI_BOLD, range));
                }
                else if (entity.Type == TextStyle::Italic)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetFontStyle(DWRITE_FONT_STYLE_ITALIC, range));
                }
                else if (entity.Type == TextStyle::Strikethrough)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetStrikethrough(TRUE, range));
                }
                else if (entity.Type == TextStyle::Underline)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetUnderline(TRUE, range));
                }
                else if (entity.Type == TextStyle::Monospace)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetFontCollection(m_systemCollection.get(), range));
                    ReturnIfFailed(result, paragraph.Layout->SetFontFamilyName(m_monospaceFamily.c_str(), range));
                }
            }
        }

        for (const InlineRange& inline_ : m_inlines)
        {
            DWRITE_TEXT_RANGE range;

            if (Intersect(paragraph.Offset, paragraph.Length, inline_.Offset, inline_.Length, range))
            {
                ReturnIfFailed(result, paragraph.Layout->SetInlineObject(inline_.Object.get(), range));
            }
        }

        return S_OK;
    }

    // Where each paragraph ends up, and what MaxLines leaves out: the paragraph the limit falls
    // in is cut to the lines that fit, and the ones after it are not drawn at all. Run again
    // whenever the wrapping may have moved, which is the width changing.
    HRESULT DirectTextLayout::Stack()
    {
        HRESULT result;

        float y = 0;
        int32_t count = 0;

        m_trimmed = false;

        for (Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.Layout == nullptr)
            {
                continue;
            }

            // Whatever the last stack cut it to is not what this one may want.
            ReturnIfFailed(result, paragraph.Layout->SetMaxHeight(INFINITY));

            DWRITE_TEXT_METRICS metrics;
            ReturnIfFailed(result, paragraph.Layout->GetMetrics(&metrics));

            paragraph.Y = y;
            paragraph.Height = metrics.height;
            paragraph.Hidden = false;

            if (m_maxLines > 1)
            {
                if (count >= m_maxLines)
                {
                    paragraph.Hidden = true;
                    paragraph.Height = 0;
                    m_trimmed = true;

                    continue;
                }
                else if (count + (int32_t)metrics.lineCount > m_maxLines)
                {
                    UINT32 actual = 0;
                    std::vector<DWRITE_LINE_METRICS> lines(metrics.lineCount);

                    ReturnIfFailed(result, paragraph.Layout->GetLineMetrics(lines.data(), metrics.lineCount, &actual));

                    float height = 0;

                    for (int32_t i = 0; i < m_maxLines - count && i < (int32_t)actual; i++)
                    {
                        height += lines[i].height;
                    }

                    ReturnIfFailed(result, paragraph.Layout->SetMaxHeight(height));

                    paragraph.Height = height;
                    m_trimmed = true;
                }

                count += metrics.lineCount;
            }

            y += paragraph.Height;
        }

        return S_OK;
    }

    // The paragraph a point falls in, or the nearest one: a point above the first or below the
    // last belongs to it, which is what dragging a selection past the text needs.
    const DirectTextLayout::Paragraph* DirectTextLayout::Find(double y) const
    {
        const Paragraph* found = nullptr;

        for (const Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.Layout == nullptr || paragraph.Hidden)
            {
                continue;
            }

            found = &paragraph;

            if (y < paragraph.Y + paragraph.Height)
            {
                break;
            }
        }

        return found;
    }

    // The width is the one thing DirectWrite reflows in place; everything else rebuilds. Where
    // the paragraphs sit is settled again either way, as a different width wraps differently.
    HRESULT DirectTextLayout::Reflow(double availableWidth)
    {
        HRESULT result;

        if (m_invalid || m_paragraphs.empty())
        {
            m_width = availableWidth;
            ReturnIfFailed(result, Build());
        }
        else if (m_width != availableWidth)
        {
            m_width = availableWidth;

            for (Paragraph& paragraph : m_paragraphs)
            {
                if (paragraph.Layout != nullptr)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetMaxWidth((float)std::max(availableWidth, 0.0)));
                }
            }

            ReturnIfFailed(result, Stack());
        }

        return S_OK;
    }

    Windows::Foundation::Size DirectTextLayout::Measure(double availableWidth)
    {
        HRESULT result;
        ReturnDefaultIfFailed(result, Reflow(availableWidth));

        return Bounds();
    }

    // What the surface has to cover: the text's own box, measured from the layout's origin
    // rather than from the text's. Left to right that is the text itself; right to left, or
    // centred, the text sits away from the origin and the space before it is part of what the
    // surface spans - which is what puts the surface in the same coordinates as everything
    // else this layout answers, with no offset to keep in step.
    Windows::Foundation::Size DirectTextLayout::Extent()
    {
        float width = 0;
        float height = 0;

        for (const Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.Layout == nullptr || paragraph.Hidden)
            {
                continue;
            }

            DWRITE_TEXT_METRICS metrics;

            if (FAILED(paragraph.Layout->GetMetrics(&metrics)))
            {
                continue;
            }

            // Clamped at zero: metrics.width leaves out the whitespace a line ends with, while
            // metrics.left is the left-most point of the text INCLUDING it - and right to left
            // that whitespace is at the left end, so a box no wider than the text puts it
            // outside and left comes back negative. Taken at face value it makes the surface
            // too narrow by exactly that space, and the text runs off the end of it. Nothing
            // can be drawn left of the origin anyway, and what is there is whitespace.
            width = std::max(width, std::max(metrics.left, 0.0f) + metrics.width);
            height = paragraph.Y + paragraph.Height;
        }

        if (m_width > 0)
        {
            width = std::min(width, (float)m_width);
        }

        return { width, height };
    }

    Windows::Foundation::Size DirectTextLayout::Bounds()
    {
        float width = 0;
        float height = 0;

        for (const Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.Layout == nullptr || paragraph.Hidden)
            {
                continue;
            }

            DWRITE_TEXT_METRICS metrics;

            if (FAILED(paragraph.Layout->GetMetrics(&metrics)))
            {
                continue;
            }

            // The widest paragraph is what the text takes; a trimmed one measures the whole of
            // itself, not the part of it that is drawn, so the stack says how tall it is.
            width = std::max(width, metrics.width);
            height = paragraph.Y + paragraph.Height;
        }

        if (m_width > 0)
        {
            width = std::min(width, (float)m_width);
        }

        return { width, height };
    }

    com_array<Windows::Foundation::Rect> DirectTextLayout::Ranges(int32_t offset, int32_t length)
    {
        std::vector<Windows::Foundation::Rect> rects;

        for (const Paragraph& paragraph : m_paragraphs)
        {
            DWRITE_TEXT_RANGE range;

            if (paragraph.Layout == nullptr || paragraph.Hidden
                || !Intersect(paragraph.Offset, paragraph.Length, offset, length, range))
            {
                continue;
            }

            DWRITE_TEXT_METRICS metrics;

            if (FAILED(paragraph.Layout->GetMetrics(&metrics)))
            {
                continue;
            }

            UINT32 count = metrics.lineCount * metrics.maxBidiReorderingDepth;
            UINT32 actual;
            std::vector<DWRITE_HIT_TEST_METRICS> ranges(count);

            auto result = paragraph.Layout->HitTestTextRange(range.startPosition, range.length, 0, paragraph.Y, ranges.data(), count, &actual);

            if (result == E_NOT_SUFFICIENT_BUFFER)
            {
                ranges.resize(actual);
                result = paragraph.Layout->HitTestTextRange(range.startPosition, range.length, 0, paragraph.Y, ranges.data(), actual, &actual);
            }

            if (FAILED(result))
            {
                continue;
            }

            rects.reserve(rects.size() + actual);

            for (UINT32 i = 0; i < actual; i++)
            {
                if (ranges[i].isTrimmed)
                {
                    break;
                }

                rects.push_back({ ranges[i].left, ranges[i].top, ranges[i].width, ranges[i].height });
            }
        }

        return com_array<Windows::Foundation::Rect>(rects.begin(), rects.end());
    }

    // One rectangle per line of a range, where Ranges gives one per bidirectional run: a shape
    // drawn down the lines cannot be built from the pieces a mixed direction line is hit tested
    // into.
    com_array<Windows::Foundation::Rect> DirectTextLayout::Lines(int32_t offset, int32_t length)
    {
        auto rects = Ranges(offset, length);
        std::vector<Windows::Foundation::Rect> lines;
        lines.reserve(rects.size());

        for (const Windows::Foundation::Rect& rect : rects)
        {
            if (!lines.empty() && lines.back().Y == rect.Y && lines.back().Height == rect.Height)
            {
                Windows::Foundation::Rect& line = lines.back();

                const auto left = std::min(line.X, rect.X);
                const auto right = std::max(line.X + line.Width, rect.X + rect.Width);

                line.X = left;
                line.Width = right - left;
            }
            else
            {
                lines.push_back(rect);
            }
        }

        return com_array<Windows::Foundation::Rect>(lines.begin(), lines.end());
    }

    com_array<int32_t> DirectTextLayout::LineLengths()
    {
        std::vector<int32_t> lengths;

        for (size_t i = 0; i < m_paragraphs.size(); i++)
        {
            const Paragraph& paragraph = m_paragraphs[i];

            if (paragraph.Layout == nullptr || paragraph.Hidden)
            {
                continue;
            }

            DWRITE_TEXT_METRICS metrics;

            if (FAILED(paragraph.Layout->GetMetrics(&metrics)))
            {
                continue;
            }

            UINT32 actual;
            std::vector<DWRITE_LINE_METRICS> lines(metrics.lineCount);

            auto result = paragraph.Layout->GetLineMetrics(lines.data(), metrics.lineCount, &actual);

            if (result == E_NOT_SUFFICIENT_BUFFER)
            {
                lines.resize(actual);
                result = paragraph.Layout->GetLineMetrics(lines.data(), actual, &actual);
            }

            if (FAILED(result))
            {
                continue;
            }

            lengths.reserve(lengths.size() + actual);

            for (UINT32 j = 0; j < actual; j++)
            {
                lengths.push_back(lines[j].length);
            }

            // What separates this paragraph from the next one - the newline between them - is
            // in the text and in no layout, so it belongs to the line this one ends with. A
            // caller walking the text by line has to arrive at the next paragraph's offset.
            if (!lengths.empty())
            {
                const auto end = paragraph.Offset + paragraph.Length;
                const auto next = i + 1 < m_paragraphs.size()
                    ? m_paragraphs[i + 1].Offset
                    : (int32_t)m_text.size();

                lengths.back() += std::max(next - end, 0);
            }
        }

        return com_array<int32_t>(lengths.begin(), lengths.end());
    }

    // The position nearest the point - DirectWrite answers with one wherever the point is,
    // which is what dragging a selection past the end of a line needs - and separately whether
    // the point was on the text at all, which is what a link needs.
    // Where a caret would go, which is past what the point landed on when it is on the
    // trailing half of it.
    //
    // Past the whole of it, not one character past its start: a hit region is longer than one
    // position whenever it is an inline object - a custom emoji or a formula stands for the
    // run of text it replaced - or a surrogate pair, or a cluster. Advancing by one lands
    // inside it, where there is no position to be, so a selection dragged to the end of a text
    // that ends in one could never reach the end.
    int32_t DirectTextLayout::HitTest(Windows::Foundation::Point point, bool& inside)
    {
        const Paragraph* paragraph;
        DWRITE_HIT_TEST_METRICS metrics;
        BOOL isTrailingHit;

        if (!HitTestPoint(point, inside, paragraph, metrics, isTrailingHit))
        {
            return -1;
        }

        return paragraph->Offset + metrics.textPosition + (isTrailingHit ? metrics.length : 0);
    }

    // What is AT the point rather than where a caret would go: where the hit region starts,
    // whichever half of it the point is on. What a link, a spoiler or a tooltip asks - the
    // trailing half of the last character of a link is still that link, and the trailing half
    // of an emoji is still the emoji rather than whatever follows it.
    int32_t DirectTextLayout::HitTestContent(Windows::Foundation::Point point, bool& inside)
    {
        const Paragraph* paragraph;
        DWRITE_HIT_TEST_METRICS metrics;
        BOOL isTrailingHit;

        if (!HitTestPoint(point, inside, paragraph, metrics, isTrailingHit))
        {
            return -1;
        }

        return paragraph->Offset + metrics.textPosition;
    }

    bool DirectTextLayout::HitTestPoint(Windows::Foundation::Point point, bool& inside, const Paragraph*& paragraph, DWRITE_HIT_TEST_METRICS& metrics, BOOL& isTrailingHit)
    {
        inside = false;
        paragraph = Find(point.Y);

        if (paragraph == nullptr)
        {
            return false;
        }

        BOOL isInside;

        if (FAILED(paragraph->Layout->HitTestPoint(point.X, point.Y - paragraph->Y, &isTrailingHit, &isInside, &metrics)))
        {
            return false;
        }

        inside = isInside;
        return true;
    }

    // The bottom right corner of the laid out text: the end of the last line, and the height it
    // ends at - which is the last paragraph that is drawn, wherever it sits in the stack.
    Windows::Foundation::Point DirectTextLayout::ContentEnd()
    {
        const Paragraph* paragraph = nullptr;

        for (const Paragraph& item : m_paragraphs)
        {
            if (item.Layout != nullptr && !item.Hidden)
            {
                paragraph = &item;
            }
        }

        if (paragraph == nullptr)
        {
            return { 0, 0 };
        }

        DWRITE_TEXT_METRICS metrics;

        if (FAILED(paragraph->Layout->GetMetrics(&metrics)))
        {
            return { 0, 0 };
        }

        BOOL isTrailingHit;
        BOOL isInside;
        DWRITE_HIT_TEST_METRICS hitTestMetrics;

        if (FAILED(paragraph->Layout->HitTestPoint(metrics.width, metrics.height, &isTrailingHit, &isInside, &hitTestMetrics)))
        {
            return { 0, 0 };
        }

        return { hitTestMetrics.left + hitTestMetrics.width, paragraph->Y + hitTestMetrics.top + hitTestMetrics.height };
    }

    CompositionDrawingSurface DirectTextLayout::Render(Windows::UI::Color color, double rasterizationScale)
    {
        if (m_closed || m_device == nullptr || rasterizationScale <= 0)
        {
            return nullptr;
        }

        const auto bounds = Extent();
        const auto pixels = Windows::Graphics::SizeInt32
        {
            (int32_t)std::ceil(bounds.Width * rasterizationScale),
            (int32_t)std::ceil(bounds.Height * rasterizationScale)
        };

        if (pixels.Width <= 0 || pixels.Height <= 0)
        {
            return nullptr;
        }

        m_color = color;
        m_scale = rasterizationScale;

        // The surface is kept rather than replaced: a text change is a redraw, and a list
        // scrolling past recycled items would otherwise allocate and free one atlas region per
        // item. Resizing drops the content, which is redrawn below anyway.
        if (m_surface != nullptr)
        {
            const auto size = m_surface.SizeInt32();

            if (size.Width != pixels.Width || size.Height != pixels.Height)
            {
                try
                {
                    m_surface.Resize(pixels);
                }
                catch (...)
                {
                    // A surface that cannot be resized is the wrong size, so it is dropped for
                    // a new one rather than drawn into.
                    m_surface = nullptr;
                }
            }
        }

        if (m_surface == nullptr)
        {
            try
            {
                m_surface = m_device.CreateDrawingSurface2(pixels, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
            }
            catch (...)
            {
                return nullptr;
            }

            // Once there is something to redraw. A layout that only answers questions - a
            // measure, a hit test - never registers, and this is where drawing starts.
            if (!m_renderingDeviceReplaced)
            {
                m_renderingDeviceReplaced = m_device.RenderingDeviceReplaced({ get_weak(), &DirectTextLayout::OnRenderingDeviceReplaced });
            }
        }

        if (FAILED(DrawSurface()))
        {
            return nullptr;
        }

        return m_surface;
    }

    // The rendering device was replaced under the surface, which survives it: only the content
    // is gone. Redrawn into the same surface rather than a new one, so the brush the caller set
    // on it stays valid and there is nothing to hand back.
    void DirectTextLayout::OnRenderingDeviceReplaced(CompositionGraphicsDevice const&, RenderingDeviceReplacedEventArgs const&)
    {
        if (m_closed || m_surface == nullptr)
        {
            return;
        }

        DrawSurface();
    }

    HRESULT DirectTextLayout::DrawSurface()
    {
        auto surfaceInterop = m_surface.as<abi::ICompositionDrawingSurfaceInterop>();

        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        // BeginDraw can fail with DXGI_ERROR_DEVICE_REMOVED. Nothing to do about it here:
        // Direct2DDevice rebuilds the device on the next access and replacing the rendering
        // device brings this back through the handler above.
        HRESULT result = surfaceInterop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), context.put_void(), &offset);

        if (FAILED(result))
        {
            return result;
        }

        // Only one surface may be open for drawing on a device at a time, so everything from
        // here reaches EndDraw whatever it returns.
        context->Clear(D2D1::ColorF(0, 0, 0, 0));

        // The surface spans the layout box from its origin, so the text is drawn where the
        // layout puts it and nothing has to be moved afterwards: what this draws and what
        // Ranges, Lines and HitTest answer are the same coordinates.
        //
        // The layout is in DIPs and the surface is in pixels, and BeginDraw says where in the
        // atlas it landed. Scaled through the transform rather than by setting the DPI on the
        // context: that context belongs to the composition graphics device and is handed to
        // every other surface drawn from it, all of which draw in pixels and expect 96 - one
        // draw here at 150% and none of them rendered again.
        context->SetTransform(
            D2D1::Matrix3x2F::Scale((float)m_scale, (float)m_scale) *
            D2D1::Matrix3x2F::Translation((float)offset.x, (float)offset.y));

        winrt::com_ptr<ID2D1SolidColorBrush> brush;
        result = context->CreateSolidColorBrush(D2D1::ColorF(m_color.R / 255.0f, m_color.G / 255.0f, m_color.B / 255.0f, m_color.A / 255.0f), brush.put());

        if (SUCCEEDED(result))
        {
            result = Draw(context.get(), brush.get());
        }

        surfaceInterop->EndDraw();
        return result;
    }

    HRESULT DirectTextLayout::Draw(ID2D1DeviceContext* context, ID2D1Brush* brush)
    {
        // A box that draws itself - a formula - is handed the target for the length of this
        // draw and nothing after it: DirectWrite gives an inline object none of the caller's
        // state, and a device context held past a draw is a device held.
        for (const InlineRange& item : m_inlines)
        {
            item.Object->BeginDraw(context, m_color);
        }

        for (const Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.Layout == nullptr || paragraph.Hidden)
            {
                continue;
            }

            // Cleared first: a drawing effect stays on the layout once it is set, so a range
            // that is no longer coloured - a spoiler that has been revealed - would keep the
            // brush it was given and go on drawing in nothing.
            paragraph.Layout->SetDrawingEffect(nullptr, { 0, (UINT32)paragraph.Length });

            // The colours as brushes from the context that is about to draw: a drawing effect
            // is not part of the layout, so none of this reflows anything.
            for (const ColorRange& item : m_colors)
            {
                DWRITE_TEXT_RANGE range;

                if (!Intersect(paragraph.Offset, paragraph.Length, item.Offset, item.Length, range))
                {
                    continue;
                }

                winrt::com_ptr<ID2D1SolidColorBrush> effect;

                if (SUCCEEDED(context->CreateSolidColorBrush(D2D1::ColorF(item.Color.R / 255.0f, item.Color.G / 255.0f, item.Color.B / 255.0f, item.Color.A / 255.0f), effect.put())))
                {
                    paragraph.Layout->SetDrawingEffect(effect.get(), range);
                }
            }

            // Colour fonts on, or every emoji comes out as an outline. Anything the text
            // flows around that draws itself is drawn from in here, at the origin DirectWrite
            // works out for it.
            context->DrawTextLayout({ 0, paragraph.Y }, paragraph.Layout.get(), brush, D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
        }

        for (const InlineRange& item : m_inlines)
        {
            item.Object->EndDraw();
        }

        return S_OK;
    }
}
