using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System;
using System.Diagnostics;

namespace ResizeMe
{
    /// <summary>
    /// Settings window for managing preset sizes (UI only - no persistence yet).
    /// </summary>
    public sealed partial class SettingsWindow : Window
    {
        private readonly ObservableCollection<PresetSizeTemp> _presets = new();

        public SettingsWindow()
        {
            InitializeComponent();
            Title = "ResizeMe Settings";
            PresetList.ItemsSource = _presets;
            SeedDefaults();
        }

        private void SeedDefaults()
        {
            _presets.Clear();
            _presets.Add(new PresetSizeTemp("HD",1280,720));
            _presets.Add(new PresetSizeTemp("Full HD",1920,1080));
            _presets.Add(new PresetSizeTemp("Laptop",1366,768));
            _presets.Add(new PresetSizeTemp("Classic",1024,768));
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameInput.Text.Trim();
            if (!int.TryParse(WidthInput.Text, out int width) || !int.TryParse(HeightInput.Text, out int height))
            {
                StatusText.Text = "Width/Height must be numbers";
                return;
            }
            if (width <= 0 || height <= 0)
            {
                StatusText.Text = "Dimensions must be > 0";
                return;
            }
            if (string.IsNullOrWhiteSpace(name)) name = $"{width}x{height}";
            _presets.Add(new PresetSizeTemp(name,width,height));
            StatusText.Text = $"Added {name}";
            NameInput.Text = WidthInput.Text = HeightInput.Text = string.Empty;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetList.SelectedItem is PresetSizeTemp item)
            {
                _presets.Remove(item);
                StatusText.Text = $"Removed {item.Name}";
            }
            else
            {
                StatusText.Text = "Nothing selected";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SeedDefaults();
            StatusText.Text = "Defaults restored";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Temporary preset model used only in PR 3.1 (replaced in PR 3.2).
    /// </summary>
    internal class PresetSizeTemp
    {
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public PresetSizeTemp(string name, int width, int height)
        {
            Name = name; Width = width; Height = height;
        }
    }
}
