﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Supergroups;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupReactionsPopup : ContentPopup
    {
        public SupergroupReactionsViewModel ViewModel => DataContext as SupergroupReactionsViewModel;

        public SupergroupReactionsPopup()
        {
            InitializeComponent();

            Title = Strings.Reactions;

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;

            CaptionInput.PreviewKeyDown += CaptionInput_PreviewKeyDown;
        }

        private void CaptionInput_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key >= Windows.System.VirtualKey.NavigationUp && e.Key <= Windows.System.VirtualKey.NavigationRight)
            {
                return;
            }
            else if (e.Key >= Windows.System.VirtualKey.Left && e.Key <= Windows.System.VirtualKey.Down)
            {
                return;
            }
            else if (e.Key == Windows.System.VirtualKey.Back || e.Key == Windows.System.VirtualKey.Delete)
            {
                return;
            }

            e.Handled = true;
        }

        public override void OnNavigatedTo()
        {
            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(ViewModel.SessionId, EmojiDrawerMode.Reactions);
            CaptionInput.CustomEmoji = CustomEmoji;

            if (ViewModel.AllowCustomEmoji)
            {
                FindName(nameof(ChannelFooter));

                var markdown = ClientEx.ParseMarkdown(Strings.ReactionCreateOwnPack);
                var previous = 0;

                foreach (var entity in markdown.Entities)
                {
                    if (entity.Offset > previous)
                    {
                        ChannelFooter.Inlines.Add(markdown.Text.Substring(previous, entity.Offset - previous));
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(markdown.Text.Substring(entity.Offset, entity.Length));
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Click += Hyperlink_Click;

                    previous = entity.Offset + entity.Length;

                    ChannelFooter.Inlines.Add(hyperlink);
                }

                if (markdown.Text.Length > previous)
                {
                    ChannelFooter.Inlines.Add(markdown.Text.Substring(previous));
                }

            }

            UpdateText();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void Hyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            MessageHelper.NavigateToUsername(ViewModel.ClientService, ViewModel.NavigationService, "stickers");
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Available")
            {
                UpdateText();
            }
        }

        private void UpdateText()
        {
            var text = new StringBuilder();
            var entities = new List<TextEntity>();

            foreach (var item in ViewModel.Items)
            {
                if (item is ReactionTypeCustomEmoji customEmoji)
                {
                    entities.Add(new TextEntity(text.Length, 2, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId)));
                    text.Append("🤡");
                    //text.Append('\uEA4F');
                }
                else if (item is ReactionTypeEmoji emoji)
                {
                    text.Append(emoji.Emoji);
                }

                //text.Append('\uFE0F');
            }

            CaptionInput.SetText(text.ToString(), entities);
        }

        #region Binding

        private bool ConvertType(ChatType type, bool channel)
        {
            if (type is ChatTypeSupergroup supergroup)
            {
                return supergroup.IsChannel == channel;
            }

            return channel;
        }

        private bool? ConvertAvailable(SupergroupAvailableReactions value)
        {
            return value != SupergroupAvailableReactions.None;
        }

        private void ConvertAvailableBack(bool? value)
        {
            ViewModel.Available = value == false
                ? SupergroupAvailableReactions.None
                : SupergroupAvailableReactions.Some;
        }

        private string ConvertHeader(ChatType type)
        {
            return type is ChatTypeSupergroup supergroup && supergroup.IsChannel
                ? Strings.AvailableReactions
                : Strings.OnlyAllowThisReactions;
        }

        private string ConvertFooter(SupergroupAvailableReactions value)
        {
            return value switch
            {
                SupergroupAvailableReactions.All => Strings.EnableAllReactionsInfo,
                SupergroupAvailableReactions.None => Strings.DisableReactionsInfo,
                _ => Strings.EnableSomeReactionsInfo
            };
        }

        private string ConvertMaximumCount(int count)
        {
            return Locale.Declension(Strings.R.MaximumReactionsValue, count);
        }

        #endregion

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.Execute();
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            var empty = Array.Empty<AvailableReaction>();
            var reactions = ViewModel.ClientService.ActiveReactions
                .Select(x => new AvailableReaction(new ReactionTypeEmoji(x), false))
                .ToList();

            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.UpdateReactions(new AvailableReactions(reactions, empty, empty, ViewModel.AllowCustomEmoji, false, null));
            EmojiFlyout.ShowAt(CaptionInput, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker)
            {
                ToggleEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private void ToggleEmoji(StickerViewModel sticker)
        {
            var reaction = sticker.ToReactionType();

            var already = ViewModel.Items.FirstOrDefault(x => x.AreTheSame(reaction));
            if (already != null)
            {
                ViewModel.Items.Remove(already);
                UpdateText();
            }
            else
            {
                if (sticker.FullType is StickerFullTypeCustomEmoji)
                {
                    var count = ViewModel.Items.Count(x => x is ReactionTypeCustomEmoji);
                    if (count >= ViewModel.BoostLevel)
                    {
                        ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.ReactionReachLvlForReactionShort, count + 1, count + 1), ToastPopupIcon.Info);
                    }

                    CaptionInput.InsertEmoji(sticker);
                    CaptionInput_TextChanged(null, null);
                }
                else
                {
                    CaptionInput.InsertText(sticker.Emoji);
                }
            }
        }

        private void CaptionInput_TextChanged(object sender, EventArgs e)
        {
            Logger.Info();

            var text = CaptionInput.GetFormattedText();

            var reactions = new List<ReactionType>();
            var previous = 0;

            void AppendEmoji(string text)
            {
                foreach (var emoji in Emoji.EnumerateByComposedCharacterSequence(text))
                {
                    reactions.Add(new ReactionTypeEmoji(emoji));
                }
            }

            foreach (var entity in text.Entities)
            {
                if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                {
                    continue;
                }

                if (entity.Offset > previous)
                {
                    AppendEmoji(text.Text.Substring(previous, entity.Offset - previous));
                }

                reactions.Add(new ReactionTypeCustomEmoji(customEmoji.CustomEmojiId));

                previous = entity.Offset + entity.Length;
            }

            if (text.Text.Length > previous)
            {
                AppendEmoji(text.Text.Substring(previous));
            }

            ViewModel.Items.ReplaceWith(reactions);
        }
    }
}
