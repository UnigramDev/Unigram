//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Drawers;
using Unigram.Converters;

namespace Unigram.Views
{
    public partial class ChatView
    {
        private void Sticker_ContextRequested(UIElement sender, ItemContextRequestedEventArgs<Sticker> args)
        {
            var element = sender as FrameworkElement;
            var sticker = args.Item;

            if (sticker == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.StickerViewCommand, sticker, Strings.Resources.ViewPackPreview, new FontIcon { Glyph = Icons.Sticker });

            if (ViewModel.ClientService.IsStickerFavorite(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.StickerUnfaveCommand, sticker, Strings.Resources.DeleteFromFavorites, new FontIcon { Glyph = Icons.StarOff });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.StickerFaveCommand, sticker, Strings.Resources.AddToFavorites, new FontIcon { Glyph = Icons.Star });
            }

            if (ViewModel.ClientService.IsStickerRecent(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.StickerDeleteCommand, sticker, Strings.Resources.DeleteFromRecent, new FontIcon { Glyph = Icons.Delete });
            }

            if (ViewModel.Type == ViewModels.DialogType.History)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.ClientService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(new RelayCommand<Sticker>(anim => ViewModel.StickerSendExecute(anim, null, true)), sticker, Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.AlertOff });
                flyout.CreateFlyoutItem(new RelayCommand<Sticker>(anim => ViewModel.StickerSendExecute(anim, true, null)), sticker, self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.CalendarClock });
            }

            args.ShowAt(flyout, element);
        }

        private void Animation_ContextRequested(UIElement sender, ItemContextRequestedEventArgs<Animation> args)
        {
            var element = sender as FrameworkElement;
            var animation = args.Item;

            if (animation == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (ViewModel.ClientService.IsAnimationSaved(animation.AnimationValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.AnimationDeleteCommand, animation, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.AnimationSaveCommand, animation, Strings.Resources.SaveToGIFs, new FontIcon { Glyph = Icons.Gif });
            }

            if (ViewModel.Type == ViewModels.DialogType.History)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.ClientService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(new RelayCommand<Animation>(anim => ViewModel.AnimationSendExecute(anim, null, true)), animation, Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.AlertOff });
                flyout.CreateFlyoutItem(new RelayCommand<Animation>(anim => ViewModel.AnimationSendExecute(anim, true, null)), animation, self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.CalendarClock });
            }

            args.ShowAt(flyout, element);
        }
    }
}
