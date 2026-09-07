#pragma once

#include "config.h"

#include "common.h"
#include "graphic/graphic.h"

#include <d2d1.h>
#include <dwrite.h>
#include <winrt/base.h>

#include <mutex>

namespace tex
{

    /**
     * The DirectWrite factory the fonts are made from. Safe to call from any thread,
     * and there is nothing to inject: DWRITE_FACTORY_TYPE_SHARED hands out the same
     * object to everyone in the process, so this is the host's factory as well.
     *
     * There is deliberately no Direct2D factory here. A stroke style belongs to the
     * factory that made it and cannot be used by a target from another, and there is
     * more than one in this app - one per Direct2DDevice, which is one per view. What
     * to draw with therefore comes from the target being drawn into, not from here.
     */
    class DWriteEnv
    {
    public:
        static IDWriteFactory* dwrite();

    private:
        static std::once_flag _initFlag;
        static winrt::com_ptr<IDWriteFactory> _dwrite;
    };

    /**************************************************************************************************/

    /**
     * Immutable after construction. Safe to share across threads.
     */
    class Font_dwrite : public Font
    {
    private:
        float _size;
        std::wstring _familyName;
        DWRITE_FONT_WEIGHT _weight;
        DWRITE_FONT_STYLE _slant;

        // Owns the family for private-file fonts. Immutable after ctor.
        winrt::com_ptr<IDWriteFontCollection> _privateCollection;

        Font_dwrite();

    public:
        int _style;  // PLAIN/BOLD/ITALIC/BOLDITALIC, kept for parity with caller code
        winrt::com_ptr<IDWriteTextFormat> _textFormat;
        winrt::com_ptr<IDWriteFontFace> _fontFace;  // for metrics

        Font_dwrite(const std::string& name, int style, float size);
        Font_dwrite(const std::string& file, float size);

        virtual float getSize() const override;
        virtual sptr<Font> deriveFont(int style) const override;
        virtual bool operator==(const Font& f) const override;
        virtual bool operator!=(const Font& f) const override;
        virtual ~Font_dwrite();

        // Ascent in DIPs at the current size. Thread-safe (reads immutable face metrics).
        float ascent() const;

        static void convertStyle(int style, DWRITE_FONT_WEIGHT& w, DWRITE_FONT_STYLE& s);
        static int packStyle(DWRITE_FONT_WEIGHT w, DWRITE_FONT_STYLE s);

    private:
        void resolveSystemFontFace();
    };

    /**************************************************************************************************/

    /**
     * Immutable after construction. The underlying IDWriteTextLayout is built once
     * in the ctor and only read via GetMetrics() / DrawTextLayout afterwards, which
     * DirectWrite documents as safe for concurrent readers.
     */
    class TextLayout_dwrite : public TextLayout
    {
    private:
        sptr<Font_dwrite> _font;
        std::wstring _txt;
        winrt::com_ptr<IDWriteTextLayout> _layout;

    public:
        TextLayout_dwrite(const std::wstring& src, const sptr<Font_dwrite>& font);

        virtual void getBounds(Rect& bounds) override;
        virtual void draw(Graphics2D& g2, float x, float y) override;

        IDWriteTextLayout* raw() const { return _layout.get(); }
    };

    /**************************************************************************************************/

    /**
     * NOT thread-safe by itself. Each Graphics2D_dwrite instance must be used from
     * a single thread at a time (the same contract as ID2D1RenderTarget). Multiple
     * instances on different threads are fine as long as the factory behind the
     * targets is multi-threaded.
     *
     * The render target is borrowed rather than owned, and everything this needs comes
     * from it: the factory its resources are made by, the transform its own compose
     * onto, and the antialiasing and transform it is put back into when this goes out
     * of scope. A target that draws more than this -- the context of a composition
     * surface, drawing a formula in a line of text -- is left as it was found.
     */
    class Graphics2D_dwrite : public Graphics2D
    {
    private:
        static const Font* defaultFont();

        color _color;
        const Font* _font;
        Stroke _stroke;

        // Borrowed; not released by us.
        ID2D1RenderTarget* _rt;

        // The target's own, so that a stroke style is made by the factory that will draw it.
        winrt::com_ptr<ID2D1Factory> _factory;

        winrt::com_ptr<ID2D1SolidColorBrush> _brush;
        winrt::com_ptr<ID2D1StrokeStyle> _strokeStyle;

        D2D1::Matrix3x2F _xform;
        float _sx, _sy;

        // What the target was drawing with when this took it over: the formula's own
        // transform composes onto it, and it is what the target is put back into.
        D2D1::Matrix3x2F _base;
        D2D1_ANTIALIAS_MODE _savedAntialias;
        D2D1_TEXT_ANTIALIAS_MODE _savedTextAntialias;

        void rebuildStrokeStyle();
        void applyTransform();

    public:
        explicit Graphics2D_dwrite(ID2D1RenderTarget* rt);
        ~Graphics2D_dwrite();

        virtual void setColor(color c) override;
        virtual color getColor() const override;

        virtual void setStroke(const Stroke& s) override;
        virtual const Stroke& getStroke() const override;
        virtual void setStrokeWidth(float w) override;

        virtual const Font* getFont() const override;
        virtual void setFont(const Font* font) override;

        virtual void translate(float dx, float dy) override;
        virtual void scale(float sx, float sy) override;
        virtual void rotate(float angle) override;
        virtual void rotate(float angle, float px, float py) override;
        virtual void reset() override;

        virtual float sx() const override;
        virtual float sy() const override;

        virtual void drawChar(wchar_t c, float x, float y) override;
        virtual void drawText(const std::wstring& c, float x, float y) override;
        virtual void drawLine(float x1, float y1, float x2, float y2) override;
        virtual void drawRect(float x, float y, float w, float h) override;
        virtual void fillRect(float x, float y, float w, float h) override;
        virtual void drawRoundRect(float x, float y, float w, float h, float rx, float ry) override;
        virtual void fillRoundRect(float x, float y, float w, float h, float rx, float ry) override;

        ID2D1RenderTarget* rt() const { return _rt; }
        ID2D1SolidColorBrush* brush() const { return _brush.get(); }
    };

}  // namespace tex
