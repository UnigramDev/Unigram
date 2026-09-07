#include "pch.h"
#include "RichMathSurface.h"
#if __has_include("RichMathSurface.g.cpp")
#include "RichMathSurface.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    std::mutex RichMathSurface::s_parseMutex;

    RichMathSurface::RichMathSurface(hstring formula)
    {
        std::lock_guard guard(s_parseMutex);

        // Idempotent, but it builds the same globals the parse then uses, so it happens under
        // the same lock.
        tex::LaTeX::initBundled();

        // Throws on an expression it cannot make sense of, which is how the caller learns this
        // was not a formula: the projection turns it into the exception it catches.
        //
        // The colour is the one every draw overrides, and it is laid out at LayoutWidth
        // whatever the caller has room for - a formula wider than that scrolls, it does not
        // reflow.
        m_render.reset(tex::LaTeX::parse(formula.c_str(), LayoutWidth, TextSize, LineSpace, 0xff000000));

        winrt::check_pointer(m_render.get());

        m_pixelWidth = m_render->getWidth();
        m_pixelHeight = m_render->getHeight();
        m_baseline = m_render->getBaseline();
    }

    void RichMathSurface::Close()
    {
        m_render.reset();
    }

    int32_t RichMathSurface::PixelWidth()
    {
        return m_pixelWidth;
    }

    int32_t RichMathSurface::PixelHeight()
    {
        return m_pixelHeight;
    }

    float RichMathSurface::Baseline()
    {
        return m_baseline;
    }

    void RichMathSurface::Draw(ID2D1RenderTarget* target, float x, float y, winrt::Windows::UI::Color foreground)
    {
        if (m_render == nullptr || target == nullptr)
        {
            return;
        }

        // Composes onto the transform the target is in, and puts it back when it goes out of
        // scope. Where the formula goes is a translation rather than the x/y the renderer
        // takes, which are whole pixels: in a line of text the position rarely is one.
        tex::Graphics2D_dwrite g2(target);
        g2.translate(x, y);

        m_render->setForeground(
            ((uint32_t)foreground.A << 24) | ((uint32_t)foreground.R << 16) | ((uint32_t)foreground.G << 8) | foreground.B);
        m_render->draw(g2, 0, 0);
    }
}
