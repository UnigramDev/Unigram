using Microsoft.UI.Xaml.Core.Direct;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Common
{
    public static class MenuFlyoutHelper
    {
        public static void ShowAt(this ContextRequestedEventArgs args, MenuFlyout flyout, FrameworkElement element)
        {
            if (flyout.Items.Count > 0 && args.TryGetPosition(element, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                try
                {
                    flyout.ShowAt(element, point);
                }
                catch { }
            }
            else if (flyout.Items.Count > 0)
            {
                try
                {
                    flyout.ShowAt(element);
                }
                catch { }
            }

            args.Handled = true;
        }

        public static void CreateFlyoutSeparator(this MenuFlyout flyout)
        {
            if (flyout.Items.Count > 0 && flyout.Items[flyout.Items.Count - 1] is MenuFlyoutItem)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }
        }

        public static void CreateFlyoutSeparator(this MenuFlyoutSubItem flyout)
        {
            if (flyout.Items.Count > 0 && flyout.Items[flyout.Items.Count - 1] is MenuFlyoutItem)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
            }
        }

        public static void CreateFlyoutItem<T>(this MenuFlyout flyout, Func<T, bool> visibility, ICommand command, T parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control) where T : class
        {
            flyout.Items.CreateFlyoutItem(visibility, command, parameter, text, icon, key, modifiers);
        }

        public static void CreateFlyoutItem<T>(this MenuFlyoutSubItem flyout, Func<T, bool> visibility, ICommand command, T parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control) where T : class
        {
            flyout.Items.CreateFlyoutItem(visibility, command, parameter, text, icon, key, modifiers);
        }

        public static void CreateFlyoutItem<T>(this IList<MenuFlyoutItemBase> items, Func<T, bool> visibility, ICommand command, T parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control) where T : class
        {
            var value = visibility(parameter as T);
            if (value)
            {
                var flyoutItem = new MenuFlyoutItem();
                flyoutItem.Command = command;
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Text = text;

                if (icon != null)
                {
                    flyoutItem.Icon = icon;
                }

                if (key.HasValue && ApiInfo.CanUseAccelerators)
                {
                    flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
                }

                items.Add(flyoutItem);
            }
        }

        // Probably used only for members context menu
        public static void CreateFlyoutItem<T1, T2, T3>(this MenuFlyout flyout, Func<T1, T2, T3, bool> visibility, ICommand command, T1 chatType, T2 status, T3 parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control) where T3 : class
        {
            var value = visibility(chatType, status, parameter as T3);
            if (value)
            {
                var flyoutItem = new MenuFlyoutItem();
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Command = command;
                flyoutItem.Text = text;

                if (icon != null)
                {
                    flyoutItem.Icon = icon;
                }

                if (key.HasValue && ApiInfo.CanUseAccelerators)
                {
                    flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
                }

                flyout.Items.Add(flyoutItem);
            }
        }

        public static void CreateFlyoutItem(this MenuFlyout flyout, ICommand command, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = command != null;
            flyoutItem.Command = command;
            flyoutItem.Text = text;

            if (icon != null)
            {
                flyoutItem.Icon = icon;
            }

            if (key.HasValue && ApiInfo.CanUseAccelerators)
            {
                flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
            }

            flyout.Items.Add(flyoutItem);
        }

        public static void CreateFlyoutItem(this MenuFlyout flyout, ICommand command, object parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            flyout.Items.CreateFlyoutItem(command, parameter, text, icon, key, modifiers);
        }

        public static void CreateFlyoutItem(this MenuFlyoutSubItem flyout, ICommand command, object parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            flyout.Items.CreateFlyoutItem(command, parameter, text, icon, key, modifiers);
        }

        public static void CreateFlyoutItem(this IList<MenuFlyoutItemBase> items, ICommand command, object parameter, string text, IconElement icon = null, VirtualKey? key = null, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = command != null;
            flyoutItem.CommandParameter = parameter;
            flyoutItem.Command = command;
            flyoutItem.Text = text;

            if (icon != null)
            {
                flyoutItem.Icon = icon;
            }

            if (key.HasValue && ApiInfo.CanUseAccelerators)
            {
                flyoutItem.KeyboardAccelerators.Add(new KeyboardAccelerator { Modifiers = modifiers, Key = key.Value, IsEnabled = false });
            }

            items.Add(flyoutItem);
        }

        public static IconElement GetGlyph(string glyph)
        {
            var direct = XamlDirect.GetDefault();
            if (direct.IsXamlDirectEnabled)
            {
                var icon = direct.CreateInstance(XamlTypeIndex.FontIcon);
                direct.SetStringProperty(icon, XamlPropertyIndex.FontIcon_Glyph, glyph);

                return direct.GetObject(icon) as FontIcon;
            }
            else
            {
                return new FontIcon { Glyph = glyph };
            }
        }
    }
}
