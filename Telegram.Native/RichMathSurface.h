#pragma once

#include "RichMathSurface.g.h"

#include "latex.h"
#include "graphic_dwrite.h"

#include <mutex>

namespace winrt::Telegram::Native::implementation
{
    struct RichMathSurface : RichMathSurfaceT<RichMathSurface>
    {
        // Parses the expression, and throws when it is not one.
        RichMathSurface(hstring formula);

        // The size the expression laid out to, in DIPs, and where its baseline sits in it as a
        // fraction of the height. Known from the parse, so a caller can give the formula its
        // place in a line before anything is drawn.
        int32_t PixelWidth();
        int32_t PixelHeight();
        float Baseline();

        // Draws at x/y in the target's own coordinates, composed onto whatever transform it
        // is already drawing with, and leaves it as it was found. Not projected: this is how a
        // native renderer that has a context open - a text layout with a formula in a line -
        // draws one without a bitmap in between. The caller is inside its own BeginDraw.
        void Draw(ID2D1RenderTarget* target, float x, float y, winrt::Windows::UI::Color foreground);

        // The box tree of a parsed formula is not small, and this is held by an element that
        // outlives the formula it shows, so it is released rather than waited for.
        void Close();

    private:
        // What the expression is laid out for, in DIPs: the width a line wraps at, and the
        // size of the glyphs. It is parsed once at this size and drawn scaled, so neither has
        // anything to do with the rasterization scale.
        static constexpr int LayoutWidth = 600;
        static constexpr float TextSize = 18;
        static constexpr float LineSpace = TextSize * 0.25f;

        // The parser keeps what it is doing in globals - the formula being built, the macros
        // and colours a formula has defined - and every view in this app runs on its own
        // thread, so one formula is parsed at a time.
        static std::mutex s_parseMutex;

        std::unique_ptr<tex::TeXRender> m_render;

        // Read after Close, so they are the object's own rather than the render's.
        int32_t m_pixelWidth{ 0 };
        int32_t m_pixelHeight{ 0 };
        float m_baseline{ 0 };
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct RichMathSurface : RichMathSurfaceT<RichMathSurface, implementation::RichMathSurface>
    {
    };
}
