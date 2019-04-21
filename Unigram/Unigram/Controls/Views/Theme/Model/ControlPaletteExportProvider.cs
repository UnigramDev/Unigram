// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//using FluentEditor.ControlPalette.Export;
using System;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace FluentEditor.ControlPalette.Model
{
    public interface IControlPaletteExportProvider
    {
        Task ShowExportView(string exportData);
        string GenerateExportData(IControlPaletteModel model, bool showAllColors = false);
    }

    public class ControlPaletteExportProvider : IControlPaletteExportProvider
    {
        private object _lock = new object();
        private bool _isWindowInitializing = false;

        private CoreApplicationView _exportWindow;

        // This is owned by the UI thread for the _exportWindow
        //private ExportViewModel _exportViewModel;

        public string GenerateExportData(IControlPaletteModel model, bool showAllColors = false)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!-- Free Public License 1.0.0 Permission to use, copy, modify, and/or distribute this code for any purpose with or without fee is hereby granted. -->");
            sb.AppendLine("<ResourceDictionary.ThemeDictionaries>");

            sb.AppendLine("    <ResourceDictionary x:Key=\"Default\">");
            sb.AppendLine("        <ResourceDictionary.MergedDictionaries>");
            sb.Append("            <ColorPaletteResources");
            if (model.DarkColorMapping != null)
            {
                foreach (var m in model.DarkColorMapping)
                {
                    sb.Append(" ");
                    sb.Append(m.Target.ToString());
                    sb.Append("=\"");
                    sb.Append(m.Source.ActiveColor.ToString());
                    sb.Append("\"");
                }
            }
            sb.AppendLine(" />");
            sb.AppendLine("            <ResourceDictionary>");

            Windows.UI.Color ChromeAltMediumHigh = model.DarkRegion.ActiveColor;
            ChromeAltMediumHigh.A = 204;

            sb.AppendLine(string.Format("                <Color x:Key=\"SystemChromeAltMediumHighColor\">{0}</Color>", ChromeAltMediumHigh.ToString()));
            sb.AppendLine(string.Format("                <Color x:Key=\"SystemChromeAltHighColor\">{0}</Color>", model.DarkRegion.ActiveColor.ToString()));
            sb.AppendLine(string.Format("                <Color x:Key=\"SystemRevealListLowColor\">{0}</Color>", model.DarkBase.Palette[8].ActiveColor.ToString()));
            sb.AppendLine(string.Format("                <Color x:Key=\"SystemRevealListMediumColor\">{0}</Color>", model.DarkBase.Palette[5].ActiveColor.ToString()));

            sb.AppendLine(string.Format("                <Color x:Key=\"RegionColor\">{0}</Color>", model.DarkRegion.ActiveColor.ToString()));
            sb.AppendLine("                <SolidColorBrush x:Key=\"RegionBrush\" Color=\"{StaticResource RegionColor}\" />");
            if (showAllColors)
            {
                sb.AppendLine(string.Format("                <Color x:Key=\"BaseColor\">{0}</Color>", model.DarkBase.BaseColor.ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette000Color\">{0}</Color>", model.DarkBase.Palette[0].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette100Color\">{0}</Color>", model.DarkBase.Palette[1].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette200Color\">{0}</Color>", model.DarkBase.Palette[2].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette300Color\">{0}</Color>", model.DarkBase.Palette[3].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette400Color\">{0}</Color>", model.DarkBase.Palette[4].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette500Color\">{0}</Color>", model.DarkBase.Palette[5].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette600Color\">{0}</Color>", model.DarkBase.Palette[6].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette700Color\">{0}</Color>", model.DarkBase.Palette[7].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette800Color\">{0}</Color>", model.DarkBase.Palette[8].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette900Color\">{0}</Color>", model.DarkBase.Palette[9].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette1000Color\">{0}</Color>", model.DarkBase.Palette[10].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryColor\">{0}</Color>", model.DarkPrimary.BaseColor.ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette000Color\">{0}</Color>", model.DarkPrimary.Palette[0].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette100Color\">{0}</Color>", model.DarkPrimary.Palette[1].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette200Color\">{0}</Color>", model.DarkPrimary.Palette[2].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette300Color\">{0}</Color>", model.DarkPrimary.Palette[3].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette400Color\">{0}</Color>", model.DarkPrimary.Palette[4].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette500Color\">{0}</Color>", model.DarkPrimary.Palette[5].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette600Color\">{0}</Color>", model.DarkPrimary.Palette[6].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette700Color\">{0}</Color>", model.DarkPrimary.Palette[7].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette800Color\">{0}</Color>", model.DarkPrimary.Palette[8].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette900Color\">{0}</Color>", model.DarkPrimary.Palette[9].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette1000Color\">{0}</Color>", model.DarkPrimary.Palette[10].ActiveColor.ToString()));
                sb.AppendLine("                <SolidColorBrush x:Key=\"BaseBrush\" Color=\"{StaticResource BaseColor}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette000Brush\" Color=\"{StaticResource BasePalette000Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette100Brush\" Color=\"{StaticResource BasePalette100Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette200Brush\" Color=\"{StaticResource BasePalette200Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette300Brush\" Color=\"{StaticResource BasePalette300Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette400Brush\" Color=\"{StaticResource BasePalette400Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette500Brush\" Color=\"{StaticResource BasePalette500Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette600Brush\" Color=\"{StaticResource BasePalette600Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette700Brush\" Color=\"{StaticResource BasePalette700Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette800Brush\" Color=\"{StaticResource BasePalette800Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette900Brush\" Color=\"{StaticResource BasePalette900Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette1000Brush\" Color=\"{StaticResource BasePalette1000Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette000Brush\" Color=\"{StaticResource PrimaryPalette000Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette100Brush\" Color=\"{StaticResource PrimaryPalette100Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette200Brush\" Color=\"{StaticResource PrimaryPalette200Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette300Brush\" Color=\"{StaticResource PrimaryPalette300Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette400Brush\" Color=\"{StaticResource PrimaryPalette400Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette500Brush\" Color=\"{StaticResource PrimaryPalette500Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette600Brush\" Color=\"{StaticResource PrimaryPalette600Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette700Brush\" Color=\"{StaticResource PrimaryPalette700Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette800Brush\" Color=\"{StaticResource PrimaryPalette800Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette900Brush\" Color=\"{StaticResource PrimaryPalette900Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette1000Brush\" Color=\"{StaticResource PrimaryPalette1000Color}\" />");
            }
            sb.AppendLine("            </ResourceDictionary>");
            sb.AppendLine("        </ResourceDictionary.MergedDictionaries>");
            sb.AppendLine("    </ResourceDictionary>");

            sb.AppendLine("    <ResourceDictionary x:Key=\"Light\">");
            sb.AppendLine("        <ResourceDictionary.MergedDictionaries>");
            sb.Append("            <ColorPaletteResources");
            if (model.LightColorMapping != null)
            {
                foreach (var m in model.LightColorMapping)
                {
                    sb.Append(" ");
                    sb.Append(m.Target.ToString());
                    sb.Append("=\"");
                    sb.Append(m.Source.ActiveColor.ToString());
                    sb.Append("\"");
                }
            }
            sb.AppendLine(" />");
            sb.AppendLine("            <ResourceDictionary>");

            ChromeAltMediumHigh = model.LightRegion.ActiveColor;
            ChromeAltMediumHigh.A = 204;

            sb.AppendLine(string.Format("                <Color x:Key=\"SystemChromeAltMediumHighColor\">{0}</Color>", ChromeAltMediumHigh.ToString()));
            sb.AppendLine(string.Format("                <Color x:Key=\"SystemChromeAltHighColor\">{0}</Color>", model.LightRegion.ActiveColor.ToString()));
            sb.AppendLine(string.Format("                <Color x:Key=\"SystemRevealListLowColor\">{0}</Color>", model.LightBase.Palette[1].ActiveColor.ToString()));
            sb.AppendLine(string.Format("                <Color x:Key=\"SystemRevealListMediumColor\">{0}</Color>", model.LightBase.Palette[5].ActiveColor.ToString()));

            sb.AppendLine("                <RevealBackgroundBrush x:Key=\"SystemControlHighlightListLowRevealBackgroundBrush\" TargetTheme=\"Light\" Color=\"{ThemeResource SystemRevealListMediumColor}\" FallbackColor=\"{ StaticResource SystemListMediumColor}\" />");

            sb.AppendLine(string.Format("                <Color x:Key=\"RegionColor\">{0}</Color>", model.LightRegion.ActiveColor.ToString()));
            sb.AppendLine("                <SolidColorBrush x:Key=\"RegionBrush\" Color=\"{StaticResource RegionColor}\" />");
            if (showAllColors)
            {
                sb.AppendLine(string.Format("                <Color x:Key=\"BaseColor\">{0}</Color>", model.LightBase.BaseColor.ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette000Color\">{0}</Color>", model.LightBase.Palette[0].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette100Color\">{0}</Color>", model.LightBase.Palette[1].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette200Color\">{0}</Color>", model.LightBase.Palette[2].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette300Color\">{0}</Color>", model.LightBase.Palette[3].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette400Color\">{0}</Color>", model.LightBase.Palette[4].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette500Color\">{0}</Color>", model.LightBase.Palette[5].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette600Color\">{0}</Color>", model.LightBase.Palette[6].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette700Color\">{0}</Color>", model.LightBase.Palette[7].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette800Color\">{0}</Color>", model.LightBase.Palette[8].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette900Color\">{0}</Color>", model.LightBase.Palette[9].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"BasePalette1000Color\">{0}</Color>", model.LightBase.Palette[10].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryColor\">{0}</Color>", model.LightPrimary.BaseColor.ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette000Color\">{0}</Color>", model.LightPrimary.Palette[0].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette100Color\">{0}</Color>", model.LightPrimary.Palette[1].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette200Color\">{0}</Color>", model.LightPrimary.Palette[2].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette300Color\">{0}</Color>", model.LightPrimary.Palette[3].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette400Color\">{0}</Color>", model.LightPrimary.Palette[4].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette500Color\">{0}</Color>", model.LightPrimary.Palette[5].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette600Color\">{0}</Color>", model.LightPrimary.Palette[6].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette700Color\">{0}</Color>", model.LightPrimary.Palette[7].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette800Color\">{0}</Color>", model.LightPrimary.Palette[8].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette900Color\">{0}</Color>", model.LightPrimary.Palette[9].ActiveColor.ToString()));
                sb.AppendLine(string.Format("                <Color x:Key=\"PrimaryPalette1000Color\">{0}</Color>", model.LightPrimary.Palette[10].ActiveColor.ToString()));
                sb.AppendLine("                <SolidColorBrush x:Key=\"BaseBrush\" Color=\"{StaticResource BaseColor}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette000Brush\" Color=\"{StaticResource BasePalette000Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette100Brush\" Color=\"{StaticResource BasePalette100Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette200Brush\" Color=\"{StaticResource BasePalette200Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette300Brush\" Color=\"{StaticResource BasePalette300Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette400Brush\" Color=\"{StaticResource BasePalette400Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette500Brush\" Color=\"{StaticResource BasePalette500Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette600Brush\" Color=\"{StaticResource BasePalette600Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette700Brush\" Color=\"{StaticResource BasePalette700Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette800Brush\" Color=\"{StaticResource BasePalette800Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette900Brush\" Color=\"{StaticResource BasePalette900Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"BasePalette1000Brush\" Color=\"{StaticResource BasePalette1000Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette000Brush\" Color=\"{StaticResource PrimaryPalette000Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette100Brush\" Color=\"{StaticResource PrimaryPalette100Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette200Brush\" Color=\"{StaticResource PrimaryPalette200Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette300Brush\" Color=\"{StaticResource PrimaryPalette300Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette400Brush\" Color=\"{StaticResource PrimaryPalette400Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette500Brush\" Color=\"{StaticResource PrimaryPalette500Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette600Brush\" Color=\"{StaticResource PrimaryPalette600Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette700Brush\" Color=\"{StaticResource PrimaryPalette700Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette800Brush\" Color=\"{StaticResource PrimaryPalette800Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette900Brush\" Color=\"{StaticResource PrimaryPalette900Color}\" />");
                sb.AppendLine("                <SolidColorBrush x:Key=\"PrimaryPalette1000Brush\" Color=\"{StaticResource PrimaryPalette1000Color}\" />");
            }
            sb.AppendLine("            </ResourceDictionary>");
            sb.AppendLine("        </ResourceDictionary.MergedDictionaries>");
            sb.AppendLine("    </ResourceDictionary>");

            sb.AppendLine("    <ResourceDictionary x:Key=\"HighContrast\">");
            sb.AppendLine("        <StaticResource x:Key=\"RegionColor\" ResourceKey=\"SystemColorWindowColor\" />");
            sb.AppendLine("        <SolidColorBrush x:Key=\"RegionBrush\" Color=\"{StaticResource RegionColor}\" />");
            sb.AppendLine("    </ResourceDictionary>");

            sb.AppendLine("</ResourceDictionary.ThemeDictionaries>");

            var retVal = sb.ToString();
            return retVal;
        }

        public async Task ShowExportView(string exportData)
        {
            CoreApplicationView exportWindow = null;
            bool init = false;
            lock (_lock)
            {
                if (_isWindowInitializing)
                {
                    return;
                }
                if (_exportWindow == null)
                {
                    init = true;
                    _isWindowInitializing = true;
                    _exportWindow = CoreApplication.CreateNewView();
                }
                exportWindow = _exportWindow;
            }

            if (init)
            {
                await _exportWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //_exportViewModel = new ExportViewModel(exportData);
                    //ExportView exportView = new ExportView(_exportViewModel);

                    //Window.Current.Content = exportView;
                    Window.Current.Activate();
                    var viewId = ApplicationView.GetForCurrentView().Id;
                    _ = ApplicationViewSwitcher.TryShowAsStandaloneAsync(viewId);
                });

                lock (_lock)
                {
                    _isWindowInitializing = false;
                }
            }
            else
            {
                await _exportWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //_exportViewModel.ExportText = exportData;
                    var w = Window.Current.Content;
                    var viewId = ApplicationView.GetForCurrentView().Id;
                    _ = ApplicationViewSwitcher.TryShowAsStandaloneAsync(viewId);
                });
            }
        }
    }
}
