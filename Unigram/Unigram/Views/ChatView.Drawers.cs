using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views
{
    public partial class ChatView
    {
        private void Sticker_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var sticker = element.Tag as StickerViewModel;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.StickerViewCommand, (Sticker)sticker, Strings.Resources.ViewPackPreview, new FontIcon { Glyph = Icons.Stickers });

            if (ViewModel.ProtoService.IsStickerFavorite(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.StickerUnfaveCommand, (Sticker)sticker, Strings.Resources.DeleteFromFavorites, new FontIcon { Glyph = Icons.Unfavorite });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.StickerFaveCommand, (Sticker)sticker, Strings.Resources.AddToFavorites, new FontIcon { Glyph = Icons.Favorite });
            }

            if (ViewModel.Type == ViewModels.DialogType.History)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.CacheService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(new RelayCommand<Sticker>(anim => ViewModel.StickerSendExecute(anim, null, true)), (Sticker)sticker, Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.Mute });
                //flyout.CreateFlyoutItem(new RelayCommand<Sticker>(anim => ViewModel.StickerSendExecute(anim, true, null)), sticker.Get(), self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.Schedule });
            }

            args.ShowAt(flyout, element);
        }

        private void Animation_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var animation = element.DataContext as Animation;

            var flyout = new MenuFlyout();

            if (ViewModel.ProtoService.IsAnimationSaved(animation.AnimationValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.AnimationDeleteCommand, animation, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.AnimationSaveCommand, animation, Strings.Resources.SaveToGIFs, new FontIcon { Glyph = Icons.Animations });
            }

            if (ViewModel.Type == ViewModels.DialogType.History)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.CacheService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(new RelayCommand<Animation>(anim => ViewModel.AnimationSendExecute(anim, null, true)), animation, Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.Mute });
                //flyout.CreateFlyoutItem(new RelayCommand<Animation>(anim => ViewModel.AnimationSendExecute(anim, true, null)), animation, self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.Schedule });
            }

            args.ShowAt(flyout, element);
        }
    }
}
