#pragma once

#include <algorithm>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

#include <dwrite_3.h>
#include <D2d1_3.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>

namespace winrt::Telegram::Native::implementation
{
    // How a paragraph's lines sit in its box, as TextAlignmentMode says it.
    enum class EditableAlignment
    {
        Leading,
        Left,
        Center,
        Right
    };

    // Everything a paragraph is shaped with that is the same all over it: what DirectTextLayout
    // hands an IDWriteTextFormat, plus the fallback it hands the layout.
    struct EditableTextFormat
    {
        winrt::com_ptr<IDWriteFactory> Factory;
        winrt::com_ptr<IDWriteFontCollection> Collection;
        std::wstring Family;

        // Null for the system's, which is what a layout without one falls back through.
        winrt::com_ptr<IDWriteFontFallback> Fallback;

        winrt::com_ptr<IDWriteFontCollection> MonospaceCollection;
        std::wstring MonospaceFamily;

        float FontSize{ 14 };
        DWRITE_FONT_WEIGHT Weight{ DWRITE_FONT_WEIGHT_NORMAL };
        DWRITE_FONT_STYLE Style{ DWRITE_FONT_STYLE_NORMAL };
        std::wstring Locale;

        bool RightToLeft{ false };
        EditableAlignment Alignment{ EditableAlignment::Leading };

        // How big sub- and superscript are against the text around them.
        float ScriptScale{ 0.7f };
    };

    // A range of TextStyle bits, as the caller's entities give them.
    struct EditableStyle
    {
        int32_t Offset;
        int32_t Length;
        uint32_t Type;
    };

    // A box the text flows around. The layout reads its metrics and, at draw time, asks it to
    // draw itself, which only a formula does. Held, because the caller can replace it before
    // the next build and a redraw can come in between.
    struct EditableInline
    {
        int32_t Offset;
        int32_t Length;
        winrt::com_ptr<IDWriteInlineObject> Object;
    };

    struct EditableColor
    {
        int32_t Offset;
        int32_t Length;
        D2D1_COLOR_F Color;
    };

    // What a point lands on: the cluster, and which half of it in reading order.
    struct EditableHit
    {
        int32_t Position{ 0 };
        int32_t Length{ 0 };
        bool Trailing{ false };
        bool Inside{ false };
    };

    // What the last build or update changed, for a caller that keeps what it drew: the rows to
    // draw again, in DIPs, and the edit, which says where the text after it moved to. All is
    // everything, which a build from scratch and a new width are.
    struct EditableChange
    {
        bool All{ true };
        float Top{ 0 };
        float Bottom{ 0 };
        int32_t Start{ 0 };
        int32_t OldEnd{ 0 };
        int32_t NewEnd{ 0 };
    };

    // An edit the caller knows it made: [Start, OldEnd) of the text before became [Start, NewEnd)
    // of the text after, and everything outside it, styles included, is as it was.
    struct EditableEdit
    {
        int32_t Start;
        int32_t OldEnd;
        int32_t NewEnd;

        // This edit followed by next, which is in the text this one left, as one.
        EditableEdit Then(const EditableEdit& next) const
        {
            const auto start = std::min(Start, next.Start);
            const auto middle = std::max(NewEnd, next.OldEnd);

            return { start, middle - (NewEnd - OldEnd), middle + (next.NewEnd - next.OldEnd) };
        }
    };

    struct EditableLine
    {
        int32_t Start;
        int32_t Length;
        float Y;
        float Height;
        float Baseline;
    };

    // One paragraph laid out on IDWriteTextAnalyzer rather than by IDWriteTextLayout: analysed,
    // split into runs by script, direction, style and font, shaped in chunks that end at word
    // boundaries, and broken into lines here. It answers what DirectTextLayout is asked about a
    // paragraph, in the same coordinates, and draws the glyph runs itself. Built whole for now;
    // the chunks are what an edit will replace without shaping the rest.
    // See notes/native-editor-performance-plan.md, phase 2.
    class EditableTextLayout
    {
    public:
        HRESULT Build(const EditableTextFormat& format, std::wstring_view text, const std::vector<EditableStyle>& styles, const std::vector<EditableInline>& inlines, float width);

        // The same input as Build, after an edit: only what is around the change is analysed,
        // shaped and broken again. Built from scratch whenever that cannot be done. The edit is
        // found by comparing the text with the one before, unless the caller says what it was.
        HRESULT Update(const EditableTextFormat& format, std::wstring_view text, const std::vector<EditableStyle>& styles, const std::vector<EditableInline>& inlines, float width, const EditableEdit* edit = nullptr);

        // The lines again at another width, from the advances already shaped.
        HRESULT Reflow(float width);

        // Whether this layout draws and answers as other does, which is what an update has to
        // end up as against a build of the same input; difference says where it does not.
        bool Matches(const EditableTextLayout& other, std::wstring& difference) const;

        // As DWRITE_TEXT_METRICS has them: where the leftmost line starts, the widest line
        // without the white space it ends with, and the height of all of them.
        float Left() const;
        float Right() const;
        float Width() const;
        float Height() const;
        int32_t LineCount() const;

        // The narrowest the paragraph can be broken into lines without breaking a word, and its
        // widest line when nothing wraps it, white space it ends with left out as Width leaves it.
        void ContentWidths(float& narrowest, float& widest) const;

        Windows::Foundation::Rect Caret(int32_t offset) const;
        void Ranges(int32_t offset, int32_t length, std::vector<Windows::Foundation::Rect>& rects) const;
        bool HitTest(float x, float y, EditableHit& hit) const;
        void LineLengths(std::vector<int32_t>& lengths) const;
        Windows::Foundation::Point ContentEnd() const;
        void Lines(std::vector<EditableLine>& lines) const;

        const EditableChange& Change() const
        {
            return m_change;
        }

        // What cold builds spend, stage by stage, in QueryPerformanceCounter ticks: for the
        // meter, process-wide and unlocked, as DirectTextLayout's counters are.
        struct Stages
        {
            int64_t Builds;
            int64_t Prepare;
            int64_t Setup;
            int64_t Analysis;
            int64_t Mapping;
            int64_t Shaping;
            int64_t Breaking;

            // The runs they came to, which a font fallback can cut far finer than the chunks.
            int64_t Runs;
        };

        inline static Stages BuildStages{};

        // The same for updates: how many, how many were told their edit rather than finding
        // it, and the stages, the last of them putting back what came after the window.
        struct UpdateStageTicks
        {
            int64_t Updates;
            int64_t Told;
            int64_t Prepare;
            int64_t Find;
            int64_t Splice;
            int64_t Analysis;
            int64_t Shaping;
            int64_t Tail;
            int64_t Breaking;
        };

        inline static UpdateStageTicks UpdateStages{};

        // Where the lines that hold [start, end) begin and end, in DIPs.
        void RangeExtent(int32_t start, int32_t end, float& top, float& bottom) const;

        // The lines that reach into [top, bottom), in DIPs.
        HRESULT Draw(ID2D1DeviceContext* context, ID2D1Brush* brush, const std::vector<EditableColor>& colors, float top, float bottom) const;

    private:
        // A stretch of the paragraph in one font, one direction, one script and one style,
        // in logical order. An inline object is a run of its own, and one cluster.
        struct Run
        {
            int32_t Start;
            int32_t Length;

            uint32_t GlyphStart;
            uint32_t GlyphCount;

            uint32_t Face;
            float EmSize;

            DWRITE_SCRIPT_ANALYSIS Script;
            uint8_t Level;
            uint32_t Style;

            int32_t Inline;

            float Ascent;
            float Descent;
            float LineGap;

            float UnderlineOffset;
            float UnderlineThickness;
            float StrikethroughOffset;
            float StrikethroughThickness;
        };

        // A run's part of a line, in the order the line shows them.
        struct Segment
        {
            uint32_t Run;
            int32_t Start;
            int32_t Length;
            uint8_t Level;
            float X;
            float Width;
        };

        struct Line
        {
            int32_t Start;
            int32_t Length;

            // Where the white space the line ends with starts: it hangs past the width.
            int32_t ContentEnd;

            float X;
            float Y;
            float Width;
            float Height;
            float Baseline;

            // How wide that white space is. It hangs on the paragraph's trailing side, which
            // right to left is the left, before the text the line is aligned by.
            float Hang;

            uint32_t SegmentStart;
            uint32_t SegmentCount;
        };

        void Prepare(std::wstring_view text, const std::vector<EditableStyle>& styles, const std::vector<EditableInline>& inlines);
        void RemapInlines();

        static bool IsEdit(const EditableEdit& edit, int32_t oldLength, int32_t newLength);
        bool SameObjectsAround(const EditableEdit& edit, const std::vector<EditableInline>& oldInlines, const std::vector<DWRITE_INLINE_OBJECT_METRICS>& oldMetrics) const;

        HRESULT ShapeRange(IDWriteTextAnalyzer* analyzer, IDWriteTextAnalysisSource* source, int32_t from, int32_t to, const DWRITE_SCRIPT_ANALYSIS* scripts, const uint8_t* levels);
        HRESULT Shape(IDWriteTextAnalyzer* analyzer, int32_t start, int32_t length, uint32_t face, float emSize, const DWRITE_SCRIPT_ANALYSIS& script, uint8_t level, uint32_t style);
        HRESULT AddFace(IDWriteFontFace* face, uint32_t& index);
        HRESULT BaseFace(uint32_t& index);
        void Measure(Run& run) const;
        bool IsWordStart(int32_t position) const;

        void Break(float width);
        void Rebreak(const std::vector<Line>& oldLines, const std::vector<Segment>& oldSegments, int32_t windowStart, int32_t windowEnd, int32_t delta, int32_t runDelta);
        int32_t NextLine(int32_t start, float limit);
        void AddLine(int32_t start, int32_t end, float& y);
        void Align();

        void ClusterGlyphs(int32_t position, uint32_t& first, uint32_t& last) const;

        bool IsHardBreak(int32_t position) const;
        bool CanBreakBefore(int32_t position) const;
        float ClusterWidth(int32_t position, float x) const;

        // The x of the edges of the cluster at position, from the left of its line.
        void ClusterEdges(const Line& line, const Segment& segment, int32_t position, float& left, float& right) const;
        const Line& LineOf(int32_t offset) const;

        EditableTextFormat m_format;
        std::wstring m_text;

        // The text as the analyzer reads it: an inline object's range is object replacement
        // characters, which is how IDWriteTextLayout sees one.
        std::wstring m_analysis;

        std::vector<winrt::com_ptr<IDWriteFontFace>> m_faces;
        uint32_t m_baseFace{ UINT32_MAX };

        // The format's fallback, or the system's when it has none.
        winrt::com_ptr<IDWriteFontFallback> m_fallback;

        std::vector<EditableInline> m_inlines;
        std::vector<DWRITE_INLINE_OBJECT_METRICS> m_inlineMetrics;

        // Whether any level is not the paragraph's own, which an update does not keep track of.
        bool m_bidi{ false };
        bool m_built{ false };

        // While a cold build shapes, which is what BuildStages charges mapping and shaping to.
        bool m_timing{ false };

        // Per text position.
        std::vector<uint32_t> m_styleOf;
        std::vector<int32_t> m_inlineOf;
        std::vector<DWRITE_LINE_BREAKPOINT> m_breakpoints;
        std::vector<uint16_t> m_clusterMap;
        std::vector<uint32_t> m_runOf;
        std::vector<int32_t> m_clusterLength;
        std::vector<float> m_clusterWidth;

        // Per glyph.
        std::vector<uint16_t> m_glyphIndices;
        std::vector<float> m_glyphAdvances;
        std::vector<DWRITE_GLYPH_OFFSET> m_glyphOffsets;

        // What an update sets aside from after its window while the window is shaped, kept
        // for their capacity: made anew, they were a few allocations of the paragraph a key.
        std::vector<Run> m_tailRuns;
        std::vector<uint16_t> m_tailIndices;
        std::vector<float> m_tailAdvances;
        std::vector<DWRITE_GLYPH_OFFSET> m_tailOffsets;

        std::vector<Run> m_runs;
        std::vector<Segment> m_segments;
        std::vector<Line> m_lines;

        float m_width{ 0 };
        float m_tabStop{ 0 };

        EditableChange m_change;
    };
}
