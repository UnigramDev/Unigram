//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls.Media;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class SettingsExpander : ContentControl
    {
        private SettingsButton ActionButton;
        private Border PopupHost;
        private ContentPresenter PopupRoot;

        public SettingsExpander()
        {
            DefaultStyleKey = typeof(SettingsExpander);
        }

        protected override void OnApplyTemplate()
        {
            ActionButton = GetTemplateChild(nameof(ActionButton)) as SettingsButton;
            ActionButton.Click += OnClick;
            ActionButton.ChevronGlyph = IsExpanded ? Icons.ChevronUp16 : Icons.ChevronDown16;
            ActionButton.CornerRadius = new CornerRadius(CornerRadius.TopLeft, CornerRadius.TopRight, IsExpanded ? 0 : CornerRadius.BottomRight, IsExpanded ? 0 : CornerRadius.BottomLeft);

            PopupHost = GetTemplateChild(nameof(PopupHost)) as Border;
            PopupRoot = GetTemplateChild(nameof(PopupRoot)) as ContentPresenter;
            PopupHost.BorderThickness = new Thickness(BorderThickness.Left, 0, BorderThickness.Right, BorderThickness.Bottom);
            PopupHost.CornerRadius = new CornerRadius(0, 0, CornerRadius.BottomRight, CornerRadius.BottomLeft);

            ElementCompositionPreview.SetIsTranslationEnabled(PopupRoot, true);

            OnExpandedChanged(IsExpanded, IsExpanded);
            base.OnApplyTemplate();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
        }

        public event EventHandler ExpandedChanged;

        #region IsExpanded

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register("IsExpanded", typeof(bool), typeof(SettingsExpander), new PropertyMetadata(false, OnExpandedChanged));

        private static void OnExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsExpander)d).OnExpandedChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private bool _expanded;
        private int _tracker;

        private void OnExpandedChanged(bool newValue, bool oldValue)
        {
            if (ActionButton == null || PopupHost == null)
            {
                return;
            }

            if (newValue != oldValue)
            {
                VisualStateManager.GoToState(this, newValue ? "Expanded" : "Normal", false);
                ExpandedChanged?.Invoke(this, EventArgs.Empty);
            }

            var tracker = _tracker++;

            _expanded = newValue;
            ActionButton.ChevronGlyph = IsExpanded ? Icons.ChevronUp16 : Icons.ChevronDown16;
            ActionButton.CornerRadius = new CornerRadius(CornerRadius.TopLeft, CornerRadius.TopRight, IsExpanded ? 0 : CornerRadius.BottomRight, IsExpanded ? 0 : CornerRadius.BottomLeft);

            PopupHost.BorderThickness = new Thickness(BorderThickness.Left, 0, BorderThickness.Right, BorderThickness.Bottom);
            PopupHost.CornerRadius = new CornerRadius(0, 0, CornerRadius.BottomRight, CornerRadius.BottomLeft);
            PopupHost.Height = newValue ? double.NaN : 0;
            PopupRoot.Margin = new Thickness(0, 0, 0, newValue ? 0 : -PopupRoot.ActualHeight);
            PopupRoot.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(PopupRoot);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(1, newValue ? 0 : 44);
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateScalarKeyFrameAnimation();
            offset.InsertKeyFrame(1, newValue ? 0 : -44);
            offset.Duration = Constants.FastAnimation;

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1, newValue ? 1 : 0);
            opacity.Duration = Constants.FastAnimation;

            var batch = visual.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_tracker == tracker)
                {
                    PopupRoot.Visibility = _expanded
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            };

            visual.Clip.StartAnimation("TopInset", clip);
            visual.StartAnimation("Translation.Y", offset);
            visual.StartAnimation("Opacity", opacity);

            batch.End();
        }

        #endregion

        #region Header

        public object Header
        {
            get { return (object)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        #endregion

        #region Badge

        public object Badge
        {
            get => GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.Register("Badge", typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        #endregion

        #region BadgeTemplate

        public DataTemplate BadgeTemplate
        {
            get => (DataTemplate)GetValue(BadgeTemplateProperty);
            set => SetValue(BadgeTemplateProperty, value);
        }

        public static readonly DependencyProperty BadgeTemplateProperty =
            DependencyProperty.Register("BadgeTemplate", typeof(DataTemplate), typeof(SettingsExpander), new PropertyMetadata(null));

        #endregion

        #region BadgeVisibility

        public Visibility BadgeVisibility
        {
            get => (Visibility)GetValue(BadgeVisibilityProperty);
            set => SetValue(BadgeVisibilityProperty, value);
        }

        public static readonly DependencyProperty BadgeVisibilityProperty =
            DependencyProperty.Register("BadgeVisibility", typeof(Visibility), typeof(SettingsExpander), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region BadgeLabel

        public string BadgeLabel
        {
            get => (string)GetValue(BadgeLabelProperty);
            set => SetValue(BadgeLabelProperty, value);
        }

        public static readonly DependencyProperty BadgeLabelProperty =
            DependencyProperty.Register("BadgeLabel", typeof(string), typeof(SettingsExpander), new PropertyMetadata(null));

        #endregion

        #region Description

        public object Description
        {
            get { return (object)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(object), typeof(SettingsExpander), new PropertyMetadata(null));

        #endregion

        #region Glyph
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(SettingsExpander), new PropertyMetadata(null));
        #endregion

        #region IsGlyphVisible

        public SettingsButtonGlyphVisibility IsGlyphVisible
        {
            get { return (SettingsButtonGlyphVisibility)GetValue(IsGlyphVisibleProperty); }
            set { SetValue(IsGlyphVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsGlyphVisibleProperty =
            DependencyProperty.Register("IsGlyphVisible", typeof(SettingsButtonGlyphVisibility), typeof(SettingsExpander), new PropertyMetadata(SettingsButtonGlyphVisibility.Auto));

        #endregion

        #region IsPremiumVisible

        public bool IsPremiumVisible
        {
            get { return (bool)GetValue(IsPremiumVisibleProperty); }
            set { SetValue(IsPremiumVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsPremiumVisibleProperty =
            DependencyProperty.Register("IsPremiumVisible", typeof(bool), typeof(SettingsExpander), new PropertyMetadata(false));

        #endregion
    }
}
