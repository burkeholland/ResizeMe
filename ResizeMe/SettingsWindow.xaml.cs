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
using System.Collections.Generic;
using Microsoft.UI.Input;
using Windows.UI.Core;

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
            LoadHotkeyDisplay();
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
                    // Set window icon
                    try
                    {
                        var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
                        if (System.IO.File.Exists(iconPath))
                        {
                            appWindow.SetIcon(iconPath);
                        }
                    }
                    catch (Exception iconEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to set settings window icon: {iconEx.Message}");
                    }
                    // Default settings window size - increased width and height for hotkey card
                    appWindow.Resize(new Windows.Graphics.SizeInt32(700, 600));
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
        }

        // Hotkey capture state
        private string _capturedModifiers = string.Empty; // temp capture store
        private string _capturedKey = string.Empty;       // temp capture store
        private bool _capturing;
        private TextBlock? _captureFlyoutStatusControl;

        private void LoadHotkeyDisplay()
        {
            try
            {
                CurrentHotkeyText.Text = $"Current: {Services.UserPreferences.HotKeyModifiers}+{Services.UserPreferences.HotKeyCode}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadHotkeyDisplay error: {ex.Message}");
            }
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
            NameInput.Text = WidthInput.Text = HeightInput.Text = string.Empty;
            NameInput.Focus(FocusState.Programmatic);
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Text = string.Empty;
            if (PresetList.SelectedItem is PresetSize ps)
            {
                bool removed = await _manager.RemovePresetAsync(ps.Name);
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
            var dialog = new ContentDialog
            {
                Title = "Reset Presets",
                Content = "Are you sure you want to reset presets to defaults? This cannot be undone.",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel"
            };

            // Ensure the dialog has a XamlRoot; otherwise ShowAsync will throw
            var fe = this.Content as FrameworkElement;
            if (fe != null) dialog.XamlRoot = fe.XamlRoot;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _manager.ResetToDefaultsAsync();
                RefreshView();
                NotifyPresetsChanged();
            }
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reset prefs error: {ex.Message}");
            }
        }

        private void NotifyPresetsChanged()
        {
            PresetsChanged?.Invoke(this, EventArgs.Empty);
        }

        private async void CustomizeHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            HotkeyErrorText.Visibility = Visibility.Collapsed;
            _capturedModifiers = string.Empty;
            _capturedKey = string.Empty;
            _capturing = true;

            // Use a Flyout anchored to the Customize button to avoid overlapping the titlebar
            var flyout = new Flyout();
            var panel = new StackPanel { Spacing = 8, Padding = new Thickness(12) };
            var instruction = new TextBlock { Text = "Press key combination now...", FontSize = 14 };
            var captureStatus = new TextBlock { Text = "", FontSize = 13 };
            _captureFlyoutStatusControl = captureStatus;
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            var applyBtn = new Button { Content = "Apply", Padding = new Thickness(12, 6, 12, 6) };
            var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(12, 6, 12, 6) };
            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(applyBtn);
            panel.Children.Add(instruction);
            panel.Children.Add(captureStatus);
            panel.Children.Add(btnPanel);
            flyout.Content = panel;

            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool?>();
            applyBtn.Click += (_, _) => tcs.TrySetResult(true);
            cancelBtn.Click += (_, _) => tcs.TrySetResult(false);

            FrameworkElement? fe = this.Content as FrameworkElement;
            if (fe != null) fe.KeyDown += SettingsCapture_KeyDown;
            flyout.ShowAt(CustomizeHotkeyButton);
            var result = await tcs.Task;
            flyout.Hide();
            if (fe != null) fe.KeyDown -= SettingsCapture_KeyDown;
            _captureFlyoutStatusControl = null;
            _capturing = false;

            if (result == true)
            {
                if (string.IsNullOrWhiteSpace(_capturedKey))
                {
                    HotkeyErrorText.Text = "No key captured.";
                    HotkeyErrorText.Visibility = Visibility.Visible;
                    return;
                }

                var mods = string.IsNullOrWhiteSpace(_capturedModifiers) ? "CTRL+WIN" : _capturedModifiers; // enforce at least one
                if (Services.HotKeyManager.IsReserved(mods, _capturedKey))
                {
                    HotkeyErrorText.Text = "This hotkey is reserved by Windows.";
                    HotkeyErrorText.Visibility = Visibility.Visible;
                    return;
                }

                UserPreferences.HotKeyModifiers = mods;
                UserPreferences.HotKeyCode = _capturedKey;

                // Re-register (use reflection bridge to main window)
                TryReRegisterFromMainWindow();
                LoadHotkeyDisplay();
                // (Status label removed) Updated; use HotkeyErrorText for errors only.
            }
        }

        private void SettingsCapture_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_capturing) return;
            var mods = new List<string>();
            var core = Windows.System.VirtualKey.None;

            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
                mods.Add("CTRL");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu) & CoreVirtualKeyStates.Down) != 0)
                mods.Add("ALT");
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0)
                mods.Add("SHIFT");
            // Windows key detection: check left/right variants as modifiers
            if ((Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.LeftWindows) & CoreVirtualKeyStates.Down) != 0
                || (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.RightWindows) & CoreVirtualKeyStates.Down) != 0)
                mods.Add("WIN");

            core = e.Key;
            // Treat left/right variants of modifiers and Windows keys as modifiers as well
            if (core == Windows.System.VirtualKey.Control || core == Windows.System.VirtualKey.Menu || core == Windows.System.VirtualKey.Shift
                || core == Windows.System.VirtualKey.LeftControl || core == Windows.System.VirtualKey.RightControl
                || core == Windows.System.VirtualKey.LeftShift || core == Windows.System.VirtualKey.RightShift
                || core == Windows.System.VirtualKey.LeftWindows || core == Windows.System.VirtualKey.RightWindows)
            {
                _capturedModifiers = string.Join("+", mods);
                // Update live status for modifiers-only presses
                var live = $"{(_capturedModifiers.Length > 0 ? _capturedModifiers + "+" : string.Empty)}{_capturedKey}";
                if (_captureFlyoutStatusControl != null) _captureFlyoutStatusControl.Text = live;
                return;
            }

            string keyToken = MapKey(core);
            _capturedKey = keyToken;
            _capturedModifiers = string.Join("+", mods);
            var live2 = $"{(_capturedModifiers.Length > 0 ? _capturedModifiers + "+" : string.Empty)}{_capturedKey}";
            if (_captureFlyoutStatusControl != null) _captureFlyoutStatusControl.Text = live2;
            if (_captureFlyoutStatusControl != null) _captureFlyoutStatusControl.Text = live2;
            e.Handled = true;
        }

        private static string MapKey(Windows.System.VirtualKey key)
        {
            if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
                return ((char)key).ToString();
            if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
                return ((char)key).ToString();
            if (key >= Windows.System.VirtualKey.F1 && key <= Windows.System.VirtualKey.F24)
            {
                int fNum = (int)key - (int)Windows.System.VirtualKey.F1 + 1;
                return $"F{fNum}";
            }
            // OEM keys mapping (common punctuation keys)
            // Some VirtualKey enums don't expose OEM names across SDKs; check numeric VK codes instead
            int v = (int)key;
            switch (v)
            {
                case 0xBB: // VK_OEM_PLUS
                    return "+";
                case 0xBC: // VK_OEM_COMMA
                    return ",";
                case 0xBD: // VK_OEM_MINUS
                    return "-";
                case 0xBE: // VK_OEM_PERIOD
                    return ".";
            }
            return key.ToString().ToUpperInvariant();
        }

        private void ResetHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            UserPreferences.HotKeyModifiers = "CTRL+WIN";
            UserPreferences.HotKeyCode = "R";
            TryReRegisterFromMainWindow();
            LoadHotkeyDisplay();
            // (Status label removed) Reset to default acknowledged.
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Attempts to re-register the application's hotkey via the main window instance.
        /// Reflection is used to avoid a tight coupling between SettingsWindow and MainWindow
        /// (we only need to trigger re-registration, not own a direct reference).
        /// This keeps the settings UI independent of the main window lifecycle.
        /// </summary>
        private void TryReRegisterFromMainWindow()
        {
            try
            {
                var main = (Application.Current as App)?.Window as MainWindow;
                var field = typeof(MainWindow).GetField("_hotKeyManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (main != null && field?.GetValue(main) is HotKeyManager hk)
                {
                    hk.ReRegister();
                }
            }
            catch (Exception ex)
            {
                HotkeyErrorText.Text = $"Re-register failed: {ex.Message}";
                HotkeyErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
