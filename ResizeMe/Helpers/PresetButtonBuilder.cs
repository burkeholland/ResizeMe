using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Text;
using System;
using ResizeMe.Models;

namespace ResizeMe.Helpers
{
    internal static class PresetButtonBuilder
    {
        public static Button Build(PresetSize preset, ResourceDictionary? resources, RoutedEventHandler clickHandler)
        {
            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 1, 0, 1)
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var itemIcon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "\xE7C4",
                FontSize = 14,
                Opacity = 0.9,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(itemIcon, 0);

            var itemTextPanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

            Style? headerStyle = null;
            Style? subTextStyle = null;
            Brush? accentBrush = null;
            Brush? borderBg = null;

            if (resources != null)
            {
                if (resources.TryGetValue("PresetHeaderTextStyle", out var hs) && hs is Style) headerStyle = (Style)hs;
                if (resources.TryGetValue("PresetSubTextStyle", out var ss) && ss is Style) subTextStyle = (Style)ss;
                if (resources.TryGetValue("AccentTextFillColorSecondaryBrush", out var ab) && ab is Brush) accentBrush = (Brush)ab;
                if (resources.TryGetValue("ControlFillColorDefaultBrush", out var bg) && bg is Brush) borderBg = (Brush)bg;
            }

            var nameText = new TextBlock { Text = preset.Name, FontWeight = FontWeights.SemiBold };
            if (headerStyle != null) nameText.Style = headerStyle; else nameText.FontSize = 13;

            var sizeText = new TextBlock { Text = $"{preset.Width} x {preset.Height}", Opacity = 0.8 };
            if (subTextStyle != null) sizeText.Style = subTextStyle; else sizeText.FontSize = 11;

            itemTextPanel.Children.Add(nameText);
            itemTextPanel.Children.Add(sizeText);
            Grid.SetColumn(itemTextPanel, 1);

            itemGrid.Children.Add(itemIcon);
            itemGrid.Children.Add(itemTextPanel);

            if (borderBg != null) itemBorder.Background = borderBg;
            itemBorder.Child = itemGrid;

            var btn = new Button
            {
                Content = itemBorder,
                Tag = $"{preset.Width}x{preset.Height}",
                Margin = new Thickness(0, 0, 0, 0),
                Height = double.NaN,
                MinHeight = 44,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            if (resources != null && resources.TryGetValue("PresetButtonBaseStyle", out var baseObj) && baseObj is Style baseStyle)
            {
                btn.Style = baseStyle;
            }

            if (clickHandler != null)
            {
                btn.Click += clickHandler;
            }

            return btn;
        }
    }
}
