using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Helpers;
using Unigram.Core.Models;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Users
{
    public sealed partial class UserCreatePage : Page
    {
        public UserCreateViewModel ViewModel => DataContext as UserCreateViewModel;

        public UserCreatePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<UserCreateViewModel>();
        }

        private void PrimaryInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(PrimaryInput.Text))
            {
                PhoneCode.Focus(FocusState.Keyboard);
                PhoneCode.SelectionStart = PhoneCode.Text.Length;
                e.Handled = true;
            }
        }

        #region Binding

        private string ConvertFormat(Country country)
        {
            if (country == null)
            {
                return null;
            }

            var groups = PhoneNumber.Parse(country.PhoneCode);
            var builder = new StringBuilder();

            for (int i = 1; i < groups.Length; i++)
            {
                for (int j = 0; j < groups[i]; j++)
                {
                    builder.Append('-');
                }

                if (i + 1 < groups.Length)
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString();
        }

        #endregion

    }
}
