using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ResizeMe.Models;
using ResizeMe.Services;
using Windows.Storage;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace ResizeMe
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly PresetManager _manager = new();
        private readonly ObservableCollection<PresetSize> _view = new();

        public event EventHandler? PresetsChanged;

        public SettingsWindow()
        {
            InitializeComponent();
            Title = "ResizeMe Settings";
            PresetList.ItemsSource = _view;
            WidthInput.KeyUp += OnDimensionKeyUp;
            HeightInput.KeyUp += OnDimensionKeyUp;
            // Set the initial window size immediately after creation so the
            // first activation shows the correctly-sized window without a flash.
            SetWindowSize();
            _ = InitializeAsync();
            // Expose a test-only button for frequently resetting local preferences while debugging
            try
            {
#if DEBUG
                ResetPrefsButton.Visibility = Visibility.Visible;
#else
                ResetPrefsButton.Visibility = Visibility.Collapsed;
#endif
            }
            catch { }
        }

        private async Task InitializeAsync()
        {
            await _manager.LoadAsync();
            RefreshView();
        }

        private void SetWindowSize()
        {
            try
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (hWnd == IntPtr.Zero) return;
                
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                if (appWindow != null)
                {
                    // Default settings window size
                    appWindow.Resize(new Windows.Graphics.SizeInt32(625, 550));
                }
            }
            catch (System.ArgumentException)
            {
                // Window sizing failed - continue without setting specific size
            }
        }

        private void RefreshView()
        {
            _view.Clear();
            foreach (var p in _manager.Presets.OrderBy(p => p.Name)) _view.Add(p);
            StatusText.Text = $"Loaded {_view.Count} presets";
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            ValidationText.Visibility = Visibility.Collapsed;
            string name = NameInput.Text.Trim();
            if (!int.TryParse(WidthInput.Text, out int width) || !int.TryParse(HeightInput.Text, out int height))
            {
                ValidationText.Text = "Width and Height must be valid numbers";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }
            if (width <= 0 || height <= 0)
            {
                ValidationText.Text = "Dimensions must be greater than 0";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }
            if (string.IsNullOrWhiteSpace(name)) name = $"{width}x{height}";
            var preset = new PresetSize { Name = name, Width = width, Height = height };
            bool added = await _manager.AddPresetAsync(preset);
            if (!added)
            {
                ValidationText.Text = "A preset with this name already exists";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }
            RefreshView();
            NotifyPresetsChanged();
            StatusText.Text = $"Successfully added '{preset.Name}' preset";
            NameInput.Text = WidthInput.Text = HeightInput.Text = string.Empty;
            NameInput.Focus(FocusState.Programmatic);
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            if (PresetList.SelectedItem is PresetSize ps)
            {
                bool removed = await _manager.RemovePresetAsync(ps.Name);
                StatusText.Text = removed ? $"Removed {ps.Name}" : "Remove failed";
                if (removed)
                {
                    RefreshView();
                    NotifyPresetsChanged();
                }
            }
            else ValidationText.Text = "Select preset first";
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            await _manager.ResetToDefaultsAsync();
            RefreshView();
            NotifyPresetsChanged();
            StatusText.Text = "Defaults restored";
        }

        private void OnDimensionKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) AddButton_Click(sender, e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ResetPrefsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Remove only the first-minimize notification flag — keep other preferences intact by default
                ApplicationData.Current.LocalSettings.Values.Remove("FirstMinimizeNotificationShown");
                // Optionally clear the first-run flag if you want the settings window to reappear on next start
                // ApplicationData.Current.LocalSettings.Values.Remove("FirstRunCompleted");
                StatusText.Text = "First minimize notification reset";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Reset failed";
                System.Diagnostics.Debug.WriteLine($"Reset prefs error: {ex.Message}");
            }
        }

        private void NotifyPresetsChanged()
        {
            PresetsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
