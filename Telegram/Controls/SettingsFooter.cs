//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class SettingsFooter : Control
    {
        public SettingsFooter()
        {
            DefaultStyleKey = typeof(SettingsFooter);
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SettingsFooter), new PropertyMetadata(string.Empty));

        #endregion

        #region TextAlignment

        public TextAlignment TextAlignment
        {
            get { return (TextAlignment)GetValue(TextAlignmentProperty); }
            set { SetValue(TextAlignmentProperty, value); }
        }

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(SettingsFooter), new PropertyMetadata(TextAlignment.Left));

        #endregion

        #region IsLink

        public bool IsLink
        {
            get { return (bool)GetValue(IsLinkProperty); }
            set { SetValue(IsLinkProperty, value); }
        }

        public static readonly DependencyProperty IsLinkProperty =
            DependencyProperty.Register("IsLink", typeof(bool), typeof(SettingsFooter), new PropertyMetadata(false));

        #endregion

        public event EventHandler<TextUrlClickEventArgs> Click;

        // Used by TextBlockHelper
        public void OnClick(string url)
        {
            Click?.Invoke(this, new TextUrlClickEventArgs(url));
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SettingsFooterAutomationPeer(this);
        }
    }

    public class SettingsFooterAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly SettingsFooter _owner;

        public SettingsFooterAutomationPeer(SettingsFooter owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetClassNameCore()
        {
            return "TextBlock";
        }

        protected override string GetNameCore()
        {
            return _owner.Text;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Text;
        }
    }
}
