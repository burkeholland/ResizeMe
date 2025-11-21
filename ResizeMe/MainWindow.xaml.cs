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
using ResizeMe.ViewModels;
using WinRT.Interop;

namespace ResizeMe
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        public MainViewModel ViewModel => _viewModel;
        
        public string? Status => _viewModel.Status;
        public bool CenterOnResize
        {
            get => _viewModel.CenterOnResize;
            set => _viewModel.CenterOnResize = value;
        }
        
        private readonly PresetStorage _presets = new();
        private readonly PresetPanelRenderer _presetPanel = new();
        private readonly WindowDiscoveryService _windowDiscovery = new();
        private readonly WindowResizeService _windowResizer = new();
        private readonly INativeWindowService _nativeWindowService = new NativeWindowService();
        private readonly IWindowSubclassService _subclassService = new WindowSubclassService();
        private TrayIconManager? _trayManager;
        private HotKeyService? _hotKey;
        private IntPtr _windowHandle;
        private AppWindow? _appWindow;
        
        private int _focusIndex = -1;
        private DateTime _lastToggle = DateTime.MinValue;

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_LBUTTONUP = 0x0202;

        public MainWindow()
        {
            _viewModel = new MainViewModel(DispatcherQueue, _presets, _windowDiscovery, _windowResizer);
            InitializeComponent();
            if (Content is FrameworkElement root)
            {
                root.DataContext = _viewModel;
            }
            AttachRootHandlers();
            Activated += OnActivated;
            Closed += OnClosed;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
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

            EnsureWindowHandle();
            ConfigureShell();
            AttachSubclass();

            await _viewModel.InitializeAsync();
            
            // Sync initial UI state
            if (CenterOnResizeToggle != null)
            {
                CenterOnResizeToggle.IsOn = _viewModel.CenterOnResize;
            }
            
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
            _hotKey.TryRegister();
        }

        private void InitializeTray()
        {
            if (_windowHandle == IntPtr.Zero || _trayManager != null)
            {
                return;
            }

            _trayManager = new TrayIconManager(_windowHandle);
            if (_trayManager.Initialize())
            {
                _trayManager.ShowRequested += OnTrayShowRequested;
                _trayManager.SettingsRequested += OnTraySettingsRequested;
                _trayManager.ExitRequested += OnTrayExitRequested;
            }
        }

        private void OnTrayShowRequested(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(ToggleVisibility);
        }

        private void OnTraySettingsRequested(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(OpenSettingsWindow);
        }

        private void OnTrayExitRequested(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(PerformExit);
        }



        private void ShowFirstRunFlows()
        {
            _trayManager?.ShowFirstRunBalloon();
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
                await _viewModel.ReloadPresetsAsync();
                DispatcherQueue.TryEnqueue(RenderPresetButtons);
            };
            window.Activate();
        }

        private void ToggleVisibility()
        {
            if ((DateTime.UtcNow - _lastToggle).TotalMilliseconds < 150)
            {
                return;
            }

            _lastToggle = DateTime.UtcNow;
            if (_viewModel.IsVisible)
            {
                _viewModel.Hide();
            }
            else
            {
                _viewModel.Show();
            }
        }

        private async Task ShowWindowAsync()
        {
            try
            {
                EnsureWindowHandle();
                await _viewModel.ReloadPresetsAsync();
                _viewModel.RefreshWindowSnapshot();

                var anchor = _viewModel.SelectedWindow;
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
                _viewModel.Show();
                RenderPresetButtons();
            }
            catch (Exception ex)
            {
                AppLog.Error(nameof(MainWindow), "ShowWindowAsync failed", ex);
            }
        }

        private void HideWindow()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                _nativeWindowService.HideWindow(_windowHandle);
            }
            _viewModel.Hide();
        }

        private void RenderPresetButtons()
        {
            if (DynamicPresetsPanel == null)
            {
                return;
            }

            _presetPanel.Render(DynamicPresetsPanel, _viewModel.Presets, App.Current?.Resources, PresetButton_Click);
            if (_viewModel.Presets.Any())
            {
                if (PresetHint != null)
                {
                    PresetHint.Text = "Customize in Settings";
                }
                var index = _presetPanel.Focus(DynamicPresetsPanel, 0);
                _focusIndex = index;
            }
            else
            {
                if (PresetHint != null)
                {
                    PresetHint.Text = "No presets defined. Add in Settings.";
                }
                _focusIndex = -1;
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

            var next = _focusIndex + delta;
            if (next < 0)
            {
                next = 0;
            }
            if (next >= buttons.Count)
            {
                next = buttons.Count - 1;
            }

            _focusIndex = next;
            buttons[next].Focus(FocusState.Programmatic);
        }



        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsVisible))
            {
                if (_viewModel.IsVisible)
                {
                    _ = ShowWindowAsync();
                }
                else
                {
                    HideWindow();
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.Status))
            {
                if (StatusText != null && _viewModel.Status != null)
                {
                    StatusText.Text = _viewModel.Status;
                }
            }
            else if (e.PropertyName == nameof(MainViewModel.CenterOnResize))
            {
                if (CenterOnResizeToggle != null)
                {
                    CenterOnResizeToggle.IsOn = _viewModel.CenterOnResize;
                }
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not PresetSize preset)
            {
                return;
            }

            _ = _viewModel.ResizeSelectedWindowAsync(preset);
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_viewModel.IsVisible)
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
                    if (DynamicPresetsPanel == null || _focusIndex < 0)
                    {
                        return;
                    }

                    var buttons = DynamicPresetsPanel.Children.OfType<Button>().ToList();
                    if (_focusIndex < buttons.Count)
                    {
                        PresetButton_Click(buttons[_focusIndex], new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated && _viewModel.IsVisible)
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
            if (_trayManager != null)
            {
                _trayManager.ShowRequested -= OnTrayShowRequested;
                _trayManager.SettingsRequested -= OnTraySettingsRequested;
                _trayManager.ExitRequested -= OnTrayExitRequested;
                _trayManager.Dispose();
                _trayManager = null;
            }
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

            _viewModel.CenterOnResize = CenterOnResizeToggle.IsOn;
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
                    await _viewModel.ReloadPresetsAsync();
                    DispatcherQueue.TryEnqueue(RenderPresetButtons);
                };
                window.Activate();
            }
            catch (Exception ex)
            {
                AppLog.Error(nameof(MainWindow), "Failed to open SettingsWindow", ex);
                WindowsApi.MessageBoxW(IntPtr.Zero, $"Failed to open settings: {ex}", "ResizeMe Error", WindowsApi.MB_OK | WindowsApi.MB_ICONERROR);
            }
        }

        private async void SettingsPresetsChangedAsync(object? sender, EventArgs e)
        {
            await _viewModel.ReloadPresetsAsync();
            DispatcherQueue.TryEnqueue(RenderPresetButtons);
        }

        private void PerformExit()
        {
            try
            {
                if (_trayManager != null)
                {
                    _trayManager.ShowRequested -= OnTrayShowRequested;
                    _trayManager.SettingsRequested -= OnTraySettingsRequested;
                    _trayManager.ExitRequested -= OnTrayExitRequested;
                    _trayManager.Dispose();
                    _trayManager = null;
                }
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

                if (_owner._trayManager != null && msg == _owner._trayManager.CallbackMessage)
                {
                    int param = lParam.ToInt32();
                    if (param == WM_RBUTTONUP)
                    {
                        _owner._trayManager.ShowContextMenu();
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
