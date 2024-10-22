//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsAppearancePage : HostedPage
    {
        public SettingsAppearanceViewModel ViewModel => DataContext as SettingsAppearanceViewModel;

        public SettingsAppearancePage()
        {
            InitializeComponent();
            Title = Strings.Appearance;

            var preview = ElementComposition.GetElementVisual(Preview);
            preview.Clip = preview.Compositor.CreateInsetClip();

            _valueChanged = new EventDebouncer<RangeBaseValueChangedEventArgs>(Constants.TypingTimeout,
                handler => ScalingSlider.ValueChanged += new RangeBaseValueChangedEventHandler(handler),
                handler => ScalingSlider.ValueChanged -= new RangeBaseValueChangedEventHandler(handler));
            _valueChanged.Invoked += Slider_ValueChanged;

            ScalingSlider.AddHandler(PointerPressedEvent, new PointerEventHandler(Slider_PointerPressed), true);
            ScalingSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerReleased), true);
            ScalingSlider.AddHandler(PointerCanceledEvent, new PointerEventHandler(Slider_PointerCanceled), true);
            ScalingSlider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(Slider_PointerCaptureLost), true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Theme.Current.Update(ActualTheme, null, null))
            {
                var forDarkTheme = Frame.ActualTheme == ElementTheme.Dark;
                var background = ViewModel.ClientService.GetDefaultBackground(forDarkTheme);
                ViewModel.Aggregator.Publish(new UpdateDefaultBackground(forDarkTheme, background));
            }

            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);

            ViewModel.PropertyChanged += OnPropertyChanged;
            ViewModel.Aggregator.Subscribe<UpdateDefaultReactionType>(this, Handle);

            UpdateDefaultReactionType();

            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                if (ViewModel.IsPremiumAvailable)
                {
                    ProfileColor.SetUser(ViewModel.ClientService, user);
                }
                else
                {
                    NameColor.Visibility = Visibility.Collapsed;
                }

                Message1.Mockup(ViewModel.ClientService, Strings.FontSizePreviewLine1, user, Strings.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.FontSizePreviewLine2, true, DateTime.Now);
            }

            ScalingSlider.Value = ViewModel.Scaling;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
            ViewModel.Aggregator.Unsubscribe(this);
        }

        #region Binding

        private string ConvertNightMode(NightMode mode)
        {
            return mode == NightMode.Scheduled
                ? Strings.AutoNightScheduled
                : mode == NightMode.Automatic
                ? Strings.AutoNightAutomatic
                : mode == NightMode.System
                ? Strings.AutoNightSystemDefault
                : Strings.AutoNightDisabled;
        }

        private string ConvertQuickAction(bool reply)
        {
            return reply
                ? Strings.QuickReactionInfo2
                : Strings.QuickReactionInfo;
        }

        #endregion

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.FontSize) || e.PropertyName == nameof(ViewModel.BubbleRadius))
            {
                Message1.UpdateMockup();
                Message2.UpdateMockup();
            }
            else if (e.PropertyName == nameof(ViewModel.UseDefaultScaling))
            {
                if (ViewModel.UseDefaultScaling)
                {
                    ScalingSlider.Value = ViewModel.Scaling;
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List.SelectedItem is ChatThemeViewModel chatTheme)
            {
                // Speed up background preview by manually applying it
                if (ActualTheme == ElementTheme.Light)
                {
                    BackgroundControl.Update(chatTheme.LightSettings.Background, false);
                }
                else
                {
                    BackgroundControl.Update(chatTheme.DarkSettings.Background, true);
                }
            }
        }

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var theme = List.ItemFromContainer(sender) as ChatThemeViewModel;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.CreateTheme, theme, Strings.CreateNewThemeMenu, Icons.Color);

            //if (!theme.IsOfficial)
            //{
            //    flyout.CreateFlyoutSeparator();
            //    flyout.CreateFlyoutItem(ViewModel.ThemeShareCommand, theme, Strings.ShareFile, Icons.Share);
            //    flyout.CreateFlyoutItem(ViewModel.ThemeEditCommand, theme, Strings.Edit, Icons.Edit);
            //    flyout.CreateFlyoutItem(ViewModel.ThemeDeleteCommand, theme, Strings.Delete, Icons.Delete);
            //}

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Theme_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatThemeCell content && args.Item is ChatThemeViewModel theme)
            {
                content.Update(theme);
                args.Handled = true;
            }
        }

        #endregion

        private readonly EventDebouncer<RangeBaseValueChangedEventArgs> _valueChanged;
        private bool _scrubbing;

        private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = true;
        }

        private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
            ViewModel.Scaling = (int)ScalingSlider.Value;
        }

        private void Slider_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
        }

        private void Slider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_scrubbing)
            {
                return;
            }

            ViewModel.Scaling = (int)e.NewValue;
        }

        private bool _compact;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var compact = e.NewSize.Width < 500;
            if (compact == _compact)
            {
                return;
            }

            _compact = compact;

            Grid.SetColumnSpan(TextSizeHeader, compact ? 2 : 1);
            Grid.SetColumnSpan(BubbleRadiusHeader, compact ? 2 : 1);

            Grid.SetRow(ScalingSlider, compact ? 1 : 0);
            Grid.SetColumn(ScalingSlider, compact ? 0 : 2);

            Grid.SetRow(FontSizeSlider, compact ? 1 : 0);
            Grid.SetColumn(FontSizeSlider, compact ? 0 : 2);

            Grid.SetRow(BubbleRadiusSlider, compact ? 1 : 0);
            Grid.SetColumn(BubbleRadiusSlider, compact ? 0 : 2);
        }

        private void Handle(UpdateDefaultReactionType update)
        {
            this.BeginOnUIThread(UpdateDefaultReactionType);
        }

        private void UpdateDefaultReactionType()
        {
            var reaction = ViewModel.ClientService.DefaultReaction;

            var clientService = ViewModel.ClientService;
            var senderId = new MessageSenderUser(clientService.Options.MyId);

            var message = new Message(0, senderId, 0, null, null, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, 0, 0, null, 0, 0, 0, 0, 0, string.Empty, 0, 0, false, string.Empty, null, null);

            var playback = TypeResolver.Current.Playback;
            var settings = TypeResolver.Current.Resolve<ISettingsService>(clientService.SessionId);

            var delegato = new ChatMessageDelegate(clientService, settings, null);
            var viewModel = new MessageViewModel(clientService, playback, delegato, null, message, true);

            Reaction.SetReaction(viewModel, new MessageReaction(reaction, 1, false, senderId, new MessageSender[] { }));

            if (Reaction.IsLoaded)
            {
                Reaction.SetUnread(new UnreadReaction(reaction, senderId, false));
            }
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            var empty = Array.Empty<AvailableReaction>();
            var reactions = ViewModel.ClientService.ActiveReactions
                .Select(x => new AvailableReaction(new ReactionTypeEmoji(x), false))
                .ToList();

            var viewModel = EmojiDrawerViewModel.Create(ViewModel.ClientService.SessionId, EmojiDrawerMode.Reactions);
            _ = viewModel.UpdateReactions(new AvailableReactions(reactions, empty, empty, true, false, null));

            var flyout = EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.Reactions, Reaction, EmojiFlyoutAlignment.TopRight, viewModel);

            flyout.EmojiSelected += (s, args) =>
            {
                ViewModel.DoubleClickToReact = true;
            };
        }
    }
}
