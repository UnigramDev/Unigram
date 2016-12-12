using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Unigram.Controls.Messages;
using Telegram.Api.TL;
using Unigram.ViewModels;
using Unigram.Controls.Views;
using Windows.UI.Xaml.Media.Animation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Themes
{
    public sealed partial class Media : ResourceDictionary
    {
        public Media()
        {
            this.InitializeComponent();
        }

        private async void ImageView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            var message = image.DataContext as TLMessage;
            var bubble = image.Ancestors<MessageControlBase>().FirstOrDefault() as MessageControlBase;
            if (bubble != null)
            {
                if (bubble.Context != null)
                {
                    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", image);

                    var test = new DialogPhotosViewModel(bubble.Context.Peer, message, bubble.Context.ProtoService);
                    var dialog = new PhotosView { DataContext = test };
                    dialog.Background = null;
                    dialog.Closing += (s, args) =>
                    {
                        var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                        if (animation != null)
                        {
                            animation.TryStart(image);
                        }
                    };

                    await dialog.ShowAsync();
                }
            }
        }
    }
}
