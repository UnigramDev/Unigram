#include "pch.h"

// GUID_PROP_INPUTSCOPE is a DEFINE_GUID, defined only where initguid.h comes first.
#include <initguid.h>
#include <InputScope.h>

#include "CoreTextEditContext.h"
#if __has_include("Text/CoreTextTextRequest.g.cpp")
#include "Text/CoreTextTextRequest.g.cpp"
#endif
#if __has_include("Text/CoreTextTextRequestedEventArgs.g.cpp")
#include "Text/CoreTextTextRequestedEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextSelectionRequest.g.cpp")
#include "Text/CoreTextSelectionRequest.g.cpp"
#endif
#if __has_include("Text/CoreTextSelectionRequestedEventArgs.g.cpp")
#include "Text/CoreTextSelectionRequestedEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextTextUpdatingEventArgs.g.cpp")
#include "Text/CoreTextTextUpdatingEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextSelectionUpdatingEventArgs.g.cpp")
#include "Text/CoreTextSelectionUpdatingEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextFormatUpdatingEventArgs.g.cpp")
#include "Text/CoreTextFormatUpdatingEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextLayoutBounds.g.cpp")
#include "Text/CoreTextLayoutBounds.g.cpp"
#endif
#if __has_include("Text/CoreTextLayoutRequest.g.cpp")
#include "Text/CoreTextLayoutRequest.g.cpp"
#endif
#if __has_include("Text/CoreTextLayoutRequestedEventArgs.g.cpp")
#include "Text/CoreTextLayoutRequestedEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextCompositionStartedEventArgs.g.cpp")
#include "Text/CoreTextCompositionStartedEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextCompositionCompletedEventArgs.g.cpp")
#include "Text/CoreTextCompositionCompletedEventArgs.g.cpp"
#endif
#if __has_include("Text/CoreTextEditContext.g.cpp")
#include "Text/CoreTextEditContext.g.cpp"
#endif
#if __has_include("Text/CoreTextServicesManager.g.cpp")
#include "Text/CoreTextServicesManager.g.cpp"
#endif

#include <cmath>
#include <oleauto.h>
#include <olectl.h>

using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::UI::Text;
using namespace winrt::Windows::UI::Text::Core;
using namespace winrt::Windows::UI::ViewManagement;

namespace winrt::Telegram::Native::Text::implementation
{
    // The one view a store has; TSF asks for it by cookie and never gets another.
    static constexpr TsViewCookie ActiveView = 1;

    static CoreTextRange MakeRange(LONG start, LONG end)
    {
        return CoreTextRange{ start, end };
    }

    static RECT ToRect(Rect const& rect)
    {
        return RECT{
            static_cast<LONG>(std::lround(rect.X)),
            static_cast<LONG>(std::lround(rect.Y)),
            static_cast<LONG>(std::lround(rect.X + rect.Width)),
            static_cast<LONG>(std::lround(rect.Y + rect.Height))
        };
    }

    // A display attribute names its colours by system colour index; the event names them by
    // UIElementType, which has a member for only some of the indices.
    static IReference<UIElementType> ToElementType(TF_DA_COLOR const& color)
    {
        if (color.type != TF_CT_SYSCOLOR)
        {
            return nullptr;
        }

        switch (color.nIndex)
        {
        case COLOR_ACTIVECAPTION: return UIElementType::ActiveCaption;
        case COLOR_BACKGROUND: return UIElementType::Background;
        case COLOR_BTNFACE: return UIElementType::ButtonFace;
        case COLOR_BTNTEXT: return UIElementType::ButtonText;
        case COLOR_CAPTIONTEXT: return UIElementType::CaptionText;
        case COLOR_GRAYTEXT: return UIElementType::GrayText;
        case COLOR_HIGHLIGHT: return UIElementType::Highlight;
        case COLOR_HIGHLIGHTTEXT: return UIElementType::HighlightText;
        case COLOR_HOTLIGHT: return UIElementType::Hotlight;
        case COLOR_INACTIVECAPTION: return UIElementType::InactiveCaption;
        case COLOR_INACTIVECAPTIONTEXT: return UIElementType::InactiveCaptionText;
        case COLOR_WINDOW: return UIElementType::Window;
        case COLOR_WINDOWTEXT: return UIElementType::WindowText;
        default: return nullptr;
        }
    }

    static UnderlineType ToUnderlineType(TF_DISPLAYATTRIBUTE const& attribute)
    {
        switch (attribute.lsStyle)
        {
        case TF_LS_SOLID: return attribute.fBoldLine ? UnderlineType::Thick : UnderlineType::Single;
        case TF_LS_DOT: return UnderlineType::Dotted;
        case TF_LS_DASH: return UnderlineType::Dash;
        case TF_LS_SQUIGGLE: return UnderlineType::Wave;
        default: return UnderlineType::None;
        }
    }

    struct InputScopes : winrt::implements<InputScopes, ITfInputScope>
    {
        InputScopes(::InputScope scope)
            : m_scope(scope)
        {
        }

        HRESULT STDMETHODCALLTYPE GetInputScopes(::InputScope** scopes, UINT* count) noexcept override
        {
            if (scopes == nullptr || count == nullptr)
            {
                return E_INVALIDARG;
            }

            *count = 0;
            *scopes = static_cast<::InputScope*>(CoTaskMemAlloc(sizeof(::InputScope)));

            if (*scopes == nullptr)
            {
                return E_OUTOFMEMORY;
            }

            **scopes = m_scope;
            *count = 1;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE GetPhrase(BSTR**, UINT*) noexcept override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE GetRegularExpression(BSTR*) noexcept override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE GetSRGS(BSTR*) noexcept override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE GetXML(BSTR*) noexcept override { return E_NOTIMPL; }

    private:
        ::InputScope m_scope;
    };

    // TSF holds the store for as long as the context it was made with, which the edit context
    // can outlive in neither direction it would like: so the store points back without owning,
    // and the edit context clears the pointer when it goes.
    struct TextStore : winrt::implements<TextStore, ITextStoreACP, ITfContextOwnerCompositionSink, ITfTextEditSink>
    {
        CoreTextEditContext* Owner = nullptr;

        bool IsLocked() const
        {
            return m_lock != 0;
        }

        bool IsComposing() const
        {
            return m_compositions > 0;
        }

        void OnTextChange(LONG start, LONG oldEnd, LONG newEnd)
        {
            if (m_sink && (m_mask & TS_AS_TEXT_CHANGE))
            {
                TS_TEXTCHANGE change{ start, oldEnd, newEnd };
                m_sink->OnTextChange(0, &change);
            }
        }

        void OnSelectionChange()
        {
            if (m_sink && (m_mask & TS_AS_SEL_CHANGE))
            {
                m_sink->OnSelectionChange();
            }
        }

        void OnLayoutChange()
        {
            if (m_sink && (m_mask & TS_AS_LAYOUT_CHANGE))
            {
                m_sink->OnLayoutChange(TS_LC_CHANGE, ActiveView);
            }
        }

        HRESULT STDMETHODCALLTYPE AdviseSink(REFIID riid, IUnknown* punk, DWORD mask) noexcept override
        {
            if (punk == nullptr || !IsEqualIID(riid, __uuidof(ITextStoreACPSink)))
            {
                return E_INVALIDARG;
            }

            winrt::com_ptr<IUnknown> identity;
            punk->QueryInterface(IID_PPV_ARGS(identity.put()));

            if (m_sink)
            {
                if (identity == m_sinkIdentity)
                {
                    m_mask = mask;
                    return S_OK;
                }

                return CONNECT_E_ADVISELIMIT;
            }

            HRESULT hr = punk->QueryInterface(IID_PPV_ARGS(m_sink.put()));
            if (FAILED(hr))
            {
                return hr;
            }

            m_sinkIdentity = identity;
            m_mask = mask;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE UnadviseSink(IUnknown* punk) noexcept override
        {
            if (punk == nullptr)
            {
                return E_INVALIDARG;
            }

            winrt::com_ptr<IUnknown> identity;
            punk->QueryInterface(IID_PPV_ARGS(identity.put()));

            if (!m_sink || identity != m_sinkIdentity)
            {
                return CONNECT_E_NOCONNECTION;
            }

            m_sink = nullptr;
            m_sinkIdentity = nullptr;
            m_mask = 0;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE RequestLock(DWORD flags, HRESULT* session) noexcept override
        {
            if (session == nullptr)
            {
                return E_INVALIDARG;
            }

            if (!m_sink)
            {
                return E_UNEXPECTED;
            }

            if (m_lock != 0)
            {
                if (flags & TS_LF_SYNC)
                {
                    *session = TS_E_SYNCHRONOUS;
                    return S_OK;
                }

                // Granted once the lock held now is released, a read and a write merging into
                // the write.
                m_queued |= flags & TS_LF_READWRITE;
                *session = TS_S_ASYNC;
                return S_OK;
            }

            auto sink = m_sink;

            m_lock = flags & TS_LF_READWRITE;
            *session = sink->OnLockGranted(m_lock);
            m_lock = 0;

            while (m_queued != 0 && m_sink)
            {
                m_lock = std::exchange(m_queued, 0);
                m_sink->OnLockGranted(m_lock);
                m_lock = 0;
            }

            m_queued = 0;

            try
            {
                if (Owner)
                {
                    Owner->FlushDeferred();
                }
            }
            catch (...)
            {
                // The lock was granted; what the owner asked for in the meantime failed on its own.
            }

            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE GetStatus(TS_STATUS* status) noexcept override
        {
            if (status == nullptr)
            {
                return E_INVALIDARG;
            }

            status->dwDynamicFlags = 0;
            status->dwStaticFlags = TS_SS_NOHIDDENTEXT;

            // The scope the owner gives text that takes no suggestions and no autocorrect.
            if (Owner && Owner->m_inputScope != CoreTextInputScope::Default)
            {
                status->dwStaticFlags |= TS_SS_TKBAUTOCORRECTENABLE | TS_SS_TKBPREDICTIONENABLE;
            }

            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE QueryInsert(LONG start, LONG end, ULONG, LONG* resultStart, LONG* resultEnd) noexcept override
        {
            if (resultStart == nullptr || resultEnd == nullptr)
            {
                return E_INVALIDARG;
            }

            if (!Owner || start < 0 || start > end || end > Owner->m_length)
            {
                return E_INVALIDARG;
            }

            *resultStart = start;
            *resultEnd = end;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE GetSelection(ULONG index, ULONG count, TS_SELECTION_ACP* selection, ULONG* fetched) noexcept override
        try
        {
            if (selection == nullptr || fetched == nullptr)
            {
                return E_INVALIDARG;
            }

            *fetched = 0;

            if (!CanRead())
            {
                return TS_E_NOLOCK;
            }

            if (index != 0 && index != TS_DEFAULT_SELECTION)
            {
                return E_INVALIDARG;
            }

            if (!Owner || count == 0)
            {
                return S_OK;
            }

            auto range = Owner->RequestSelection();

            selection->acpStart = range.StartCaretPosition;
            selection->acpEnd = range.EndCaretPosition;
            selection->style.ase = TS_AE_END;
            selection->style.fInterimChar = FALSE;
            *fetched = 1;
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE SetSelection(ULONG count, TS_SELECTION_ACP const* selection) noexcept override
        try
        {
            if (selection == nullptr || count == 0)
            {
                return E_INVALIDARG;
            }

            if (!CanWrite())
            {
                return TS_E_NOLOCK;
            }

            if (!Owner)
            {
                return E_UNEXPECTED;
            }

            auto start = selection->acpStart;
            auto end = selection->acpEnd;

            if (start < 0 || start > end || end > Owner->m_length)
            {
                return TS_E_INVALIDPOS;
            }

            auto args = winrt::make<CoreTextSelectionUpdatingEventArgs>(MakeRange(start, end));
            Owner->m_selectionUpdating(*Owner, args);

            return args.Result() == CoreTextSelectionUpdatingResult::Succeeded ? S_OK : E_FAIL;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE GetText(LONG start, LONG end, WCHAR* plain, ULONG plainCount, ULONG* plainFetched, TS_RUNINFO* runs, ULONG runCount, ULONG* runFetched, LONG* next) noexcept override
        try
        {
            if (plainFetched == nullptr || runFetched == nullptr || next == nullptr)
            {
                return E_INVALIDARG;
            }

            *plainFetched = 0;
            *runFetched = 0;
            *next = start;

            if (!CanRead())
            {
                return TS_E_NOLOCK;
            }

            if (!Owner)
            {
                return E_UNEXPECTED;
            }

            auto length = Owner->m_length;
            if (end == -1)
            {
                end = length;
            }

            if (start < 0 || start > end || end > length)
            {
                return TS_E_INVALIDPOS;
            }

            auto count = static_cast<ULONG>(end - start);

            // Asked for the runs alone: the text is all plain, so they are the range.
            if (plainCount == 0 || plain == nullptr)
            {
                if (runCount > 0 && runs != nullptr && count > 0)
                {
                    runs[0] = TS_RUNINFO{ count, TS_RT_PLAIN };
                    *runFetched = 1;
                }

                *next = end;
                return S_OK;
            }

            count = std::min(count, plainCount);

            ULONG copied = 0;
            if (count > 0)
            {
                auto text = Owner->RequestText(start, start + static_cast<LONG>(count));

                copied = std::min(count, static_cast<ULONG>(text.size()));
                std::copy_n(text.data(), copied, plain);
            }

            *plainFetched = copied;

            if (runCount > 0 && runs != nullptr && copied > 0)
            {
                runs[0] = TS_RUNINFO{ copied, TS_RT_PLAIN };
                *runFetched = 1;
            }

            *next = start + static_cast<LONG>(copied);
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE SetText(DWORD, LONG start, LONG end, WCHAR const* text, ULONG count, TS_TEXTCHANGE* change) noexcept override
        try
        {
            if (change == nullptr || (text == nullptr && count > 0))
            {
                return E_INVALIDARG;
            }

            if (!CanWrite())
            {
                return TS_E_NOLOCK;
            }

            if (!Owner)
            {
                return E_UNEXPECTED;
            }

            if (start < 0 || start > end || end > Owner->m_length)
            {
                return TS_E_INVALIDPOS;
            }

            auto inserted = static_cast<LONG>(count);
            auto delta = inserted - (end - start);

            // The selection where the edit leaves it: before the range it stays, after it moves
            // with it, and inside it goes to the end of what replaced it.
            auto shift = [&](LONG position)
            {
                if (position < start || (position == start && start < end))
                {
                    return position;
                }

                return position >= end ? position + delta : start + inserted;
            };

            auto selection = Owner->RequestSelection();
            auto moved = MakeRange(shift(selection.StartCaretPosition), shift(selection.EndCaretPosition));

            if (!Owner->UpdateText(start, end, winrt::hstring{ text, count }, moved))
            {
                return E_FAIL;
            }

            *change = TS_TEXTCHANGE{ start, end, start + inserted };
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE GetFormattedText(LONG, LONG, IDataObject**) noexcept override
        {
            return E_NOTIMPL;
        }

        HRESULT STDMETHODCALLTYPE GetEmbedded(LONG, REFGUID, REFIID, IUnknown**) noexcept override
        {
            return E_NOTIMPL;
        }

        HRESULT STDMETHODCALLTYPE QueryInsertEmbedded(GUID const*, FORMATETC const*, BOOL* insertable) noexcept override
        {
            if (insertable == nullptr)
            {
                return E_INVALIDARG;
            }

            *insertable = FALSE;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE InsertEmbedded(DWORD, LONG, LONG, IDataObject*, TS_TEXTCHANGE*) noexcept override
        {
            return E_NOTIMPL;
        }

        HRESULT STDMETHODCALLTYPE InsertTextAtSelection(DWORD flags, WCHAR const* text, ULONG count, LONG* resultStart, LONG* resultEnd, TS_TEXTCHANGE* change) noexcept override
        try
        {
            if (!Owner)
            {
                return E_UNEXPECTED;
            }

            if (flags & TS_IAS_QUERYONLY)
            {
                if (!CanRead())
                {
                    return TS_E_NOLOCK;
                }

                auto selection = Owner->RequestSelection();

                if (resultStart)
                {
                    *resultStart = selection.StartCaretPosition;
                }

                if (resultEnd)
                {
                    *resultEnd = selection.EndCaretPosition;
                }

                return S_OK;
            }

            if (text == nullptr && count > 0)
            {
                return E_INVALIDARG;
            }

            if (!CanWrite())
            {
                return TS_E_NOLOCK;
            }

            auto selection = Owner->RequestSelection();
            auto start = selection.StartCaretPosition;
            auto end = selection.EndCaretPosition;
            auto caret = start + static_cast<LONG>(count);

            if (!Owner->UpdateText(start, end, winrt::hstring{ text, count }, MakeRange(caret, caret)))
            {
                return E_FAIL;
            }

            if ((flags & TS_IAS_NOQUERY) == 0)
            {
                if (resultStart)
                {
                    *resultStart = start;
                }

                if (resultEnd)
                {
                    *resultEnd = caret;
                }
            }

            if (change)
            {
                *change = TS_TEXTCHANGE{ start, end, caret };
            }

            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE InsertEmbeddedAtSelection(DWORD, IDataObject*, LONG*, LONG*, TS_TEXTCHANGE*) noexcept override
        {
            return E_NOTIMPL;
        }

        // The one attribute the store has is the input scope, which text services read through
        // these when focus arrives.
        HRESULT STDMETHODCALLTYPE RequestSupportedAttrs(DWORD, ULONG count, TS_ATTRID const* attributes) noexcept override
        {
            WantAttributes(count, attributes);
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE RequestAttrsAtPosition(LONG, ULONG count, TS_ATTRID const* attributes, DWORD) noexcept override
        {
            WantAttributes(count, attributes);
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE RequestAttrsTransitioningAtPosition(LONG, ULONG, TS_ATTRID const*, DWORD) noexcept override
        {
            m_wantInputScope = false;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE FindNextAttrTransition(LONG, LONG halt, ULONG, TS_ATTRID const*, DWORD, LONG* next, BOOL* found, LONG* foundOffset) noexcept override
        {
            if (next == nullptr || found == nullptr || foundOffset == nullptr)
            {
                return E_INVALIDARG;
            }

            *next = halt;
            *found = FALSE;
            *foundOffset = 0;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE RetrieveRequestedAttrs(ULONG count, TS_ATTRVAL* values, ULONG* fetched) noexcept override
        try
        {
            if (values == nullptr || fetched == nullptr)
            {
                return E_INVALIDARG;
            }

            *fetched = 0;

            if (count == 0 || !m_wantInputScope || !Owner)
            {
                return S_OK;
            }

            m_wantInputScope = false;

            // CoreTextInputScope takes its values from the InputScope enumeration.
            auto scope = winrt::make_self<InputScopes>(static_cast<::InputScope>(Owner->m_inputScope));

            values[0].idAttr = GUID_PROP_INPUTSCOPE;
            values[0].dwOverlapId = 0;
            values[0].varValue.vt = VT_UNKNOWN;
            values[0].varValue.punkVal = static_cast<ITfInputScope*>(scope.detach());
            *fetched = 1;
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE GetEndACP(LONG* end) noexcept override
        {
            if (end == nullptr)
            {
                return E_INVALIDARG;
            }

            if (!CanRead())
            {
                return TS_E_NOLOCK;
            }

            *end = Owner ? Owner->m_length : 0;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE GetActiveView(TsViewCookie* view) noexcept override
        {
            if (view == nullptr)
            {
                return E_INVALIDARG;
            }

            *view = ActiveView;
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE GetACPFromPoint(TsViewCookie, POINT const*, DWORD, LONG*) noexcept override
        {
            return E_NOTIMPL;
        }

        HRESULT STDMETHODCALLTYPE GetTextExt(TsViewCookie view, LONG start, LONG end, RECT* rect, BOOL* clipped) noexcept override
        try
        {
            if (rect == nullptr || clipped == nullptr || view != ActiveView)
            {
                return E_INVALIDARG;
            }

            if (!CanRead())
            {
                return TS_E_NOLOCK;
            }

            if (!Owner)
            {
                return E_UNEXPECTED;
            }

            *rect = ToRect(Owner->RequestLayout(start, end).TextBounds());
            *clipped = FALSE;
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE GetScreenExt(TsViewCookie view, RECT* rect) noexcept override
        try
        {
            if (rect == nullptr || view != ActiveView)
            {
                return E_INVALIDARG;
            }

            if (!Owner)
            {
                return E_UNEXPECTED;
            }

            auto selection = Owner->RequestSelection();
            *rect = ToRect(Owner->RequestLayout(selection.StartCaretPosition, selection.EndCaretPosition).ControlBounds());
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE GetWnd(TsViewCookie view, HWND* window) noexcept override
        {
            if (window == nullptr || view != ActiveView)
            {
                return E_INVALIDARG;
            }

            *window = Owner && Owner->m_window ? Owner->m_window : GetFocus();
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE OnStartComposition(ITfCompositionView*, BOOL* ok) noexcept override
        try
        {
            if (ok == nullptr)
            {
                return E_INVALIDARG;
            }

            *ok = TRUE;

            if (m_compositions++ == 0 && Owner)
            {
                Owner->m_compositionStarted(*Owner, winrt::make<CoreTextCompositionStartedEventArgs>());
            }

            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        HRESULT STDMETHODCALLTYPE OnUpdateComposition(ITfCompositionView*, ITfRange*) noexcept override
        {
            return S_OK;
        }

        HRESULT STDMETHODCALLTYPE OnEndComposition(ITfCompositionView*) noexcept override
        try
        {
            if (m_compositions > 0 && --m_compositions == 0 && Owner)
            {
                Owner->m_compositionCompleted(*Owner, winrt::make<CoreTextCompositionCompletedEventArgs>());
            }

            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

        // How the composition looks is a property TSF keeps on the context, not something the
        // store is told: read after every edit while there is one.
        HRESULT STDMETHODCALLTYPE OnEndEdit(ITfContext* context, TfEditCookie cookie, ITfEditRecord*) noexcept override
        try
        {
            if (m_compositions > 0 && Owner)
            {
                Owner->ReportFormats(context, cookie);
            }

            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }

    private:
        bool CanRead() const
        {
            return (m_lock & TS_LF_READ) == TS_LF_READ;
        }

        bool CanWrite() const
        {
            return (m_lock & TS_LF_READWRITE) == TS_LF_READWRITE;
        }

        void WantAttributes(ULONG count, TS_ATTRID const* attributes)
        {
            m_wantInputScope = false;

            for (ULONG i = 0; attributes != nullptr && i < count; i++)
            {
                if (IsEqualGUID(attributes[i], GUID_PROP_INPUTSCOPE))
                {
                    m_wantInputScope = true;
                }
            }
        }

        winrt::com_ptr<ITextStoreACPSink> m_sink;
        winrt::com_ptr<IUnknown> m_sinkIdentity;
        DWORD m_mask = 0;

        DWORD m_lock = 0;
        DWORD m_queued = 0;

        int32_t m_compositions = 0;
        bool m_wantInputScope = false;
    };

    CoreTextLayoutRequest::CoreTextLayoutRequest(CoreTextRange range)
        : m_range(range)
        , m_layoutBounds(winrt::make<CoreTextLayoutBounds>())
    {
    }


    CoreTextEditContext::CoreTextEditContext(CoreTextServicesManager* manager)
    {
        m_manager.copy_from(manager);
        m_store = winrt::make_self<TextStore>();
        m_store->Owner = this;

        try
        {
            auto threadMgr = manager->ThreadMgr();
            winrt::check_hresult(threadMgr->CreateDocumentMgr(m_documentMgr.put()));
            winrt::check_hresult(m_documentMgr->CreateContext(manager->ClientId(), 0, static_cast<ITextStoreACP*>(m_store.get()), m_context.put(), &m_contextCookie));
            winrt::check_hresult(m_documentMgr->Push(m_context.get()));

            if (auto source = m_context.try_as<ITfSource>())
            {
                source->AdviseSink(__uuidof(ITfTextEditSink), static_cast<ITfTextEditSink*>(m_store.get()), &m_textEditCookie);
            }
        }
        catch (...)
        {
            m_store->Owner = nullptr;
            throw;
        }

        manager->Register(this);
    }

    CoreTextEditContext::~CoreTextEditContext()
    {
        m_manager->Unregister(this);
        m_store->Owner = nullptr;

        if (m_textEditCookie != TF_INVALID_COOKIE)
        {
            if (auto source = m_context.try_as<ITfSource>())
            {
                source->UnadviseSink(m_textEditCookie);
            }
        }

        auto threadMgr = m_manager->ThreadMgr();

        winrt::com_ptr<ITfDocumentMgr> focus;
        if (SUCCEEDED(threadMgr->GetFocus(focus.put())) && focus.get() == m_documentMgr.get())
        {
            threadMgr->SetFocus(m_manager->EmptyDocumentMgr());
        }

        m_documentMgr->Pop(TF_POPF_ALL);
    }

    void CoreTextEditContext::NotifyFocusEnter()
    {
        if (m_store->IsLocked())
        {
            m_deferredEnter = true;
            return;
        }

        EnterFocus();
    }

    void CoreTextEditContext::NotifyFocusLeave()
    {
        if (m_store->IsLocked())
        {
            m_deferredLeave = true;
            m_deferredEnter = false;
            return;
        }

        LeaveFocus();
    }

    void CoreTextEditContext::EnterFocus()
    {
        m_window = GetFocus();

        // Before SetFocus, which raises OnSetFocus on the way, for every context, this one too.
        m_focused = true;
        m_manager->ThreadMgr()->SetFocus(m_documentMgr.get());
    }

    void CoreTextEditContext::LeaveFocus()
    {
        m_focused = false;

        // Leaving ends what was being composed, as it does for a text box.
        if (m_store->IsComposing())
        {
            if (auto services = m_context.try_as<ITfContextOwnerCompositionServices>())
            {
                services->TerminateComposition(nullptr);
            }
        }

        // Only if it is still here: another context may have taken it already, and TSF focus is
        // the thread's, not this context's to drop.
        auto threadMgr = m_manager->ThreadMgr();

        winrt::com_ptr<ITfDocumentMgr> focus;
        if (SUCCEEDED(threadMgr->GetFocus(focus.put())) && focus.get() == m_documentMgr.get())
        {
            threadMgr->SetFocus(m_manager->EmptyDocumentMgr());
        }
    }

    void CoreTextEditContext::OnFocusMoved(ITfDocumentMgr* focus)
    {
        if (m_focused && focus != m_documentMgr.get())
        {
            m_focused = false;
            m_focusRemoved(*this, nullptr);
        }
    }

    void CoreTextEditContext::NotifyTextChanged(CoreTextRange const& modifiedRange, int32_t newLength, CoreTextRange const& newSelection)
    {
        auto previous = m_length;
        auto start = std::clamp<LONG>(modifiedRange.StartCaretPosition, 0, m_length);
        auto oldEnd = std::clamp<LONG>(modifiedRange.EndCaretPosition, start, m_length);
        auto newEnd = start + std::max<LONG>(newLength, 0);

        m_length = m_length - (oldEnd - start) + (newEnd - start);

        if (m_store->IsLocked())
        {
            if (m_deferredText)
            {
                // Two changes made into one: everything from the first position either touched.
                m_deferredStart = std::min(m_deferredStart, start);
                m_deferredOldEnd = m_deferredLength;
                m_deferredNewEnd = m_length;
            }
            else
            {
                m_deferredText = true;
                m_deferredLength = previous;
                m_deferredStart = start;
                m_deferredOldEnd = oldEnd;
                m_deferredNewEnd = newEnd;
            }

            m_deferredSelection = true;
            return;
        }

        m_store->OnTextChange(start, oldEnd, newEnd);
        m_store->OnSelectionChange();
    }

    void CoreTextEditContext::NotifySelectionChanged(CoreTextRange const&)
    {
        if (m_store->IsLocked())
        {
            m_deferredSelection = true;
            return;
        }

        m_store->OnSelectionChange();
    }

    void CoreTextEditContext::NotifyLayoutChanged()
    {
        if (m_store->IsLocked())
        {
            m_deferredLayout = true;
            return;
        }

        m_store->OnLayoutChange();
    }

    void CoreTextEditContext::FlushDeferred()
    {
        if (std::exchange(m_deferredLeave, false))
        {
            LeaveFocus();
        }

        if (std::exchange(m_deferredEnter, false))
        {
            EnterFocus();
        }

        if (std::exchange(m_deferredText, false))
        {
            m_store->OnTextChange(m_deferredStart, m_deferredOldEnd, m_deferredNewEnd);
        }

        if (std::exchange(m_deferredSelection, false))
        {
            m_store->OnSelectionChange();
        }

        if (std::exchange(m_deferredLayout, false))
        {
            m_store->OnLayoutChange();
        }
    }

    void CoreTextEditContext::ReceiveCharacter(char16_t character)
    {
        if (!m_focused || m_store->IsLocked() || m_store->IsComposing())
        {
            m_highSurrogate = 0;
            return;
        }

        wchar_t buffer[2];
        uint32_t count;

        if (IS_HIGH_SURROGATE(character))
        {
            m_highSurrogate = character;
            return;
        }
        else if (IS_LOW_SURROGATE(character))
        {
            if (m_highSurrogate == 0)
            {
                return;
            }

            buffer[0] = m_highSurrogate;
            buffer[1] = character;
            count = 2;
        }
        else if (character < 0x20 || character == 0x7F)
        {
            // Enter, Tab, Backspace and the control keys are keys, which the owner handles.
            m_highSurrogate = 0;
            return;
        }
        else
        {
            buffer[0] = character;
            count = 1;
        }

        m_highSurrogate = 0;

        auto selection = RequestSelection();
        auto start = selection.StartCaretPosition;
        auto end = selection.EndCaretPosition;
        auto caret = start + static_cast<LONG>(count);

        if (UpdateText(start, end, winrt::hstring{ buffer, count }, MakeRange(caret, caret)))
        {
            m_store->OnTextChange(start, end, caret);
            m_store->OnSelectionChange();
        }
    }

    CoreTextRange CoreTextEditContext::RequestSelection()
    {
        auto request = winrt::make<CoreTextSelectionRequest>();
        m_selectionRequested(*this, winrt::make<CoreTextSelectionRequestedEventArgs>(request));

        auto selection = request.Selection();
        auto start = std::clamp<LONG>(std::min(selection.StartCaretPosition, selection.EndCaretPosition), 0, m_length);
        auto end = std::clamp<LONG>(std::max(selection.StartCaretPosition, selection.EndCaretPosition), start, m_length);

        return MakeRange(start, end);
    }

    winrt::hstring CoreTextEditContext::RequestText(LONG start, LONG end)
    {
        auto request = winrt::make<CoreTextTextRequest>(MakeRange(start, end));
        m_textRequested(*this, winrt::make<CoreTextTextRequestedEventArgs>(request));

        return request.Text();
    }

    winrt::Telegram::Native::Text::CoreTextLayoutBounds CoreTextEditContext::RequestLayout(LONG start, LONG end)
    {
        auto request = winrt::make<CoreTextLayoutRequest>(MakeRange(start, end));
        m_layoutRequested(*this, winrt::make<CoreTextLayoutRequestedEventArgs>(request));

        return request.LayoutBounds();
    }

    bool CoreTextEditContext::UpdateText(LONG start, LONG end, winrt::hstring const& text, CoreTextRange newSelection)
    {
        auto args = winrt::make<CoreTextTextUpdatingEventArgs>(MakeRange(start, end), text, newSelection);
        m_textUpdating(*this, args);

        if (args.Result() != CoreTextTextUpdatingResult::Succeeded)
        {
            return false;
        }

        m_length = std::max<LONG>(0, m_length - (end - start) + static_cast<LONG>(text.size()));
        return true;
    }

    void CoreTextEditContext::ReportFormats(ITfContext* context, TfEditCookie cookie)
    {
        auto categoryMgr = m_manager->CategoryMgr();
        auto displayAttributeMgr = m_manager->DisplayAttributeMgr();

        if (categoryMgr == nullptr || displayAttributeMgr == nullptr)
        {
            return;
        }

        winrt::com_ptr<ITfProperty> property;
        if (FAILED(context->GetProperty(GUID_PROP_ATTRIBUTE, property.put())))
        {
            return;
        }

        winrt::com_ptr<IEnumTfRanges> ranges;
        if (FAILED(property->EnumRanges(cookie, ranges.put(), nullptr)))
        {
            return;
        }

        while (true)
        {
            winrt::com_ptr<ITfRange> range;
            ULONG fetched = 0;

            if (ranges->Next(1, range.put(), &fetched) != S_OK || fetched == 0)
            {
                break;
            }

            VARIANT value;
            VariantInit(&value);

            if (FAILED(property->GetValue(cookie, range.get(), &value)) || value.vt != VT_I4)
            {
                VariantClear(&value);
                continue;
            }

            GUID guid;
            auto atom = static_cast<TfGuidAtom>(value.lVal);
            VariantClear(&value);

            if (FAILED(categoryMgr->GetGUID(atom, &guid)))
            {
                continue;
            }

            winrt::com_ptr<ITfDisplayAttributeInfo> info;
            TF_DISPLAYATTRIBUTE attribute;

            if (FAILED(displayAttributeMgr->GetDisplayAttributeInfo(guid, info.put(), nullptr)) || FAILED(info->GetAttributeInfo(&attribute)))
            {
                continue;
            }

            auto acp = range.try_as<ITfRangeACP>();
            LONG start;
            LONG length;

            if (!acp || FAILED(acp->GetExtent(&start, &length)))
            {
                continue;
            }

            IReference<UnderlineType> underline{ ToUnderlineType(attribute) };
            auto args = winrt::make<CoreTextFormatUpdatingEventArgs>(MakeRange(start, start + length), underline, ToElementType(attribute.crLine), ToElementType(attribute.crBk));

            m_formatUpdating(*this, args);
        }
    }


    winrt::Telegram::Native::Text::CoreTextServicesManager CoreTextServicesManager::GetForCurrentView()
    {
        // Never released: it lives as long as the thread, as the framework's lives as long as the
        // view, and a thread_local object would be destroyed after COM is gone.
        static thread_local void* s_current = nullptr;

        winrt::Telegram::Native::Text::CoreTextServicesManager current{ nullptr };

        if (s_current == nullptr)
        {
            auto manager = winrt::make_self<CoreTextServicesManager>();
            manager->Initialize();

            current = manager.as<winrt::Telegram::Native::Text::CoreTextServicesManager>();
            s_current = winrt::detach_abi(winrt::Telegram::Native::Text::CoreTextServicesManager{ current });
        }
        else
        {
            winrt::copy_from_abi(current, s_current);
        }

        return current;
    }

    void CoreTextServicesManager::Initialize()
    {
        m_threadMgr = winrt::create_instance<ITfThreadMgr>(CLSID_TF_ThreadMgr);
        winrt::check_hresult(m_threadMgr->Activate(&m_clientId));
        winrt::check_hresult(m_threadMgr->CreateDocumentMgr(m_emptyDocumentMgr.put()));

        auto source = m_threadMgr.as<ITfSource>();
        source->AdviseSink(__uuidof(ITfThreadMgrEventSink), static_cast<ITfThreadMgrEventSink*>(this), &m_threadMgrEventCookie);
        source->AdviseSink(__uuidof(ITfInputProcessorProfileActivationSink), static_cast<ITfInputProcessorProfileActivationSink*>(this), &m_profileActivationCookie);
    }

    winrt::Telegram::Native::Text::CoreTextEditContext CoreTextServicesManager::CreateEditContext()
    {
        return winrt::make<CoreTextEditContext>(this);
    }

    winrt::Windows::Globalization::Language CoreTextServicesManager::InputLanguage()
    {
        // A text service's profile sets the thread's layout to its language as well, so this
        // answers for an IME as for a plain keyboard.
        auto layout = GetKeyboardLayout(0);
        auto language = LOWORD(reinterpret_cast<UINT_PTR>(layout));

        wchar_t name[LOCALE_NAME_MAX_LENGTH];
        if (LCIDToLocaleName(MAKELCID(language, SORT_DEFAULT), name, LOCALE_NAME_MAX_LENGTH, 0) == 0)
        {
            return nullptr;
        }

        return winrt::Windows::Globalization::Language{ name };
    }

    ITfCategoryMgr* CoreTextServicesManager::CategoryMgr()
    {
        if (!m_categoryMgr)
        {
            m_categoryMgr = winrt::try_create_instance<ITfCategoryMgr>(CLSID_TF_CategoryMgr);
        }

        return m_categoryMgr.get();
    }

    ITfDisplayAttributeMgr* CoreTextServicesManager::DisplayAttributeMgr()
    {
        if (!m_displayAttributeMgr)
        {
            m_displayAttributeMgr = winrt::try_create_instance<ITfDisplayAttributeMgr>(CLSID_TF_DisplayAttributeMgr);
        }

        return m_displayAttributeMgr.get();
    }

    void CoreTextServicesManager::Register(CoreTextEditContext* context)
    {
        m_contexts.push_back(context);
    }

    void CoreTextServicesManager::Unregister(CoreTextEditContext* context)
    {
        std::erase(m_contexts, context);
    }

    HRESULT STDMETHODCALLTYPE CoreTextServicesManager::OnSetFocus(ITfDocumentMgr* focus, ITfDocumentMgr*) noexcept
    try
    {
        // By index: a handler may make or drop a context.
        for (size_t i = 0; i < m_contexts.size(); i++)
        {
            m_contexts[i]->OnFocusMoved(focus);
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }

    HRESULT STDMETHODCALLTYPE CoreTextServicesManager::OnActivated(DWORD, LANGID, REFCLSID, REFGUID, REFGUID, HKL, DWORD flags) noexcept
    try
    {
        if (flags & TF_IPSINK_FLAG_ACTIVE)
        {
            m_inputLanguageChanged(*this, nullptr);
        }

        return S_OK;
    }
    catch (...)
    {
        return winrt::to_hresult();
    }
}
