﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class SettingsHeadline : Control
    {
        public SettingsHeadline()
        {
            DefaultStyleKey = typeof(SettingsHeadline);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SettingsHeadlineAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (Text.Length > 0)
            {
                GetTemplateChild("Headline");
            }
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SettingsHeadline), new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsHeadline)d).OnTextChanged((string)e.NewValue);
        }

        private void OnTextChanged(string newValue)
        {
            if (newValue?.Length > 0)
            {
                GetTemplateChild("Headline");
            }
        }

        #endregion

        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(AnimatedImageSource), typeof(SettingsHeadline), new PropertyMetadata(null));

        #endregion

        #region LoopCount

        public int LoopCount
        {
            get { return (int)GetValue(LoopCountProperty); }
            set { SetValue(LoopCountProperty, value); }
        }

        public static readonly DependencyProperty LoopCountProperty =
            DependencyProperty.Register("LoopCount", typeof(bool), typeof(SettingsHeadline), new PropertyMetadata(1));

        #endregion
    }

    public class SettingsHeadlineAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly SettingsHeadline _owner;

        public SettingsHeadlineAutomationPeer(SettingsHeadline owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.Text ?? base.GetNameCore();
        }
    }
}
