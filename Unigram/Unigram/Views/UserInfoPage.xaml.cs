﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserInfoPage : Page
    {
        public UserInfoViewModel ViewModel => DataContext as UserInfoViewModel;

        public UserInfoPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            DataContext = UnigramContainer.Instance.ResolverType<UserInfoViewModel>();

            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            VisualStateManager.GoToState(this, e.NewSize.Width < 500 ? "NarrowState" : "FilledState", false);
        }
    }

    // Experiment
    public class TableStackPanel : StackPanel
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (finalSize.Width >= 500)
            {
                //Margin = new Thickness(12, 0, 12, 0);
                //CornerRadius = new CornerRadius(8);
                //BorderThickness = new Thickness(0);

                HyperButton first = null;
                HyperButton last = null;

                foreach (var item in Children)
                {
                    if (item.Visibility == Visibility.Visible)
                    {
                        if (first == null)
                        {
                            first = item as HyperButton;
                        }

                        last = item as HyperButton;

                        if (last != null)
                        {
                            last.BorderBrush = Application.Current.Resources["SystemControlForegroundBaseLowBrush"] as SolidColorBrush;
                        }
                    }
                }

                var lastRadius = new CornerRadius(0, 0, 8, 8);

                if (first != null)
                {
                    if (first == last)
                    {
                        last.CornerRadius = new CornerRadius(8, 8, 8, 8);
                        last.BorderBrush = null;
                    }
                    else
                    {
                        first.CornerRadius = new CornerRadius(8, 8, 0, 0);

                        if (last != null)
                        {
                            last.CornerRadius = new CornerRadius(0, 0, 8, 8);
                            last.BorderBrush = null;
                        }
                    }
                }
            }

            return base.ArrangeOverride(finalSize);
        }
    }
}
