using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.AllJoyn;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Helpers
{
    public class AuthenticationHelper
    {
        private const string LocalPasscodeResourceName = "Unigram-App";
        private const string LocalPasscodeUserName = "Unigram-App-Local-Passcode";
        private const string WindowsHelloUserName = "Unigram-App-Hello";

        public static async Task<bool> AuthenticateUsingWindowsHello()
        {
            if (await KeyCredentialManager.IsSupportedAsync())
            {
                // Get credentials for current user and app
                var result = await KeyCredentialManager.OpenAsync(WindowsHelloUserName);
                if (result.Credential != null)
                {
                    var signResult = await result.Credential.RequestSignAsync(CryptographicBuffer.ConvertStringToBinary("LoginAuth", BinaryStringEncoding.Utf8));

                    return signResult.Status == KeyCredentialStatus.Success;
                }

                // If no credentials found create one
                var creationResult = await KeyCredentialManager.RequestCreateAsync(WindowsHelloUserName,
                    KeyCredentialCreationOption.ReplaceExisting);
                return creationResult.Status == KeyCredentialStatus.Success;
            }
            return false;
        }

        //Note from @prajjwaldimri: I discourage using this as it exposes the stored password to the memory which can be a real problem.
        public static async Task<bool> AuthenticateLocally()
        {
            var contentDialog = new ContentDialog()
            {
                Title = "Passcode required to start app",
                PrimaryButtonText = "Submit",
                SecondaryButtonText = "Cancel"
            };

            var stackPanel = new StackPanel();

            var passcodeTextBox = new PasswordBox
            {
                PlaceholderText = "Enter your passcode here",
                Margin = new Thickness(0,10,0,0)
            };
            var errorTextBlock = new TextBlock
            {
                Margin = new Thickness(0,5,0,0),
                Visibility = Visibility.Collapsed,
                Foreground = new SolidColorBrush(Colors.DarkRed)
            };

            stackPanel.Children.Add(passcodeTextBox);
            stackPanel.Children.Add(errorTextBlock);

            contentDialog.Content = stackPanel;

            var result = false;

            contentDialog.PrimaryButtonClick += async (sender, args) =>
            {
                result = await VerifyLocalPasscode(passcodeTextBox.Password);
                if (!result)
                {
                    errorTextBlock.Text = "Wrong Passcode!";
                    errorTextBlock.Visibility = Visibility.Visible;
                    passcodeTextBox.Password = "";
                    args.Cancel = true;
                }
            };
            await contentDialog.ShowAsync();
            return result;
        }

        private static async Task<bool> VerifyLocalPasscode(string inputPasscode)
        {
            var vault = new PasswordVault();
            var storedPasscode = vault.Retrieve(LocalPasscodeResourceName, LocalPasscodeUserName).Password;

            return storedPasscode.Trim().Equals(inputPasscode.Trim());
        }

        public static async Task<bool> SaveLocalPasscode()
        {
            var contentDialog = new ContentDialog()
            {
                Title = "Add passcode to Unigram",
                PrimaryButtonText = "Submit",
                SecondaryButtonText = "Cancel"
            };

            var stackpanel = new StackPanel();

            var passcodeTextBox = new PasswordBox
            {
                PlaceholderText = "Enter new passcode here",
                Margin = new Thickness(0, 10, 0, 0)
            };
            var verifyPasscodeTextBox = new PasswordBox
            {
                PlaceholderText = "Enter passcode once again",
                Margin = new Thickness(0, 10, 0, 0),
            };
            var errorTextBlock = new TextBlock
            {
                Margin = new Thickness(0, 5, 0, 0),
                Visibility = Visibility.Collapsed,
                Foreground = new SolidColorBrush(Colors.DarkRed)
            };

            stackpanel.Children.Add(passcodeTextBox);
            stackpanel.Children.Add(verifyPasscodeTextBox);
            stackpanel.Children.Add(errorTextBlock);
            contentDialog.Content = stackpanel;

            var result = false;

            contentDialog.PrimaryButtonClick += (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(passcodeTextBox.Password) ||
                    string.IsNullOrWhiteSpace(verifyPasscodeTextBox.Password))
                {
                    errorTextBlock.Text = "Passcodes empty";
                    errorTextBlock.Visibility = Visibility.Visible;
                    passcodeTextBox.Password = "";
                    verifyPasscodeTextBox.Password = "";
                    args.Cancel = true;
                }
                else if (passcodeTextBox.Password.Equals(verifyPasscodeTextBox.Password))
                {
                    var vault = new PasswordVault();
                    vault.Add(new PasswordCredential(LocalPasscodeResourceName, LocalPasscodeUserName,
                        passcodeTextBox.Password));
                    result = true;
                }
                else
                {
                    errorTextBlock.Text = "Passcodes do not match!";
                    errorTextBlock.Visibility = Visibility.Visible;
                    passcodeTextBox.Password = "";
                    verifyPasscodeTextBox.Password = "";
                    args.Cancel = true;
                }
            };
            await contentDialog.ShowAsync();
            return result;
        }

        public static Task RemoveLocalPasscode()
        {
            var vault = new PasswordVault();
            var passcode = vault.Retrieve(LocalPasscodeResourceName, LocalPasscodeUserName).Password;
            vault.Remove(new PasswordCredential(LocalPasscodeResourceName, LocalPasscodeUserName, passcode));
        }
    }
}
