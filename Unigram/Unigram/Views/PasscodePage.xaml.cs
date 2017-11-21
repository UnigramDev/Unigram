using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PasscodePage : Page
    {
        private readonly IPasscodeService _passcodeService;

        public PasscodePage()
        {
            InitializeComponent();

            _passcodeService = UnigramContainer.Current.ResolveType<IPasscodeService>();

            var user = InMemoryCacheService.Current.GetUser(SettingsHelper.UserId);
            if (user != null)
            {
                Photo.Source = DefaultPhotoConverter.Convert(user, false) as ImageSource;
                FullName.Text = user.FullName;
            }
        }

        private void Field_TextChanged(object sender, RoutedEventArgs e)
        {
            if (Field.Password.Length == 4 && Field.Password.All(x => x >= '0' && x <= '9'))
            {
                if (_passcodeService.Check(Field.Password))
                {
                    Unlock();
                }
                else
                {
                    VisualUtilities.ShakeView(Field);
                }
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_passcodeService.IsBiometricsEnabled)
            {
                return;
            }

            if (await KeyCredentialManager.IsSupportedAsync())
            {
                var result = await KeyCredentialManager.OpenAsync(Strings.Android.AppName);
                if (result.Credential != null)
                {
                    var signResult = await result.Credential.RequestSignAsync(CryptographicBuffer.ConvertStringToBinary(Package.Current.Id.Name, BinaryStringEncoding.Utf8));
                    if (signResult.Status == KeyCredentialStatus.Success)
                    {
                        Unlock();
                    }
                }
                else
                {
                    var creationResult = await KeyCredentialManager.RequestCreateAsync(Strings.Android.AppName, KeyCredentialCreationOption.ReplaceExisting);
                    if (creationResult.Status == KeyCredentialStatus.Success)
                    {
                        Unlock();
                    }
                }
            }
        }

        private void Unlock()
        {
            _passcodeService.Unlock();
            App.Current.ModalDialog.IsModal = false;
            App.Current.ModalContent = null;
        }
    }
}
