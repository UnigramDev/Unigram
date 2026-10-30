#pragma once

#include "WidgetTheme.g.h"
#include "WidgetOptions.g.h"
#include "WidgetStorageCommit.g.h"
#include "WidgetRequest.g.h"

#include "twidget/widget_host.h"

#include <limits>

namespace winrt::Telegram::Native::Widgets::implementation
{
    struct WidgetTheme : WidgetThemeT<WidgetTheme>
    {
        WidgetTheme() = default;

        hstring ColorScheme() const { return m_colorScheme; }
        void ColorScheme(hstring const& value) { m_colorScheme = value; }
        Windows::Foundation::Collections::IMap<hstring, hstring> Params() const { return m_params; }

        hstring ButtonBackground() const { return m_buttonBackground; }
        void ButtonBackground(hstring const& value) { m_buttonBackground = value; }
        hstring ButtonForeground() const { return m_buttonForeground; }
        void ButtonForeground(hstring const& value) { m_buttonForeground = value; }
        hstring ButtonRadius() const { return m_buttonRadius; }
        void ButtonRadius(hstring const& value) { m_buttonRadius = value; }
        hstring ButtonFont() const { return m_buttonFont; }
        void ButtonFont(hstring const& value) { m_buttonFont = value; }
        hstring ButtonPadding() const { return m_buttonPadding; }
        void ButtonPadding(hstring const& value) { m_buttonPadding = value; }
        hstring ButtonHeight() const { return m_buttonHeight; }
        void ButtonHeight(hstring const& value) { m_buttonHeight = value; }
        double ButtonPressedOpacity() const { return m_buttonPressedOpacity; }
        void ButtonPressedOpacity(double value) { m_buttonPressedOpacity = value; }
        double ButtonDisabledOpacity() const { return m_buttonDisabledOpacity; }
        void ButtonDisabledOpacity(double value) { m_buttonDisabledOpacity = value; }
        double TextFontSize() const { return m_textFontSize; }
        void TextFontSize(double value) { m_textFontSize = value; }
        double TextLineHeight() const { return m_textLineHeight; }
        void TextLineHeight(double value) { m_textLineHeight = value; }
        hstring TextPadding() const { return m_textPadding; }
        void TextPadding(hstring const& value) { m_textPadding = value; }

        twidget::WidgetTheme ToHost() const;

    private:
        hstring m_colorScheme{ L"light" };
        Windows::Foundation::Collections::IMap<hstring, hstring> m_params{ single_threaded_map<hstring, hstring>() };
        hstring m_buttonBackground;
        hstring m_buttonForeground;
        hstring m_buttonRadius;
        hstring m_buttonFont;
        hstring m_buttonPadding;
        hstring m_buttonHeight;
        double m_buttonPressedOpacity = std::numeric_limits<double>::quiet_NaN();
        double m_buttonDisabledOpacity = std::numeric_limits<double>::quiet_NaN();
        double m_textFontSize = std::numeric_limits<double>::quiet_NaN();
        double m_textLineHeight = std::numeric_limits<double>::quiet_NaN();
        hstring m_textPadding;
    };

    struct WidgetOptions : WidgetOptionsT<WidgetOptions>
    {
        WidgetOptions() = default;

        double Width() const { return m_width; }
        void Width(double value) { m_width = value; }
        double DevicePixelRatio() const { return m_dpr; }
        void DevicePixelRatio(double value) { m_dpr = value; }
        hstring Locale() const { return m_locale; }
        void Locale(hstring const& value) { m_locale = value; }
        hstring StoppedLabel() const { return m_stoppedLabel; }
        void StoppedLabel(hstring const& value) { m_stoppedLabel = value; }
        Windows::Foundation::Collections::IMap<hstring, hstring> LocalStorage() const { return m_local; }
        Windows::Foundation::Collections::IMap<hstring, hstring> SessionStorage() const { return m_session; }

        hstring ChatData() const { return m_chatData; }
        void ChatData(hstring const& value) { m_chatData = value; }
        hstring PublicData() const { return m_publicData; }
        void PublicData(hstring const& value) { m_publicData = value; }

        bool IsCopy() const { return m_isCopy; }
        void IsCopy(bool value) { m_isCopy = value; }
        bool CanQuery() const { return m_canQuery; }
        void CanQuery(bool value) { m_canQuery = value; }
        bool BackgroundQuery() const { return m_backgroundQuery; }
        void BackgroundQuery(bool value) { m_backgroundQuery = value; }
        hstring ChatType() const { return m_chatType; }
        void ChatType(hstring const& value) { m_chatType = value; }
        int64_t ViewerId() const { return m_viewerId; }
        void ViewerId(int64_t value) { m_viewerId = value; }
        hstring ViewerFirstName() const { return m_viewerFirstName; }
        void ViewerFirstName(hstring const& value) { m_viewerFirstName = value; }
        hstring ViewerLastName() const { return m_viewerLastName; }
        void ViewerLastName(hstring const& value) { m_viewerLastName = value; }
        hstring ViewerUsername() const { return m_viewerUsername; }
        void ViewerUsername(hstring const& value) { m_viewerUsername = value; }
        bool ViewerIsPremium() const { return m_viewerIsPremium; }
        void ViewerIsPremium(bool value) { m_viewerIsPremium = value; }

        Telegram::Native::Widgets::WidgetContentSafeArea ContentSafeArea() const { return m_contentSafeArea; }
        void ContentSafeArea(Telegram::Native::Widgets::WidgetContentSafeArea const& value) { m_contentSafeArea = value; }

        twidget::WidgetData ToData() const;
        twidget::WidgetContext ToContext() const;

    private:
        double m_width = 0;
        double m_dpr = 1;
        hstring m_locale;
        hstring m_stoppedLabel;
        Windows::Foundation::Collections::IMap<hstring, hstring> m_local{ single_threaded_map<hstring, hstring>() };
        Windows::Foundation::Collections::IMap<hstring, hstring> m_session{ single_threaded_map<hstring, hstring>() };
        hstring m_chatData;
        hstring m_publicData;
        bool m_isCopy = false;
        bool m_canQuery = false;
        bool m_backgroundQuery = false;
        hstring m_chatType;
        int64_t m_viewerId = 0;
        hstring m_viewerFirstName;
        hstring m_viewerLastName;
        hstring m_viewerUsername;
        bool m_viewerIsPremium = false;
        Telegram::Native::Widgets::WidgetContentSafeArea m_contentSafeArea{};
    };

    struct WidgetStorageCommit : WidgetStorageCommitT<WidgetStorageCommit>
    {
        WidgetStorageCommit(hstring area, Windows::Foundation::Collections::IMapView<hstring, hstring> values)
            : m_area(std::move(area)), m_values(std::move(values))
        {
        }

        hstring Area() const { return m_area; }
        Windows::Foundation::Collections::IMapView<hstring, hstring> Values() const { return m_values; }

    private:
        hstring m_area;
        Windows::Foundation::Collections::IMapView<hstring, hstring> m_values;
    };

    struct WidgetRequest : WidgetRequestT<WidgetRequest>
    {
        using Respond = std::function<void(const twidget::WidgetRequestOutcome&)>;

        WidgetRequest(hstring data, Respond respond)
            : m_data(std::move(data)), m_respond(std::move(respond))
        {
        }

        // A request dropped without an outcome would leave the widget busy for good.
        ~WidgetRequest() { Settle({ twidget::WidgetRequestFailure::Failed, {} }); }

        hstring Data() const { return m_data; }
        void Complete(hstring const& result);
        void Fail(Telegram::Native::Widgets::WidgetRequestFailure failure);

    private:
        void Settle(const twidget::WidgetRequestOutcome& outcome);

        hstring m_data;
        std::mutex m_lock;
        Respond m_respond;
    };

    twidget::WidgetData ToData(hstring const& chatData, hstring const& publicData);
    twidget::StorageArea ToStorageArea(Windows::Foundation::Collections::IMap<hstring, hstring> const& map);
    twidget::WidgetContentSafeArea ToContentSafeArea(Telegram::Native::Widgets::WidgetContentSafeArea const& area);
}

namespace winrt::Telegram::Native::Widgets::factory_implementation
{
    struct WidgetTheme : WidgetThemeT<WidgetTheme, implementation::WidgetTheme>
    {
    };

    struct WidgetOptions : WidgetOptionsT<WidgetOptions, implementation::WidgetOptions>
    {
    };
}
