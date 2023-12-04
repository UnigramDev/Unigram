//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public sealed partial class ProfileColorBadge : UserControl
    {
        private IClientService _clientService;

        private int _accentColorId;
        private int _profileAccentColorId;

        public ProfileColorBadge()
        {
            InitializeComponent();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_clientService != null)
            {
                SetColors(_clientService, _accentColorId, _profileAccentColorId);
            }
        }

        public void SetUser(IClientService clientService, User user)
        {
            SetColors(clientService, user.AccentColorId, user.ProfileAccentColorId);
        }

        public void SetChat(IClientService clientService, Chat chat)
        {
            SetColors(clientService, chat.AccentColorId, -1);
        }

        private void SetColors(IClientService clientService, int nameId, int profileId)
        {
            _clientService = clientService;
            _accentColorId = nameId;
            _profileAccentColorId = profileId;

            var theme = WindowContext.Current.ActualTheme;

            if (clientService.TryGetProfileColor(profileId, out ProfileColor profile))
            {
                var colors = profile.ForTheme(theme);

                ProfilePrimary.Background = new SolidColorBrush(colors.PaletteColors[0]);
                ProfileSecondary.Fill = colors.PaletteColors.Count > 1
                    ? new SolidColorBrush(colors.PaletteColors[1])
                    : null;

                ProfilePrimary.Visibility = Visibility.Visible;

                var device = CanvasDevice.GetSharedDevice();
                var ellipse1 = CanvasGeometry.CreateRectangle(device, 0, 0, 24, 24);
                var ellipse2 = CanvasGeometry.CreateEllipse(device, 28, 12, 12, 12);
                var group = CanvasGeometry.CreateGroup(device, new[] { ellipse1, ellipse2 }, CanvasFilledRegionDetermination.Alternate);

                var visual = ElementCompositionPreview.GetElementVisual(NamePrimary);
                visual.Clip = Window.Current.Compositor.CreateGeometricClip(Window.Current.Compositor.CreatePathGeometry(new CompositionPath(group)));
            }
            else
            {
                ProfilePrimary.Visibility = Visibility.Collapsed;
            }

            var name = clientService.GetAccentColor(nameId);
            var color = name.ForTheme(theme);

            NamePrimary.Background = new SolidColorBrush(color[0]);
            NameSecondary.Fill = color.Count > 1
                ? new SolidColorBrush(color[1])
                : null;
            NameTertiary.Fill = color.Count > 2
                ? new SolidColorBrush(color[2])
                : null;
        }
    }
}
