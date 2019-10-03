using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Template10.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Unigram.Controls
{
    [TemplatePart(Name = "BackButton", Type = typeof(Button))]
    public class PageHeader : Control
    {
        public PageHeader()
        {
            DefaultStyleKey = typeof(PageHeader);
        }

        protected override void OnApplyTemplate()
        {
            var button = GetTemplateChild("BackButton") as Button;
            if (button != null)
            {
                button.Click += Back_Click;
            }

            base.OnApplyTemplate();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var command = BackCommand;
            if (command != null)
            {
                command.Execute(null);
            }
            else
            {
                BootStrapper.Current.RaiseBackRequested();
            }
        }

        #region BackCommand

        public ICommand BackCommand
        {
            get { return (ICommand)GetValue(BackCommandProperty); }
            set { SetValue(BackCommandProperty, value); }
        }

        public static readonly DependencyProperty BackCommandProperty =
            DependencyProperty.Register("BackCommand", typeof(ICommand), typeof(PageHeader), new PropertyMetadata(null));

        #endregion

        #region BackVisibility

        public Visibility BackVisibility
        {
            get { return (Visibility)GetValue(BackVisibilityProperty); }
            set { SetValue(BackVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BackVisibilityProperty =
            DependencyProperty.Register("BackVisibility", typeof(Visibility), typeof(PageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

        #endregion

        #region IsLoading

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(PageHeader), new PropertyMetadata(false));

        #endregion
    }

    public class ButtonPageHeader : PageHeader
    {
        public ButtonPageHeader()
        {
            DefaultStyleKey = typeof(ButtonPageHeader);
        }

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region Command

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region CommandParameter

        public object CommandParameter
        {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion

        #region CommandVisibility

        public Visibility CommandVisibility
        {
            get { return (Visibility)GetValue(CommandVisibilityProperty); }
            set { SetValue(CommandVisibilityProperty, value); }
        }

        public static readonly DependencyProperty CommandVisibilityProperty =
            DependencyProperty.Register("CommandVisibility", typeof(Visibility), typeof(ButtonPageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region CommandToolTip

        public string CommandToolTip
        {
            get { return (string)GetValue(CommandToolTipProperty); }
            set { SetValue(CommandToolTipProperty, value); }
        }

        public static readonly DependencyProperty CommandToolTipProperty =
            DependencyProperty.Register("CommandToolTip", typeof(string), typeof(ButtonPageHeader), new PropertyMetadata(null));

        #endregion
    }

    [TemplatePart(Name = "BackButton", Type = typeof(Button))]
    public class ContentPageHeader : ContentControl
    {
        public ContentPageHeader()
        {
            DefaultStyleKey = typeof(ContentPageHeader);
        }

        protected override void OnApplyTemplate()
        {
            var button = GetTemplateChild("BackButton") as Button;
            if (button != null)
            {
                button.Click += Back_Click;
            }

            base.OnApplyTemplate();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var command = BackCommand;
            if (command != null)
            {
                command.Execute(null);
            }
            else
            {
                BootStrapper.Current.RaiseBackRequested();
            }
        }

        #region BackCommand

        public ICommand BackCommand
        {
            get { return (ICommand)GetValue(BackCommandProperty); }
            set { SetValue(BackCommandProperty, value); }
        }

        public static readonly DependencyProperty BackCommandProperty =
            DependencyProperty.Register("BackCommand", typeof(ICommand), typeof(ContentPageHeader), new PropertyMetadata(null));

        #endregion

        #region BackVisibility

        public Visibility BackVisibility
        {
            get { return (Visibility)GetValue(BackVisibilityProperty); }
            set { SetValue(BackVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BackVisibilityProperty =
            DependencyProperty.Register("BackVisibility", typeof(Visibility), typeof(ContentPageHeader), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region IsLoading

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(ContentPageHeader), new PropertyMetadata(false));

        #endregion
    }
}
