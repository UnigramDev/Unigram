//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using Telegram.Controls.Media;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public enum SettingsButtonGlyphVisibility
    {
        Auto = -1,
        False = 0,
        True = 1
    }

    public partial class SettingsButton : GlyphButton
    {
        private BadgeButtonAutomationPeer _peer;

        private UIElement GlyphPresenter;
        private ContentPresenter ContentPresenter;
        private UIElement DescriptionPresenter;
        private UIElement Chevron;
        private UIElement Premium;

        public SettingsButton()
        {
            DefaultStyleKey = typeof(SettingsButton);
        }

        protected override void OnApplyTemplate()
        {
            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;

            if (ComputedIsGlyphVisible is false)
            {
                GlyphPresenter = GetTemplateChild(nameof(GlyphPresenter)) as UIElement;
                GlyphPresenter?.Visibility = Visibility.Collapsed;
            }

            if (ComputedIsDescriptionVisible)
            {
                DescriptionPresenter = GetTemplateChild(nameof(DescriptionPresenter)) as UIElement;
                DescriptionPresenter?.Visibility = Visibility.Visible;
            }

            if (IsChevronVisible)
            {
                Chevron = GetTemplateChild(nameof(Chevron)) as UIElement;
                Chevron?.Visibility = Visibility.Visible;
            }

            if (IsPremiumVisible)
            {
                Premium = GetTemplateChild(nameof(Premium)) as UIElement;
                Premium?.Visibility = Visibility.Visible;

                Grid.SetColumnSpan(ContentPresenter, 1);
            }

            base.OnApplyTemplate();
        }

        #region Badge

        public object Badge
        {
            get => GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.Register("Badge", typeof(object), typeof(SettingsButton), new PropertyMetadata(null, OnBadgeChanged));

        private static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsButton)d).OnBadgeChanged(e.NewValue, e.OldValue);
        }

        private void OnBadgeChanged(object newValue, object oldValue)
        {
            if (_peer != null && (newValue is string || newValue is null))
            {
                var newText = newValue?.ToString() ?? string.Empty;
                var oldText = oldValue?.ToString() ?? string.Empty;

                _peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, oldText, newText);
            }
        }

        #endregion

        #region BadgeTemplate

        public DataTemplate BadgeTemplate
        {
            get => (DataTemplate)GetValue(BadgeTemplateProperty);
            set => SetValue(BadgeTemplateProperty, value);
        }

        public static readonly DependencyProperty BadgeTemplateProperty =
            DependencyProperty.Register("BadgeTemplate", typeof(DataTemplate), typeof(SettingsButton), new PropertyMetadata(null));

        #endregion

        #region BadgeVisibility

        public Visibility BadgeVisibility
        {
            get => (Visibility)GetValue(BadgeVisibilityProperty);
            set => SetValue(BadgeVisibilityProperty, value);
        }

        public static readonly DependencyProperty BadgeVisibilityProperty =
            DependencyProperty.Register("BadgeVisibility", typeof(Visibility), typeof(SettingsButton), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region BadgeLabel

        public string BadgeLabel
        {
            get => (string)GetValue(BadgeLabelProperty);
            set => SetValue(BadgeLabelProperty, value);
        }

        public static readonly DependencyProperty BadgeLabelProperty =
            DependencyProperty.Register("BadgeLabel", typeof(string), typeof(SettingsButton), new PropertyMetadata(null, OnBadgeChanged));

        #endregion

        #region VerticalBadgeAlignment

        public VerticalAlignment VerticalBadgeAlignment
        {
            get { return (VerticalAlignment)GetValue(VerticalBadgeAlignmentProperty); }
            set { SetValue(VerticalBadgeAlignmentProperty, value); }
        }

        public static readonly DependencyProperty VerticalBadgeAlignmentProperty =
            DependencyProperty.Register(nameof(VerticalBadgeAlignment), typeof(VerticalAlignment), typeof(SettingsButton), new PropertyMetadata(VerticalAlignment.Center));

        #endregion

        #region Description

        public object Description
        {
            get { return (object)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(object), typeof(SettingsButton), new PropertyMetadata(null, OnDescriptionChanged));

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as SettingsButton;
            if (sender?.DescriptionPresenter != null || sender.ComputedIsDescriptionVisible)
            {
                sender.DescriptionPresenter ??= sender.GetTemplateChild(nameof(sender.DescriptionPresenter)) as UIElement;

                sender.DescriptionPresenter?.Visibility = sender.ComputedIsDescriptionVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region IsDescriptionVisible

        public bool ComputedIsDescriptionVisible => Description is string description ? !string.IsNullOrEmpty(description) : Description != null;

        #endregion

        #region IconSource

        public IAnimatedVisualSource2 IconSource
        {
            get { return (IAnimatedVisualSource2)GetValue(IconSourceProperty); }
            set { SetValue(IconSourceProperty, value); }
        }

        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register("IconSource", typeof(IAnimatedVisualSource2), typeof(SettingsButton), new PropertyMetadata(null));

        #endregion

        #region IsGlyphVisible

        public SettingsButtonGlyphVisibility IsGlyphVisible
        {
            get { return (SettingsButtonGlyphVisibility)GetValue(IsGlyphVisibleProperty); }
            set { SetValue(IsGlyphVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsGlyphVisibleProperty =
            DependencyProperty.Register("IsGlyphVisible", typeof(SettingsButtonGlyphVisibility), typeof(SettingsButton), new PropertyMetadata(SettingsButtonGlyphVisibility.Auto, OnGlyphVisibleChanged));

        private static void OnGlyphVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as SettingsButton;
            if (sender?.GlyphPresenter != null || !sender.ComputedIsGlyphVisible)
            {
                sender.GlyphPresenter ??= sender.GetTemplateChild(nameof(sender.GlyphPresenter)) as UIElement;

                sender.GlyphPresenter?.Visibility = sender.ComputedIsGlyphVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public bool ComputedIsGlyphVisible
        {
            get
            {
                var visible = IsGlyphVisible;
                if (visible == SettingsButtonGlyphVisibility.Auto)
                {
                    return !string.IsNullOrEmpty(Glyph);
                }

                return visible == SettingsButtonGlyphVisibility.True;
            }
        }

        #endregion

        #region IsPremiumVisible

        public bool IsPremiumVisible
        {
            get { return (bool)GetValue(IsPremiumVisibleProperty); }
            set { SetValue(IsPremiumVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsPremiumVisibleProperty =
            DependencyProperty.Register("IsPremiumVisible", typeof(bool), typeof(SettingsButton), new PropertyMetadata(false, OnPremiumVisibleChanged));

        private static void OnPremiumVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as SettingsButton;
            if (sender?.Premium != null || (bool)e.NewValue)
            {
                sender.Premium ??= sender.GetTemplateChild(nameof(sender.Premium)) as UIElement;

                if (sender.Premium != null)
                {
                    sender.Premium.Visibility = (bool)e.NewValue
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                    Grid.SetColumnSpan(sender.ContentPresenter, (bool)e.NewValue ? 1 : 2);
                }
            }
        }

        #endregion

        #region IsChevronVisible

        public bool IsChevronVisible
        {
            get { return (bool)GetValue(IsChevronVisibleProperty); }
            set { SetValue(IsChevronVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsChevronVisibleProperty =
            DependencyProperty.Register("IsChevronVisible", typeof(bool), typeof(SettingsButton), new PropertyMetadata(false, OnChevronVisibleChanged));

        private static void OnChevronVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as SettingsButton;
            if (sender?.Chevron != null || (bool)e.NewValue)
            {
                sender.Chevron ??= sender.GetTemplateChild(nameof(sender.Chevron)) as UIElement;

                sender.Chevron?.Visibility = (bool)e.NewValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region ChevronGlyph

        public string ChevronGlyph
        {
            get { return (string)GetValue(ChevronGlyphProperty); }
            set { SetValue(ChevronGlyphProperty, value); }
        }

        public static readonly DependencyProperty ChevronGlyphProperty =
            DependencyProperty.Register("ChevronGlyph", typeof(string), typeof(SettingsButton), new PropertyMetadata(Icons.ChevronRight16));

        #endregion

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return _peer ??= new BadgeButtonAutomationPeer(this);
        }
    }

    public partial class BadgeButtonWithImage : SettingsButton
    {


        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(BadgeButtonWithImage), new PropertyMetadata(null));


    }

    public partial class BadgeButtonAutomationPeer : ButtonAutomationPeer, IValueProvider
    {
        private readonly SettingsButton _owner;

        public BadgeButtonAutomationPeer(SettingsButton owner) : base(owner)
        {
            _owner = owner;
        }

        protected override object GetPatternCore(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Value)
            {
                return this;
            }

            return base.GetPatternCore(patternInterface);
        }

        protected override IList<AutomationPeer> GetChildrenCore()
        {
            return null;
        }

        protected override string GetFullDescriptionCore()
        {
            if (_owner.Description is FrameworkElement element)
            {
                var peer = FrameworkElementAutomationPeer.FromElement(element);
                if (peer != null)
                {
                    return peer.GetName();
                }
            }

            return _owner.Description?.ToString() ?? string.Empty;
        }

        public string Value
        {
            get
            {
                if (_owner.Badge is string badge)
                {
                    return badge;
                }

                return _owner.BadgeLabel ?? string.Empty;
            }
        }

        public void SetValue(string value)
        {
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }
    }
}
