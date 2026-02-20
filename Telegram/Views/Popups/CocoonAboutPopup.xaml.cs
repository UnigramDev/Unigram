//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class CocoonAboutPopup : ContentPopup
    {
        public CocoonAboutPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Understood;
            ButtonsLayout = ContentPopupButtonsLayout.Vertical;

            SetText(Feature1, Strings.CocoonFeature1Text);
            SetText(Feature2, Strings.CocoonFeature2Text);
            SetText(Feature3, Strings.CocoonFeature3Text);
            SetText(Footer, Strings.CocoonFooter);

            static void SetText(DependencyObject info, string text)
            {
                var markdown = ClientEx.ParseMarkdown(text);
                if (markdown.Entities.Count == 1)
                {
                    markdown.Entities[0].Type = new TextEntityTypeTextUrl();
                }

                TextBlockHelper.SetFormattedText(info, markdown);
            }
        }

        private void Feature1_Click(object sender, TextUrlClickEventArgs e)
        {
            OpenUrl(Strings.CocoonFeature1TextLink);
        }

        private void Feature2_Click(object sender, TextUrlClickEventArgs e)
        {

        }

        private void Feature3_Click(object sender, TextUrlClickEventArgs e)
        {
            OpenUrl(Strings.CocoonFeature3TextLink);
        }

        private void Footer_Click(object sender, TextUrlClickEventArgs e)
        {
            OpenUrl(Strings.CocoonFooterLink);
        }

        private void OpenUrl(string url)
        {
            var navigationService = WindowContext.GetNavigationService(XamlRoot);
            if (navigationService == null)
            {
                return;
            }

            var clientService = navigationService.Session.Resolve<IClientService>();

            Hide();
            MessageHelper.OpenUrl(clientService, navigationService, url);
        }
    }
}
