//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class TextEditorPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly FormattedText _text;

        private readonly TaskCompletionSource<FormattedText> _tcs;
        private bool _closedExpected;

        private string _translateToLanguage;

        public TextEditorPopup(IClientService clientService, INavigationService navigationService, FormattedText text, TaskCompletionSource<FormattedText> result)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _text = text;
            _tcs = result;

            _translateToLanguage = navigationService.Session.Settings.Translate.To;

            TabStyleItems.ItemsSource = clientService.TextCompositionStyles;
            TabStyleOutput.SetText(clientService, text);

            Navigation.SelectedIndex = 1;
            Navigation.SelectionChanged += Navigation_SelectionChanged;

            Title = Strings.AIEditor;
            PrimaryButtonText = Strings.AIEditorApply;

            Closed += OnClosed;

            clientService.Session.Aggregator.Subscribe<UpdateTextCompositionStyles>(this, Handle);
        }

        private void Handle(UpdateTextCompositionStyles update)
        {
            this.BeginOnUIThread(() =>
            {
                var style = TabStyleItems.SelectedItem as TextCompositionStyle;

                TabStyleItems.ItemsSource = update.Styles;
                TabStyleItems.SelectedItem = update.Styles.FirstOrDefault(x => x.Name == style?.Name);
            });
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (_closedExpected)
            {
                _closedExpected = false;
                return;
            }

            _tcs.TrySetResult(null);
            _clientService.Session.Aggregator.Unsubscribe(this);
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabTranslate.Visibility = Navigation.SelectedIndex == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            TabStyle.Visibility = Navigation.SelectedIndex == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
            TabFix.Visibility = Navigation.SelectedIndex == 2
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (Navigation.SelectedIndex == 0 && _translations == null)
            {
                InitializeTranslate();
            }
            else if (Navigation.SelectedIndex == 2 && _fix == null)
            {
                InitializeFix();
            }
        }

        private void InitializeTranslate()
        {
            _translations = new Dictionary<string, object>();

            TabTranslateOriginal.SetText(_clientService, _text);

            UpdateTranslateLanguage(_navigationService.Session.Settings.Translate.To);
        }

        private void UpdateTranslateLanguage(string languageId)
        {
            _translateToLanguage = languageId;

            var culture = new CultureInfo(languageId);

            var hyperlink = new Hyperlink();
            hyperlink.Click += TabTranslateLanguage_Click;
            hyperlink.UnderlineStyle = UnderlineStyle.None;
            hyperlink.Inlines.Add(culture.DisplayName);

            var info = Strings.AIEditorTo;
            var index = info.IndexOf("{0}");

            var prefix = info.Substring(0, index);
            var suffix = info.Substring(index + 3);

            TabTranslateTo.Inlines.Clear();
            TabTranslateTo.Inlines.Add(prefix);
            TabTranslateTo.Inlines.Add(hyperlink);
            TabTranslateTo.Inlines.Add(suffix);

            TabTranslateOutput.ShowHideSkeleton(true);
            TabTranslateOutput.SetText(_clientService, _text);

            UpdateTranslate();
        }

        private void UpdateTranslate()
        {
            var addEmojis = TabTranslateEmoji.IsChecked is true;
            var styleName = _translateToLanguage + "_" + addEmojis;

            if (_translations.TryGetValue(styleName, out object result))
            {
                if (result is FormattedText text)
                {
                    TabTranslateOutput.ShowHideSkeleton(false);
                    TabTranslateOutput.SetText(_clientService, text);
                }
            }
            else
            {
                TabTranslateOutput.ShowHideSkeleton(true);
                TabTranslateOutput.InvalidateArrange();

                _translations[styleName] = new object();
                _clientService.Send(new ComposeTextWithAi(_text, _translateToLanguage, string.Empty, addEmojis), result =>
                {
                    this.BeginOnUIThread(() =>
                    {
                        if (result is FormattedText text)
                        {
                            _translations[styleName] = text;
                            TabTranslateOutput.ShowHideSkeleton(false);
                            TabTranslateOutput.SetText(_clientService, text);
                        }
                        else
                        {
                            _translations[styleName] = new MessageTranslateResultError();
                        }
                    });
                });
            }
        }

        private async void TabTranslateLanguage_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();

            var popup = new TranslateToPopup();

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary && popup.SelectedItem != null)
            {
                UpdateTranslateLanguage(popup.SelectedItem);
            }

            await ShowQueuedAsync(XamlRoot);
        }

        private void TabTranslateEmoji_Checked(object sender, RoutedEventArgs e)
        {
            UpdateTranslate();
        }

        private async void InitializeFix()
        {
            _fix = new object();

            TabFixOriginal.SetText(_clientService, _text);

            TabFixLoading.ShowHideSkeleton(true);
            TabFixLoading.SetText(_clientService, _text);

            var response = await _clientService.SendAsync(new FixTextWithAi(_text));
            if (response is FixedText text)
            {
                _fix = text;

                TabFixLoading.Visibility = Visibility.Collapsed;
                TabFixResult.Document.SetText(Windows.UI.Text.TextSetOptions.None, text.DiffText.Text);

                foreach (var diff in text.DiffText.Entities)
                {
                    var range = TabFixResult.Document.GetRange(diff.Offset, diff.Offset + diff.Length);
                    range.CharacterFormat.Underline = Windows.UI.Text.UnderlineType.Wave;
                }

                TabFixResult.IsReadOnly = true;
            }
        }

        private void TabStyleItems_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TopNavViewItem
                {
                    ContentTemplate = sender.ItemTemplate,
                    Style = sender.ItemContainerStyle
                };

                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void TabStyleItems_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is TextCompositionStyle style)
            {
                var animated = content.Children[0] as AnimatedImage;
                var text = content.Children[1] as TextBlock;

                animated.Source = new CustomEmojiFileSource(_clientService, style.CustomEmojiId);
                text.Text = style.Title;
            }

            args.Handled = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var style = TabStyleItems.ItemFromContainer(sender) as TextCompositionStyle;
            if (style == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (style.IsCreator)
            {
                flyout.CreateFlyoutItem(EditStyle, style, Strings.AIEditorEditStyle, Icons.Edit);
            }

            flyout.CreateFlyoutItem(ShareStyle, style, Strings.AIEditorShareStyle, Icons.Share);
            flyout.CreateFlyoutItem(DeleteStyle, style, style.IsCreator ? Strings.AIEditorDeleteStyle : Strings.AIEditorRemoveStyle, Icons.Delete, destructive: true);

            flyout.ShowAt(sender, args);
        }

        private async void EditStyle(TextCompositionStyle style)
        {
            _closedExpected = true;
            Hide();
            await _navigationService.ShowPopupAsync(new TextStylePopup(_clientService, _navigationService, style));
            await ShowQueuedAsync(XamlRoot);
        }

        private async void ShareStyle(TextCompositionStyle style)
        {
            _closedExpected = true;
            Hide();
            await _navigationService.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeTextCompositionStyle(style.Name)));
            await ShowQueuedAsync(XamlRoot);
        }

        private async void DeleteStyle(TextCompositionStyle style)
        {
            if (style.IsCreator)
            {
                var confirm = await _navigationService.ShowPopupAsync(Strings.AIEditorDeleteStyleText, Strings.AIEditorDeleteStyle, Strings.Delete, Strings.Cancel, destructive: true);
                if (confirm == ContentDialogResult.Primary)
                {
                    _clientService.Send(new DeleteTextCompositionStyle(style.Name));
                }
            }
            else
            {
                _clientService.Send(new RemoveTextCompositionStyle(style.Name));
            }
        }

        private Dictionary<string, object> _translations;
        private Dictionary<string, object> _styles = new();
        private object _fix;

        private void TabStyleItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStyle();
        }

        private void TabStyleEmoji_Checked(object sender, RoutedEventArgs e)
        {
            UpdateStyle();
        }

        private void UpdateStyle()
        {
            if (TabStyleItems.SelectedItem is not TextCompositionStyle style)
            {
                return;
            }

            var addEmojis = TabStyleEmoji.IsChecked is true;
            var styleName = style.Name + "_" + addEmojis;

            if (_styles.TryGetValue(styleName, out object result))
            {
                if (result is FormattedText text)
                {
                    TabStyleOutput.ShowHideSkeleton(false);
                    TabStyleOutput.SetText(_clientService, text);
                }
            }
            else
            {
                TabStyleOutput.ShowHideSkeleton(true);
                TabStyleOutput.InvalidateArrange();

                _styles[styleName] = new object();
                _clientService.Send(new ComposeTextWithAi(_text, string.Empty, style.Name, addEmojis), result =>
                {
                    this.BeginOnUIThread(() =>
                    {
                        TabStyleState.Text = Strings.AIEditorResult;

                        if (result is FormattedText text)
                        {
                            _styles[styleName] = text;
                            TabStyleOutput.ShowHideSkeleton(false);
                            TabStyleOutput.SetText(_clientService, text);
                        }
                        else
                        {
                            _styles[styleName] = new MessageTranslateResultError();
                        }
                    });
                });
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Navigation.SelectedIndex == 0)
            {
                var addEmojis = TabTranslateEmoji.IsChecked is true;
                var styleName = _translateToLanguage + "_" + addEmojis;

                if (_translations.TryGetValue(styleName, out object result))
                {
                    if (result is FormattedText text)
                    {
                        _tcs.TrySetResult(text);
                        return;
                    }
                }
            }
            else if (Navigation.SelectedIndex == 1)
            {
                if (TabStyleItems.SelectedItem is not TextCompositionStyle style)
                {
                    return;
                }

                var addEmojis = TabStyleEmoji.IsChecked is true;
                var styleName = style.Name + "_" + addEmojis;

                if (_styles.TryGetValue(styleName, out object result))
                {
                    if (result is FormattedText text)
                    {
                        _tcs.TrySetResult(text);
                        return;
                    }
                }
            }
            else if (Navigation.SelectedIndex == 2)
            {
                if (_fix is FixedText text)
                {
                    _tcs.TrySetResult(text.Text);
                    return;
                }
            }

            args.Cancel = true;
        }

        private async void TabStyleCreate_Click(object sender, RoutedEventArgs e)
        {
            _closedExpected = true;
            Hide();

            await _navigationService.ShowPopupAsync(new TextStylePopup(_clientService, _navigationService));
            await ShowQueuedAsync(XamlRoot);
        }
    }

    public partial class TabStylePanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var maxWidth = 0d;
            var maxHeight = 0d;

            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            return new Size(Math.Max(availableSize.Width, maxWidth * Children.Count), maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = finalSize.Width / Children.Count;
            var x = 0d;

            foreach (var child in Children)
            {
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width;
            }

            return finalSize;
        }
    }
}
