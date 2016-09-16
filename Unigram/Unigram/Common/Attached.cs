using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Common
{
    public class Attached
    {
        #region Tapped
        public static ICommand GetTapped(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(TappedProperty);
        }

        public static void SetTapped(DependencyObject obj, ICommand value)
        {
            obj.SetValue(TappedProperty, value);
        }

        public static readonly DependencyProperty TappedProperty =
            DependencyProperty.RegisterAttached("Tapped", typeof(ICommand), typeof(Attached), new PropertyMetadata(null, OnTappedChanged));

        private static void OnTappedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as Control;
            if (sender != null)
            {
                sender.IsTapEnabled = true;
                sender.Tapped -= OnTapped;
                sender.Tapped += OnTapped;
            }
        }

        private static void OnTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var command = GetTapped(sender as Control);
            var parameter = GetTappedParameter(sender as Control);
            if (command != null)
            {
                command.Execute(parameter ?? (sender as Control).DataContext);
            }
        }
        #endregion

        #region TappedParameter
        public static object GetTappedParameter(DependencyObject obj)
        {
            return (object)obj.GetValue(TappedParameterProperty);
        }

        public static void SetTappedParameter(DependencyObject obj, object value)
        {
            obj.SetValue(TappedParameterProperty, value);
        }

        public static readonly DependencyProperty TappedParameterProperty =
            DependencyProperty.RegisterAttached("TappedParameter", typeof(object), typeof(Attached), new PropertyMetadata(null));
        #endregion
    }
}
