#include "pch.h"

#include "config.h"

#include "graphic_dwrite.h"

#include <d2d1.h>
#include <dwrite.h>
#include <winrt/base.h>

#include <mutex>

using namespace std;
using namespace tex;
using winrt::check_hresult;
using winrt::com_ptr;

namespace
{

    inline D2D1_COLOR_F toD2DColor(color c)
    {
        // `color` is packed ARGB (matching the GDI+ uint32_t ctor used by the original).
        const float a = ((c >> 24) & 0xff) / 255.f;
        const float r = ((c >> 16) & 0xff) / 255.f;
        const float g = ((c >> 8) & 0xff) / 255.f;
        const float b = (c & 0xff) / 255.f;
        return D2D1::ColorF(r, g, b, a);
    }

    // ---------------------------------------------------------------------------
    // Custom font collection loader, C++/WinRT style. winrt::implements handles
    // QueryInterface/AddRef/Release; we just write the interface methods.
    // ---------------------------------------------------------------------------

    struct FileEnumerator
        : winrt::implements<FileEnumerator, IDWriteFontFileEnumerator>
    {
        FileEnumerator(IDWriteFactory* f, wstring path)
            : _factory(f), _path(std::move(path))
        {}

        HRESULT __stdcall MoveNext(BOOL* hasCurrentFile) noexcept override
        {
            try
            {
                if (++_index == 0)
                {
                    check_hresult(_factory->CreateFontFileReference(
                        _path.c_str(), nullptr, _current.put()));
                    *hasCurrentFile = TRUE;
                }
                else
                {
                    *hasCurrentFile = FALSE;
                }
                return S_OK;
            }
            catch (...)
            {
                *hasCurrentFile = FALSE;
                return winrt::to_hresult();
            }
        }

        HRESULT __stdcall GetCurrentFontFile(IDWriteFontFile** fontFile) noexcept override
        {
            if (!_current) return E_FAIL;
            _current.copy_to(fontFile);
            return S_OK;
        }

    private:
        IDWriteFactory* _factory;
        wstring _path;
        int _index = -1;
        com_ptr<IDWriteFontFile> _current;
    };

    struct CollectionLoader
        : winrt::implements<CollectionLoader, IDWriteFontCollectionLoader>
    {
        HRESULT __stdcall CreateEnumeratorFromKey(
            IDWriteFactory* factory,
            void const* collectionKey,
            UINT32 collectionKeySize,
            IDWriteFontFileEnumerator** out) noexcept override
        {
            try
            {
                if (collectionKeySize % sizeof(wchar_t) != 0) return E_INVALIDARG;
                wstring path(static_cast<const wchar_t*>(collectionKey),
                    collectionKeySize / sizeof(wchar_t));
                auto enumr = winrt::make<FileEnumerator>(factory, std::move(path));
                enumr.copy_to(out);
                return S_OK;
            }
            catch (...)
            {
                return winrt::to_hresult();
            }
        }
    };

    // One process-wide loader, registered once. Returns the underlying COM pointer
    // without bumping the refcount — DWrite holds its own ref via Register.
    IDWriteFontCollectionLoader* sharedLoader(IDWriteFactory* f)
    {
        static std::once_flag flag;
        static com_ptr<IDWriteFontCollectionLoader> loader;
        std::call_once(flag, [&] {
            loader = winrt::make<CollectionLoader>();
            check_hresult(f->RegisterFontCollectionLoader(loader.get()));
            });
        return loader.get();
    }

}  // namespace

/**************************************************************************************************/
// DWriteEnv

std::once_flag DWriteEnv::_initFlag;
com_ptr<IDWriteFactory> DWriteEnv::_dwrite;

IDWriteFactory* DWriteEnv::dwrite()
{
    std::call_once(_initFlag, [] {
        check_hresult(DWriteCreateFactory(
            DWRITE_FACTORY_TYPE_SHARED,
            __uuidof(IDWriteFactory),
            reinterpret_cast<IUnknown**>(_dwrite.put())));
        });

    return _dwrite.get();
}

/**************************************************************************************************/
// Font_dwrite

void Font_dwrite::convertStyle(int style, DWRITE_FONT_WEIGHT& w, DWRITE_FONT_STYLE& s)
{
    switch (style)
    {
    case PLAIN:
        w = DWRITE_FONT_WEIGHT_REGULAR;
        s = DWRITE_FONT_STYLE_NORMAL;
        break;
    case BOLD:
        w = DWRITE_FONT_WEIGHT_BOLD;
        s = DWRITE_FONT_STYLE_NORMAL;
        break;
    case ITALIC:
        w = DWRITE_FONT_WEIGHT_REGULAR;
        s = DWRITE_FONT_STYLE_ITALIC;
        break;
    case BOLDITALIC:
        w = DWRITE_FONT_WEIGHT_BOLD;
        s = DWRITE_FONT_STYLE_ITALIC;
        break;
    default:
        w = DWRITE_FONT_WEIGHT_REGULAR;
        s = DWRITE_FONT_STYLE_NORMAL;
        break;
    }
}

int Font_dwrite::packStyle(DWRITE_FONT_WEIGHT w, DWRITE_FONT_STYLE s)
{
    const bool bold = w >= DWRITE_FONT_WEIGHT_BOLD;
    const bool italic = s != DWRITE_FONT_STYLE_NORMAL;
    if (bold && italic) return BOLDITALIC;
    if (bold) return BOLD;
    if (italic) return ITALIC;
    return PLAIN;
}

Font_dwrite::Font_dwrite()
    : _size(0),
    _weight(DWRITE_FONT_WEIGHT_REGULAR),
    _slant(DWRITE_FONT_STYLE_NORMAL),
    _style(PLAIN)
{}

Font_dwrite::Font_dwrite(const string& name, int style, float size)
    : _size(size), _style(style)
{
    // Map abstract Serif/SansSerif → Windows defaults (mirrors GDI+ GenericSerif/SansSerif).
    if (name == "Serif")
    {
        _familyName = L"Times New Roman";
    }
    else if (name == "SansSerif")
    {
        _familyName = L"Segoe UI";
    }
    else
    {
        _familyName = utf82wide(name.c_str());
    }
    convertStyle(style, _weight, _slant);

    IDWriteFactory* f = DWriteEnv::dwrite();
    check_hresult(f->CreateTextFormat(
        _familyName.c_str(),
        nullptr,
        _weight,
        _slant,
        DWRITE_FONT_STRETCH_NORMAL,
        _size,
        L"en-us",
        _textFormat.put()));

    resolveSystemFontFace();
    if (!_fontFace)
    {
        throw ex_invalid_state("specified font style not available!");
    }
}

Font_dwrite::Font_dwrite(const string& file, float size) : _size(size)
{
    IDWriteFactory* f = DWriteEnv::dwrite();
    IDWriteFontCollectionLoader* loader = sharedLoader(f);

    wstring wfile = utf82wide(file.c_str());
    HRESULT hr = f->CreateCustomFontCollection(
        loader,
        wfile.c_str(),
        static_cast<UINT32>(wfile.size() * sizeof(wchar_t)),
        _privateCollection.put());
    if (FAILED(hr))
    {
        throw ex_invalid_state("cannot load font file " + file);
    }

    if (_privateCollection->GetFontFamilyCount() == 0)
    {
        throw ex_invalid_state("cannot load font file " + file);
    }

    com_ptr<IDWriteFontFamily> family;
    check_hresult(_privateCollection->GetFontFamily(0, family.put()));

    // Recover the family name (needed for CreateTextFormat).
    com_ptr<IDWriteLocalizedStrings> names;
    check_hresult(family->GetFamilyNames(names.put()));
    UINT32 idx = 0;
    BOOL exists = FALSE;
    names->FindLocaleName(L"en-us", &idx, &exists);
    if (!exists) idx = 0;
    UINT32 len = 0;
    check_hresult(names->GetStringLength(idx, &len));
    _familyName.resize(len);
    check_hresult(names->GetString(idx, &_familyName[0], len + 1));

    // Try regular → bold → italic → bold-italic, matching the original priority.
    struct Cand { DWRITE_FONT_WEIGHT w; DWRITE_FONT_STYLE s; int style; };
    static constexpr Cand cands[] = {
        {DWRITE_FONT_WEIGHT_REGULAR, DWRITE_FONT_STYLE_NORMAL, PLAIN},
        {DWRITE_FONT_WEIGHT_BOLD,    DWRITE_FONT_STYLE_NORMAL, BOLD},
        {DWRITE_FONT_WEIGHT_REGULAR, DWRITE_FONT_STYLE_ITALIC, ITALIC},
        {DWRITE_FONT_WEIGHT_BOLD,    DWRITE_FONT_STYLE_ITALIC, BOLDITALIC},
    };

    com_ptr<IDWriteFont> chosen;
    for (const auto& c : cands)
    {
        com_ptr<IDWriteFont> font;
        if (SUCCEEDED(family->GetFirstMatchingFont(
            c.w, DWRITE_FONT_STRETCH_NORMAL, c.s, font.put())))
        {
            // GetFirstMatchingFont never returns null; check simulations to know
            // whether DWrite faked the style. Reject fakes to mirror GDI+
            // IsStyleAvailable() semantics.
            if (font->GetSimulations() == DWRITE_FONT_SIMULATIONS_NONE)
            {
                _weight = c.w;
                _slant = c.s;
                _style = c.style;
                chosen = font;
                break;
            }
        }
    }
    if (!chosen)
    {
        throw ex_invalid_state("no available font in file " + file);
    }

    check_hresult(chosen->CreateFontFace(_fontFace.put()));

    check_hresult(f->CreateTextFormat(
        _familyName.c_str(),
        _privateCollection.get(),
        _weight,
        _slant,
        DWRITE_FONT_STRETCH_NORMAL,
        _size,
        L"en-us",
        _textFormat.put()));
}

Font_dwrite::~Font_dwrite() = default;

void Font_dwrite::resolveSystemFontFace()
{
    IDWriteFactory* f = DWriteEnv::dwrite();
    com_ptr<IDWriteFontCollection> sys;
    if (FAILED(f->GetSystemFontCollection(sys.put(), FALSE))) return;

    UINT32 idx = 0;
    BOOL exists = FALSE;
    sys->FindFamilyName(_familyName.c_str(), &idx, &exists);
    if (!exists) return;

    com_ptr<IDWriteFontFamily> family;
    if (FAILED(sys->GetFontFamily(idx, family.put()))) return;

    com_ptr<IDWriteFont> font;
    if (FAILED(family->GetFirstMatchingFont(
        _weight, DWRITE_FONT_STRETCH_NORMAL, _slant, font.put())))
    {
        return;
    }
    if (font->GetSimulations() != DWRITE_FONT_SIMULATIONS_NONE) return;
    font->CreateFontFace(_fontFace.put());
}

float Font_dwrite::getSize() const
{
    return _size;
}

float Font_dwrite::ascent() const
{
    if (!_fontFace) return 0.f;
    DWRITE_FONT_METRICS m;
    _fontFace->GetMetrics(&m);  // GetMetrics is thread-safe on an immutable face
    return _size * m.ascent / static_cast<float>(m.designUnitsPerEm);
}

sptr<Font> Font_dwrite::deriveFont(int style) const
{
    DWRITE_FONT_WEIGHT w;
    DWRITE_FONT_STYLE s;
    convertStyle(style, w, s);

    // The default ctor is private, so we can't go through make_shared/sptrOf
    // from here — construct via raw pointer and wrap.
    Font_dwrite* raw = new Font_dwrite();
    raw->_size = _size;
    raw->_familyName = _familyName;
    raw->_weight = w;
    raw->_slant = s;
    raw->_style = style;
    raw->_privateCollection = _privateCollection;

    IDWriteFactory* f = DWriteEnv::dwrite();
    HRESULT hr = f->CreateTextFormat(
        _familyName.c_str(),
        _privateCollection.get(),  // null for system fonts is fine
        w,
        s,
        DWRITE_FONT_STRETCH_NORMAL,
        _size,
        L"en-us",
        raw->_textFormat.put());
    if (FAILED(hr))
    {
        delete raw;
        throw ex_invalid_state("specified font style not available!");
    }

    // Resolve face for metrics.
    com_ptr<IDWriteFontCollection> col = _privateCollection;
    if (!col) f->GetSystemFontCollection(col.put(), FALSE);
    if (col)
    {
        UINT32 idx = 0;
        BOOL exists = FALSE;
        col->FindFamilyName(_familyName.c_str(), &idx, &exists);
        if (exists)
        {
            com_ptr<IDWriteFontFamily> family;
            col->GetFontFamily(idx, family.put());
            com_ptr<IDWriteFont> font;
            if (family && SUCCEEDED(family->GetFirstMatchingFont(
                w, DWRITE_FONT_STRETCH_NORMAL, s, font.put())))
            {
                if (font->GetSimulations() != DWRITE_FONT_SIMULATIONS_NONE)
                {
                    delete raw;
                    throw ex_invalid_state("specified font style not available!");
                }
                font->CreateFontFace(raw->_fontFace.put());
            }
        }
    }
    if (!raw->_fontFace)
    {
        delete raw;
        throw ex_invalid_state("specified font style not available!");
    }
    return sptr<Font>(raw);
}

bool Font_dwrite::operator==(const Font& ft) const
{
    const Font_dwrite& f = static_cast<const Font_dwrite&>(ft);
    return _familyName == f._familyName
        && _weight == f._weight
        && _slant == f._slant
        && _size == f._size;
}

bool Font_dwrite::operator!=(const Font& f) const
{
    return !(*this == f);
}

Font* Font::create(const string& file, float s)
{
    return new Font_dwrite(file, s);
}

sptr<Font> Font::_create(const string& name, int style, float size)
{
    return sptrOf<Font_dwrite>(name, style, size);
}

/**************************************************************************************************/
// TextLayout_dwrite

TextLayout_dwrite::TextLayout_dwrite(const wstring& src, const sptr<Font_dwrite>& font)
    : _font(font), _txt(src)
{
    IDWriteFactory* f = DWriteEnv::dwrite();
    check_hresult(f->CreateTextLayout(
        _txt.c_str(),
        static_cast<UINT32>(_txt.size()),
        _font->_textFormat.get(),
        FLT_MAX,
        FLT_MAX,
        _layout.put()));
}

void TextLayout_dwrite::getBounds(Rect& r)
{
    DWRITE_TEXT_METRICS m{};
    _layout->GetMetrics(&m);
    const float ap = _font->ascent();
    r.x = 0;
    r.y = -ap;
    r.w = m.widthIncludingTrailingWhitespace;
    r.h = m.height;
}

void TextLayout_dwrite::draw(Graphics2D& g2, float x, float y)
{
    const Font* prev = g2.getFont();
    g2.setFont(_font.get());
    g2.drawText(_txt, x, y);
    g2.setFont(prev);
}

sptr<TextLayout> TextLayout::create(const wstring& src, const sptr<Font>& font)
{
    sptr<Font_dwrite> f = static_pointer_cast<Font_dwrite>(font);
    return sptrOf<TextLayout_dwrite>(src, f);
}

/**************************************************************************************************/
// Graphics2D_dwrite

const Font* Graphics2D_dwrite::defaultFont()
{
    // Constructed once, kept alive for the lifetime of the process. The Font_dwrite
    // ctor depends on DWriteEnv being initialized, which it will be by the time
    // anything calls this.
    static std::once_flag flag;
    static Font* font = nullptr;
    std::call_once(flag, [] {
        font = new Font_dwrite("Arial", PLAIN, 72.f);
        });
    return font;
}

Graphics2D_dwrite::Graphics2D_dwrite(ID2D1RenderTarget* rt)
    : _color(black), _font(defaultFont()), _rt(rt), _sx(1.f), _sy(1.f)
{
    _rt->GetFactory(_factory.put());

    // What the target was already drawing with is the base everything here composes onto: a
    // caller that is drawing scaled and offset - a text layout into a composition surface -
    // has put the formula's place in the line into that transform.
    _rt->GetTransform(&_base);
    _savedAntialias = _rt->GetAntialiasMode();
    _savedTextAntialias = _rt->GetTextAntialiasMode();

    _xform = D2D1::Matrix3x2F::Identity();
    applyTransform();

    _rt->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
    _rt->SetAntialiasMode(D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);

    check_hresult(_rt->CreateSolidColorBrush(toD2DColor(_color), _brush.put()));
    rebuildStrokeStyle();
}

Graphics2D_dwrite::~Graphics2D_dwrite()
{
    _rt->SetTransform(_base);
    _rt->SetAntialiasMode(_savedAntialias);
    _rt->SetTextAntialiasMode(_savedTextAntialias);
}

void Graphics2D_dwrite::applyTransform()
{
    _rt->SetTransform(_xform * _base);
}

void Graphics2D_dwrite::rebuildStrokeStyle()
{
    D2D1_CAP_STYLE cap;
    switch (_stroke.cap)
    {
    case CAP_ROUND:  cap = D2D1_CAP_STYLE_ROUND;    break;
    case CAP_SQUARE: cap = D2D1_CAP_STYLE_SQUARE;   break;
    case CAP_BUTT:
    default:         cap = D2D1_CAP_STYLE_FLAT;     break;
    }
    D2D1_LINE_JOIN join;
    switch (_stroke.join)
    {
    case JOIN_BEVEL: join = D2D1_LINE_JOIN_BEVEL;   break;
    case JOIN_ROUND: join = D2D1_LINE_JOIN_ROUND;   break;
    case JOIN_MITER:
    default:         join = D2D1_LINE_JOIN_MITER;   break;
    }
    const D2D1_STROKE_STYLE_PROPERTIES props = D2D1::StrokeStyleProperties(
        cap, cap, D2D1_CAP_STYLE_ROUND,
        join, _stroke.miterLimit < 1.f ? 1.f : _stroke.miterLimit,
        D2D1_DASH_STYLE_SOLID, 0.f);

    _strokeStyle = nullptr;
    check_hresult(_factory->CreateStrokeStyle(props, nullptr, 0, _strokeStyle.put()));
}

void Graphics2D_dwrite::setColor(color color)
{
    _color = color;
    _brush->SetColor(toD2DColor(color));
}

color Graphics2D_dwrite::getColor() const
{
    return _color;
}

void Graphics2D_dwrite::setStroke(const Stroke& s)
{
    _stroke = s;
    rebuildStrokeStyle();
}

const Stroke& Graphics2D_dwrite::getStroke() const
{
    return _stroke;
}

void Graphics2D_dwrite::setStrokeWidth(float w)
{
    _stroke.lineWidth = w;
    // Width is passed per draw call; no need to rebuild stroke style.
}

const Font* Graphics2D_dwrite::getFont() const
{
    return _font;
}

void Graphics2D_dwrite::setFont(const Font* font)
{
    _font = font;
}

void Graphics2D_dwrite::translate(float dx, float dy)
{
    _xform = D2D1::Matrix3x2F::Translation(dx, dy) * _xform;
    applyTransform();
}

void Graphics2D_dwrite::scale(float sx, float sy)
{
    _sx *= sx;
    _sy *= sy;
    _xform = D2D1::Matrix3x2F::Scale(sx, sy) * _xform;
    applyTransform();
}

void Graphics2D_dwrite::rotate(float angle)
{
    _xform = D2D1::Matrix3x2F::Rotation(angle / PI * 180.f) * _xform;
    applyTransform();
}

void Graphics2D_dwrite::rotate(float angle, float px, float py)
{
    _xform = D2D1::Matrix3x2F::Rotation(angle / PI * 180.f, D2D1::Point2F(px, py)) * _xform;
    applyTransform();
}

void Graphics2D_dwrite::reset()
{
    _xform = D2D1::Matrix3x2F::Identity();
    _sx = _sy = 1.f;
    applyTransform();
}

float Graphics2D_dwrite::sx() const { return _sx; }
float Graphics2D_dwrite::sy() const { return _sy; }

void Graphics2D_dwrite::drawChar(wchar_t c, float x, float y)
{
    wchar_t str[]{ c, L'\0' };
    drawText(str, x, y);
}

void Graphics2D_dwrite::drawText(const wstring& c, float x, float y)
{
    const Font_dwrite* f = static_cast<const Font_dwrite*>(_font);
    const float ap = f->ascent();

    IDWriteFactory* dw = DWriteEnv::dwrite();
    com_ptr<IDWriteTextLayout> layout;
    HRESULT hr = dw->CreateTextLayout(
        c.c_str(),
        static_cast<UINT32>(c.size()),
        f->_textFormat.get(),
        FLT_MAX, FLT_MAX,
        layout.put());
    if (FAILED(hr)) return;

    // (x, y) is the baseline origin in caller coords; shift up by ascent to get
    // the top-left origin that DrawTextLayout expects.
    _rt->DrawTextLayout(D2D1::Point2F(x, y - ap), layout.get(), _brush.get(),
        D2D1_DRAW_TEXT_OPTIONS_NONE);
}

void Graphics2D_dwrite::drawLine(float x1, float y1, float x2, float y2)
{
    _rt->DrawLine(D2D1::Point2F(x1, y1), D2D1::Point2F(x2, y2), _brush.get(),
        _stroke.lineWidth, _strokeStyle.get());
}

void Graphics2D_dwrite::drawRect(float x, float y, float w, float h)
{
    _rt->DrawRectangle(D2D1::RectF(x, y, x + w, y + h), _brush.get(),
        _stroke.lineWidth, _strokeStyle.get());
}

void Graphics2D_dwrite::fillRect(float x, float y, float w, float h)
{
    _rt->FillRectangle(D2D1::RectF(x, y, x + w, y + h), _brush.get());
}

void Graphics2D_dwrite::drawRoundRect(float x, float y, float w, float h, float rx, float ry)
{
    _rt->DrawRoundedRectangle(
        D2D1::RoundedRect(D2D1::RectF(x, y, x + w, y + h), rx, ry),
        _brush.get(), _stroke.lineWidth, _strokeStyle.get());
}

void Graphics2D_dwrite::fillRoundRect(float x, float y, float w, float h, float rx, float ry)
{
    _rt->FillRoundedRectangle(
        D2D1::RoundedRect(D2D1::RectF(x, y, x + w, y + h), rx, ry),
        _brush.get());
}
