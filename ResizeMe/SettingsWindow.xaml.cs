using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ResizeMe.Models;
using ResizeMe.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace ResizeMe
{
    /// <summary>
    /// Settings window with persistent preset management.
    /// </summary>
    public sealed partial class SettingsWindow : Window
    {
        public event RoutedEventHandler? Loaded;
        private readonly PresetManager _manager = new();
        private readonly ObservableCollection<PresetSize> _viewPresets = new();

        public SettingsWindow()
        {
            InitializeComponent();
            Title = "ResizeMe Settings";
            PresetList.ItemsSource = _viewPresets;
            AttachLoadedHandler();
            Loaded += SettingsWindow_Loaded;
        }

        private void AttachLoadedHandler()
        {
            if (Content is FrameworkElement root)
            {
                root.Loaded += Root_Loaded;
            }
            else
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await _manager.LoadAsync();
                    RefreshView();
                });
            }
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            if (Content is FrameworkElement root)
            {
                root.Loaded -= Root_Loaded;
            }
            Loaded?.Invoke(this, e);
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _manager.LoadAsync();
            RefreshView();
        }

        private void RefreshView()
        {
            _viewPresets.Clear();
            foreach (var p in _manager.Presets.OrderBy(p => p.Name)) _viewPresets.Add(p);
            StatusText.Text = $"Loaded {_viewPresets.Count} presets";
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameInput.Text.Trim();
            if (!int.TryParse(WidthInput.Text, out int width) || !int.TryParse(HeightInput.Text, out int height))
            {
                StatusText.Text = "Width/Height must be numbers"; return;
            }
            if (width <= 0 || height <= 0)
            {
                StatusText.Text = "Dimensions must be > 0"; return;
            }
            if (string.IsNullOrWhiteSpace(name)) name = $"{width}x{height}";
            var preset = new PresetSize { Name = name, Width = width, Height = height };
            bool added = await _manager.AddPresetAsync(preset);
            StatusText.Text = added ? $"Added {preset}" : "Duplicate name";
            if (added) RefreshView();
            NameInput.Text = WidthInput.Text = HeightInput.Text = string.Empty;
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PresetList.SelectedItem is PresetSize item)
            {
                bool removed = await _manager.RemovePresetAsync(item.Name);
                StatusText.Text = removed ? $"Removed {item.Name}" : "Remove failed";
                if (removed) RefreshView();
            }
            else StatusText.Text = "Nothing selected";
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await _manager.ResetToDefaultsAsync();
            RefreshView();
            StatusText.Text = "Defaults restored";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
