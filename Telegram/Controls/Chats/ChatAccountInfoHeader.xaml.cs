//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Premium.Popups;
using Windows.Foundation;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatAccountInfoHeader : Grid
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private IClientService _clientService;
        private UIElement _parent;

        private long _thumbnailToken;

        public ChatAccountInfoHeader()
        {
            InitializeComponent();
        }

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        public void UpdateUser(IClientService clientService, Chat chat, User user, UserFullInfo fullInfo)
        {
            _clientService = clientService;

            if ((chat?.ActionBar == null && user?.EmojiStatus == null) || (fullInfo == null || fullInfo.IncomingPaidMessageStarCount == 0))
            {
                ShowHide(false);
                return;
            }

            ShowHide(true);

            if (chat.ActionBar != null && user.EmojiStatus != null)
            {
                PremiumUserRoot.Visibility = Visibility.Visible;
                PremiumUserText.Inlines.Clear();

                var markdown = ClientEx.ParseMarkdown(string.Format(Strings.ReportSpamUserEmojiStatusHint2, "__1__"));
                if (markdown.Entities.Count == 2)
                {
                    var e1 = markdown.Entities[0];
                    var e2 = markdown.Entities[1];

                    if (e1.Offset > 0)
                    {
                        PremiumUserText.Inlines.Add(markdown.Text.Substring(0, e1.Offset));
                    }

                    if (e1.Type is TextEntityTypeItalic)
                    {
                        var player = new CustomEmojiIcon();
                        player.Width = 20;
                        player.Height = 20;
                        player.FrameSize = new Size(20, 20);
                        player.Source = new CustomEmojiFileSource(clientService, user.EmojiStatus.Type);

                        //if (style != null)
                        //{
                        //    // "InfoCustomEmojiStyle"
                        //    player.Style = BootStrapper.Current.Resources[style] as Style;
                        //}

                        //var baseline = parent.FontSize == 11 ? -3 : 0;

                        var inline = new InlineUIContainer();
                        inline.Child = new CustomEmojiContainer(PremiumUser, player, size: 20);

                        // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                        if (PremiumUserText.Inlines.Empty())
                        {
                            PremiumUserText.Inlines.Add(Icons.ZWNJ);
                        }

                        PremiumUserText.Inlines.Add(inline);
                        PremiumUserText.Inlines.Add(Icons.ZWNJ);
                    }

                    PremiumUserText.Inlines.Add(markdown.Text.Substring(e1.Offset + e1.Length, e2.Offset - (e1.Offset + e1.Length)));

                    if (e2.Type is TextEntityTypeBold)
                    {
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += Premium_Click;
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
                        hyperlink.Inlines.Add(markdown.Text.Substring(e2.Offset, e2.Length));

                        PremiumUserText.Inlines.Add(hyperlink);
                    }

                    if (e2.Offset + e2.Length < markdown.Text.Length)
                    {
                        PremiumUserText.Inlines.Add(markdown.Text.Substring(e2.Offset + e2.Length));
                    }
                }
            }
            else
            {
                PremiumUserRoot.Visibility = Visibility.Collapsed;
            }

            if (fullInfo.IncomingPaidMessageStarCount > 0)
            {
                PayingUser.Visibility = Visibility.Visible;
                PayingUserText.Inlines.Clear();

                var text = string.Format(Strings.MessageLockedStarsRemoveFee, "{0}", fullInfo.IncomingPaidMessageStarCount.ToString("N0")).Replace("\u2B50", Icons.Premium + "\u200A");

                var markdown = ClientEx.ParseMarkdown(text);
                if (markdown.Entities.Count == 1)
                {
                    var e1 = markdown.Entities[0];
                    if (e1.Offset > 0)
                    {
                        PayingUserText.Inlines.Add(string.Format(markdown.Text.Substring(0, e1.Offset), user.FirstName));
                    }

                    if (e1.Type is TextEntityTypeBold)
                    {
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += RemoveFee_Click;
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
                        hyperlink.Inlines.Add(markdown.Text.Substring(e1.Offset, e1.Length));

                        PayingUserText.Inlines.Add(hyperlink);
                    }

                    if (e1.Offset + e1.Length < markdown.Text.Length)
                    {
                        PayingUserText.Inlines.Add(markdown.Text.Substring(e1.Offset + e1.Length));
                    }
                }
            }
            else
            {
                PayingUser.Visibility = Visibility.Collapsed;
            }
        }

        private async void Premium_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (ViewModel.Chat?.EmojiStatus?.Type is EmojiStatusTypeCustomEmoji emojiStatusTypeCustomEmoji)
            {
                var response = await ViewModel.ClientService.SendAsync(new GetCustomEmojiStickers(new[] { emojiStatusTypeCustomEmoji.CustomEmojiId }));
                if (response is Stickers stickers)
                {
                    var second = await ViewModel.ClientService.SendAsync(new GetStickerSet(stickers.StickersValue[0].SetId));
                    if (second is StickerSet stickerSet)
                    {
                        ViewModel.NavigationService.ShowPopup(new PromoPopup(ViewModel.ClientService, ViewModel.Chat, stickerSet), new PremiumSourceFeature(new PremiumFeatureEmojiStatus()));
                        return;
                    }
                }
            }
            else if (ViewModel.Chat?.EmojiStatus?.Type is EmojiStatusTypeUpgradedGift emojiStatusTypeUpgradedGift)
            {

            }

            if (ViewModel.ClientService.TryGetUser(ViewModel.Chat, out User user) && user.IsPremium)
            {
                ViewModel.NavigationService.ShowPopup(new PromoPopup(ViewModel.ClientService, ViewModel.Chat, null), new PremiumSourceFeature(new PremiumFeatureEmojiStatus()));
            }
        }

        private async void RemoveFee_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (ViewModel.Chat.Type is not ChatTypePrivate privata)
            {
                return;
            }

            var response = await ViewModel.ClientService.SendAsync(new GetPaidMessageRevenue(privata.UserId));
            if (response is not StarCount starCount)
            {
                return;
            }

            var popup = new MessagePopup
            {
                Title = Strings.RemoveMessageFeeTitle,
                Message = string.Format(Strings.RemoveMessageFeeMessage, ViewModel.Chat.Title),
                PrimaryButtonText = Strings.Confirm,
                SecondaryButtonText = Strings.Cancel
            };

            if (starCount.StarCountValue > 0)
            {
                popup.CheckBoxLabel = Locale.Declension(Strings.R.RemoveMessageFeeRefund, starCount.StarCountValue);
            }

            var confirm = await ViewModel.ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                ViewModel.ClientService.Send(new AllowUnpaidMessagesFromUser(privata.UserId, popup.IsChecked is true));
            }
        }

        private bool _collapsed = true;

        private async void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            if (show)
            {
                await this.UpdateLayoutAsync();
            }

            var parent = ElementComposition.GetElementVisual(_parent);
            var visual = ElementComposition.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_collapsed)
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, ActualSize.Y);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -ActualSize.Y, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
