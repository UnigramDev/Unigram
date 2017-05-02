﻿using System;
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
using Unigram.Views;
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

namespace Unigram.Views.Users
{
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
            if (user.HasPhoto && user.Photo is TLUserProfilePhoto photo)
            {
                var viewModel = new UserPhotosViewModel(user, ViewModel.ProtoService);
                await GalleryView.Current.ShowAsync(viewModel, (s, args) =>
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                    if (animation != null)
                    {
                        animation.TryStart(Picture);
                    }
                });
            }
        }
    }
}
