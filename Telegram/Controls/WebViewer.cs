using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Telegram.Native;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class WebViewerEventReceivedEventArgs : EventArgs
    {
        public string EventName { get; }

        public JsonObject EventData { get; }

        public WebViewerEventReceivedEventArgs(string eventName, JsonObject eventData)
        {
            EventName = eventName;
            EventData = eventData;
        }
    }

    public class WebViewer : ContentControl
    {
        private WebPresenter _presenter;

        public WebViewer()
        {
            DefaultStyleKey = typeof(WebViewer);

            if (ChromiumWebPresenter.IsSupported())
            {
                _presenter = new ChromiumWebPresenter();
                _presenter.EventReceived += OnEventReceived;
            }
            else
            {
                _presenter = new EdgeWebPresenter();
                _presenter.EventReceived += OnEventReceived;
            }

            Content = _presenter;
        }

        private void OnEventReceived(object sender, WebViewerEventReceivedEventArgs e)
        {
            EventReceived?.Invoke(this, e);
        }

        public event EventHandler<WebViewerEventReceivedEventArgs> EventReceived;

        public void Navigate(string uri) => _presenter.Navigate(uri);

        public void NavigateToString(string htmlContent) => _presenter.NavigateToString(htmlContent);

        public void InvokeScript(string javaScript) => _ = _presenter.InvokeScriptAsync(javaScript);

        public Task InvokeScriptAsync(string javaScript) => _presenter.InvokeScriptAsync(javaScript);

        public void Reload() => _presenter.Reload();

        public void Close()
        {
            _presenter.Close();
            _presenter = null;

            Content = null;
        }
    }

    public abstract class WebPresenter : Control
    {
        public event EventHandler<WebViewerEventReceivedEventArgs> EventReceived;

        public abstract void Navigate(string uri);

        public abstract void NavigateToString(string htmlContent);

        public abstract Task InvokeScriptAsync(string javaScript);

        public abstract void Reload();

        public abstract void Close();

        protected void OnEventReceived(string eventName, JsonObject eventData)
        {
            EventReceived?.Invoke(this, new WebViewerEventReceivedEventArgs(eventName, eventData));
        }
    }

    public class EdgeWebPresenter : WebPresenter
    {
        private WebView View;

        private readonly TaskCompletionSource<bool> _templatedApplied = new();

        public EdgeWebPresenter()
        {
            DefaultStyleKey = typeof(EdgeWebPresenter);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            View = GetTemplateChild(nameof(View)) as WebView;
            View.NavigationStarting += OnNavigationStarting;
            View.Unloaded += OnUnloaded;

            _templatedApplied.TrySetResult(true);
        }

        private void OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramWebviewProxy(ReceiveEvent));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ReceiveEvent(string eventName, string eventData)
        {
            if (JsonObject.TryParse(eventData, out JsonObject data))
            {
                OnEventReceived(eventName, data);
            }
            else
            {
                OnEventReceived(eventName, null);
            }
        }

        public override async void Navigate(string uri)
        {
            if (await _templatedApplied.Task)
            {
                View?.Navigate(new Uri(uri));
            }
        }

        public override async void NavigateToString(string htmlContent)
        {
            if (await _templatedApplied.Task)
            {
                View?.NavigateToString(htmlContent);
            }
        }

        public override async Task InvokeScriptAsync(string javaScript)
        {
            if (await _templatedApplied.Task && View != null)
            {
                await View.InvokeScriptAsync("eval", new[] { javaScript });
            }
        }

        public override async void Reload()
        {
            if (await _templatedApplied.Task)
            {
                View?.Refresh();
            }
        }

        public override async void Close()
        {
            if (await _templatedApplied.Task)
            {
                View?.NavigateToString(string.Empty);
            }
        }
    }

    public class ChromiumWebPresenter : WebPresenter
    {
        private WebView2 View;

        private readonly TaskCompletionSource<bool> _templatedApplied = new();

        public ChromiumWebPresenter()
        {
            DefaultStyleKey = typeof(ChromiumWebPresenter);
        }

        public static bool IsSupported()
        {
            try
            {
                return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch
            {
                return false;
            }
        }

        protected override async void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            View = GetTemplateChild(nameof(View)) as WebView2;
            View.WebMessageReceived += OnWebMessageReceived;

            await View.EnsureCoreWebView2Async();
            await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"window.external={invoke:s=>window.chrome.webview.postMessage(s)}");
            await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
window.TelegramWebviewProxy = {
postEvent: function(eventType, eventData) {
	if (window.external && window.external.invoke) {
		window.external.invoke(JSON.stringify([eventType, eventData]));
	}
}
}");

            View.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            View.CoreWebView2.Settings.AreDevToolsEnabled = false;
            View.CoreWebView2.Settings.IsStatusBarEnabled = false;

            _templatedApplied.TrySetResult(true);
        }

        private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = args.TryGetWebMessageAsString();

            if (JsonArray.TryParse(json, out JsonArray message))
            {
                var eventName = message.GetStringAt(0);
                var eventData = message.GetStringAt(1);

                if (JsonObject.TryParse(eventData, out JsonObject data))
                {
                    OnEventReceived(eventName, data);
                }
                else
                {
                    OnEventReceived(eventName, null);
                }
            }
        }

        public override async void Navigate(string uri)
        {
            if (await _templatedApplied.Task)
            {
                View?.CoreWebView2?.Navigate(uri);
            }
        }

        public override async void NavigateToString(string htmlContent)
        {
            if (await _templatedApplied.Task)
            {
                View?.CoreWebView2?.NavigateToString(htmlContent);
            }
        }

        public override async Task InvokeScriptAsync(string javaScript)
        {
            if (await _templatedApplied.Task && View?.CoreWebView2 != null)
            {
                await View.CoreWebView2.ExecuteScriptAsync(javaScript);
            }
        }

        public override async void Reload()
        {
            if (await _templatedApplied.Task)
            {
                View?.Reload();
            }
        }

        public override async void Close()
        {
            if (await _templatedApplied.Task)
            {
                View?.Close();
            }
        }
    }
}
