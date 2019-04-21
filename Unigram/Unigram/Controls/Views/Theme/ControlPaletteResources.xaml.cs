// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Windows.UI.Xaml;

namespace FluentEditor.ControlPalette
{
    public sealed partial class ControlPaletteResources : ResourceDictionary
    {
        public ControlPaletteResources()
        {
            this.InitializeComponent();
        }

        private List<ColorMappingInstance> _lightColorMappings;
        private List<ColorMappingInstance> _darkColorMappings;

        #region LinkedElementProperty

        public static readonly DependencyProperty LinkedElementProperty = DependencyProperty.Register("LinkedElement", typeof(FrameworkElement), typeof(ControlPaletteResources), new PropertyMetadata(null, new PropertyChangedCallback(OnLinkedElementPropertyChanged)));

        private static void OnLinkedElementPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ControlPaletteResources target)
            {
                target.OnLinkedElementChanged(e.OldValue as FrameworkElement, e.NewValue as FrameworkElement);
            }
        }

        private void OnLinkedElementChanged(FrameworkElement oldValue, FrameworkElement newValue)
        {
            if (_lightColorMappings != null)
            {
                foreach (var mapping in _lightColorMappings)
                {
                    mapping.LinkedElement = newValue;
                }
            }
            if (_darkColorMappings != null)
            {
                foreach (var mapping in _darkColorMappings)
                {
                    mapping.LinkedElement = newValue;
                }
            }
        }

        public FrameworkElement LinkedElement
        {
            get { return GetValue(LinkedElementProperty) as FrameworkElement; }
            set { SetValue(LinkedElementProperty, value); }
        }

        #endregion

        #region LightColorMappingProperty

        public static readonly DependencyProperty LightColorMappingProperty = DependencyProperty.Register("LightColorMapping", typeof(IReadOnlyList<ColorMapping>), typeof(ControlPaletteResources), new PropertyMetadata(null, new PropertyChangedCallback(OnLightColorMappingPropertyChanged)));

        private static void OnLightColorMappingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ControlPaletteResources target)
            {
                target.OnLightColorMappingChanged(e.OldValue as IReadOnlyList<ColorMapping>, e.NewValue as IReadOnlyList<ColorMapping>);
            }
        }

        private void OnLightColorMappingChanged(IReadOnlyList<ColorMapping> oldValue, IReadOnlyList<ColorMapping> newValue)
        {
            if (_lightColorMappings != null && _lightColorMappings.Count > 0)
            {
                for (int i = 0; i < _lightColorMappings.Count; i++)
                {
                    _lightColorMappings[i].Dispose();
                }
                _lightColorMappings.Clear();
                _lightColorMappings = null;
            }

            if (newValue != null && newValue.Count > 0)
            {
                var linkedElement = LinkedElement;
                _lightColorMappings = new List<ColorMappingInstance>(newValue.Count);
                for (int i = 0; i < newValue.Count; i++)
                {
                    var instance = newValue[i].CreateInstance(LightColorPaletteResources);
                    instance.LinkedElement = linkedElement;
                    _lightColorMappings.Add(instance);
                }
            }
        }

        public IReadOnlyList<ColorMapping> LightColorMapping
        {
            get { return GetValue(LightColorMappingProperty) as IReadOnlyList<ColorMapping>; }
            set { SetValue(LightColorMappingProperty, value); }
        }

        #endregion

        #region DarkColorMappingProperty

        public static readonly DependencyProperty DarkColorMappingProperty = DependencyProperty.Register("DarkColorMapping", typeof(IReadOnlyList<ColorMapping>), typeof(ControlPaletteResources), new PropertyMetadata(null, new PropertyChangedCallback(OnDarkColorMappingPropertyChanged)));

        private static void OnDarkColorMappingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ControlPaletteResources target)
            {
                target.OnDarkColorMappingChanged(e.OldValue as IReadOnlyList<ColorMapping>, e.NewValue as IReadOnlyList<ColorMapping>);
            }
        }

        private void OnDarkColorMappingChanged(IReadOnlyList<ColorMapping> oldValue, IReadOnlyList<ColorMapping> newValue)
        {
            if (_darkColorMappings != null && _darkColorMappings.Count > 0)
            {
                for (int i = 0; i < _darkColorMappings.Count; i++)
                {
                    _darkColorMappings[i].Dispose();
                }
                _darkColorMappings.Clear();
                _darkColorMappings = null;
            }

            if (newValue != null && newValue.Count > 0)
            {
                var linkedElement = LinkedElement;
                _darkColorMappings = new List<ColorMappingInstance>(newValue.Count);
                for (int i = 0; i < newValue.Count; i++)
                {
                    var instance = newValue[i].CreateInstance(DarkColorPaletteResources);
                    instance.LinkedElement = linkedElement;
                    _darkColorMappings.Add(instance);
                }
            }
        }

        public IReadOnlyList<ColorMapping> DarkColorMapping
        {
            get { return GetValue(DarkColorMappingProperty) as IReadOnlyList<ColorMapping>; }
            set { SetValue(DarkColorMappingProperty, value); }
        }

        #endregion
    }
}
