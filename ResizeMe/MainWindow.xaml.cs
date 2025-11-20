using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ResizeMe.Features.MainLayout;
using ResizeMe.Features.Settings;
using ResizeMe.Features.SystemIntegration;
using ResizeMe.Features.WindowManagement;
using ResizeMe.Models;
using ResizeMe.Native;
using ResizeMe.Shared.Config;
using ResizeMe.Shared.Logging;
using WinRT.Interop;

namespace ResizeMe
{
    public sealed partial class MainWindow : Window
    {
        private readonly PresetStorage _presets = new();
        private readonly PresetPanelRenderer _presetPanel = new();
        private readonly MainWindowState _state = new();
        private readonly WindowDiscoveryService _windowDiscovery = new();
        private readonly WindowResizeService _windowResizer = new();
        private readonly INativeWindowService _nativeWindowService = new NativeWindowService();
        private readonly IWindowSubclassService _subclassService = new WindowSubclassService();
        private StatusBanner? _status;
        private TrayIconService? _trayIcon;
        private HotKeyService? _hotKey;
        private IntPtr _windowHandle;
        private AppWindow? _appWindow;
        
        private bool _initialized;
        private WindowInfo? _selectedWindow;
        private List<WindowInfo> _windows = new();
        private DateTime _lastToggle = DateTime.MinValue;

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONUP = 0x0202;

        public MainWindow()
        {
            InitializeComponent();
            AttachRootHandlers();
            Activated += OnActivated;
            Closed += OnClosed;
        }

        private void AttachRootHandlers()
        {
            if (Content is FrameworkElement root)
            {
                root.KeyDown -= OnKeyDown;
                root.KeyDown += OnKeyDown;
                root.Loaded -= OnRootLoaded;
                root.Loaded += OnRootLoaded;
            }
        }

        private async void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement root)
            {
                root.Loaded -= OnRootLoaded;
            }
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (StatusText != null)
            {
                _status = new StatusBanner(StatusText);
            }

            EnsureWindowHandle();
            ConfigureShell();
            AttachSubclass();
            SyncCenterToggle();

            await _presets.LoadAsync();
            RenderPresetButtons();

            InitializeIntegration();
            HideWindow();
            ShowFirstRunFlows();
        }

        private void EnsureWindowHandle()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                return;
            }

            _windowHandle = _nativeWindowService.EnsureWindowHandle(this);
            _appWindow = _nativeWindowService.GetAppWindow(this);
        }

        private void ConfigureShell()
        {
            try
            {
                Title = "ResizeMe";
                if (_appWindow == null)
                {
                    return;
                }

                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
                _nativeWindowService.ConfigureShell(_appWindow, "ResizeMe", iconPath, 320, 540);
            }
            catch (Exception ex)
            {
                AppLog.Warn(nameof(MainWindow), $"ConfigureShell failed: {ex.Message}");
            }
        }

        private async void AttachSubclass()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }
            var handler = new WindowMessageHandler(this);
            await _subclassService.AttachAsync(_windowHandle, handler);
        }

        private void InitializeIntegration()
        {
            InitializeHotKey();
            InitializeTray();
        }

        private void InitializeHotKey()
        {
            if (_windowHandle == IntPtr.Zero || _hotKey != null)
            {
                return;
            }

            _hotKey = new HotKeyService(_windowHandle);
            _hotKey.HotKeyTriggered += (_, _) => DispatcherQueue.TryEnqueue(ToggleVisibility);
            if (_hotKey.TryRegister())
            {
                _status?.Show($"Ready ({UserSettingsStore.HotKeyModifiers}+{UserSettingsStore.HotKeyCode})", TimeSpan.Zero);
            }
            else
            {
                _status?.Show("Hotkey unavailable", TimeSpan.FromSeconds(4));
            }
        }

        private void InitializeTray()
        {
            if (_windowHandle == IntPtr.Zero || _trayIcon != null)
            {
                return;
            }

            _trayIcon = new TrayIconService(_windowHandle, "ResizeMe");
            if (_trayIcon.Initialize())
            {
                _trayIcon.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(ToggleVisibility);
                _trayIcon.SettingsRequested += (_, _) => DispatcherQueue.TryEnqueue(OpenSettingsWindow);
                _trayIcon.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(PerformExit);
            }
        }

        private void SyncCenterToggle()
        {
            var center = UserSettingsStore.CenterOnResize;
            _state.SetCenterOnResize(center);
            if (CenterOnResizeToggle != null)
            {
                CenterOnResizeToggle.IsOn = center;
            }
        }

        private void ShowFirstRunFlows()
        {
            ShowTrayNotificationOnce();
            CheckFirstRunSettings();
        }

        private void ShowTrayNotificationOnce()
        {
            if (UserSettingsStore.FirstMinimizeNotificationShown)
            {
                return;
            }

            try
            {
                var message = $"ResizeMe is running in the tray. Press {UserSettingsStore.HotKeyModifiers}+{UserSettingsStore.HotKeyCode} to open.";
                WindowsApi.MessageBoxW(IntPtr.Zero, message, "ResizeMe", WindowsApi.MB_OK | WindowsApi.MB_TOPMOST);
                UserSettingsStore.FirstMinimizeNotificationShown = true;
                HideWindow();
            }
            catch (Exception ex)
            {
                AppLog.Warn(nameof(MainWindow), $"Tray notification failed: {ex.Message}");
            }
        }

        private void CheckFirstRunSettings()
        {
            if (UserSettingsStore.FirstRunCompleted)
            {
                return;
            }

            var window = new SettingsWindow();
            window.PresetsChanged += SettingsPresetsChangedAsync;
            window.Closed += async (_, _) =>
            {
                window.PresetsChanged -= SettingsPresetsChangedAsync;
                UserSettingsStore.FirstRunCompleted = true;
                await _presets.LoadAsync(true);
                DispatcherQueue.TryEnqueue(RenderPresetButtons);
            };
            window.Activate();
            _status?.Show("First-run setup", TimeSpan.FromSeconds(3));
        }

        private void ToggleVisibility()
        {
            if ((DateTime.UtcNow - _lastToggle).TotalMilliseconds < 150)
            {
                return;
            }

            _lastToggle = DateTime.UtcNow;
            if (_state.IsVisible)
            {
                HideWindow();
            }
            else
            {
                _ = ShowWindowAsync();
            }
        }

        private async Task ShowWindowAsync()
        {
            try
            {
                EnsureWindowHandle();
                await ReloadPresetsAsync();
                RefreshWindowSnapshot();

                var anchor = _windowDiscovery.GetActiveWindow() ?? _windows.FirstOrDefault();
                if (anchor != null)
                {
                    WindowPositionService.CenterOnWindow(this, anchor);
                }
                else
                {
                    WindowPositionService.CenterOnScreen(this);
                }

                if (_windowHandle != IntPtr.Zero)
                {
                    _nativeWindowService.RestoreWindow(_windowHandle);
                    _nativeWindowService.FocusWindow(_windowHandle);
                }

                Activate();
                _state.Show();
                _status?.Show("Ready", TimeSpan.Zero);
                RenderPresetButtons();
            }
            catch (Exception ex)
            {
                AppLog.Error(nameof(MainWindow), "ShowWindowAsync failed", ex);
                _status?.Show("Show failed", TimeSpan.FromSeconds(3));
            }
        }

        private void HideWindow()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                _nativeWindowService.HideWindow(_windowHandle);
            }
            _state.Hide();
            _status?.Show("Hidden", TimeSpan.Zero);
        }

        private async Task ReloadPresetsAsync()
        {
            if (!_presets.IsLoaded)
            {
                await _presets.LoadAsync();
            }
        }

        private void RefreshWindowSnapshot()
        {
            _windows = _windowDiscovery.GetWindows().ToList();
            _selectedWindow = _windowDiscovery.GetActiveWindow() ?? _windows.FirstOrDefault();
        }

        private void RenderPresetButtons()
        {
            if (DynamicPresetsPanel == null)
            {
                return;
            }

            _presetPanel.Render(DynamicPresetsPanel, _presets.Presets, App.Current?.Resources, PresetButton_Click);
            if (_presets.Presets.Any())
            {
                if (PresetHint != null)
                {
                    PresetHint.Text = "Customize in Settings";
                }
                var index = _presetPanel.Focus(DynamicPresetsPanel, 0);
                _state.SetFocusIndex(index);
            }
            else
            {
                if (PresetHint != null)
                {
                    PresetHint.Text = "No presets defined. Add in Settings.";
                }
                _state.SetFocusIndex(-1);
            }
        }

        private void MoveFocus(int delta)
        {
            if (DynamicPresetsPanel == null)
            {
                return;
            }

            var buttons = DynamicPresetsPanel.Children.OfType<Button>().ToList();
            if (buttons.Count == 0)
            {
                return;
            }

            var next = _state.FocusIndex + delta;
            if (next < 0)
            {
                next = 0;
            }
            if (next >= buttons.Count)
            {
                next = buttons.Count - 1;
            }

            _state.SetFocusIndex(next);
            buttons[next].Focus(FocusState.Programmatic);
        }

        private async Task ResizeSelectedWindowAsync(int width, int height, string sizeTag)
        {
            var target = _selectedWindow ?? _windows.FirstOrDefault();
            if (target == null)
            {
                _status?.Show("No window", TimeSpan.FromSeconds(2));
                return;
            }

            _status?.Show($"Resizing {sizeTag}...", TimeSpan.FromSeconds(5));
            var result = _windowResizer.Resize(target, width, height);
            if (result.Success)
            {
                if (_state.CenterOnResize)
                {
                    WindowPositionService.CenterExternalWindow(target);
                }

                _windowResizer.Activate(target);
                _presetPanel.ApplyActiveStyle(DynamicPresetsPanel, sizeTag, App.Current?.Resources);
                _state.SetActivePreset(sizeTag);
                _status?.Show($"Done {sizeTag}", TimeSpan.FromSeconds(2));
                await Task.Delay(500);
                HideWindow();
            }
            else
            {
                _status?.Show(result.ErrorMessage ?? "Resize failed", TimeSpan.FromSeconds(3));
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag)
            {
                return;
            }

            var parts = tag.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
            {
                _status?.Show("Invalid preset", TimeSpan.FromSeconds(3));
                return;
            }

            _ = ResizeSelectedWindowAsync(width, height, tag);
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_state.IsVisible)
            {
                return;
            }

            switch (e.Key)
            {
                case Windows.System.VirtualKey.Escape:
                    HideWindow();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Right:
                case Windows.System.VirtualKey.Down:
                    MoveFocus(1);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Left:
                case Windows.System.VirtualKey.Up:
                    MoveFocus(-1);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Enter:
                    if (DynamicPresetsPanel == null || _state.FocusIndex < 0)
                    {
                        return;
                    }

                    var buttons = DynamicPresetsPanel.Children.OfType<Button>().ToList();
                    if (_state.FocusIndex < buttons.Count)
                    {
                        PresetButton_Click(buttons[_state.FocusIndex], new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && _state.IsVisible)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(150);
                    DispatcherQueue.TryEnqueue(HideWindow);
                });
                return;
            }

            InitializeHotKey();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _trayIcon?.Dispose();
            _hotKey?.Dispose();
            _subclassService.Detach();
            _subclassService.Dispose();
        }

        private void CenterOnResizeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (CenterOnResizeToggle == null)
            {
                return;
            }

            var value = CenterOnResizeToggle.IsOn;
            _state.SetCenterOnResize(value);
            UserSettingsStore.CenterOnResize = value;
            _status?.Show(value ? "Center on resize on" : "Center on resize off", TimeSpan.FromSeconds(2));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }

        private void OpenSettingsWindow()
        {
            try
            {
                var window = new SettingsWindow();
                window.PresetsChanged += SettingsPresetsChangedAsync;
                window.Closed += async (_, _) =>
                {
                    window.PresetsChanged -= SettingsPresetsChangedAsync;
                    await _presets.LoadAsync(true);
                    DispatcherQueue.TryEnqueue(RenderPresetButtons);
                };
                window.Activate();
                _status?.Show("Settings opened", TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                AppLog.Error(nameof(MainWindow), "Failed to open SettingsWindow", ex);
                _status?.Show($"Error: {ex.Message}", TimeSpan.FromSeconds(5));
                WindowsApi.MessageBoxW(IntPtr.Zero, $"Failed to open settings: {ex}", "ResizeMe Error", WindowsApi.MB_OK | WindowsApi.MB_ICONERROR);
            }
        }

        private async void SettingsPresetsChangedAsync(object? sender, EventArgs e)
        {
            await _presets.LoadAsync(true);
            DispatcherQueue.TryEnqueue(RenderPresetButtons);
        }

        private void PerformExit()
        {
            try
            {
                _trayIcon?.Dispose();
                _hotKey?.Dispose();
            }
            finally
            {
                DispatcherQueue.TryEnqueue(Close);
            }
        }

        public void RefreshHotKeyRegistration()
        {
            if (_hotKey == null)
            {
                InitializeHotKey();
            }
            else
            {
                _hotKey.ReRegister();
                _status?.Show($"Hotkey set to {UserSettingsStore.HotKeyModifiers}+{UserSettingsStore.HotKeyCode}", TimeSpan.FromSeconds(3));
            }
        }

        private sealed class WindowMessageHandler : IWindowMessageHandler
        {
            private readonly MainWindow _owner;


            public WindowMessageHandler(MainWindow owner)
            {
                _owner = owner;
            }

            public IntPtr HandleWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
            {
                if (msg == WM_SYSCOMMAND && wParam.ToInt32() == SC_CLOSE)
                {
                    _owner.HideWindow();
                    return new IntPtr(1);
                }

                if (_owner._trayIcon != null && msg == _owner._trayIcon.CallbackMessage)
                {
                    int param = lParam.ToInt32();
                    if (param == WM_RBUTTONUP)
                    {
                        _owner._trayIcon.ShowContextMenu();
                        return new IntPtr(1);
                    }
                    if (param == WM_LBUTTONUP)
                    {
                        _owner.ToggleVisibility();
                        return new IntPtr(1);
                    }
                }

                if (_owner._hotKey != null && _owner._hotKey.HandleMessage(msg, wParam, lParam))
                {
                    return new IntPtr(1);
                }

                return IntPtr.Zero;
            }
        }

        
    }
}
