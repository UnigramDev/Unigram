#include "pch.h"
#include "DirectTextLayout.h"
#if __has_include("DirectTextLayout.g.cpp")
#include "DirectTextLayout.g.cpp"
#endif

#include "Helpers\COMHelper.h"
#include "RichMathSurface.h"

#include <cmath>
#include <format>
#include <mutex>

#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.System.UserProfile.h>

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

    namespace
    {
        // What the layout spends, in QueryPerformanceCounter ticks - the clock Stopwatch reads
        // on the managed side, so the two sets of numbers on the diagnostics page are in the
        // same units. Written from every view's thread without interlocks, for the reason
        // TextThroughput gives: a meter is not worth a locked bus cycle per draw.
        struct Counter
        {
            int64_t Calls;
            int64_t Ticks;
            int64_t Peak;
        };

        Counter s_build;
        Counter s_reflow;
        Counter s_extent;
        Counter s_surfaceCreate;

        // Draws that found the surface they already had big enough, which is what the rounding
        // up is for, and the ones that gave it back because it was not.
        int64_t s_surfaceKept;
        int64_t s_surfaceDropped;
        Counter s_beginDraw;
        Counter s_drawText;
        Counter s_endDraw;

        // The analyzer's share of a build, and its updates checked against cold builds. The
        // last difference is a string, so it alone is written under a lock.
        Counter s_analyzer;
        bool s_verifyAnalyzer;
        int64_t s_verified;
        int64_t s_mismatched;
        std::mutex s_mismatchMutex;
        std::wstring s_mismatch;

        int64_t Now()
        {
            LARGE_INTEGER value;
            QueryPerformanceCounter(&value);

            return value.QuadPart;
        }

        void Record(Counter& counter, int64_t started)
        {
            const auto elapsed = Now() - started;

            counter.Calls++;
            counter.Ticks += elapsed;

            if (elapsed > counter.Peak)
            {
                counter.Peak = elapsed;
            }
        }

        // What a surface is made in whole steps of, and how far past the text one is allowed to
        // be before it is worth resizing. Both in pixels: a line of text is around 20 of them
        // tall, so a step of 32 covers most of what a redraw asks for.
        constexpr int32_t c_surfaceStep = 32;

        int32_t Grow(int32_t value)
        {
            return ((value + c_surfaceStep - 1) / c_surfaceStep) * c_surfaceStep;
        }

        // Whether a surface this big is worth keeping for a text that needs this much. Twice
        // the pixels plus a fixed allowance, because the two vary in different ways: a block is
        // as wide as the bubble it is in and stays that way, while its height is a line or
        // twenty. Without the allowance every one-line message after a long one resizes; with
        // it, a block holds at most half a megabyte it is not using, and only until it is
        // handed something long again.
        constexpr int64_t c_surfaceSlack = 32768;

        // An edited text's surface spans its box up to this many pixels, past which the box is
        // the stand-in for an unbounded measure rather than a width anything is shown at.
        constexpr double c_editingMaxWidth = 8192;

        // And takes at least this much more height than the text, in DIPs: a few lines.
        constexpr double c_editingSpareHeight = 64;

        // An edited text taller than this many pixels is drawn into a virtual surface instead,
        // in tiles, and only those in the viewport and a margin around it: a surface that
        // holds the whole paragraph is drawn whole on every key, and past the largest a surface
        // can be - 40,000 pixels for 100,000 characters - is never drawn at all.
        constexpr int32_t c_virtualHeight = 2048;
        constexpr int32_t c_tileHeight = 256;
        constexpr int32_t c_tileMargin = 512;
        constexpr int32_t c_virtualMaxHeight = 1 << 24;

        bool Keep(Windows::Graphics::SizeInt32 size, Windows::Graphics::SizeInt32 pixels)
        {
            if (size.Width < pixels.Width || size.Height < pixels.Height)
            {
                return false;
            }

            const int64_t have = (int64_t)size.Width * size.Height;
            const int64_t used = (int64_t)pixels.Width * pixels.Height;

            return have <= used * 2 + c_surfaceSlack;
        }

        void Append(std::wstring& text, const wchar_t* name, const Counter& counter, int64_t frequency)
        {
            if (counter.Calls == 0)
            {
                text += std::format(L"{}: nothing yet\n", name);
                return;
            }

            const double seconds = counter.Ticks / (double)frequency;

            text += std::format(L"{}: {} calls, {:.1f} \u00b5s each, {:.1f} \u00b5s peak, {:.2f}s total\n",
                name, counter.Calls, seconds * 1000000 / counter.Calls,
                counter.Peak / (double)frequency * 1000000, seconds);
        }
    }

    hstring DirectTextLayout::Counters()
    {
        LARGE_INTEGER frequency;
        QueryPerformanceFrequency(&frequency);

        std::wstring text;

        Append(text, L"Build", s_build, frequency.QuadPart);
        Append(text, L"Reflow", s_reflow, frequency.QuadPart);
        Append(text, L"Extent", s_extent, frequency.QuadPart);
        Append(text, L"Surface create", s_surfaceCreate, frequency.QuadPart);

        text += std::format(L"Surface kept: {}, dropped: {}\n", s_surfaceKept, s_surfaceDropped);
        Append(text, L"BeginDraw", s_beginDraw, frequency.QuadPart);
        Append(text, L"DrawTextLayout", s_drawText, frequency.QuadPart);
        Append(text, L"EndDraw", s_endDraw, frequency.QuadPart);

        if (s_analyzer.Calls > 0)
        {
            Append(text, L"Analyzer layout", s_analyzer, frequency.QuadPart);
        }

        const auto& stages = EditableTextLayout::BuildStages;
        if (stages.Builds > 0)
        {
            const auto each = [&](int64_t ticks)
                {
                    return ticks / (double)frequency.QuadPart * 1000000 / stages.Builds;
                };

            text += std::format(L"Analyzer cold builds: {}, µs each: preparing {:.1f}, setting up {:.1f}, analysing {:.1f}, mapping fonts {:.1f}, shaping {:.1f}, breaking lines {:.1f}; {:.1f} runs each\n",
                stages.Builds, each(stages.Prepare), each(stages.Setup), each(stages.Analysis), each(stages.Mapping), each(stages.Shaping), each(stages.Breaking), stages.Runs / (double)stages.Builds);
        }

        const auto& updates = EditableTextLayout::UpdateStages;
        if (updates.Updates > 0)
        {
            const auto each = [&](int64_t ticks)
                {
                    return ticks / (double)frequency.QuadPart * 1000000 / updates.Updates;
                };

            text += std::format(L"Analyzer updates: {}, {} told their edit, µs each: preparing {:.1f}, finding the edit {:.1f}, splicing {:.1f}, analysing {:.1f}, shaping {:.1f}, putting back the tail {:.1f}, breaking lines {:.1f}\n",
                updates.Updates, updates.Told, each(updates.Prepare), each(updates.Find), each(updates.Splice), each(updates.Analysis), each(updates.Shaping), each(updates.Tail), each(updates.Breaking));
        }

        if (s_verified > 0)
        {
            std::lock_guard const guard(s_mismatchMutex);
            text += std::format(L"Analyzer updates checked: {}, unlike a cold build: {}{}{}\n", s_verified, s_mismatched, s_mismatch.empty() ? L"" : L", the last at ", s_mismatch);
        }

        return hstring(text);
    }

    bool DirectTextLayout::VerifyAnalyzer()
    {
        return s_verifyAnalyzer;
    }

    void DirectTextLayout::VerifyAnalyzer(bool value)
    {
        s_verifyAnalyzer = value;
    }

    void DirectTextLayout::ResetCounters()
    {
        s_build = {};
        s_reflow = {};
        s_extent = {};
        s_surfaceCreate = {};
        s_surfaceKept = 0;
        s_surfaceDropped = 0;
        s_beginDraw = {};
        s_drawText = {};
        s_endDraw = {};
        s_analyzer = {};
        EditableTextLayout::BuildStages = {};
        EditableTextLayout::UpdateStages = {};
        s_verified = 0;
        s_mismatched = 0;

        std::lock_guard const guard(s_mismatchMutex);
        s_mismatch.clear();
    }

    // Which language a Han character belongs to cannot be read off the character: simplified
    // Chinese, traditional Chinese, Japanese and Korean share code points and not glyphs, so
    // the font DirectWrite falls back to is chosen from the locale the text is laid out for.
    // Given none, it falls back in a fixed order - which is what showed a Chinese reader his
    // messages in a Japanese font.
    //
    // XAML does not answer this with the system language. It expands the element's language
    // into a fallback language LIST (Mui_GetFontFallbackLanguageList) and hands DirectWrite
    // that list, so the languages the user has added decide. There is no public API for the
    // list and a text layout takes a single locale name, so the choice is made here instead:
    // the first of the user's languages that reads the script in front of us, and the one
    // everything else is laid out for when none of them does. For the reader this came from -
    // Windows in English, Chinese among his languages - that is en-US for Latin and his own
    // Chinese for Han.
    struct UserLocales
    {
        std::wstring Primary;
        std::wstring EastAsian;
    };

    static bool IsEastAsian(std::wstring_view language)
    {
        return language.starts_with(L"zh") || language.starts_with(L"ja") || language.starts_with(L"ko");
    }

    // Kana, Han and Hangul, and the ideographs above the BMP through the high surrogates that
    // lead them. Punctuation shared with Latin is deliberately not in here: a locale is worth
    // choosing for the script the text is in, not for the comma in it.
    static bool IsEastAsianText(const wchar_t* text, int32_t length)
    {
        for (int32_t i = 0; i < length; i++)
        {
            const wchar_t c = text[i];

            if ((c >= 0x3040 && c <= 0x9FFF) || (c >= 0xAC00 && c <= 0xD7AF)
                || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xD840 && c <= 0xD87F))
            {
                return true;
            }
        }

        return false;
    }

    static const UserLocales& Locales()
    {
        static UserLocales locales;
        static std::once_flag once;

        std::call_once(once, []
            {
                try
                {
                    // The languages the user has added, in the order they prefer them - the
                    // same list the font fallback list above is built from.
                    for (const hstring& language : Windows::System::UserProfile::GlobalizationPreferences::Languages())
                    {
                        if (locales.Primary.empty())
                        {
                            locales.Primary = language;
                        }

                        if (locales.EastAsian.empty() && IsEastAsian(language))
                        {
                            locales.EastAsian = language;
                        }
                    }
                }
                catch (...)
                {
                    // A user with no language preferences at all is not a case worth having a
                    // path for, but this runs before anything is drawn.
                }

                if (locales.Primary.empty())
                {
                    wchar_t locale[LOCALE_NAME_MAX_LENGTH]{};
                    locales.Primary = GetUserDefaultLocaleName(locale, LOCALE_NAME_MAX_LENGTH) > 0 ? locale : L"en-us";
                }
            });

        return locales;
    }

    FontChains::FontChains(winrt::com_ptr<IDWriteFactory> factory, winrt::com_ptr<IDWriteFontCollection> systemCollection, std::wstring root, std::wstring monospaceFamily)
        : m_factory(factory.try_as<IDWriteFactory5>())
        , m_systemCollection(systemCollection)
        , m_root(std::move(root))
        , m_monospaceFamily(std::move(monospaceFamily))
    {
    }

    const ResolvedFontFamily* FontChains::Resolve(std::wstring_view chain) noexcept
    {
        try
        {
            std::lock_guard const guard(m_mutex);

            std::wstring key(chain);

            auto found = m_chains.find(key);
            if (found == m_chains.end())
            {
                ResolvedFontFamily resolved;

                // A chain that failed is remembered as one, so it is not built again on every
                // text that asks for it.
                if (m_factory == nullptr || FAILED(Build(chain, resolved)))
                {
                    resolved = ResolvedFontFamily();
                }

                found = m_chains.emplace(std::move(key), std::move(resolved)).first;
            }

            return found->second.Family.empty() ? nullptr : &found->second;
        }
        catch (...)
        {
            return nullptr;
        }
    }

    static std::wstring_view Trim(std::wstring_view value)
    {
        while (!value.empty() && (value.front() == L' ' || value.front() == L'\'' || value.front() == L'"'))
        {
            value.remove_prefix(1);
        }

        while (!value.empty() && (value.back() == L' ' || value.back() == L'\'' || value.back() == L'"'))
        {
            value.remove_suffix(1);
        }

        return value;
    }

    static bool HasFamily(IDWriteFontCollection* collection, const std::wstring& family)
    {
        UINT32 index;
        BOOL exists = FALSE;

        return SUCCEEDED(collection->FindFamilyName(family.c_str(), &index, &exists)) && exists;
    }

    // What XAML does with a family: each character in the first font of the chain that has it,
    // then the system's choice. DirectWrite gets the first name that exists as the family, and
    // each later one as a mapping over all of Unicode - a font without the character passes it
    // on to the next mapping, and the system fallback goes last.
    //
    // Every mapping is keyed to the family it falls back from, because a fallback belongs to the
    // whole layout while a code span names a family of its own: keyed, the code span skips the
    // text's chain. It gets one of its own instead, the text's chain after it, which is what the
    // theme's monospace family is.
    HRESULT FontChains::Build(std::wstring_view chain, ResolvedFontFamily& resolved)
    {
        HRESULT result;

        struct Entry
        {
            std::wstring Family;
            winrt::com_ptr<IDWriteFontCollection> Collection;
        };

        std::vector<Entry> entries;

        while (!chain.empty())
        {
            const auto comma = chain.find(L',');
            const auto name = Trim(chain.substr(0, comma));

            chain = comma == std::wstring_view::npos ? std::wstring_view() : chain.substr(comma + 1);

            if (name.empty())
            {
                continue;
            }

            Entry entry;

            // ms-appx:///Assets/Emoji/apple.ttf#Segoe UI Emoji
            const auto hash = name.find(L'#');
            if (hash != std::wstring_view::npos)
            {
                entry.Collection = Packaged(name.substr(0, hash));
                entry.Family = Trim(name.substr(hash + 1));

                if (entry.Collection == nullptr)
                {
                    continue;
                }

                // A font's name in its file is not always the one XAML is given - a weight in
                // it, say - and a file with one family in it can only mean that one.
                if (!HasFamily(entry.Collection.get(), entry.Family) && entry.Collection->GetFontFamilyCount() == 1)
                {
                    winrt::com_ptr<IDWriteFontFamily> family;
                    winrt::com_ptr<IDWriteLocalizedStrings> names;
                    WCHAR buffer[LF_FACESIZE]{};

                    if (SUCCEEDED(entry.Collection->GetFontFamily(0, family.put())) && SUCCEEDED(family->GetFamilyNames(names.put())) && SUCCEEDED(names->GetString(0, buffer, ARRAYSIZE(buffer))))
                    {
                        entry.Family = buffer;
                    }
                }
            }
            else
            {
                // XAML's own default, which it resolves to the UI font.
                entry.Family = name == L"XamlAutoFontFamily" ? L"Segoe UI" : std::wstring(name);
                entry.Collection = m_systemCollection;
            }

            if (!HasFamily(entry.Collection.get(), entry.Family))
            {
                continue;
            }

            const auto duplicate = std::any_of(entries.begin(), entries.end(), [&](const Entry& other)
                {
                    return other.Family == entry.Family && other.Collection == entry.Collection;
                });

            if (!duplicate)
            {
                entries.push_back(std::move(entry));
            }
        }

        if (entries.empty())
        {
            return E_FAIL;
        }

        resolved.Family = entries[0].Family;
        resolved.Collection = entries[0].Collection;

        const auto monospace = !m_monospaceFamily.empty() && entries[0].Family != m_monospaceFamily;
        if (entries.size() == 1 && !monospace)
        {
            return S_OK;
        }

        winrt::com_ptr<IDWriteFontFallbackBuilder> builder;
        ReturnIfFailed(result, m_factory->CreateFontFallbackBuilder(builder.put()));

        const DWRITE_UNICODE_RANGE all{ 0, 0x10FFFF };

        for (size_t i = 0; i < entries.size(); i++)
        {
            const wchar_t* family = entries[i].Family.c_str();

            if (i > 0)
            {
                ReturnIfFailed(result, builder->AddMapping(&all, 1, &family, 1, entries[i].Collection.get(), nullptr, resolved.Family.c_str(), 1));
            }

            if (monospace && entries[i].Family != m_monospaceFamily)
            {
                ReturnIfFailed(result, builder->AddMapping(&all, 1, &family, 1, entries[i].Collection.get(), nullptr, m_monospaceFamily.c_str(), 1));
            }
        }

        winrt::com_ptr<IDWriteFontFallback> system;
        ReturnIfFailed(result, m_factory->GetSystemFontFallback(system.put()));
        ReturnIfFailed(result, builder->AddMappings(system.get()));
        ReturnIfFailed(result, builder->CreateFontFallback(resolved.Fallback.put()));

        return S_OK;
    }

    // A font file in the package, by the path XAML names it with. Null, and remembered as null,
    // when there is no such file.
    winrt::com_ptr<IDWriteFontCollection> FontChains::Packaged(std::wstring_view path)
    {
        std::wstring key(path);

        auto found = m_files.find(key);
        if (found != m_files.end())
        {
            return found->second;
        }

        winrt::com_ptr<IDWriteFontCollection> collection;

        for (std::wstring_view prefix : { L"ms-appx:///", L"ms-appx://", L"/" })
        {
            if (path.size() >= prefix.size() && CompareStringOrdinal(path.data(), (int)prefix.size(), prefix.data(), (int)prefix.size(), TRUE) == CSTR_EQUAL)
            {
                path.remove_prefix(prefix.size());
                break;
            }
        }

        std::wstring file = m_root + L"\\" + std::wstring(path);
        std::replace(file.begin(), file.end(), L'/', L'\\');

        winrt::com_ptr<IDWriteFontSetBuilder1> builder;
        winrt::com_ptr<IDWriteFontFile> reference;
        winrt::com_ptr<IDWriteFontSet> set;
        winrt::com_ptr<IDWriteFontCollection1> created;

        if (SUCCEEDED(m_factory->CreateFontSetBuilder(builder.put()))
            && SUCCEEDED(m_factory->CreateFontFileReference(file.c_str(), nullptr, reference.put()))
            && SUCCEEDED(builder->AddFontFile(reference.get()))
            && SUCCEEDED(builder->CreateFontSet(set.put()))
            && SUCCEEDED(m_factory->CreateFontCollectionFromFontSet(set.get(), created.put())))
        {
            collection.copy_from(created.get());
        }

        m_files.emplace(std::move(key), collection);
        return collection;
    }

    DirectTextLayout::DirectTextLayout(CompositionGraphicsDevice device, winrt::com_ptr<IDWriteFactory> factory, winrt::com_ptr<IDWriteFontCollection> fontCollection, winrt::com_ptr<IDWriteFontCollection> systemCollection, hstring monospaceFamily, std::shared_ptr<FontChains> chains)
        : m_device(device)
        , m_factory(factory)
        , m_fontCollection(fontCollection)
        , m_systemCollection(systemCollection)
        , m_monospaceFamily(monospaceFamily)
        , m_chains(std::move(chains))
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
        m_virtualSurface = nullptr;
        m_virtual = false;

        m_paragraphs.clear();
        m_editable = nullptr;
        m_previous = nullptr;
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
            m_resolvedFamily = value.empty() || m_chains == nullptr ? nullptr : m_chains->Resolve(value);
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
        if (m_editable != nullptr)
        {
            return m_editable->LineCount();
        }

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

    bool DirectTextLayout::IsReadOnly()
    {
        return m_readOnly;
    }

    // Only how the surface is sized: the text is laid out the same either way, unless the
    // analyzer lays out what is edited.
    void DirectTextLayout::IsReadOnly(bool value)
    {
        if (m_readOnly != value)
        {
            m_readOnly = value;
            m_invalid |= m_useAnalyzer;
        }
    }

    bool DirectTextLayout::UseAnalyzer()
    {
        return m_useAnalyzer;
    }

    void DirectTextLayout::UseAnalyzer(bool value)
    {
        if (m_useAnalyzer != value)
        {
            m_useAnalyzer = value;
            m_invalid = true;
        }
    }

    // The colours and the inline objects belong to the text they were set over, so they go
    // with it: a caller sets the text first and describes it afterwards.
    void DirectTextLayout::SetText(hstring text, IVector<Telegram::Native::TextStylePart> entities)
    {
        m_text = text;
        m_entities = entities;

        m_replaceKnown = false;
        m_replacePending = false;

        m_colors.clear();
        m_inlines.clear();

        // One paragraph over the whole of it until a caller says otherwise, reading the way
        // RightToLeft and Alignment say. Built at the next build, so a direction set after this
        // still reaches it.
        m_implicit = true;
        m_paragraphs.clear();

        if (m_editable != nullptr)
        {
            m_previous = std::move(m_editable);
        }

        m_invalid = true;
    }

    // Only the edit crosses from the caller, rather than the whole text as a string: in a long
    // paragraph that string was a copy of it on the managed heap on every key.
    void DirectTextLayout::ReplaceText(int32_t offset, int32_t length, hstring text, IVector<Telegram::Native::TextStylePart> entities)
    {
        const auto size = (int32_t)m_text.size();

        offset = std::clamp(offset, 0, size);
        length = std::clamp(length, 0, size - offset);

        m_replaced.assign(m_text.data(), offset);
        m_replaced.append(text.data(), text.size());
        m_replaced.append(m_text.data() + offset + length, size - offset - length);

        // Whether every change since the last build is known, which SetText ends.
        const auto known = m_replaceKnown;
        const auto pending = m_replacePending;

        auto colors = std::move(m_colors);

        SetText(hstring(m_replaced), entities);

        const EditableEdit edit{ offset, offset + length, offset + (int32_t)text.size() };

        if (known)
        {
            m_replace = pending ? m_replace.Then(edit) : edit;
            m_replaceKnown = true;
            m_replacePending = true;
        }

        // The colours go with the text rather than with SetText: a code block's are its tokens,
        // and dropping them on every key drew it plain until it was tokenized again. One the
        // edit cuts into is dropped; one around it takes in what was typed.
        const auto delta = edit.NewEnd - edit.OldEnd;

        for (ColorRange& item : colors)
        {
            const auto end = item.Offset + item.Length;

            if (end <= edit.Start)
            {
                m_colors.push_back(item);
            }
            else if (item.Offset >= edit.OldEnd)
            {
                m_colors.push_back({ item.Offset + delta, item.Length, item.Color });
            }
            else if (item.Offset <= edit.Start && end >= edit.OldEnd && item.Length + delta > 0)
            {
                m_colors.push_back({ item.Offset, item.Length + delta, item.Color });
            }
        }
    }

    void DirectTextLayout::ClearColors()
    {
        m_colors.clear();
    }

    // Appended as they come, without looking for a range set before: a code block's tokens
    // are thousands, and SetColor looking each one up among the others is quadratic.
    void DirectTextLayout::AddColors(array_view<int32_t const> offsets, array_view<int32_t const> lengths, array_view<Windows::UI::Color const> colors)
    {
        const auto count = std::min({ offsets.size(), lengths.size(), colors.size() });
        m_colors.reserve(m_colors.size() + count);

        for (uint32_t i = 0; i < count; i++)
        {
            m_colors.push_back({ offsets[i], lengths[i], colors[i] });
        }
    }

    hstring DirectTextLayout::Text()
    {
        return m_text;
    }

    // The division of the text. Offsets are into the text, and what lies between one paragraph
    // and the next - the newline that separates them - belongs to neither: it is in no layout,
    // and the line the paragraph before it ends with is where a caller walking by line finds
    // it.
    void DirectTextLayout::SetParagraphs(IVector<Telegram::Native::TextParagraph> paragraphs)
    {
        m_implicit = false;
        m_paragraphs.clear();
        m_editable = nullptr;
        m_previous = nullptr;

        m_replaceKnown = false;
        m_replacePending = false;

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

        // The edits since the last build, if every one of them was said, and from here on the
        // ones since this one.
        const auto replaced = m_replaceKnown && m_replacePending;
        const auto edit = m_replace;

        m_replaceKnown = true;
        m_replacePending = false;

        // What the last build left, which an edit is worked out from.
        std::unique_ptr<EditableTextLayout> previous = std::move(m_previous);

        if (previous == nullptr)
        {
            previous = std::move(m_editable);
        }

        m_editable = nullptr;

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

        // Laid out by DirectWrite after all when the analyzer fails, which while it is being
        // proven is a defect to find in the comparison rather than text to lose.
        if (UsesAnalyzer())
        {
            auto editable = previous != nullptr ? std::move(previous) : std::make_unique<EditableTextLayout>();

            const auto started = Now();
            result = BuildEditable(*editable, replaced ? &edit : nullptr);

            Record(s_analyzer, started);

            if (SUCCEEDED(result))
            {
                if (s_verifyAnalyzer)
                {
                    VerifyEditable(*editable);
                }

                m_paragraphs.clear();
                m_editable = std::move(editable);

                TakeChange();

                m_invalid = false;
                return S_OK;
            }
        }

        for (Paragraph& paragraph : m_paragraphs)
        {
            ReturnIfFailed(result, BuildParagraph(paragraph));
        }

        ReturnIfFailed(result, Stack());

        m_invalid = false;
        return S_OK;
    }

    HRESULT DirectTextLayout::ApplyFallback(IDWriteTextLayout* layout)
    {
        if (m_resolvedFamily == nullptr || m_resolvedFamily->Fallback == nullptr)
        {
            return S_OK;
        }

        winrt::com_ptr<IDWriteTextLayout2> layout2;

        HRESULT result;
        ReturnIfFailed(result, layout->QueryInterface(IID_PPV_ARGS(layout2.put())));
        return layout2->SetFontFallback(m_resolvedFamily->Fallback.get());
    }

    HRESULT DirectTextLayout::BuildParagraph(Paragraph& paragraph)
    {
        HRESULT result;

        paragraph.Layout = nullptr;
        paragraph.Ellipsis = nullptr;
        paragraph.Height = 0;
        paragraph.Hidden = false;

        // No family is the app's own emoji font, which carries no letters and leaves them to the
        // system fallback. A family is a chain, resolved by FontChains; one it could make
        // nothing of is passed through as a system name, and falls back the same way.
        const wchar_t* family;
        IDWriteFontCollection* collection;

        if (m_resolvedFamily != nullptr)
        {
            family = m_resolvedFamily->Family.c_str();
            collection = m_resolvedFamily->Collection.get();
        }
        else if (m_fontFamily.empty())
        {
            family = L"Segoe UI Emoji";
            collection = m_fontCollection.get();
        }
        else
        {
            family = m_fontFamily.c_str();
            collection = m_systemCollection.get();
        }

        // Per paragraph rather than per layout: a message in one script and a quote in another
        // is ordinary, and this is where each of them is built.
        const auto& locales = Locales();
        const auto& locale = locales.EastAsian.empty() || !IsEastAsianText(m_text.data() + paragraph.Offset, paragraph.Length)
            ? locales.Primary
            : locales.EastAsian;

        winrt::com_ptr<IDWriteTextFormat> format;
        ReturnIfFailed(result, m_factory->CreateTextFormat(
            family,
            collection,
            (DWRITE_FONT_WEIGHT)m_fontWeight,
            m_italic ? DWRITE_FONT_STYLE_ITALIC : DWRITE_FONT_STYLE_NORMAL,
            DWRITE_FONT_STRETCH_NORMAL,
            (float)m_fontSize,
            locale.c_str(),
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
        ReturnIfFailed(result, ApplyFallback(paragraph.Layout.get()));

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
                else if (entity.Type == TextStyle::Subscript || entity.Type == TextStyle::Superscript)
                {
                    // Laid out at the size it is drawn at, so its advances, its hit testing and
                    // the caret are right; DrawScript moves it off the baseline at draw time,
                    // which DirectWrite has no range property for.
                    ReturnIfFailed(result, paragraph.Layout->SetFontSize((FLOAT)(m_fontSize * ScriptScale), range));
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

        if (m_invalid || (m_paragraphs.empty() && m_editable == nullptr))
        {
            const auto started = Now();

            m_width = availableWidth;
            result = Build();

            Record(s_build, started);

            if (FAILED(result))
            {
                return result;
            }
        }
        else if (m_width != availableWidth && m_editable != nullptr)
        {
            const auto started = Now();

            m_width = availableWidth;
            result = m_editable->Reflow((float)std::max(availableWidth, 0.0));

            Record(s_reflow, started);

            if (FAILED(result))
            {
                return result;
            }
        }
        else if (m_width != availableWidth)
        {
            const auto started = Now();

            m_width = availableWidth;

            for (Paragraph& paragraph : m_paragraphs)
            {
                if (paragraph.Layout != nullptr)
                {
                    ReturnIfFailed(result, paragraph.Layout->SetMaxWidth((float)std::max(availableWidth, 0.0)));
                }
            }

            result = Stack();

            Record(s_reflow, started);

            if (FAILED(result))
            {
                return result;
            }
        }

        return S_OK;
    }

    Windows::Foundation::Size DirectTextLayout::Measure(double availableWidth)
    {
        HRESULT result;
        ReturnDefaultIfFailed(result, Reflow(availableWidth));

        const auto bounds = Bounds();

        // The box laid out again in the width the text needs, which is where it has to end up:
        // a paragraph that is not drawn from the leading edge is placed IN the box - right to
        // left against its far edge, centred against its middle - and a box wider than the
        // block puts that outside it.
        //
        // Only then. Reflowing is where a measure spends its time, and for a paragraph drawn
        // from the leading edge the width of the box behind it moves nothing.
        //
        // Text with no width too - an empty line held open by a zero-width character: centred
        // in the box it was measured in, its caret is wherever the middle of that is, which
        // for an unbounded measure is nowhere near the block.
        //
        // Not an edited text in a box of its own width, which is laid out in all of it: shrunk
        // to the text, the caller places the box by the pixels the text was drawn in, a whole
        // number of them, while the text ends where it ends, so a line against the far edge
        // moved by a fraction of a pixel with every key.
        if (bounds.Width < availableWidth && !IsLeading() && (m_readOnly || availableWidth > c_editingMaxWidth))
        {
            ReturnDefaultIfFailed(result, Reflow(bounds.Width));

            return Bounds();
        }

        return bounds;
    }

    // Whether every paragraph is drawn from the leading edge of the box, left to right - the
    // ordinary case, and the one where the box is only ever as wide as it has to be.
    bool DirectTextLayout::IsLeading() const
    {
        if (m_editable != nullptr)
        {
            return !m_rtl && (m_alignment == Telegram::Native::TextAlignmentMode::Leading || m_alignment == Telegram::Native::TextAlignmentMode::Left);
        }

        for (const Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.RightToLeft
                || (paragraph.Alignment != Telegram::Native::TextAlignmentMode::Leading
                    && paragraph.Alignment != Telegram::Native::TextAlignmentMode::Left))
            {
                return false;
            }
        }

        return true;
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

        if (m_editable != nullptr)
        {
            width = m_editable->Right();
            height = m_editable->Height();
        }

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

            // metrics.width leaves out the whitespace a line ends with, while metrics.left is the
            // left-most point of the text INCLUDING it - and right to left that whitespace is at
            // the left end, so the text ends that much further right than left + width says:
            // the surface came out short by it and the text was moved off the box's edge, a
            // space further with every space typed. Left to right, left is clamped at zero, as
            // nothing can be drawn left of the origin.
            const auto right = paragraph.RightToLeft
                ? metrics.left + metrics.widthIncludingTrailingWhitespace
                : std::max(metrics.left, 0.0f) + metrics.width;

            width = std::max(width, right);
            height = paragraph.Y + paragraph.Height;
        }

        if (m_width > 0)
        {
            width = std::min(width, (float)m_width);
        }

        // Where the pen ended, which is not quite where the ink does: an italic leans past the
        // advance width it was measured with. What covers that is the surface being rounded up
        // to a whole number of 32 pixel steps - see Grow - rather than anything measured here.
        // DirectWrite will say where the ink ends (GetOverhangMetrics) and it was tried: it
        // costs 40 microseconds a draw against the 1.4 this function costs, because it walks
        // the glyph runs to find out. See the note.
        return { width, height };
    }

    // The size of the text as it was last drawn, in pixels, which is what the surface holds in
    // its top left corner - the surface itself is rounded up from it.
    Windows::Graphics::SizeInt32 DirectTextLayout::RenderedPixels()
    {
        return m_pixels;
    }

    Windows::Foundation::Size DirectTextLayout::Bounds()
    {
        float width = 0;
        float height = 0;

        if (m_editable != nullptr)
        {
            width = m_editable->Width();
            height = m_editable->Height();
        }

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

        if (m_editable != nullptr)
        {
            m_editable->Ranges(offset, length, rects);
        }

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

    // A sub- or superscript range, drawn again at its own size and moved off the line's
    // baseline: the layout already gave it the right advance at that size, so only the height
    // is decided here. One small layout per line the range covers, as a range may wrap.
    void DirectTextLayout::DrawScript(ID2D1DeviceContext* context, const Paragraph& paragraph, DWRITE_TEXT_RANGE range, bool superscript, ID2D1Brush* brush)
    {
        UINT32 count = 0;
        paragraph.Layout->HitTestTextRange(range.startPosition, range.length, 0, 0, nullptr, 0, &count);

        if (count == 0)
        {
            return;
        }

        std::vector<DWRITE_HIT_TEST_METRICS> hits(count);
        if (FAILED(paragraph.Layout->HitTestTextRange(range.startPosition, range.length, 0, 0, hits.data(), count, &count)))
        {
            return;
        }

        UINT32 lineCount = 0;
        paragraph.Layout->GetLineMetrics(nullptr, 0, &lineCount);

        std::vector<DWRITE_LINE_METRICS> lines(lineCount);
        if (lineCount == 0 || FAILED(paragraph.Layout->GetLineMetrics(lines.data(), lineCount, &lineCount)))
        {
            return;
        }

        const auto size = (FLOAT)(m_fontSize * ScriptScale);

        // How far off the baseline: the same proportions XAML's own Typography variants use,
        // a third of the text up and a sixth down.
        const auto shift = (FLOAT)(superscript ? -m_fontSize / 3 : m_fontSize / 6);

        for (UINT32 i = 0; i < count; i++)
        {
            const auto& hit = hits[i];

            // The line the hit is on, and where its baseline is.
            FLOAT top = 0;
            FLOAT baseline = 0;

            for (const auto& line : lines)
            {
                if (hit.top < top + line.height)
                {
                    baseline = top + line.baseline;
                    break;
                }

                top += line.height;
            }

            WCHAR family[100];
            if (FAILED(paragraph.Layout->GetFontFamilyName(hit.textPosition, family, ARRAYSIZE(family))))
            {
                continue;
            }

            winrt::com_ptr<IDWriteFontCollection> collection;
            DWRITE_FONT_WEIGHT weight;
            DWRITE_FONT_STYLE style;

            paragraph.Layout->GetFontCollection(hit.textPosition, collection.put());
            paragraph.Layout->GetFontWeight(hit.textPosition, &weight);
            paragraph.Layout->GetFontStyle(hit.textPosition, &style);

            winrt::com_ptr<IDWriteTextFormat> format;
            if (FAILED(m_factory->CreateTextFormat(family, collection.get(), weight, style, DWRITE_FONT_STRETCH_NORMAL, size, L"", format.put())))
            {
                continue;
            }

            winrt::com_ptr<IDWriteTextLayout> layout;
            if (FAILED(m_factory->CreateTextLayout(m_text.data() + paragraph.Offset + hit.textPosition, hit.length, format.get(), INFINITY, INFINITY, layout.put())) || FAILED(ApplyFallback(layout.get())))
            {
                continue;
            }

            DWRITE_LINE_METRICS own;
            UINT32 ownCount = 0;

            if (FAILED(layout->GetLineMetrics(&own, 1, &ownCount)) || ownCount == 0)
            {
                continue;
            }

            // The colour the range was given, if it was given one - a link - over the text's.
            winrt::com_ptr<ID2D1SolidColorBrush> colored;
            const auto position = paragraph.Offset + (int32_t)hit.textPosition;

            for (const ColorRange& item : m_colors)
            {
                if (position >= item.Offset && position < item.Offset + item.Length)
                {
                    context->CreateSolidColorBrush(D2D1::ColorF(item.Color.R / 255.0f, item.Color.G / 255.0f, item.Color.B / 255.0f, item.Color.A / 255.0f), colored.put());
                }
            }

            const D2D1_POINT_2F origin{ hit.left, paragraph.Y + baseline - own.baseline + shift };
            context->DrawTextLayout(origin, layout.get(), colored != nullptr ? colored.get() : brush, D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
        }
    }

    // Where a caret before the character at offset is drawn: zero wide, as tall as its line.
    // A character's box is not that - at the end of a line the caret belongs after the last
    // character, and in right to left text the leading edge is the box's right side - so the
    // layout is asked for the position itself.
    Windows::Foundation::Rect DirectTextLayout::Caret(int32_t offset)
    {
        if (m_editable != nullptr)
        {
            return m_editable->Caret(offset);
        }

        const Paragraph* found = nullptr;

        for (const Paragraph& paragraph : m_paragraphs)
        {
            if (paragraph.Layout == nullptr || paragraph.Hidden)
            {
                continue;
            }

            // A position in the newline between two paragraphs is the end of the first.
            if (found != nullptr && offset < paragraph.Offset)
            {
                break;
            }

            found = &paragraph;

            if (offset <= paragraph.Offset + paragraph.Length)
            {
                break;
            }
        }

        if (found == nullptr)
        {
            return { 0, 0, 0, 0 };
        }

        const auto position = (UINT32)std::clamp(offset - found->Offset, 0, found->Length);

        FLOAT x;
        FLOAT y;
        DWRITE_HIT_TEST_METRICS metrics;

        if (FAILED(found->Layout->HitTestTextPosition(position, FALSE, &x, &y, &metrics)))
        {
            return { 0, 0, 0, 0 };
        }

        return { x, found->Y + y, 0, metrics.height };
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

        if (m_editable != nullptr)
        {
            m_editable->LineLengths(lengths);
        }

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
        if (m_editable != nullptr)
        {
            EditableHit hit;

            if (!m_editable->HitTest(point.X, point.Y, hit))
            {
                inside = false;
                return -1;
            }

            inside = hit.Inside;
            return hit.Position + (hit.Trailing ? hit.Length : 0);
        }

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
        if (m_editable != nullptr)
        {
            EditableHit hit;

            if (!m_editable->HitTest(point.X, point.Y, hit))
            {
                inside = false;
                return -1;
            }

            inside = hit.Inside;
            return hit.Position;
        }

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
        if (m_editable != nullptr)
        {
            return m_editable->ContentEnd();
        }

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

        auto started = Now();

        const auto bounds = Extent();
        const auto pixels = Windows::Graphics::SizeInt32
        {
            (int32_t)std::ceil(bounds.Width * rasterizationScale),
            (int32_t)std::ceil(bounds.Height * rasterizationScale)
        };

        Record(s_extent, started);

        if (pixels.Width <= 0 || pixels.Height <= 0)
        {
            return nullptr;
        }

        m_color = color;
        m_scale = rasterizationScale;
        m_pixels = pixels;

        if (UsesVirtualSurface(pixels))
        {
            return RenderVirtual(pixels);
        }

        // Short enough again, or no longer edited: a surface of the ordinary kind.
        if (m_virtual)
        {
            m_surface = nullptr;
            m_virtualSurface = nullptr;
            m_virtual = false;
        }

        // The surface is kept rather than replaced: a text change is a redraw, and a list
        // scrolling past recycled items would otherwise allocate and free one atlas region per
        // item. Resizing drops the content, which is redrawn below anyway.
        //
        // Kept at a size the next text is likely to fit in as well, rather than at the size of
        // this one: a recycled block is handed text of a different size nearly every time, so
        // an exact fit gives the surface back on nearly every draw. The caller is told what was
        // drawn (RenderedPixels) rather than reading the surface, which is now bigger than the
        // text.
        //
        // One that no longer fits is given back rather than resized. Resizing was measured at
        // five times the cost of making a new one - 293 microseconds against 52, and 17
        // milliseconds at its worst, a dropped frame on its own - because it has to reconcile
        // the region with the compositor, where a new surface is only another region.
        if (m_surface != nullptr)
        {
            if (KeepSurface(pixels))
            {
                s_surfaceKept++;
            }
            else
            {
                s_surfaceDropped++;
                m_surface = nullptr;
            }
        }

        if (m_surface == nullptr)
        {
            const auto started = Now();

            try
            {
                m_surface = m_device.CreateDrawingSurface2(SurfaceSize(pixels), DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
            }
            catch (...)
            {
                return nullptr;
            }

            Record(s_surfaceCreate, started);

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

    // Shown, a surface fits the text it is made for, rounded up. Edited, the text changes with
    // every key and mostly grows, so a surface that fits it is outgrown every few characters:
    // it spans the box instead, unless that is the unbounded width a caller measures with, and
    // takes lines more than the text so that a new line is a redraw as well.
    Windows::Graphics::SizeInt32 DirectTextLayout::SurfaceSize(Windows::Graphics::SizeInt32 pixels)
    {
        if (m_readOnly)
        {
            return { Grow(pixels.Width), Grow(pixels.Height) };
        }

        auto width = pixels.Width;
        const auto box = std::ceil(m_width * m_scale);

        if (box > width && box <= c_editingMaxWidth)
        {
            width = (int32_t)box;
        }

        const auto spare = std::max(pixels.Height / 2, (int32_t)std::ceil(c_editingSpareHeight * m_scale));
        return { Grow(width), Grow(pixels.Height + spare) };
    }

    // Edited, a surface is only given back when the text has outgrown it: one that holds more
    // than the text needs is the room it was given to grow into, and deleting is no reason to
    // replace it.
    bool DirectTextLayout::KeepSurface(Windows::Graphics::SizeInt32 pixels)
    {
        const auto size = m_surface.SizeInt32();

        if (m_readOnly)
        {
            return Keep(size, pixels);
        }

        return size.Width >= pixels.Width && size.Height >= pixels.Height;
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

        if (m_virtual)
        {
            m_dirtyAll = true;
            DrawVirtual();
        }
        else
        {
            DrawSurface();
        }
    }

    HRESULT DirectTextLayout::DrawSurface()
    {
        auto surfaceInterop = m_surface.as<abi::ICompositionDrawingSurfaceInterop>();

        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        // BeginDraw can fail with DXGI_ERROR_DEVICE_REMOVED. Nothing to do about it here:
        // Direct2DDevice rebuilds the device on the next access and replacing the rendering
        // device brings this back through the handler above.
        auto started = Now();
        HRESULT result = surfaceInterop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), context.put_void(), &offset);
        Record(s_beginDraw, started);

        if (FAILED(result))
        {
            return result;
        }

        started = Now();

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
            result = m_editable != nullptr
                ? DrawEditable(context.get(), brush.get(), -INFINITY, INFINITY)
                : Draw(context.get(), brush.get());
        }

        Record(s_drawText, started);

        started = Now();
        surfaceInterop->EndDraw();
        Record(s_endDraw, started);

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

            // Sub- and superscript are drawn by DrawScript, off the baseline; here they are
            // drawn in nothing, so the layout still flows and hit tests them where they are.
            winrt::com_ptr<ID2D1SolidColorBrush> transparent;
            auto scripts = false;

            if (m_entities != nullptr && m_entities.Size() > 0)
            {
                for (const Telegram::Native::TextStylePart& entity : m_entities)
                {
                    DWRITE_TEXT_RANGE range;

                    if ((entity.Type != TextStyle::Subscript && entity.Type != TextStyle::Superscript)
                        || !Intersect(paragraph.Offset, paragraph.Length, entity.Offset, entity.Length, range))
                    {
                        continue;
                    }

                    if (transparent == nullptr && FAILED(context->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, 0), transparent.put())))
                    {
                        break;
                    }

                    paragraph.Layout->SetDrawingEffect(transparent.get(), range);
                    scripts = true;
                }
            }

            // Colour fonts on, or every emoji comes out as an outline. Anything the text
            // flows around that draws itself is drawn from in here, at the origin DirectWrite
            // works out for it.
            context->DrawTextLayout({ 0, paragraph.Y }, paragraph.Layout.get(), brush, D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);

            if (scripts)
            {
                for (const Telegram::Native::TextStylePart& entity : m_entities)
                {
                    DWRITE_TEXT_RANGE range;

                    if ((entity.Type == TextStyle::Subscript || entity.Type == TextStyle::Superscript)
                        && Intersect(paragraph.Offset, paragraph.Length, entity.Offset, entity.Length, range))
                    {
                        DrawScript(context, paragraph, range, entity.Type == TextStyle::Superscript, brush);
                    }
                }
            }
        }

        for (const InlineRange& item : m_inlines)
        {
            item.Object->EndDraw();
        }

        return S_OK;
    }

    bool DirectTextLayout::UsesAnalyzer() const
    {
        return m_useAnalyzer && !m_readOnly && m_implicit && m_maxLines == 0;
    }

    // The same state BuildParagraph hands DirectWrite, in the shape the analyzer takes it.
    HRESULT DirectTextLayout::BuildEditable(EditableTextLayout& layout, const EditableEdit* edit)
    {
        EditableTextFormat format;
        format.Factory = m_factory;

        if (m_resolvedFamily != nullptr)
        {
            format.Family = m_resolvedFamily->Family;
            format.Collection = m_resolvedFamily->Collection;
            format.Fallback = m_resolvedFamily->Fallback;
        }
        else if (m_fontFamily.empty())
        {
            format.Family = L"Segoe UI Emoji";
            format.Collection = m_fontCollection;
        }
        else
        {
            format.Family = m_fontFamily;
            format.Collection = m_systemCollection;
        }

        format.MonospaceCollection = m_systemCollection;
        format.MonospaceFamily = m_monospaceFamily;

        const auto& locales = Locales();
        format.Locale = locales.EastAsian.empty() || !IsEastAsianText(m_text.data(), (int32_t)m_text.size())
            ? locales.Primary
            : locales.EastAsian;

        format.FontSize = (float)m_fontSize;
        format.Weight = (DWRITE_FONT_WEIGHT)m_fontWeight;
        format.Style = m_italic ? DWRITE_FONT_STYLE_ITALIC : DWRITE_FONT_STYLE_NORMAL;
        format.RightToLeft = m_rtl;
        format.ScriptScale = (float)ScriptScale;

        format.Alignment = m_alignment == Telegram::Native::TextAlignmentMode::Center ? EditableAlignment::Center
            : m_alignment == Telegram::Native::TextAlignmentMode::Left ? EditableAlignment::Left
            : m_alignment == Telegram::Native::TextAlignmentMode::Right ? EditableAlignment::Right
            : EditableAlignment::Leading;

        // A style is one entity's whole type, as BuildParagraph reads them: an entity that
        // combines bits is not one of these to it either.
        std::vector<EditableStyle> styles;

        if (m_entities != nullptr && m_entities.Size() > 0)
        {
            for (const Telegram::Native::TextStylePart& entity : m_entities)
            {
                switch (entity.Type)
                {
                case TextStyle::Bold:
                case TextStyle::Italic:
                case TextStyle::Monospace:
                case TextStyle::Strikethrough:
                case TextStyle::Underline:
                case TextStyle::Subscript:
                case TextStyle::Superscript:
                    styles.push_back({ entity.Offset, entity.Length, (uint32_t)entity.Type });
                    break;
                default:
                    break;
                }
            }
        }

        std::vector<EditableInline> inlines;
        inlines.reserve(m_inlines.size());

        for (const InlineRange& item : m_inlines)
        {
            EditableInline editable{ item.Offset, item.Length, nullptr };
            editable.Object.copy_from(item.Object.get());

            inlines.push_back(std::move(editable));
        }

        // An update of whatever the layout already holds, which for a new one is a build.
        return layout.Update(format, std::wstring_view(m_text.data(), m_text.size()), styles, inlines, (float)std::max(m_width, 0.0), edit);
    }

    // An update against a build of the same state from scratch, which is what it has to be.
    void DirectTextLayout::VerifyEditable(const EditableTextLayout& layout)
    {
        EditableTextLayout cold;
        std::wstring difference;

        s_verified++;

        if (SUCCEEDED(BuildEditable(cold)) && !layout.Matches(cold, difference))
        {
            s_mismatched++;

            std::lock_guard const guard(s_mismatchMutex);
            s_mismatch = std::format(L"{} characters: {}", m_text.size(), difference);
        }
    }

    HRESULT DirectTextLayout::DrawEditable(ID2D1DeviceContext* context, ID2D1Brush* brush, float top, float bottom)
    {
        for (const InlineRange& item : m_inlines)
        {
            item.Object->BeginDraw(context, m_color);
        }

        std::vector<EditableColor> colors;
        colors.reserve(m_colors.size());

        for (const ColorRange& item : m_colors)
        {
            colors.push_back({ item.Offset, item.Length, D2D1::ColorF(item.Color.R / 255.0f, item.Color.G / 255.0f, item.Color.B / 255.0f, item.Color.A / 255.0f) });
        }

        const auto result = m_editable->Draw(context, brush, colors, top, bottom);

        for (const InlineRange& item : m_inlines)
        {
            item.Object->EndDraw();
        }

        return result;
    }

    // =========================================================================================
    // The virtual surface
    // =========================================================================================

    bool DirectTextLayout::HasVirtualSurface()
    {
        return m_virtual;
    }

    bool DirectTextLayout::UsesVirtualSurface(Windows::Graphics::SizeInt32 pixels)
    {
        return m_editable != nullptr && !m_readOnly && SurfaceSize(pixels).Height > c_virtualHeight;
    }

    // What the analyzer says the last build or update changed, added to what has changed since
    // the surface was last drawn.
    void DirectTextLayout::TakeChange()
    {
        const auto& change = m_editable->Change();

        if (change.All)
        {
            m_dirtyAll = true;
            return;
        }

        MarkDirty(change.Top, change.Bottom);

        // Text that was handed over again unchanged is no edit.
        if (change.OldEnd != change.Start || change.NewEnd != change.Start)
        {
            if (m_edits++ == 0)
            {
                m_edit = change;
            }
        }
    }

    void DirectTextLayout::MarkDirty(float top, float bottom)
    {
        if (top >= bottom)
        {
            return;
        }

        if (m_dirtyTop >= m_dirtyBottom)
        {
            m_dirtyTop = top;
            m_dirtyBottom = bottom;
        }
        else
        {
            m_dirtyTop = std::min(m_dirtyTop, top);
            m_dirtyBottom = std::max(m_dirtyBottom, bottom);
        }
    }

    void DirectTextLayout::MarkRangeDirty(int32_t start, int32_t end)
    {
        const auto length = (int32_t)m_text.size();

        start = std::clamp(start, 0, length);
        end = std::clamp(end, start, length);

        float top;
        float bottom;
        m_editable->RangeExtent(start, std::max(end, start + 1), top, bottom);

        MarkDirty(top, bottom);
    }

    // The colours set now against the ones last drawn, moved by the edit in between: a link or
    // a code token after the edit moved with the text and needs nothing, one that was added,
    // taken away or recoloured - a spoiler revealed - needs its lines drawn again. Every SetText
    // sets them all over again, so comparing is what keeps a key from redrawing every line a
    // link is on.
    void DirectTextLayout::DiffColors()
    {
        if (m_dirtyAll || (m_colors.empty() && m_drawnColors.empty()))
        {
            return;
        }

        // Moved by more than one edit, which is rare and not worth following.
        if (m_edits > 1)
        {
            m_dirtyAll = true;
            return;
        }

        const auto edited = m_edits == 1;
        const auto delta = edited ? m_edit.NewEnd - m_edit.OldEnd : 0;

        struct Range
        {
            int32_t Offset;
            int32_t Length;
            uint32_t Color;

            bool operator<(const Range& other) const
            {
                return Offset != other.Offset ? Offset < other.Offset
                    : Length != other.Length ? Length < other.Length
                    : Color < other.Color;
            }

            bool operator==(const Range& other) const
            {
                return Offset == other.Offset && Length == other.Length && Color == other.Color;
            }
        };

        const auto pack = [](Windows::UI::Color color)
            {
                return (uint32_t)color.A << 24 | (uint32_t)color.R << 16 | (uint32_t)color.G << 8 | color.B;
            };

        std::vector<Range> before;
        std::vector<Range> after;

        before.reserve(m_drawnColors.size());
        after.reserve(m_colors.size());

        for (const ColorRange& range : m_drawnColors)
        {
            const auto end = range.Offset + range.Length;

            // Into the edit: its lines were broken again, and whatever it covered after the
            // edit is drawn again with them.
            if (edited && end > m_edit.Start && range.Offset < m_edit.OldEnd)
            {
                MarkRangeDirty(std::min(range.Offset, m_edit.Start), end > m_edit.OldEnd ? end + delta : m_edit.NewEnd);
                continue;
            }

            const auto offset = edited && range.Offset >= m_edit.OldEnd ? range.Offset + delta : range.Offset;
            before.push_back({ offset, range.Length, pack(range.Color) });
        }

        for (const ColorRange& range : m_colors)
        {
            after.push_back({ range.Offset, range.Length, pack(range.Color) });
        }

        std::sort(before.begin(), before.end());
        std::sort(after.begin(), after.end());

        size_t i = 0;
        size_t j = 0;

        while (i < before.size() || j < after.size())
        {
            if (j == after.size() || (i < before.size() && before[i] < after[j]))
            {
                MarkRangeDirty(before[i].Offset, before[i].Offset + before[i].Length);
                i++;
            }
            else if (i == before.size() || after[j] < before[i])
            {
                MarkRangeDirty(after[j].Offset, after[j].Offset + after[j].Length);
                j++;
            }
            else
            {
                i++;
                j++;
            }
        }
    }

    // A surface as wide as the box and twice as tall as the text: a virtual surface holds only
    // what is drawn into it, so the room to grow into costs nothing, and replacing it is a
    // redraw of everything in view.
    CompositionDrawingSurface DirectTextLayout::RenderVirtual(Windows::Graphics::SizeInt32 pixels)
    {
        const auto size = SurfaceSize(pixels);

        if (m_virtual && m_surface != nullptr)
        {
            const auto current = m_surface.SizeInt32();

            if (current.Width < size.Width || current.Height < pixels.Height)
            {
                m_surface = nullptr;
                m_virtualSurface = nullptr;
            }
        }
        else
        {
            m_surface = nullptr;
            m_virtualSurface = nullptr;
        }

        if (m_surface == nullptr)
        {
            const auto started = Now();

            try
            {
                const Windows::Graphics::SizeInt32 virtualSize{ size.Width, std::min(std::max(size.Height, pixels.Height * 2), c_virtualMaxHeight) };

                m_virtualSurface = m_device.CreateVirtualDrawingSurface(virtualSize, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
                m_surface = m_virtualSurface;
            }
            catch (...)
            {
                m_virtual = false;
                return nullptr;
            }

            Record(s_surfaceCreate, started);

            m_virtual = true;
            m_dirtyAll = true;
            m_tiles.clear();
            m_windowFrom = 0;
            m_windowTo = 0;

            if (!m_renderingDeviceReplaced)
            {
                m_renderingDeviceReplaced = m_device.RenderingDeviceReplaced({ get_weak(), &DirectTextLayout::OnRenderingDeviceReplaced });
            }
        }
        else
        {
            s_surfaceKept++;
        }

        if (m_color != m_drawnColor || m_scale != m_drawnScale)
        {
            m_dirtyAll = true;
        }

        DiffColors();

        if (FAILED(DrawVirtual()))
        {
            return nullptr;
        }

        return m_surface;
    }

    // Where the text is seen, in DIPs from the top of the layout. Tiles that come into view
    // are drawn at once, as there is no measure or arrange to wait for when only the
    // viewport moved.
    void DirectTextLayout::SetViewport(double top, double bottom)
    {
        if (m_viewportTop == top && m_viewportBottom == bottom)
        {
            return;
        }

        m_viewportTop = top;
        m_viewportBottom = bottom;

        if (m_virtual && m_surface != nullptr && !m_closed)
        {
            DrawVirtual();
        }
    }

    // The tiles in view and in the margin around it: the ones not drawn yet whole, the ones
    // drawn only where they changed. The rest are given back.
    HRESULT DirectTextLayout::DrawVirtual()
    {
        HRESULT result;

        // The text was replaced and not laid out again yet; the arrange that does draws.
        if (m_editable == nullptr)
        {
            return S_OK;
        }

        const auto size = m_surface.SizeInt32();
        const auto count = (size.Height + c_tileHeight - 1) / c_tileHeight;

        if ((int32_t)m_tiles.size() != count)
        {
            m_tiles.assign(count, false);
            m_windowFrom = 0;
            m_windowTo = 0;
        }

        if (m_dirtyAll)
        {
            std::fill(m_tiles.begin(), m_tiles.end(), false);
        }

        // Before the first viewport, the start of the text.
        int32_t top = 0;
        int32_t bottom = 2 * c_tileMargin;

        if (m_viewportBottom > m_viewportTop)
        {
            top = (int32_t)std::floor(m_viewportTop * m_scale) - c_tileMargin;
            bottom = (int32_t)std::ceil(m_viewportBottom * m_scale) + c_tileMargin;
        }

        const auto from = std::clamp(top / c_tileHeight, 0, count);
        const auto to = std::clamp((bottom + c_tileHeight - 1) / c_tileHeight, from, count);

        if (from != m_windowFrom || to != m_windowTo)
        {
            for (int32_t i = 0; i < count; i++)
            {
                if (i < from || i >= to)
                {
                    m_tiles[i] = false;
                }
            }

            const Windows::Graphics::RectInt32 keep{ 0, from * c_tileHeight, size.Width, (to - from) * c_tileHeight };

            try
            {
                if (to > from)
                {
                    m_virtualSurface.Trim({ keep });
                }
                else
                {
                    m_virtualSurface.Trim({});
                }
            }
            catch (...)
            {
                // Holding more than it has to is no reason not to draw.
            }

            m_windowFrom = from;
            m_windowTo = to;
        }

        const auto dirtyTop = m_dirtyAll ? 0 : (int32_t)std::floor(m_dirtyTop * m_scale);
        const auto dirtyBottom = m_dirtyAll ? size.Height : (int32_t)std::min((double)size.Height, std::ceil(m_dirtyBottom * m_scale));

        for (auto i = from; i < to; )
        {
            auto j = i;

            if (!m_tiles[i])
            {
                while (j < to && !m_tiles[j])
                {
                    j++;
                }

                ReturnIfFailed(result, DrawRows(i * c_tileHeight, std::min(j * c_tileHeight, size.Height)));

                for (auto k = i; k < j; k++)
                {
                    m_tiles[k] = true;
                }
            }
            else
            {
                while (j < to && m_tiles[j])
                {
                    j++;
                }

                const auto rowsTop = std::max(i * c_tileHeight, dirtyTop);
                const auto rowsBottom = std::min(std::min(j * c_tileHeight, size.Height), dirtyBottom);

                if (rowsTop < rowsBottom)
                {
                    ReturnIfFailed(result, DrawRows(rowsTop, rowsBottom));
                }
            }

            i = j;
        }

        m_dirtyAll = false;
        m_dirtyTop = 0;
        m_dirtyBottom = 0;
        m_edits = 0;

        m_drawnColors = m_colors;
        m_drawnColor = m_color;
        m_drawnScale = m_scale;

        return S_OK;
    }

    // The rows [top, bottom) of the surface, in pixels, cleared and drawn again. The lines are
    // drawn from a little above and below them, so that what reaches into the rows from a
    // line outside - a descender, an emoji - is drawn again too, and cut where the rows end.
    HRESULT DirectTextLayout::DrawRows(int32_t top, int32_t bottom)
    {
        auto surfaceInterop = m_surface.as<abi::ICompositionDrawingSurfaceInterop>();

        const auto width = m_surface.SizeInt32().Width;
        const RECT update{ 0, top, width, bottom };

        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        auto started = Now();
        HRESULT result = surfaceInterop->BeginDraw(&update, __uuidof(ID2D1DeviceContext), context.put_void(), &offset);
        Record(s_beginDraw, started);

        if (FAILED(result))
        {
            return result;
        }

        started = Now();

        // The update rectangle is where BeginDraw put it, and only it is cleared: the context
        // draws into an atlas that holds other surfaces too.
        context->SetTransform(D2D1::Matrix3x2F::Identity());
        context->PushAxisAlignedClip(D2D1::RectF((float)offset.x, (float)offset.y, (float)(offset.x + width), (float)(offset.y + bottom - top)), D2D1_ANTIALIAS_MODE_ALIASED);
        context->Clear(D2D1::ColorF(0, 0, 0, 0));

        context->SetTransform(
            D2D1::Matrix3x2F::Scale((float)m_scale, (float)m_scale) *
            D2D1::Matrix3x2F::Translation((float)offset.x, (float)(offset.y - top)));

        winrt::com_ptr<ID2D1SolidColorBrush> brush;
        result = context->CreateSolidColorBrush(D2D1::ColorF(m_color.R / 255.0f, m_color.G / 255.0f, m_color.B / 255.0f, m_color.A / 255.0f), brush.put());

        if (SUCCEEDED(result))
        {
            const auto margin = (float)m_fontSize * 2;
            result = DrawEditable(context.get(), brush.get(), (float)(top / m_scale) - margin, (float)(bottom / m_scale) + margin);
        }

        context->PopAxisAlignedClip();

        Record(s_drawText, started);

        started = Now();
        surfaceInterop->EndDraw();
        Record(s_endDraw, started);

        return result;
    }

    // Where the analyzer's layout of this text disagrees with DirectWrite's, which is the
    // answer it has to give: line breaks, line metrics, carets, hit tests and the box, and what
    // each took to build. Diagnostics only; nothing here is fast.
    hstring DirectTextLayout::CompareWithAnalyzer()
    {
        if (m_factory == nullptr || m_text.empty())
        {
            return hstring(L"Nothing to compare: no text.\n");
        }

        if (!m_implicit || m_maxLines != 0)
        {
            return hstring(L"Nothing to compare: the analyzer only lays out one paragraph without MaxLines.\n");
        }

        LARGE_INTEGER frequency;
        QueryPerformanceFrequency(&frequency);

        const auto micro = [&](int64_t ticks)
            {
                return ticks * 1000000.0 / frequency.QuadPart;
            };

        const auto snippet = [&](int32_t offset, int32_t length)
            {
                std::wstring text(m_text.data() + offset, std::min(length, 24));

                for (wchar_t& c : text)
                {
                    if (c == L'\n' || c == L'\r')
                    {
                        c = 0x23CE;
                    }
                }

                return text;
            };

        std::wstring report;
        const auto length = (int32_t)m_text.size();

        Paragraph paragraph;
        paragraph.Offset = 0;
        paragraph.Length = length;
        paragraph.RightToLeft = m_rtl;
        paragraph.Alignment = m_alignment;

        auto started = Now();
        auto result = BuildParagraph(paragraph);

        DWRITE_TEXT_METRICS metrics{};

        if (SUCCEEDED(result))
        {
            result = paragraph.Layout->GetMetrics(&metrics);
        }

        const auto directWriteTicks = Now() - started;

        if (FAILED(result))
        {
            return hstring(std::format(L"DirectWrite failed: 0x{:08X}\n", (uint32_t)result));
        }

        EditableTextLayout editable;

        started = Now();
        result = BuildEditable(editable);

        const auto analyzerTicks = Now() - started;

        if (FAILED(result))
        {
            return hstring(std::format(L"Analyzer failed: 0x{:08X}\n", (uint32_t)result));
        }

        report += std::format(L"{} characters, width {:.1f}\n", length, m_width);
        report += std::format(L"Build: DirectWrite {:.0f} µs, analyzer {:.0f} µs\n", micro(directWriteTicks), micro(analyzerTicks));
        report += std::format(L"Box: DirectWrite left {:.2f} width {:.2f} height {:.2f}; analyzer left {:.2f} width {:.2f} height {:.2f}\n",
            metrics.left, metrics.width, metrics.height, editable.Left(), editable.Width(), editable.Height());

        // Lines.
        std::vector<DWRITE_LINE_METRICS> lines(metrics.lineCount);
        UINT32 lineCount = 0;
        paragraph.Layout->GetLineMetrics(lines.data(), metrics.lineCount, &lineCount);
        lines.resize(lineCount);

        std::vector<EditableLine> own;
        editable.Lines(own);

        report += std::format(L"Lines: DirectWrite {}, analyzer {}\n", lines.size(), own.size());

        int32_t breaks = 0;
        int32_t heights = 0;
        float heightDelta = 0;
        float baselineDelta = 0;
        float top = 0;
        int32_t start = 0;

        for (size_t i = 0; i < lines.size() && i < own.size(); i++)
        {
            const auto& line = lines[i];
            const auto& mine = own[i];

            const auto count = (int32_t)line.length;

            if (start != mine.Start || count != mine.Length)
            {
                if (breaks++ < 5)
                {
                    report += std::format(L"  line {}: DirectWrite {}+{} \"{}\", analyzer {}+{} \"{}\"\n",
                        i, start, count, snippet(start, count), mine.Start, mine.Length, snippet(mine.Start, mine.Length));
                }
            }

            const auto height = std::abs(line.height - mine.Height);
            const auto baseline = std::abs(line.baseline - mine.Baseline);

            if (height > 0.01f || baseline > 0.01f || std::abs(top - mine.Y) > 0.01f)
            {
                if (heights++ < 5)
                {
                    report += std::format(L"  line {}: DirectWrite y {:.2f} height {:.2f} baseline {:.2f}, analyzer y {:.2f} height {:.2f} baseline {:.2f}\n",
                        i, top, line.height, line.baseline, mine.Y, mine.Height, mine.Baseline);
                }
            }

            heightDelta = std::max(heightDelta, height);
            baselineDelta = std::max(baselineDelta, baseline);

            start += count;
            top += line.height;
        }

        report += std::format(L"Line breaks that differ: {}\n", breaks);
        report += std::format(L"Line metrics that differ: {} (height by {:.2f} at most, baseline by {:.2f})\n", heights, heightDelta, baselineDelta);

        // Carets, at every position: across, down and how tall, apart, as they go wrong for
        // different reasons.
        struct CaretDelta
        {
            const wchar_t* Name;
            int32_t Count;
            float Worst;
            int32_t At;
            float Theirs;
            float Ours;
        };

        CaretDelta deltas[] =
        {
            { L"x", 0, 0, -1, 0, 0 },
            { L"y", 0, 0, -1, 0, 0 },
            { L"height", 0, 0, -1, 0, 0 }
        };

        for (int32_t i = 0; i <= length; i++)
        {
            FLOAT x;
            FLOAT y;
            DWRITE_HIT_TEST_METRICS hit;

            if (FAILED(paragraph.Layout->HitTestTextPosition((UINT32)i, FALSE, &x, &y, &hit)))
            {
                continue;
            }

            const auto caret = editable.Caret(i);
            const float theirs[] = { x, y, hit.height };
            const float ours[] = { caret.X, caret.Y, caret.Height };

            for (size_t k = 0; k < std::size(deltas); k++)
            {
                const auto delta = std::abs(theirs[k] - ours[k]);

                if (delta > 0.5f)
                {
                    deltas[k].Count++;
                }

                if (delta > deltas[k].Worst)
                {
                    deltas[k] = { deltas[k].Name, deltas[k].Count, delta, i, theirs[k], ours[k] };
                }
            }
        }

        for (const CaretDelta& delta : deltas)
        {
            report += std::format(L"Caret {} more than half a DIP apart: {} of {}, {:.2f} at most", delta.Name, delta.Count, length + 1, delta.Worst);

            if (delta.At >= 0)
            {
                report += std::format(L", at {} \"{}\": DirectWrite {:.2f}, analyzer {:.2f}", delta.At, snippet(delta.At, length - delta.At), delta.Theirs, delta.Ours);
            }

            report += L"\n";
        }

        // Hit tests, across every line.
        int32_t hits = 0;
        int32_t tested = 0;
        const auto span = std::isfinite(m_width) && m_width > 0 ? (float)m_width : metrics.width;

        top = 0;

        for (size_t i = 0; i < lines.size(); i++)
        {
            const auto y = top + lines[i].height / 2;

            for (int32_t step = 0; step <= 16; step++)
            {
                const auto x = -4 + (span + 8) * step / 16;

                BOOL isTrailingHit;
                BOOL isInside;
                DWRITE_HIT_TEST_METRICS hit;

                if (FAILED(paragraph.Layout->HitTestPoint(x, y, &isTrailingHit, &isInside, &hit)))
                {
                    continue;
                }

                EditableHit mine;
                editable.HitTest(x, y, mine);

                const auto theirs = (int32_t)hit.textPosition + (isTrailingHit ? (int32_t)hit.length : 0);
                const auto ours = mine.Position + (mine.Trailing ? mine.Length : 0);

                tested++;

                if (theirs != ours || (isInside != FALSE) != mine.Inside)
                {
                    if (hits++ < 5)
                    {
                        report += std::format(L"  hit {:.1f},{:.1f}: DirectWrite {}{}, analyzer {}{}\n",
                            x, y, theirs, isInside ? L" inside" : L"", ours, mine.Inside ? L" inside" : L"");
                    }
                }
            }

            top += lines[i].height;
        }

        report += std::format(L"Hit tests that differ: {} of {}\n", hits, tested);

        // Range rectangles, a line at a time and for the whole text: how many, and where.
        int32_t ranges = 0;
        int32_t rangesTested = 0;
        start = 0;

        for (size_t i = 0; i <= lines.size(); i++)
        {
            const auto from = i < lines.size() ? start : 0;
            const auto count = i < lines.size() ? (int32_t)lines[i].length : length;

            if (i < lines.size())
            {
                start += count;
            }

            if (count == 0)
            {
                continue;
            }

            UINT32 actual = 0;
            paragraph.Layout->HitTestTextRange((UINT32)from, (UINT32)count, 0, 0, nullptr, 0, &actual);

            std::vector<DWRITE_HIT_TEST_METRICS> theirs(actual);
            if (actual == 0 || FAILED(paragraph.Layout->HitTestTextRange((UINT32)from, (UINT32)count, 0, 0, theirs.data(), actual, &actual)))
            {
                continue;
            }

            std::vector<Windows::Foundation::Rect> ours;
            editable.Ranges(from, count, ours);

            rangesTested++;

            auto same = ours.size() == actual;

            for (UINT32 k = 0; same && k < actual; k++)
            {
                same = std::abs(theirs[k].left - ours[k].X) <= 0.5f
                    && std::abs(theirs[k].top - ours[k].Y) <= 0.5f
                    && std::abs(theirs[k].width - ours[k].Width) <= 0.5f
                    && std::abs(theirs[k].height - ours[k].Height) <= 0.5f;
            }

            if (!same && ranges++ < 5)
            {
                report += std::format(L"  range {}+{} \"{}\": DirectWrite {} rectangles, analyzer {}\n", from, count, snippet(from, count), actual, ours.size());
            }
        }

        report += std::format(L"Ranges that differ: {} of {}\n", ranges, rangesTested);

        // As ContentEnd asks DirectWrite for it.
        BOOL isTrailingHit;
        BOOL isInside;
        DWRITE_HIT_TEST_METRICS end;

        if (SUCCEEDED(paragraph.Layout->HitTestPoint(metrics.width, metrics.height, &isTrailingHit, &isInside, &end)))
        {
            const auto ours = editable.ContentEnd();

            report += std::format(L"Content end: DirectWrite {:.2f},{:.2f}, analyzer {:.2f},{:.2f}\n",
                end.left + end.width, end.top + end.height, ours.X, ours.Y);
        }

        return hstring(report);
    }
}
