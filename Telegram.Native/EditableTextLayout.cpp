#include "pch.h"
#include "EditableTextLayout.h"

#include "Helpers\COMHelper.h"

#include <algorithm>
#include <cmath>
#include <format>

namespace winrt::Telegram::Native::implementation
{
    namespace
    {
        // TextStyle's bits, as far as they change how text is shaped or drawn.
        constexpr uint32_t c_bold = 0x1;
        constexpr uint32_t c_italic = 0x2;
        constexpr uint32_t c_monospace = 0x4;
        constexpr uint32_t c_strikethrough = 0x8;
        constexpr uint32_t c_underline = 0x10;
        constexpr uint32_t c_subscript = 0x400;
        constexpr uint32_t c_superscript = 0x800;
        constexpr uint32_t c_styles = c_bold | c_italic | c_monospace | c_strikethrough | c_underline | c_subscript | c_superscript;

        // What an inline object is to the analyzer, as it is to IDWriteTextLayout.
        constexpr wchar_t c_objectReplacement = 0xFFFC;

        // How long a chunk is shaped at once, at most, before it ends at the next word: short
        // enough that an edit reshapes little, long enough that a paragraph is few of them.
        constexpr int32_t c_chunkLength = 256;

        // A line is as wide as it may be up to this much past the box, which is rounding.
        constexpr float c_epsilon = 0.0001f;

        bool SameScript(const DWRITE_SCRIPT_ANALYSIS& x, const DWRITE_SCRIPT_ANALYSIS& y)
        {
            return x.script == y.script && x.shapes == y.shapes;
        }

        // The paragraph as the analyzer reads it, and where it writes what it found: one value
        // per text position of the window being analysed, filled in whatever ranges the
        // analyzer reports them in. The source is always the whole paragraph, which is the
        // context the analyzer reads around a window.
        class Analysis : public winrt::implements<Analysis, IDWriteTextAnalysisSource, IDWriteTextAnalysisSink>
        {
        public:
            Analysis(const wchar_t* text, UINT32 length, const wchar_t* locale, bool rightToLeft, UINT32 windowStart, UINT32 windowLength)
                : Scripts(windowLength, DWRITE_SCRIPT_ANALYSIS{ 0, DWRITE_SCRIPT_SHAPES_DEFAULT })
                , Levels(windowLength, (uint8_t)(rightToLeft ? 1 : 0))
                , Breakpoints(windowLength, DWRITE_LINE_BREAKPOINT{})
                , m_text(text)
                , m_length(length)
                , m_locale(locale)
                , m_direction(rightToLeft ? DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION_LEFT_TO_RIGHT)
                , m_windowStart(windowStart)
                , m_windowEnd(windowStart + windowLength)
            {
            }

            // Indexed from the start of the window.
            std::vector<DWRITE_SCRIPT_ANALYSIS> Scripts;
            std::vector<uint8_t> Levels;
            std::vector<DWRITE_LINE_BREAKPOINT> Breakpoints;

            IFACEMETHODIMP GetTextAtPosition(UINT32 textPosition, WCHAR const** textString, UINT32* textLength) noexcept override
            {
                if (textPosition >= m_length)
                {
                    *textString = nullptr;
                    *textLength = 0;
                }
                else
                {
                    *textString = m_text + textPosition;
                    *textLength = m_length - textPosition;
                }

                return S_OK;
            }

            IFACEMETHODIMP GetTextBeforePosition(UINT32 textPosition, WCHAR const** textString, UINT32* textLength) noexcept override
            {
                if (textPosition == 0 || textPosition > m_length)
                {
                    *textString = nullptr;
                    *textLength = 0;
                }
                else
                {
                    *textString = m_text;
                    *textLength = textPosition;
                }

                return S_OK;
            }

            IFACEMETHODIMP_(DWRITE_READING_DIRECTION) GetParagraphReadingDirection() noexcept override
            {
                return m_direction;
            }

            IFACEMETHODIMP GetLocaleName(UINT32 textPosition, UINT32* textLength, WCHAR const** localeName) noexcept override
            {
                *localeName = m_locale;
                *textLength = textPosition < m_length ? m_length - textPosition : 0;
                return S_OK;
            }

            IFACEMETHODIMP GetNumberSubstitution(UINT32 textPosition, UINT32* textLength, IDWriteNumberSubstitution** numberSubstitution) noexcept override
            {
                *numberSubstitution = nullptr;
                *textLength = textPosition < m_length ? m_length - textPosition : 0;
                return S_OK;
            }

            IFACEMETHODIMP SetScriptAnalysis(UINT32 textPosition, UINT32 textLength, DWRITE_SCRIPT_ANALYSIS const* scriptAnalysis) noexcept override
            {
                const auto from = std::max(textPosition, m_windowStart);
                const auto to = std::min(textPosition + textLength, m_windowEnd);

                for (auto i = from; i < to; i++)
                {
                    Scripts[i - m_windowStart] = *scriptAnalysis;
                }

                return S_OK;
            }

            IFACEMETHODIMP SetLineBreakpoints(UINT32 textPosition, UINT32 textLength, DWRITE_LINE_BREAKPOINT const* lineBreakpoints) noexcept override
            {
                const auto from = std::max(textPosition, m_windowStart);
                const auto to = std::min(textPosition + textLength, m_windowEnd);

                for (auto i = from; i < to; i++)
                {
                    Breakpoints[i - m_windowStart] = lineBreakpoints[i - textPosition];
                }

                return S_OK;
            }

            IFACEMETHODIMP SetBidiLevel(UINT32 textPosition, UINT32 textLength, UINT8, UINT8 resolvedLevel) noexcept override
            {
                const auto from = std::max(textPosition, m_windowStart);
                const auto to = std::min(textPosition + textLength, m_windowEnd);

                for (auto i = from; i < to; i++)
                {
                    Levels[i - m_windowStart] = resolvedLevel;
                }

                return S_OK;
            }

            IFACEMETHODIMP SetNumberSubstitution(UINT32, UINT32, IDWriteNumberSubstitution*) noexcept override
            {
                return S_OK;
            }

        private:
            const wchar_t* m_text;
            UINT32 m_length;
            const wchar_t* m_locale;
            DWRITE_READING_DIRECTION m_direction;
            UINT32 m_windowStart;
            UINT32 m_windowEnd;
        };

        // Characters that make a paragraph's levels anything but its own: the right to left
        // scripts, their presentation forms and the directional controls. An edit that brings
        // none of them into a paragraph that has none leaves every level where it was.
        bool IsBidiCharacter(wchar_t c)
        {
            return (c >= 0x0590 && c <= 0x08FF)
                || (c >= 0xFB1D && c <= 0xFDFF)
                || (c >= 0xFE70 && c <= 0xFEFF)
                || c == 0x200E || c == 0x200F || c == 0x061C
                || (c >= 0x202A && c <= 0x202E)
                || (c >= 0x2066 && c <= 0x2069)
                // The high surrogates of the right to left blocks above the BMP.
                || c == 0xD802 || c == 0xD803 || c == 0xD83A || c == 0xD83B;
        }

        // A range of a per-position array replaced by one of another length, the new part
        // value-initialized for the caller to fill.
        template <typename T>
        void Splice(std::vector<T>& values, int32_t at, int32_t removed, int32_t inserted)
        {
            if (inserted > removed)
            {
                values.insert(values.begin() + at + removed, (size_t)(inserted - removed), T{});
            }
            else if (removed > inserted)
            {
                values.erase(values.begin() + at + inserted, values.begin() + at + removed);
            }

            std::fill(values.begin() + at, values.begin() + at + inserted, T{});
        }

        // Everything an update has to build from scratch for when it changes.
        bool SameFormat(const EditableTextFormat& x, const EditableTextFormat& y)
        {
            return x.Factory == y.Factory
                && x.Collection == y.Collection
                && x.Family == y.Family
                && x.Fallback == y.Fallback
                && x.MonospaceCollection == y.MonospaceCollection
                && x.MonospaceFamily == y.MonospaceFamily
                && x.FontSize == y.FontSize
                && x.Weight == y.Weight
                && x.Style == y.Style
                && x.Locale == y.Locale
                && x.RightToLeft == y.RightToLeft
                && x.Alignment == y.Alignment
                && x.ScriptScale == y.ScriptScale;
        }

        bool IsBreakCharacter(wchar_t c)
        {
            return c == L'\n' || c == L'\r' || c == 0x0085 || c == 0x2028 || c == 0x2029;
        }

        bool IsBitmapFormat(DWRITE_GLYPH_IMAGE_FORMATS format)
        {
            return format == DWRITE_GLYPH_IMAGE_FORMATS_PNG
                || format == DWRITE_GLYPH_IMAGE_FORMATS_JPEG
                || format == DWRITE_GLYPH_IMAGE_FORMATS_TIFF
                || format == DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8;
        }

        // A run of glyphs as DrawTextLayout with colour fonts on draws one: split into the
        // layers the font has for it - bitmaps for the packaged emoji font, which is CBDT,
        // coloured outlines for the system's - and drawn as plain glyphs when it has none.
        void DrawGlyphs(ID2D1DeviceContext* context, ID2D1DeviceContext4* context4, IDWriteFactory4* factory, const DWRITE_MATRIX& transform, D2D1_POINT_2F origin, const DWRITE_GLYPH_RUN& run, ID2D1Brush* brush)
        {
            if (factory != nullptr && context4 != nullptr)
            {
                const auto formats = DWRITE_GLYPH_IMAGE_FORMATS_TRUETYPE
                    | DWRITE_GLYPH_IMAGE_FORMATS_CFF
                    | DWRITE_GLYPH_IMAGE_FORMATS_COLR
                    | DWRITE_GLYPH_IMAGE_FORMATS_SVG
                    | DWRITE_GLYPH_IMAGE_FORMATS_PNG
                    | DWRITE_GLYPH_IMAGE_FORMATS_JPEG
                    | DWRITE_GLYPH_IMAGE_FORMATS_TIFF
                    | DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8;

                winrt::com_ptr<IDWriteColorGlyphRunEnumerator1> layers;

                // DWRITE_E_NOCOLOR is the usual answer, for text with no colour glyph in it.
                if (SUCCEEDED(factory->TranslateColorGlyphRun(origin, &run, nullptr, formats, DWRITE_MEASURING_MODE_NATURAL, &transform, 0, layers.put())))
                {
                    BOOL hasRun = FALSE;

                    while (SUCCEEDED(layers->MoveNext(&hasRun)) && hasRun)
                    {
                        DWRITE_COLOR_GLYPH_RUN1 const* layer;

                        if (FAILED(layers->GetCurrentRun(&layer)))
                        {
                            break;
                        }

                        const D2D1_POINT_2F at{ layer->baselineOriginX, layer->baselineOriginY };

                        if (IsBitmapFormat(layer->glyphImageFormat))
                        {
                            context4->DrawColorBitmapGlyphRun(layer->glyphImageFormat, at, &layer->glyphRun, DWRITE_MEASURING_MODE_NATURAL, D2D1_COLOR_BITMAP_GLYPH_SNAP_OPTION_DEFAULT);
                        }
                        else if (layer->glyphImageFormat == DWRITE_GLYPH_IMAGE_FORMATS_SVG)
                        {
                            context4->DrawSvgGlyphRun(at, &layer->glyphRun, brush, nullptr, 0, DWRITE_MEASURING_MODE_NATURAL);
                        }
                        else if (layer->paletteIndex == 0xFFFF)
                        {
                            // The layer in the text's own colour.
                            context->DrawGlyphRun(at, &layer->glyphRun, layer->glyphRunDescription, brush, DWRITE_MEASURING_MODE_NATURAL);
                        }
                        else
                        {
                            winrt::com_ptr<ID2D1SolidColorBrush> color;

                            if (SUCCEEDED(context->CreateSolidColorBrush(layer->runColor, color.put())))
                            {
                                context->DrawGlyphRun(at, &layer->glyphRun, layer->glyphRunDescription, color.get(), DWRITE_MEASURING_MODE_NATURAL);
                            }
                        }
                    }

                    return;
                }
            }

            context->DrawGlyphRun(origin, &run, nullptr, brush, DWRITE_MEASURING_MODE_NATURAL);
        }
    }

    // =========================================================================================
    // Building
    // =========================================================================================

    HRESULT EditableTextLayout::Build(const EditableTextFormat& format, std::wstring_view text, const std::vector<EditableStyle>& styles, const std::vector<EditableInline>& inlines, float width)
    {
        HRESULT result;

        m_built = false;
        m_bidi = false;
        m_change = EditableChange{};
        m_format = format;
        m_text.assign(text);

        Prepare(text, styles, inlines);

        const auto length = (int32_t)m_text.size();

        m_faces.clear();
        m_baseFace = UINT32_MAX;
        m_runs.clear();
        m_glyphIndices.clear();
        m_glyphAdvances.clear();
        m_glyphOffsets.clear();

        m_breakpoints.assign(length, DWRITE_LINE_BREAKPOINT{});
        m_clusterMap.assign(length, 0);
        m_runOf.assign(length, 0);
        m_clusterLength.assign(length, 0);
        m_clusterWidth.assign(length, 0);

        // The tab stops a text format made like IDWriteTextLayout's would have.
        m_tabStop = m_format.FontSize * 4;

        winrt::com_ptr<IDWriteTextFormat> textFormat;
        if (SUCCEEDED(m_format.Factory->CreateTextFormat(m_format.Family.c_str(), m_format.Collection.get(), m_format.Weight, m_format.Style, DWRITE_FONT_STRETCH_NORMAL, m_format.FontSize, m_format.Locale.c_str(), textFormat.put())))
        {
            m_tabStop = textFormat->GetIncrementalTabStop();
        }

        // The fallback the layout would be given, or the system's, which is what one without
        // any falls back through.
        m_fallback = m_format.Fallback;

        if (m_fallback == nullptr)
        {
            if (auto factory2 = m_format.Factory.try_as<IDWriteFactory2>())
            {
                factory2->GetSystemFontFallback(m_fallback.put());
            }
        }

        ReturnIfFailed(result, BaseFace(m_baseFace));

        if (length > 0)
        {
            winrt::com_ptr<IDWriteTextAnalyzer> analyzer;
            ReturnIfFailed(result, m_format.Factory->CreateTextAnalyzer(analyzer.put()));

            auto analysis = winrt::make_self<Analysis>(m_analysis.c_str(), (UINT32)length, m_format.Locale.c_str(), m_format.RightToLeft, 0, (UINT32)length);

            ReturnIfFailed(result, analyzer->AnalyzeScript(analysis.get(), 0, (UINT32)length, analysis.get()));
            ReturnIfFailed(result, analyzer->AnalyzeBidi(analysis.get(), 0, (UINT32)length, analysis.get()));
            ReturnIfFailed(result, analyzer->AnalyzeLineBreakpoints(analysis.get(), 0, (UINT32)length, analysis.get()));

            std::copy(analysis->Breakpoints.begin(), analysis->Breakpoints.end(), m_breakpoints.begin());

            const uint8_t paragraphLevel = m_format.RightToLeft ? 1 : 0;

            for (const uint8_t level : analysis->Levels)
            {
                if (level != paragraphLevel)
                {
                    m_bidi = true;
                    break;
                }
            }

            ReturnIfFailed(result, ShapeRange(analyzer.get(), analysis.get(), 0, length, analysis->Scripts.data(), analysis->Levels.data()));
        }

        Break(width);

        m_built = true;
        return S_OK;
    }

    // The paragraph after an edit, worked out from what is already shaped: the runs around the
    // change analysed and shaped again, the ones after it moved, and the lines broken again
    // from the one before the change until they meet the old ones. Anything this does not
    // handle - another format, a paragraph or an edit with right to left text in it, an empty
    // text, a failure half way - is a cold build, which leaves nothing of the old state.
    HRESULT EditableTextLayout::Update(const EditableTextFormat& format, std::wstring_view text, const std::vector<EditableStyle>& styles, const std::vector<EditableInline>& inlines, float width)
    {
        const auto oldLength = (int32_t)m_text.size();
        const auto newLength = (int32_t)text.size();

        // A right to left paragraph is never one level: a digit or a Latin letter typed into it
        // is at the next one up.
        if (!m_built || m_bidi || m_format.RightToLeft || m_runs.empty() || oldLength == 0 || newLength == 0 || !SameFormat(m_format, format))
        {
            return Build(format, text, styles, inlines, width);
        }

        const auto oldStyleOf = std::move(m_styleOf);
        const auto oldInlineOf = std::move(m_inlineOf);
        const auto oldMetrics = std::move(m_inlineMetrics);

        Prepare(text, styles, inlines);

        // Whether a position shapes the same before and after: the same character in the same
        // style, and the same box where there is an object. Which object draws the box is not
        // asked: a caller hands over new ones with every text, and drawing reads the new ones.
        const auto same = [&](int32_t o, int32_t n)
            {
                if (m_text[o] != text[n] || oldStyleOf[o] != m_styleOf[n])
                {
                    return false;
                }

                const auto x = oldInlineOf[o];
                const auto y = m_inlineOf[n];

                if (x < 0 || y < 0)
                {
                    return x < 0 && y < 0;
                }

                const auto startsX = o == 0 || oldInlineOf[o - 1] != x;
                const auto startsY = n == 0 || m_inlineOf[n - 1] != y;

                return startsX == startsY
                    && oldMetrics[x].width == m_inlineMetrics[y].width
                    && oldMetrics[x].height == m_inlineMetrics[y].height
                    && oldMetrics[x].baseline == m_inlineMetrics[y].baseline;
            };

        const auto shorter = std::min(oldLength, newLength);

        int32_t prefix = 0;

        while (prefix < shorter && same(prefix, prefix))
        {
            prefix++;
        }

        int32_t suffix = 0;

        while (suffix < shorter - prefix && same(oldLength - 1 - suffix, newLength - 1 - suffix))
        {
            suffix++;
        }

        if (prefix == oldLength && prefix == newLength)
        {
            RemapInlines();

            if (width != m_width)
            {
                m_change = EditableChange{};
                Break(width);
            }
            else
            {
                m_change = { false, 0, 0, newLength, newLength, newLength };
            }

            return S_OK;
        }

        const auto oldEnd = oldLength - suffix;
        const auto newEnd = newLength - suffix;
        const auto delta = newLength - oldLength;

        for (auto i = prefix; i < newEnd; i++)
        {
            if (IsBidiCharacter(text[i]))
            {
                return Build(format, text, styles, inlines, width);
            }
        }

        // The window shaped again: from the run the character before the edit is in to the
        // end of the run the first character after it is in. Its ends were chunk ends, which
        // are word starts, or the ends of the paragraph.
        const auto firstRun = m_runOf[prefix > 0 ? prefix - 1 : 0];
        const auto lastRun = oldEnd < oldLength ? m_runOf[oldEnd] : (uint32_t)m_runs.size() - 1;

        const auto windowStart = m_runs[firstRun].Start;
        const auto oldWindowEnd = m_runs[lastRun].Start + m_runs[lastRun].Length;
        const auto newWindowEnd = oldWindowEnd + delta;

        const auto glyphStart = m_runs[firstRun].GlyphStart;
        const auto glyphEnd = m_runs[lastRun].GlyphStart + m_runs[lastRun].GlyphCount;

        std::vector<Line> oldLines = std::move(m_lines);
        std::vector<Segment> oldSegments = std::move(m_segments);

        m_lines.clear();
        m_segments.clear();

        m_text.assign(text);

        const auto removed = oldWindowEnd - windowStart;
        const auto inserted = newWindowEnd - windowStart;

        Splice(m_breakpoints, windowStart, removed, inserted);
        Splice(m_clusterMap, windowStart, removed, inserted);
        Splice(m_runOf, windowStart, removed, inserted);
        Splice(m_clusterLength, windowStart, removed, inserted);
        Splice(m_clusterWidth, windowStart, removed, inserted);

        // What comes after the window, put back once the window is shaped onto the end.
        const std::vector<Run> tailRuns(m_runs.begin() + lastRun + 1, m_runs.end());
        const std::vector<uint16_t> tailIndices(m_glyphIndices.begin() + glyphEnd, m_glyphIndices.end());
        const std::vector<float> tailAdvances(m_glyphAdvances.begin() + glyphEnd, m_glyphAdvances.end());
        const std::vector<DWRITE_GLYPH_OFFSET> tailOffsets(m_glyphOffsets.begin() + glyphEnd, m_glyphOffsets.end());

        m_runs.resize(firstRun);
        m_glyphIndices.resize(glyphStart);
        m_glyphAdvances.resize(glyphStart);
        m_glyphOffsets.resize(glyphStart);

        winrt::com_ptr<IDWriteTextAnalyzer> analyzer;

        if (FAILED(m_format.Factory->CreateTextAnalyzer(analyzer.put())))
        {
            return Build(format, text, styles, inlines, width);
        }

        // Analysed with a line of text on either side, of which only the window is kept: the
        // analyzer takes a range for the whole text, so the first position of one is never a
        // break and its script is guessed without what comes before it.
        const auto lineAt = [&](int32_t offset)
            {
                return (size_t)(std::upper_bound(oldLines.begin(), oldLines.end(), offset, [](int32_t value, const Line& line)
                    {
                        return value < line.Start;
                    }) - oldLines.begin()) - 1;
            };

        auto before = lineAt(windowStart);
        before = before > 0 ? before - 1 : 0;

        const auto contextStart = std::min(oldLines[before].Start, windowStart);
        auto contextEnd = newLength;

        if (oldWindowEnd < oldLength)
        {
            auto after = lineAt(oldWindowEnd);
            auto end = oldLines[after].Start + oldLines[after].Length;

            if (end <= oldWindowEnd && after + 1 < oldLines.size())
            {
                end = oldLines[after + 1].Start + oldLines[after + 1].Length;
            }

            contextEnd = std::clamp(end + delta, newWindowEnd, newLength);
        }

        // The levels are the paragraph's own throughout, which is what Analysis starts them at:
        // nothing in the paragraph or in the edit says otherwise.
        auto analysis = winrt::make_self<Analysis>(m_analysis.c_str(), (UINT32)newLength, m_format.Locale.c_str(), m_format.RightToLeft, (UINT32)windowStart, (UINT32)inserted);

        if (FAILED(analyzer->AnalyzeScript(analysis.get(), (UINT32)contextStart, (UINT32)(contextEnd - contextStart), analysis.get()))
            || FAILED(analyzer->AnalyzeLineBreakpoints(analysis.get(), (UINT32)contextStart, (UINT32)(contextEnd - contextStart), analysis.get())))
        {
            return Build(format, text, styles, inlines, width);
        }

        std::copy(analysis->Breakpoints.begin(), analysis->Breakpoints.end(), m_breakpoints.begin() + windowStart);

        if (FAILED(ShapeRange(analyzer.get(), analysis.get(), windowStart, newWindowEnd, analysis->Scripts.data(), analysis->Levels.data())))
        {
            return Build(format, text, styles, inlines, width);
        }

        const auto runDelta = (int32_t)m_runs.size() - (int32_t)(lastRun + 1);
        const auto glyphDelta = (int64_t)m_glyphIndices.size() - (int64_t)glyphEnd;

        for (Run run : tailRuns)
        {
            run.Start += delta;
            run.GlyphStart = (uint32_t)(run.GlyphStart + glyphDelta);

            m_runs.push_back(run);
        }

        m_glyphIndices.insert(m_glyphIndices.end(), tailIndices.begin(), tailIndices.end());
        m_glyphAdvances.insert(m_glyphAdvances.end(), tailAdvances.begin(), tailAdvances.end());
        m_glyphOffsets.insert(m_glyphOffsets.end(), tailOffsets.begin(), tailOffsets.end());

        for (auto i = newWindowEnd; i < newLength; i++)
        {
            m_runOf[i] = (uint32_t)(m_runOf[i] + runDelta);
        }

        RemapInlines();

        if (width != m_width)
        {
            m_change = EditableChange{};
            Break(width);
        }
        else
        {
            // Rebreak says which rows.
            m_change = { false, 0, 0, prefix, oldEnd, newEnd };
            Rebreak(oldLines, oldSegments, windowStart, newWindowEnd, delta, runDelta);
        }

        return S_OK;
    }

    HRESULT EditableTextLayout::Reflow(float width)
    {
        m_change = EditableChange{};
        Break(width);
        return S_OK;
    }

    void EditableTextLayout::RangeExtent(int32_t start, int32_t end, float& top, float& bottom) const
    {
        if (m_lines.empty())
        {
            top = 0;
            bottom = 0;
            return;
        }

        const Line& first = LineOf(std::max(start, 0));
        const Line& last = LineOf(std::max(end - 1, start));

        top = first.Y;
        bottom = last.Y + last.Height;
    }

    // What the caller hands over, per position: the inline object, if any, and the style bits,
    // and the text the analyzer reads, with each object's range as object replacements.
    void EditableTextLayout::Prepare(std::wstring_view text, const std::vector<EditableStyle>& styles, const std::vector<EditableInline>& inlines)
    {
        const auto length = (int32_t)text.size();

        m_inlines.clear();
        m_inlineMetrics.clear();
        m_inlineOf.assign(length, -1);
        m_styleOf.assign(length, 0);
        m_analysis.assign(text);

        // Clamped to the text: the text and the objects over it are two calls, and a range past
        // the end of it is a defect rather than a crash.
        for (const EditableInline& item : inlines)
        {
            const auto from = std::clamp(item.Offset, 0, length);
            const auto to = std::clamp(item.Offset + item.Length, from, length);

            if (to <= from || item.Object == nullptr)
            {
                continue;
            }

            DWRITE_INLINE_OBJECT_METRICS metrics{};
            if (FAILED(item.Object->GetMetrics(&metrics)))
            {
                continue;
            }

            const auto index = (int32_t)m_inlines.size();
            m_inlines.push_back({ from, to - from, item.Object });
            m_inlineMetrics.push_back(metrics);

            for (int32_t i = from; i < to; i++)
            {
                m_inlineOf[i] = index;
                m_analysis[i] = c_objectReplacement;
            }
        }

        for (const EditableStyle& style : styles)
        {
            const auto from = std::clamp(style.Offset, 0, length);
            const auto to = std::clamp(style.Offset + style.Length, from, length);

            for (int32_t i = from; i < to; i++)
            {
                m_styleOf[i] |= style.Type & c_styles;
            }
        }
    }

    // Every object run pointed at the object over it now: the objects are replaced with every
    // update, and their order is all an unchanged run can be matched by.
    void EditableTextLayout::RemapInlines()
    {
        for (Run& run : m_runs)
        {
            if (run.Inline >= 0)
            {
                run.Inline = m_inlineOf[run.Start];
            }
        }
    }

    // Runs for [from, to), appended to the ones before it: split into items wherever an object,
    // the level, the style or the script changes, then by font as the fallback maps it, then
    // into chunks. The scripts and levels are indexed from from.
    HRESULT EditableTextLayout::ShapeRange(IDWriteTextAnalyzer* analyzer, IDWriteTextAnalysisSource* source, int32_t from, int32_t to, const DWRITE_SCRIPT_ANALYSIS* scripts, const uint8_t* levels)
    {
        HRESULT result;

        int32_t position = from;

        while (position < to)
        {
            if (m_inlineOf[position] >= 0)
            {
                const auto index = m_inlineOf[position];
                const auto& item = m_inlines[index];
                const auto& metrics = m_inlineMetrics[index];

                Run run{};
                run.Start = position;
                run.Length = std::min(item.Offset + item.Length, to) - position;
                run.GlyphStart = (uint32_t)m_glyphIndices.size();
                run.GlyphCount = 0;
                run.Face = m_baseFace;
                run.EmSize = m_format.FontSize;
                run.Level = levels[position - from];
                run.Style = m_styleOf[position];
                run.Inline = index;
                run.Ascent = metrics.baseline;
                run.Descent = metrics.height - metrics.baseline;
                run.LineGap = 0;

                const auto runIndex = (uint32_t)m_runs.size();
                m_runs.push_back(run);

                for (int32_t i = run.Start; i < run.Start + run.Length; i++)
                {
                    m_runOf[i] = runIndex;
                }

                m_clusterLength[run.Start] = run.Length;
                m_clusterWidth[run.Start] = metrics.width;

                position = run.Start + run.Length;
                continue;
            }

            // An item: as far as nothing that decides how it is shaped changes.
            const auto& script = scripts[position - from];
            const auto level = levels[position - from];
            const auto style = m_styleOf[position];

            auto end = position + 1;

            while (end < to && m_inlineOf[end] < 0 && levels[end - from] == level && m_styleOf[end] == style && SameScript(scripts[end - from], script))
            {
                end++;
            }

            const auto monospace = (style & c_monospace) != 0 && !m_format.MonospaceFamily.empty();

            const auto& collection = monospace && m_format.MonospaceCollection != nullptr ? m_format.MonospaceCollection : m_format.Collection;
            const auto& family = monospace ? m_format.MonospaceFamily : m_format.Family;
            const auto weight = (style & c_bold) != 0 ? DWRITE_FONT_WEIGHT_SEMI_BOLD : m_format.Weight;
            const auto fontStyle = (style & c_italic) != 0 ? DWRITE_FONT_STYLE_ITALIC : m_format.Style;
            const auto scriptScale = (style & (c_subscript | c_superscript)) != 0 ? m_format.ScriptScale : 1.0f;

            // By font, as the fallback maps it: the same object chooses as the layout's does.
            auto mapped = position;

            while (mapped < end)
            {
                UINT32 mappedLength = 0;
                FLOAT scale = 1;
                winrt::com_ptr<IDWriteFont> font;

                if (m_fallback == nullptr
                    || FAILED(m_fallback->MapCharacters(source, (UINT32)mapped, (UINT32)(end - mapped), collection.get(), family.c_str(), weight, fontStyle, DWRITE_FONT_STRETCH_NORMAL, &mappedLength, font.put(), &scale))
                    || mappedLength == 0)
                {
                    mappedLength = (UINT32)(end - mapped);
                    font = nullptr;
                    scale = 1;
                }

                // No font has it: drawn in the text's own, which is what draws its .notdef.
                uint32_t face = m_baseFace;

                if (font != nullptr)
                {
                    winrt::com_ptr<IDWriteFontFace> created;

                    if (SUCCEEDED(font->CreateFontFace(created.put())))
                    {
                        ReturnIfFailed(result, AddFace(created.get(), face));
                    }
                }

                const auto emSize = m_format.FontSize * scale * scriptScale;
                const auto stop = std::min(mapped + (int32_t)mappedLength, end);

                // In chunks that end where a word starts, so that no chunk's glyphs depend on
                // its neighbour's: nothing is shaped across a space.
                auto chunk = mapped;

                while (chunk < stop)
                {
                    auto next = std::min(chunk + c_chunkLength, stop);

                    if (next < stop)
                    {
                        auto word = next;

                        while (word > chunk + 1 && !IsWordStart(word))
                        {
                            word--;
                        }

                        if (word > chunk + 1)
                        {
                            next = word;
                        }
                        else
                        {
                            // One word longer than a chunk: to its end.
                            word = next;

                            while (word < stop && !IsWordStart(word))
                            {
                                word++;
                            }

                            next = word;
                        }
                    }

                    ReturnIfFailed(result, Shape(analyzer, chunk, next - chunk, face, emSize, script, level, style));
                    chunk = next;
                }

                mapped = stop;
            }

            position = end;
        }

        return S_OK;
    }

    // Where white space ends and a word may start a line: where a chunk may end.
    bool EditableTextLayout::IsWordStart(int32_t position) const
    {
        return m_breakpoints[position - 1].isWhitespace && !m_breakpoints[position].isWhitespace && CanBreakBefore(position);
    }

    HRESULT EditableTextLayout::Shape(IDWriteTextAnalyzer* analyzer, int32_t start, int32_t length, uint32_t face, float emSize, const DWRITE_SCRIPT_ANALYSIS& script, uint8_t level, uint32_t style)
    {
        HRESULT result;

        const auto text = m_text.c_str() + start;
        const BOOL rightToLeft = (level & 1) != 0;
        const auto locale = m_format.Locale.c_str();
        const auto fontFace = m_faces[face].get();

        std::vector<DWRITE_SHAPING_TEXT_PROPERTIES> textProps(length);
        std::vector<uint16_t> glyphs;
        std::vector<DWRITE_SHAPING_GLYPH_PROPERTIES> glyphProps;

        auto clusterMap = m_clusterMap.data() + start;
        auto maxGlyphs = (UINT32)(length * 3 / 2 + 16);
        UINT32 actual = 0;

        for (;;)
        {
            glyphs.resize(maxGlyphs);
            glyphProps.resize(maxGlyphs);

            result = analyzer->GetGlyphs(text, (UINT32)length, fontFace, FALSE, rightToLeft, &script, locale, nullptr, nullptr, nullptr, 0, maxGlyphs, clusterMap, textProps.data(), glyphs.data(), glyphProps.data(), &actual);

            if (result == E_NOT_SUFFICIENT_BUFFER)
            {
                maxGlyphs *= 2;
                continue;
            }

            break;
        }

        if (FAILED(result))
        {
            return result;
        }

        const auto glyphStart = (uint32_t)m_glyphIndices.size();

        m_glyphIndices.insert(m_glyphIndices.end(), glyphs.begin(), glyphs.begin() + actual);
        m_glyphAdvances.resize(glyphStart + actual);
        m_glyphOffsets.resize(glyphStart + actual);

        ReturnIfFailed(result, analyzer->GetGlyphPlacements(text, clusterMap, textProps.data(), (UINT32)length, glyphs.data(), glyphProps.data(), actual, fontFace, emSize, FALSE, rightToLeft, &script, locale, nullptr, nullptr, 0, m_glyphAdvances.data() + glyphStart, m_glyphOffsets.data() + glyphStart));

        Run run{};
        run.Start = start;
        run.Length = length;
        run.GlyphStart = glyphStart;
        run.GlyphCount = actual;
        run.Face = face;
        run.EmSize = emSize;
        run.Script = script;
        run.Level = level;
        run.Style = style;
        run.Inline = -1;

        Measure(run);

        const auto runIndex = (uint32_t)m_runs.size();
        m_runs.push_back(run);

        // The clusters: characters that share their first glyph are one. Glyphs are in logical
        // order whichever way the run reads, so a cluster's glyphs run up to the next one's.
        for (int32_t i = 0; i < length; )
        {
            auto j = i + 1;

            while (j < length && clusterMap[j] == clusterMap[i])
            {
                j++;
            }

            const auto first = glyphStart + clusterMap[i];
            const auto last = glyphStart + (j < length ? clusterMap[j] : actual);

            float width = 0;

            // A line break takes no room, as in a layout, whatever glyph the font has for it.
            if (IsHardBreak(start + i))
            {
                for (auto g = first; g < last; g++)
                {
                    m_glyphAdvances[g] = 0;
                }
            }
            else
            {
                for (auto g = first; g < last; g++)
                {
                    width += m_glyphAdvances[g];
                }
            }

            m_clusterLength[start + i] = j - i;
            m_clusterWidth[start + i] = width;

            for (auto k = i; k < j; k++)
            {
                m_runOf[start + k] = runIndex;
            }

            i = j;
        }

        return S_OK;
    }

    HRESULT EditableTextLayout::AddFace(IDWriteFontFace* face, uint32_t& index)
    {
        // The same font can come back as another face object, from every item it is mapped for.
        winrt::com_ptr<IDWriteFontFace5> face5;
        face->QueryInterface(IID_PPV_ARGS(face5.put()));

        for (size_t i = 0; i < m_faces.size(); i++)
        {
            if (m_faces[i].get() == face || (face5 != nullptr && face5->Equals(m_faces[i].get())))
            {
                index = (uint32_t)i;
                return S_OK;
            }
        }

        winrt::com_ptr<IDWriteFontFace> copy;
        copy.copy_from(face);

        index = (uint32_t)m_faces.size();
        m_faces.push_back(std::move(copy));

        return S_OK;
    }

    // The text's own font: what an empty line is measured with, and what draws a character no
    // font in the fallback has.
    HRESULT EditableTextLayout::BaseFace(uint32_t& index)
    {
        HRESULT result;

        winrt::com_ptr<IDWriteFontCollection> collection = m_format.Collection;

        if (collection == nullptr)
        {
            ReturnIfFailed(result, m_format.Factory->GetSystemFontCollection(collection.put(), FALSE));
        }

        UINT32 familyIndex = 0;
        BOOL exists = FALSE;

        if (FAILED(collection->FindFamilyName(m_format.Family.c_str(), &familyIndex, &exists)) || !exists)
        {
            familyIndex = 0;
        }

        winrt::com_ptr<IDWriteFontFamily> family;
        ReturnIfFailed(result, collection->GetFontFamily(familyIndex, family.put()));

        winrt::com_ptr<IDWriteFont> font;
        ReturnIfFailed(result, family->GetFirstMatchingFont(m_format.Weight, DWRITE_FONT_STRETCH_NORMAL, m_format.Style, font.put()));

        winrt::com_ptr<IDWriteFontFace> face;
        ReturnIfFailed(result, font->CreateFontFace(face.put()));

        return AddFace(face.get(), index);
    }

    void EditableTextLayout::Measure(Run& run) const
    {
        DWRITE_FONT_METRICS metrics{};
        m_faces[run.Face]->GetMetrics(&metrics);

        const auto scale = metrics.designUnitsPerEm > 0 ? run.EmSize / metrics.designUnitsPerEm : 0;

        run.Ascent = metrics.ascent * scale;
        run.Descent = metrics.descent * scale;
        run.LineGap = metrics.lineGap * scale;

        // Positive below the baseline, as an underline is drawn; the font gives them above.
        run.UnderlineOffset = -metrics.underlinePosition * scale;
        run.UnderlineThickness = metrics.underlineThickness * scale;
        run.StrikethroughOffset = -metrics.strikethroughPosition * scale;
        run.StrikethroughThickness = metrics.strikethroughThickness * scale;
    }

    // =========================================================================================
    // Lines
    // =========================================================================================

    bool EditableTextLayout::IsHardBreak(int32_t position) const
    {
        return position >= 0 && position < (int32_t)m_text.size() && IsBreakCharacter(m_text[position]);
    }

    // Whether a line may end before position. The analyzer marks both sides of every boundary;
    // a boundary next to an inline object is neutral on that side, and neutral on both is a
    // break, as it is around an object in a layout.
    bool EditableTextLayout::CanBreakBefore(int32_t position) const
    {
        if (position <= 0 || position >= (int32_t)m_breakpoints.size())
        {
            return false;
        }

        const auto after = m_breakpoints[position - 1].breakConditionAfter;
        const auto before = m_breakpoints[position].breakConditionBefore;

        if (after == DWRITE_BREAK_CONDITION_MAY_NOT_BREAK || before == DWRITE_BREAK_CONDITION_MAY_NOT_BREAK)
        {
            return false;
        }

        return true;
    }

    // A tab reaches the next stop after x; everything else is as wide as it was shaped.
    float EditableTextLayout::ClusterWidth(int32_t position, float x) const
    {
        if (m_text[position] == L'\t' && m_tabStop > 0)
        {
            const auto next = (std::floor(x / m_tabStop) + 1) * m_tabStop;
            return next - x;
        }

        return m_clusterWidth[position];
    }

    // Greedy, as a layout breaks: as many clusters as fit, back to the last place a line may
    // end, white space hanging past the width, and inside a word only when the word alone is
    // wider than the line.
    void EditableTextLayout::Break(float width)
    {
        m_width = width;
        m_lines.clear();
        m_segments.clear();

        const auto length = (int32_t)m_text.size();
        const auto limit = std::max(width, 0.0f);

        float y = 0;
        int32_t position = 0;

        while (position < length)
        {
            const auto end = NextLine(position, limit);

            AddLine(position, end, y);
            position = end;
        }

        // An empty paragraph is a line, and so is the end of one that ends with a break.
        if (length == 0 || IsHardBreak(length - 1))
        {
            AddLine(length, length, y);
        }

        Align();
    }

    // The lines after an edit: the ones before the line the window starts on, and the one
    // before that, which a word can move up to, are kept; from there they are broken again
    // until one ends past the window where an old one ended, which every one after it then
    // did too, moved by the edit.
    void EditableTextLayout::Rebreak(const std::vector<Line>& oldLines, const std::vector<Segment>& oldSegments, int32_t windowStart, int32_t windowEnd, int32_t delta, int32_t runDelta)
    {
        const auto length = (int32_t)m_text.size();
        const auto limit = std::max(m_width, 0.0f);

        auto first = (size_t)(std::upper_bound(oldLines.begin(), oldLines.end(), windowStart, [](int32_t value, const Line& line)
            {
                return value < line.Start;
            }) - oldLines.begin());

        first = first > 1 ? first - 2 : 0;

        m_lines.assign(oldLines.begin(), oldLines.begin() + first);
        m_segments.assign(oldSegments.begin(), oldSegments.begin() + oldLines[first].SegmentStart);

        auto y = oldLines[first].Y;
        auto position = oldLines[first].Start;

        // Every row from the first line broken again, until shown otherwise below.
        m_change.Top = y;
        m_change.Bottom = INFINITY;

        while (position < length)
        {
            const auto end = NextLine(position, limit);

            AddLine(position, end, y);
            position = end;

            if (position < windowEnd || position >= length)
            {
                continue;
            }

            const auto old = position - delta;
            auto it = std::lower_bound(oldLines.begin(), oldLines.end(), old, [](const Line& line, int32_t value)
                {
                    return line.Start < value;
                });

            if (it == oldLines.end() || it->Start != old)
            {
                continue;
            }

            const auto shift = y - it->Y;

            // The old lines after it are where they were, unless the ones before them took
            // more or less room.
            if (shift == 0)
            {
                m_change.Bottom = y;
            }

            for (; it != oldLines.end(); ++it)
            {
                Line line = *it;
                line.Start += delta;
                line.ContentEnd += delta;
                line.Y += shift;
                line.SegmentStart = (uint32_t)m_segments.size();

                for (uint32_t i = 0; i < it->SegmentCount; i++)
                {
                    Segment segment = oldSegments[it->SegmentStart + i];
                    segment.Run = (uint32_t)(segment.Run + runDelta);
                    segment.Start += delta;

                    m_segments.push_back(segment);
                }

                m_lines.push_back(line);
            }

            Align();
            return;
        }

        if (IsHardBreak(length - 1))
        {
            AddLine(length, length, y);
        }

        Align();
    }

    // Where the line that starts at start ends: after a hard break, or where the last cluster
    // that fits is followed by a place a line may end.
    int32_t EditableTextLayout::NextLine(int32_t start, float limit)
    {
        const auto length = (int32_t)m_text.size();

        float x = 0;
        int32_t breakAt = -1;
        auto p = start;

        while (p < length)
        {
            if (IsHardBreak(p))
            {
                auto end = p + 1;

                if (m_text[p] == L'\r' && end < length && m_text[end] == L'\n')
                {
                    end++;
                }

                return end;
            }

            const auto advance = ClusterWidth(p, x);
            const auto white = m_breakpoints[p].isWhitespace != 0;

            if (!white && p > start && x + advance > limit + c_epsilon)
            {
                return breakAt > start ? breakAt : p;
            }

            // Where the tab landed is the width it is drawn at.
            if (m_text[p] == L'\t')
            {
                m_clusterWidth[p] = advance;

                const auto& run = m_runs[m_runOf[p]];
                if (run.Inline < 0 && run.GlyphCount > 0)
                {
                    m_glyphAdvances[run.GlyphStart + m_clusterMap[p]] = advance;
                }
            }

            x += advance;
            p += std::max(m_clusterLength[p], 1);

            if (p < length && CanBreakBefore(p))
            {
                breakAt = p;
            }
        }

        return length;
    }

    void EditableTextLayout::AddLine(int32_t start, int32_t end, float& y)
    {
        Line line{};
        line.Start = start;
        line.Length = end - start;
        line.Y = y;

        auto content = end;

        while (content > start && (IsHardBreak(content - 1) || m_breakpoints[content - 1].isWhitespace))
        {
            content--;
        }

        line.ContentEnd = content;

        for (auto p = start; p < content; p += std::max(m_clusterLength[p], 1))
        {
            line.Width += m_clusterWidth[p];
        }

        // The tallest of what is on the line, which is DirectWrite's default spacing; an empty
        // one is as tall as the text's own font, or as the line break before it.
        float ascent = 0;
        float descent = 0;
        float gap = 0;
        bool measured = false;

        if (end > start)
        {
            for (auto r = m_runOf[start]; r <= m_runOf[end - 1] && r < m_runs.size(); r++)
            {
                ascent = std::max(ascent, m_runs[r].Ascent);
                descent = std::max(descent, m_runs[r].Descent);
                gap = std::max(gap, m_runs[r].LineGap);
                measured = true;
            }
        }
        else if (start > 0 && !m_runs.empty())
        {
            const auto& run = m_runs[m_runOf[start - 1]];

            ascent = run.Ascent;
            descent = run.Descent;
            gap = run.LineGap;
            measured = true;
        }

        if (!measured && m_baseFace < m_faces.size())
        {
            Run base{};
            base.Face = m_baseFace;
            base.EmSize = m_format.FontSize;
            Measure(base);

            ascent = base.Ascent;
            descent = base.Descent;
            gap = base.LineGap;
        }

        line.Baseline = ascent;
        line.Height = ascent + descent + gap;

        // The runs on the line, in logical order, and the white space it ends with at the
        // paragraph's own level (UAX #9, L1).
        const uint8_t paragraphLevel = m_format.RightToLeft ? 1 : 0;
        const auto first = m_segments.size();

        if (end > start)
        {
            for (auto r = m_runOf[start]; r <= m_runOf[end - 1] && r < m_runs.size(); r++)
            {
                const auto& run = m_runs[r];
                const auto from = std::max(run.Start, start);
                const auto to = std::min(run.Start + run.Length, end);

                if (to <= from)
                {
                    continue;
                }

                if (from < content && to > content)
                {
                    m_segments.push_back({ r, from, content - from, run.Level, 0, 0 });
                    m_segments.push_back({ r, content, to - content, paragraphLevel, 0, 0 });
                }
                else
                {
                    m_segments.push_back({ r, from, to - from, from >= content ? paragraphLevel : run.Level, 0, 0 });
                }
            }
        }

        const auto pieces = m_segments.begin() + first;
        const auto count = m_segments.size() - first;

        // In the order they are shown (UAX #9, L2): from the highest level down to the lowest
        // odd one, every stretch at that level or above reversed.
        uint8_t highest = 0;
        uint8_t lowest = UINT8_MAX;

        for (size_t i = 0; i < count; i++)
        {
            highest = std::max(highest, pieces[i].Level);
            lowest = std::min(lowest, pieces[i].Level);
        }

        if (count > 0)
        {
            const uint8_t lowestOdd = (lowest & 1) != 0 ? lowest : (uint8_t)(lowest + 1);

            for (int level = highest; level >= lowestOdd; level--)
            {
                for (size_t i = 0; i < count; )
                {
                    if (pieces[i].Level < level)
                    {
                        i++;
                        continue;
                    }

                    auto j = i;

                    while (j < count && pieces[j].Level >= level)
                    {
                        j++;
                    }

                    std::reverse(pieces + i, pieces + j);
                    i = j;
                }
            }
        }

        float x = 0;

        for (size_t i = 0; i < count; i++)
        {
            Segment& piece = pieces[i];
            piece.X = x;
            piece.Width = 0;

            for (auto p = piece.Start; p < piece.Start + piece.Length; p += std::max(m_clusterLength[p], 1))
            {
                piece.Width += m_clusterWidth[p];
            }

            x += piece.Width;
        }

        line.Hang = x - line.Width;
        line.SegmentStart = (uint32_t)first;
        line.SegmentCount = (uint32_t)count;

        y += line.Height;
        m_lines.push_back(line);
    }

    // Where each line starts in the box. An unbounded box - a measure asking how wide the text
    // wants to be - is as wide as the widest line, so that nothing is placed at infinity.
    void EditableTextLayout::Align()
    {
        const auto box = std::isfinite(m_width) ? m_width : Width();

        for (Line& line : m_lines)
        {
            const auto free = box - line.Width;

            switch (m_format.Alignment)
            {
            case EditableAlignment::Center:
                line.X = free / 2;
                break;
            case EditableAlignment::Right:
                line.X = free;
                break;
            case EditableAlignment::Left:
                line.X = 0;
                break;
            default:
                line.X = m_format.RightToLeft ? free : 0;
                break;
            }

            if (m_format.RightToLeft)
            {
                line.X -= line.Hang;
            }
        }
    }

    // =========================================================================================
    // Questions
    // =========================================================================================

    float EditableTextLayout::Left() const
    {
        if (m_lines.empty())
        {
            return 0;
        }

        auto left = m_lines[0].X;

        for (const Line& line : m_lines)
        {
            left = std::min(left, line.X);
        }

        return left;
    }

    float EditableTextLayout::Width() const
    {
        float width = 0;

        for (const Line& line : m_lines)
        {
            width = std::max(width, line.Width);
        }

        return width;
    }

    float EditableTextLayout::Height() const
    {
        return m_lines.empty() ? 0 : m_lines.back().Y + m_lines.back().Height;
    }

    int32_t EditableTextLayout::LineCount() const
    {
        return (int32_t)m_lines.size();
    }

    // The line a position is on: a position where a wrapped line ends is where the next one
    // starts, and the end of the text is on the last line.
    const EditableTextLayout::Line& EditableTextLayout::LineOf(int32_t offset) const
    {
        // The last line starting at or before it, as each starts where the one before ends.
        auto it = std::upper_bound(m_lines.begin(), m_lines.end(), offset, [](int32_t value, const Line& line)
            {
                return value < line.Start;
            });

        return it == m_lines.begin() ? m_lines.front() : *(it - 1);
    }

    void EditableTextLayout::ClusterEdges(const Line&, const Segment& segment, int32_t position, float& left, float& right) const
    {
        float before = 0;
        float width = 0;

        for (auto p = segment.Start; p < segment.Start + segment.Length; p += std::max(m_clusterLength[p], 1))
        {
            const auto length = std::max(m_clusterLength[p], 1);

            if (position < p + length)
            {
                width = m_clusterWidth[p];
                break;
            }

            before += m_clusterWidth[p];
        }

        if ((segment.Level & 1) != 0)
        {
            right = segment.X + segment.Width - before;
            left = right - width;
        }
        else
        {
            left = segment.X + before;
            right = left + width;
        }
    }

    // A caret before the character at offset: on its leading edge, which is its right side
    // when it reads right to left. At the end of the text, after the last character instead.
    Windows::Foundation::Rect EditableTextLayout::Caret(int32_t offset) const
    {
        if (m_lines.empty())
        {
            return { 0, 0, 0, 0 };
        }

        const auto length = (int32_t)m_text.size();
        offset = std::clamp(offset, 0, length);

        const Line& line = LineOf(offset);

        float x = 0;
        float top = line.Y;
        float height = line.Height;
        auto position = offset;
        auto trailing = false;

        // Past the end of the line: the trailing edge of the last thing on it.
        if (position >= line.Start + line.Length && line.Length > 0)
        {
            position = line.Start + line.Length - 1;
            trailing = true;
        }

        for (uint32_t i = 0; i < line.SegmentCount; i++)
        {
            const Segment& segment = m_segments[line.SegmentStart + i];

            if (position >= segment.Start && position < segment.Start + segment.Length)
            {
                float left;
                float right;
                ClusterEdges(line, segment, position, left, right);

                const auto rightToLeft = (segment.Level & 1) != 0;
                x = rightToLeft != trailing ? right : left;

                // As tall as the run it is in, from that run's ascent above the baseline: what
                // DirectWrite answers, which is lower and shorter than the line for a run in a
                // smaller font, and for sub- and superscript, which it does not know are moved.
                const Run& run = m_runs[segment.Run];
                top = line.Y + line.Baseline - run.Ascent;
                height = run.Ascent + run.Descent;
                break;
            }
        }

        return { line.X + x, top, 0, height };
    }

    void EditableTextLayout::Ranges(int32_t offset, int32_t length, std::vector<Windows::Foundation::Rect>& rects) const
    {
        const auto from = std::max(offset, 0);
        const auto to = std::min(offset + length, (int32_t)m_text.size());

        if (m_lines.empty() || to <= from)
        {
            return;
        }

        for (auto index = (size_t)(&LineOf(from) - m_lines.data()); index < m_lines.size(); index++)
        {
            const Line& line = m_lines[index];

            if (line.Start >= to)
            {
                break;
            }

            if (line.Start + line.Length <= from)
            {
                continue;
            }

            // Whether this line has a rectangle yet, and the level of the last one.
            auto merged = false;
            uint8_t level = 0;

            for (uint32_t i = 0; i < line.SegmentCount; i++)
            {
                const Segment& segment = m_segments[line.SegmentStart + i];
                const auto start = std::max(segment.Start, from);
                const auto stop = std::min(segment.Start + segment.Length, to);

                if (stop <= start)
                {
                    continue;
                }

                float firstLeft;
                float firstRight;
                float lastLeft;
                float lastRight;

                ClusterEdges(line, segment, start, firstLeft, firstRight);
                ClusterEdges(line, segment, stop - 1, lastLeft, lastRight);

                const auto left = std::min(firstLeft, lastLeft);
                const auto right = std::max(firstRight, lastRight);

                // One rectangle per direction run on a line, as DirectWrite hit tests a range:
                // the runs a line is split into for its fonts and chunks are not the caller's
                // business, and a shape drawn per rectangle would show every one of them.
                if (merged && level == segment.Level && std::abs(rects.back().X + rects.back().Width - (line.X + left)) < 0.01f)
                {
                    rects.back().Width = line.X + right - rects.back().X;
                }
                else
                {
                    rects.push_back({ line.X + left, line.Y, right - left, line.Height });
                }

                merged = true;
                level = segment.Level;
            }
        }
    }

    // The cluster nearest the point, and which half of it, in reading order, the point is on.
    bool EditableTextLayout::HitTest(float x, float y, EditableHit& hit) const
    {
        if (m_lines.empty())
        {
            return false;
        }

        // The first line the point is above the bottom of, or the last one below them all.
        auto found = std::partition_point(m_lines.begin(), m_lines.end(), [y](const Line& line)
            {
                return line.Y + line.Height <= y;
            });

        const Line& line = found == m_lines.end() ? m_lines.back() : *found;
        const auto px = x - line.X;

        hit = {};
        hit.Position = line.Start;

        auto best = INFINITY;
        float extent = 0;

        for (uint32_t i = 0; i < line.SegmentCount; i++)
        {
            const Segment& segment = m_segments[line.SegmentStart + i];
            extent = std::max(extent, segment.X + segment.Width);

            for (auto p = segment.Start; p < segment.Start + segment.Length; p += std::max(m_clusterLength[p], 1))
            {
                // A line break is no place to put a caret on: the end of the line is.
                if (IsHardBreak(p))
                {
                    continue;
                }

                float left;
                float right;
                ClusterEdges(line, segment, p, left, right);

                const auto distance = px < left ? left - px : px > right ? px - right : 0.0f;

                if (distance < best)
                {
                    best = distance;

                    const auto middle = (left + right) / 2;
                    const auto rightToLeft = (segment.Level & 1) != 0;

                    hit.Position = p;
                    hit.Length = std::max(m_clusterLength[p], 1);
                    hit.Trailing = rightToLeft ? px < middle : px > middle;
                }
            }
        }

        hit.Inside = y >= line.Y && y < line.Y + line.Height && px >= 0 && px < extent;
        return true;
    }

    void EditableTextLayout::LineLengths(std::vector<int32_t>& lengths) const
    {
        lengths.reserve(lengths.size() + m_lines.size());

        for (const Line& line : m_lines)
        {
            lengths.push_back(line.Length);
        }
    }

    // Where the last line's text ends on the right, which is where DirectTextLayout asks a
    // layout for it: the trailing edge of whatever is rightmost on that line.
    Windows::Foundation::Point EditableTextLayout::ContentEnd() const
    {
        if (m_lines.empty())
        {
            return { 0, 0 };
        }

        const Line& line = m_lines.back();
        float right = 0;

        for (uint32_t i = 0; i < line.SegmentCount; i++)
        {
            const Segment& segment = m_segments[line.SegmentStart + i];

            if (segment.Width > 0)
            {
                right = std::max(right, segment.X + segment.Width);
            }
        }

        return { line.X + right, line.Y + line.Height };
    }

    void EditableTextLayout::Lines(std::vector<EditableLine>& lines) const
    {
        lines.reserve(m_lines.size());

        for (const Line& line : m_lines)
        {
            lines.push_back({ line.Start, line.Length, line.Y, line.Height, line.Baseline });
        }
    }

    // =========================================================================================
    // Checking
    // =========================================================================================

    // The glyphs of the cluster that starts at position.
    void EditableTextLayout::ClusterGlyphs(int32_t position, uint32_t& first, uint32_t& last) const
    {
        const Run& run = m_runs[m_runOf[position]];
        const auto next = position + std::max(m_clusterLength[position], 1);

        first = run.GlyphStart + m_clusterMap[position];
        last = next < run.Start + run.Length ? run.GlyphStart + m_clusterMap[next] : run.GlyphStart + run.GlyphCount;
    }

    // What has to be the same for two layouts of one input to draw and answer the same: the
    // break opportunities, each cluster's font, size, glyphs and placement, and the lines. Not
    // the runs themselves - an update chunks differently from a cold build, and should.
    bool EditableTextLayout::Matches(const EditableTextLayout& other, std::wstring& difference) const
    {
        constexpr float tolerance = 0.001f;

        if (m_text != other.m_text)
        {
            difference = L"the text";
            return false;
        }

        const auto length = (int32_t)m_text.size();

        for (int32_t p = 0; p < length; p++)
        {
            const auto& x = m_breakpoints[p];
            const auto& y = other.m_breakpoints[p];

            if (x.breakConditionBefore != y.breakConditionBefore || x.breakConditionAfter != y.breakConditionAfter || x.isWhitespace != y.isWhitespace || x.isSoftHyphen != y.isSoftHyphen)
            {
                difference = std::format(L"the break opportunity at {}: before {} after {} white {} against before {} after {} white {}", p,
                    (int)x.breakConditionBefore, (int)x.breakConditionAfter, (int)x.isWhitespace,
                    (int)y.breakConditionBefore, (int)y.breakConditionAfter, (int)y.isWhitespace);
                return false;
            }

            if (m_clusterLength[p] != other.m_clusterLength[p])
            {
                difference = std::format(L"the cluster at {}", p);
                return false;
            }

            if (m_clusterLength[p] == 0)
            {
                continue;
            }

            if (std::abs(m_clusterWidth[p] - other.m_clusterWidth[p]) > tolerance)
            {
                difference = std::format(L"the advance at {}: {:.3f} against {:.3f}", p, m_clusterWidth[p], other.m_clusterWidth[p]);
                return false;
            }

            const Run& a = m_runs[m_runOf[p]];
            const Run& b = other.m_runs[other.m_runOf[p]];

            const auto faceA = m_faces[a.Face].get();
            const auto faceB = other.m_faces[b.Face].get();

            auto sameFace = faceA == faceB;

            if (!sameFace)
            {
                winrt::com_ptr<IDWriteFontFace5> face5;
                faceA->QueryInterface(IID_PPV_ARGS(face5.put()));
                sameFace = face5 != nullptr && face5->Equals(faceB);
            }

            if (!sameFace || a.EmSize != b.EmSize || a.Level != b.Level || a.Style != b.Style || (a.Inline < 0) != (b.Inline < 0))
            {
                difference = std::format(L"the run at {}", p);
                return false;
            }

            if (a.Inline >= 0)
            {
                continue;
            }

            uint32_t firstA;
            uint32_t lastA;
            uint32_t firstB;
            uint32_t lastB;

            ClusterGlyphs(p, firstA, lastA);
            other.ClusterGlyphs(p, firstB, lastB);

            if (lastA - firstA != lastB - firstB)
            {
                difference = std::format(L"the glyph count at {}", p);
                return false;
            }

            for (uint32_t g = 0; g < lastA - firstA; g++)
            {
                const auto& offsetA = m_glyphOffsets[firstA + g];
                const auto& offsetB = other.m_glyphOffsets[firstB + g];

                if (m_glyphIndices[firstA + g] != other.m_glyphIndices[firstB + g]
                    || std::abs(m_glyphAdvances[firstA + g] - other.m_glyphAdvances[firstB + g]) > tolerance
                    || std::abs(offsetA.advanceOffset - offsetB.advanceOffset) > tolerance
                    || std::abs(offsetA.ascenderOffset - offsetB.ascenderOffset) > tolerance)
                {
                    difference = std::format(L"the glyphs at {}", p);
                    return false;
                }
            }
        }

        if (m_lines.size() != other.m_lines.size())
        {
            difference = std::format(L"the line count: {} against {}", m_lines.size(), other.m_lines.size());
            return false;
        }

        for (size_t i = 0; i < m_lines.size(); i++)
        {
            const Line& a = m_lines[i];
            const Line& b = other.m_lines[i];

            if (a.Start != b.Start || a.Length != b.Length || a.ContentEnd != b.ContentEnd)
            {
                difference = std::format(L"line {}: {}+{} against {}+{}", i, a.Start, a.Length, b.Start, b.Length);
                return false;
            }

            if (std::abs(a.X - b.X) > tolerance || std::abs(a.Y - b.Y) > tolerance || std::abs(a.Width - b.Width) > tolerance
                || std::abs(a.Height - b.Height) > tolerance || std::abs(a.Baseline - b.Baseline) > tolerance || std::abs(a.Hang - b.Hang) > tolerance)
            {
                difference = std::format(L"line {}'s metrics", i);
                return false;
            }

        }

        // Where everything is drawn, through the carets: the segments themselves follow the
        // runs, so they differ wherever the chunks do.
        for (int32_t p = 0; p <= length; p++)
        {
            if (p < length && m_clusterLength[p] == 0)
            {
                continue;
            }

            const auto a = Caret(p);
            const auto b = other.Caret(p);

            if (std::abs(a.X - b.X) > tolerance || std::abs(a.Y - b.Y) > tolerance)
            {
                difference = std::format(L"the caret at {}: {:.3f},{:.3f} against {:.3f},{:.3f}", p, a.X, a.Y, b.X, b.Y);
                return false;
            }
        }

        return true;
    }

    // =========================================================================================
    // Drawing
    // =========================================================================================

    HRESULT EditableTextLayout::Draw(ID2D1DeviceContext* context, ID2D1Brush* brush, const std::vector<EditableColor>& colors, float top, float bottom) const
    {
        HRESULT result;

        // The lines that reach into [top, bottom), and the text they hold.
        const auto firstLine = std::partition_point(m_lines.begin(), m_lines.end(), [top](const Line& line)
            {
                return line.Y + line.Height <= top;
            });

        const auto lastLine = std::partition_point(firstLine, m_lines.end(), [bottom](const Line& line)
            {
                return line.Y < bottom;
            });

        if (firstLine == lastLine)
        {
            return S_OK;
        }

        const auto textStart = firstLine->Start;
        const auto textEnd = (lastLine - 1)->Start + (lastLine - 1)->Length;

        winrt::com_ptr<ID2D1DeviceContext4> context4;
        context->QueryInterface(IID_PPV_ARGS(context4.put()));

        const auto factory4 = m_format.Factory.try_as<IDWriteFactory4>();

        // The transform the surface is drawn with, which decides which strike of a bitmap font
        // is picked, as it does for the layout.
        D2D1_MATRIX_3X2_F matrix;
        context->GetTransform(&matrix);

        FLOAT dpiX;
        FLOAT dpiY;
        context->GetDpi(&dpiX, &dpiY);

        const auto sx = dpiX / 96;
        const auto sy = dpiY / 96;
        const DWRITE_MATRIX transform{ matrix._11 * sx, matrix._12 * sy, matrix._21 * sx, matrix._22 * sy, matrix._31 * sx, matrix._32 * sy };

        // The colour of every position drawn, the later range over the earlier, as drawing
        // effects set one after the other end up.
        std::vector<int32_t> colorOf;

        if (!colors.empty())
        {
            colorOf.assign(textEnd - textStart, -1);

            for (size_t i = 0; i < colors.size(); i++)
            {
                const auto from = std::clamp(colors[i].Offset, textStart, textEnd);
                const auto to = std::clamp(colors[i].Offset + colors[i].Length, from, textEnd);

                for (auto p = from; p < to; p++)
                {
                    colorOf[p - textStart] = (int32_t)i;
                }
            }
        }

        const auto colorAt = [&](int32_t position)
            {
                return colorOf.empty() ? -1 : colorOf[position - textStart];
            };

        std::vector<winrt::com_ptr<ID2D1SolidColorBrush>> brushes(colors.size());

        for (auto it = firstLine; it != lastLine; ++it)
        {
            const Line& line = *it;
            const auto baseline = line.Y + line.Baseline;

            for (uint32_t i = 0; i < line.SegmentCount; i++)
            {
                const Segment& segment = m_segments[line.SegmentStart + i];
                const Run& run = m_runs[segment.Run];

                if (run.Inline >= 0)
                {
                    if (segment.Start == run.Start)
                    {
                        const auto& metrics = m_inlineMetrics[run.Inline];
                        m_inlines[run.Inline].Object->Draw(nullptr, nullptr, line.X + segment.X, baseline - metrics.baseline, FALSE, FALSE, nullptr);
                    }

                    continue;
                }

                // Sub- and superscript sit off the baseline by the proportions XAML's own
                // typography variants use.
                const auto shift = (run.Style & c_superscript) != 0 ? -m_format.FontSize / 3
                    : (run.Style & c_subscript) != 0 ? m_format.FontSize / 6
                    : 0.0f;

                const auto rightToLeft = (segment.Level & 1) != 0;
                const auto segmentEnd = segment.Start + segment.Length;
                const auto runEnd = run.Start + run.Length;

                float before = 0;

                // In stretches of one colour, each drawn as a glyph run of its own.
                for (auto p = segment.Start; p < segmentEnd; )
                {
                    const auto color = colorAt(p);
                    const auto hardBreak = IsHardBreak(p);

                    float width = 0;
                    auto q = p;

                    while (q < segmentEnd && colorAt(q) == color && IsHardBreak(q) == hardBreak)
                    {
                        width += m_clusterWidth[q];
                        q += std::max(m_clusterLength[q], 1);
                    }

                    const auto first = run.GlyphStart + m_clusterMap[p];
                    const auto last = q < runEnd ? run.GlyphStart + m_clusterMap[q] : run.GlyphStart + run.GlyphCount;

                    ID2D1Brush* stretch = brush;
                    auto visible = !hardBreak && last > first;

                    if (color >= 0)
                    {
                        if (brushes[color] == nullptr)
                        {
                            ReturnIfFailed(result, context->CreateSolidColorBrush(colors[color].Color, brushes[color].put()));
                        }

                        stretch = brushes[color].get();
                    }

                    const auto x = rightToLeft
                        ? line.X + segment.X + segment.Width - before
                        : line.X + segment.X + before;

                    if (visible)
                    {
                        DWRITE_GLYPH_RUN glyphRun{};
                        glyphRun.fontFace = m_faces[run.Face].get();
                        glyphRun.fontEmSize = run.EmSize;
                        glyphRun.glyphCount = last - first;
                        glyphRun.glyphIndices = m_glyphIndices.data() + first;
                        glyphRun.glyphAdvances = m_glyphAdvances.data() + first;
                        glyphRun.glyphOffsets = m_glyphOffsets.data() + first;
                        glyphRun.isSideways = FALSE;
                        glyphRun.bidiLevel = segment.Level;

                        DrawGlyphs(context, context4.get(), factory4.get(), transform, { x, baseline + shift }, glyphRun, stretch);

                        const auto left = rightToLeft ? x - width : x;

                        if ((run.Style & c_underline) != 0)
                        {
                            const auto top = baseline + run.UnderlineOffset;
                            context->FillRectangle({ left, top, left + width, top + run.UnderlineThickness }, stretch);
                        }

                        if ((run.Style & c_strikethrough) != 0)
                        {
                            const auto top = baseline + run.StrikethroughOffset;
                            context->FillRectangle({ left, top, left + width, top + run.StrikethroughThickness }, stretch);
                        }
                    }

                    before += width;
                    p = q;
                }
            }
        }

        return S_OK;
    }
}
