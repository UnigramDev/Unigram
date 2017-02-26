using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Controls;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Users
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserDetailsPage : Page
    {
        public UserDetailsViewModel ViewModel => DataContext as UserDetailsViewModel;

        public UserDetailsPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            DataContext = UnigramContainer.Current.ResolveType<UserDetailsViewModel>();

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, e.NewSize.Width < 500 ? "NarrowState" : "FilledState", false);
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", Picture);

            var user = ViewModel.Item as TLUser;
            if (user.HasPhoto)
            {
                var photo = user.Photo as TLUserProfilePhoto;
                if (photo != null)
                {
                    var test = new UserPhotosViewModel(user, ViewModel.ProtoService);
                    var dialog = new PhotosView { DataContext = test };
                    dialog.Background = null;
                    dialog.OverlayBrush = null;
                    dialog.Closing += (s, args) =>
                    {
                        var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                        if (animation != null)
                        {
                            animation.TryStart(Picture);
                        }
                    };

                    await dialog.ShowAsync();
                }
            }
        }
    }
}
