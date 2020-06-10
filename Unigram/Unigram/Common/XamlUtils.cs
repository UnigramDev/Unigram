using System;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
{
    public static class XamlUtils
    {
        public static void UpdateBindings(UserControl page)
        {
            var field = page.GetType().GetTypeInfo().GetDeclaredField("Bindings");
            var bindings = field?.GetValue(page);
            var update = bindings?.GetType().GetRuntimeMethod("Update", new Type[] { });
            update?.Invoke(bindings, null);
        }

        public static void InitializeBindings(UserControl page)
        {
            var field = page.GetType().GetTypeInfo().GetDeclaredField("Bindings");
            var bindings = field?.GetValue(page);
            var update = bindings?.GetType().GetRuntimeMethod("Initialize", new Type[] { });
            update?.Invoke(bindings, null);
        }

        public static void StopTrackingBindings(UserControl page)
        {
            var field = page.GetType().GetTypeInfo().GetDeclaredField("Bindings");
            var bindings = field?.GetValue(page);
            var update = bindings?.GetType().GetRuntimeMethod("StopTracking", new Type[] { });
            update?.Invoke(bindings, null);
        }

        public static void SetAsNotSet(this DependencyObject o, DependencyProperty dp)
        {
            o.SetValue(dp, DependencyProperty.UnsetValue);
        }

        public static void SetIfNotSet(this DependencyObject o, DependencyProperty dp, object value)
        {
            if (o.ReadLocalValue(dp) == DependencyProperty.UnsetValue)
                o.SetValue(dp, value);
        }
    }
}
