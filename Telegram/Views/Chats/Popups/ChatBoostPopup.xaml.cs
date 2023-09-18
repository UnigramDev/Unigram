using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Chats.Popups
{
    public sealed partial class ChatBoostPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly Chat _chat;
        private readonly ChatBoostStatus _status;
        private readonly CanBoostChatResult _result;

        public ChatBoostPopup(IClientService clientService, Chat chat, ChatBoostStatus status, CanBoostChatResult result)
        {
            InitializeComponent();

            _clientService = clientService;
            _chat = chat;
            _status = status;
            _result = result;

            Title = status.IsBoosted
                ? Strings.YouBoostedChannel
                : status.Level == 0
                ? Strings.BoostingEnableStoriesForChannel
                : Strings.HelpUpgradeChannel;

            ChatPhoto.Width = ChatPhoto.Height = 28;

            ChatTitle.Text = chat.Title;
            ChatPhoto.SetChat(clientService, chat, 28);

            TextBlockHelper.SetMarkdown(Description, status.Level == 0
                ? string.Format(Strings.ChannelNeedBoostsDescriptionLevel1, status.NextLevelBoostCount - status.BoostCount)
                : string.Format(Strings.ChannelNeedBoostsAlreadyBoostedDescriptionLevelNext, status.NextLevelBoostCount - status.BoostCount, status.Level + 1));

            var justReached = status.IsBoosted
                ? status.CurrentLevelBoostCount - status.BoostCount == 0
                : status.NextLevelBoostCount - status.BoostCount == 1;

            if (justReached)
            {
                TextBlockHelper.SetMarkdown(DescriptionBoosted, status.Level == 0
                    ? Strings.ChannelBoostsJustReachedLevel1
                    : string.Format(Strings.ChannelBoostsJustReachedLevelNext, status.Level + 1, status.Level + 1));
            }
            else
            {
                TextBlockHelper.SetMarkdown(DescriptionBoosted, status.Level == 0
                    ? string.Format(Strings.ChannelNeedBoostsAlreadyBoostedDescriptionLevel1, status.NextLevelBoostCount - status.BoostCount - 1)
                    : string.Format(Strings.ChannelNeedBoostsAlreadyBoostedDescriptionLevelNext, status.NextLevelBoostCount - status.BoostCount - 1, status.Level + 1));
            }

            DescriptionBoosted.Opacity = status.IsBoosted ? 1 : 0;
            Description.Opacity = status.IsBoosted ? 0 : 1;

            _alreadyBoostedCollapsed = !status.IsBoosted;
            PurchaseCommand.Content = status.IsBoosted
                ? Strings.OK
                : Strings.BoostChannel;

            Progress.Minimum = Slid.Minimum = status.CurrentLevelBoostCount;
            Progress.Maximum = Slid.Maximum = status.NextLevelBoostCount;
            Progress.Value = Slid.Value = status.BoostCount;

            if (justReached && status.IsBoosted)
            {
                Progress.Minimum = Slid.Minimum = 0;
                Progress.Maximum = Slid.Maximum = status.BoostCount;
                Progress.Value = Slid.Value = status.BoostCount;

                Progress.MinimumText = string.Format(Strings.BoostsLevel, status.Level - 1);
                Progress.MaximumText = string.Format(Strings.BoostsLevel, status.Level);
            }
            else
            {
                Progress.Minimum = Slid.Minimum = status.CurrentLevelBoostCount;
                Progress.Maximum = Slid.Maximum = status.NextLevelBoostCount;
                Progress.Value = Slid.Value = status.BoostCount;

                Progress.MinimumText = string.Format(Strings.BoostsLevel, status.Level);
                Progress.MaximumText = string.Format(Strings.BoostsLevel, status.Level + 1);
            }

            ElementCompositionPreview.SetIsTranslationEnabled(Description, true);
            ElementCompositionPreview.SetIsTranslationEnabled(DescriptionBoosted, true);
        }

        private void PurchaseShadow_Loaded(object sender, RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (_alreadyBoostedCollapsed is false)
            {
                Hide();
            }
            else if (_result is CanBoostChatResultOk resultOk)
            {
                if (_clientService.TryGetChat(resultOk.CurrentlyBoostedChatId, out Chat currently))
                {
                    var panel = new Grid();
                    panel.ColumnDefinitions.Add(new ColumnDefinition());
                    panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    panel.ColumnDefinitions.Add(new ColumnDefinition());
                    panel.RowDefinitions.Add(new RowDefinition());
                    panel.RowDefinitions.Add(new RowDefinition());

                    var photo1 = new ProfilePicture
                    {
                        Width = 64,
                        Height = 64,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    var photo2 = new ProfilePicture
                    {
                        Width = 64,
                        Height = 64,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    photo1.SetChat(_clientService, currently, 64);
                    photo2.SetChat(_clientService, _chat, 64);

                    var label = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 16, 0, 0)
                    };

                    var chevron = new TextBlock
                    {
                        Text = "\uE0E3",
                        Foreground = BootStrapper.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as Brush,
                        FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily,
                        FontSize = 28,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    TextBlockHelper.SetMarkdown(label, string.Format(Strings.ReplaceBoostChannelDescription, currently.Title, _chat.Title));
                    Grid.SetColumnSpan(label, 3);
                    Grid.SetRow(label, 1);

                    Grid.SetColumn(chevron, 1);
                    Grid.SetColumn(photo2, 2);

                    panel.Children.Add(photo1);
                    panel.Children.Add(photo2);
                    panel.Children.Add(label);
                    panel.Children.Add(chevron);

                    var confirm = await MessagePopup.ShowAsync(target: null, panel, Strings.Replace, Strings.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    await Task.Delay(333);
                }

                _clientService.Send(new BoostChat(_chat.Id));
                ShowHideAlreadyBoosted(true);
            }
            else if (_result is CanBoostChatResultWaitNeeded resultWaitNeeded)
            {
                await MessagePopup.ShowAsync(target: null, string.Format(Strings.CantBoostToOftenDescription, Locale.FormatCallDuration(resultWaitNeeded.RetryAfter)), Strings.CantBoostToOften, Strings.OK);
            }
            else if (_result is CanBoostChatResultPremiumSubscriptionNeeded)
            {
                await MessagePopup.ShowAsync(target: null, Strings.CantBoostWithGiftedPremiumDescription, Strings.CantBoostWithGiftedPremium, Strings.OK);
            }
            else if (_result is CanBoostChatResultPremiumNeeded)
            {
                var confirm = await MessagePopup.ShowAsync(target: null, Strings.PremiumNeededForBoosting, Strings.PremiumNeeded, Strings.OK, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    // TODO: do the premium thing
                }
            }
        }

        private bool _alreadyBoostedCollapsed = true;

        private void ShowHideAlreadyBoosted(bool show)
        {
            if (_alreadyBoostedCollapsed != show)
            {
                return;
            }

            _alreadyBoostedCollapsed = !show;
            Description.Opacity = 1;
            DescriptionBoosted.Opacity = 1;

            Progress.Value = show ? _status.BoostCount + 1 : _status.BoostCount;

            var visual1 = ElementCompositionPreview.GetElementVisual(Description);
            var visual2 = ElementCompositionPreview.GetElementVisual(DescriptionBoosted);

            var panel = ElementCompositionPreview.GetElementVisual(DescriptionRoot);
            panel.Clip ??= panel.Compositor.CreateInsetClip();

            var compositor = visual1.Compositor;
            var duration = TimeSpan.FromSeconds(0.167);

            var height = DescriptionRoot.ActualSize.Y;

            var animOffset1 = compositor.CreateScalarKeyFrameAnimation();
            animOffset1.InsertKeyFrame(show ? 0 : 1, 0);
            animOffset1.InsertKeyFrame(show ? 1 : 0, -height + 24);
            animOffset1.Duration = duration;

            var animOffset2 = compositor.CreateScalarKeyFrameAnimation();
            animOffset2.InsertKeyFrame(show ? 0 : 1, height - 24);
            animOffset2.InsertKeyFrame(show ? 1 : 0, 0);
            animOffset2.Duration = duration;

            var animFade1 = compositor.CreateScalarKeyFrameAnimation();
            animFade1.InsertKeyFrame(show ? 0 : 1, 1);
            animFade1.InsertKeyFrame(show ? 1 : 0, 0);
            animFade1.Duration = duration;

            var animFade2 = compositor.CreateScalarKeyFrameAnimation();
            animFade2.InsertKeyFrame(show ? 0 : 1, 0);
            animFade2.InsertKeyFrame(show ? 1 : 0, 1);
            animFade2.Duration = duration;

            visual1.StartAnimation("Translation.Y", animOffset1);
            visual2.StartAnimation("Translation.Y", animOffset2);
            visual1.StartAnimation("Opacity", animFade1);
            visual2.StartAnimation("Opacity", animFade2);

            PurchaseCommand.Content = show
                ? Strings.OK
                : Strings.BoostChannel;

            if (show)
            {
                var aggregator = TLContainer.Current.Resolve<IEventAggregator>(_clientService.SessionId);
                aggregator.Publish(new UpdateConfetti());
            }
        }
    }
}
