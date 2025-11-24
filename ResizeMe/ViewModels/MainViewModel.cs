using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.UI.Dispatching;
using ResizeMe.Features.Settings;
using ResizeMe.Features.WindowManagement;
using ResizeMe.Models;
using ResizeMe.Shared.Config;
using ResizeMe.Shared.Logging;

namespace ResizeMe.ViewModels
{
    /// <summary>
    /// Main ViewModel orchestrating window resize operations.
    /// 
    /// Key invariants:
    /// - SelectedWindow is captured BEFORE showing the UI (via CaptureTargetWindow)
    /// - SelectedWindow is cleared when hiding to ensure fresh capture on next show
    /// - IsVisible triggers UI show/hide via PropertyChanged event in MainWindow
    /// - Presets are loaded once at initialization and reloaded on-demand
    /// </summary>
    public sealed class MainViewModel : ViewModelBase
    {
        private const string LogTag = nameof(MainViewModel);
        
        // Delay after successful resize before auto-hiding UI (milliseconds)
        private const int PostResizeHideDelayMs = 500;

        private readonly PresetStorage _presets;
        private readonly WindowDiscoveryService _windowDiscovery;
        private readonly WindowResizeService _windowResizer;
        private readonly DispatcherQueue _dispatcherQueue;

        private bool _isVisible;
        private bool _centerOnResize;
        private WindowInfo? _selectedWindow;
        private string? _status;
        private ObservableCollection<PresetSize> _presetCollection = new();

        public ICommand ResizeCommand { get; }

        public ObservableCollection<PresetSize> Presets
        {
            get => _presetCollection;
            private set => SetProperty(ref _presetCollection, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            private set => SetProperty(ref _isVisible, value);
        }

        public bool CenterOnResize
        {
            get => _centerOnResize;
            set
            {
                if (SetProperty(ref _centerOnResize, value))
                {
                    UserSettingsStore.CenterOnResize = value;
                    AppLog.Info(LogTag, $"CenterOnResize changed to {value}");
                }
            }
        }

        public WindowInfo? SelectedWindow
        {
            get => _selectedWindow;
            private set => SetProperty(ref _selectedWindow, value);
        }

        public string? Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        internal MainViewModel(
            DispatcherQueue dispatcherQueue,
            PresetStorage presets,
            WindowDiscoveryService windowDiscovery,
            WindowResizeService windowResizer)
        {
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            _presets = presets ?? throw new ArgumentNullException(nameof(presets));
            _windowDiscovery = windowDiscovery ?? throw new ArgumentNullException(nameof(windowDiscovery));
            _windowResizer = windowResizer ?? throw new ArgumentNullException(nameof(windowResizer));
            
            _centerOnResize = UserSettingsStore.CenterOnResize;
            ResizeCommand = new RelayCommand<PresetSize>(async (preset) => await ResizeSelectedWindowAsync(preset));
            
            AppLog.Info(LogTag, "ViewModel initialized");
        }

        public async Task InitializeAsync()
        {
            AppLog.Info(LogTag, "InitializeAsync starting");
            
            try
            {
                await _presets.LoadAsync();
                RefreshPresetCollection();
                RefreshWindowSnapshot();
                
                AppLog.Info(LogTag, $"InitializeAsync completed, loaded {Presets.Count} presets");
            }
            catch (Exception ex)
            {
                AppLog.Error(LogTag, "InitializeAsync failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Refreshes the selected window only if not already set.
        /// This preserves the window captured before ResizeMe UI was shown.
        /// </summary>
        public void RefreshWindowSnapshot()
        {
            if (SelectedWindow != null)
            {
                AppLog.Info(LogTag, $"RefreshWindowSnapshot skipped, already have: {SelectedWindow.Title}");
                return;
            }
            
            CaptureTargetWindow();
        }

        /// <summary>
        /// Captures the current foreground window as the resize target.
        /// Called BEFORE the ResizeMe UI is shown to ensure we capture
        /// the window that was active when the hotkey was pressed.
        /// </summary>
        public void CaptureTargetWindow()
        {
            var activeWindow = _windowDiscovery.GetActiveWindow();
            
            if (activeWindow != null)
            {
                SelectedWindow = activeWindow;
                AppLog.Info(LogTag, $"Captured active window: {activeWindow.Title}");
                return;
            }

            // Fall back to first available window if no active window found
            var windows = _windowDiscovery.GetWindows().ToList();
            var firstWindow = windows.FirstOrDefault();
            
            if (firstWindow != null)
            {
                SelectedWindow = firstWindow;
                AppLog.Info(LogTag, $"Captured fallback window: {firstWindow.Title}");
            }
            else
            {
                AppLog.Warn(LogTag, "CaptureTargetWindow found no suitable windows");
            }
        }

        public async Task ReloadPresetsAsync()
        {
            AppLog.Info(LogTag, "ReloadPresetsAsync starting");
            
            await _presets.LoadAsync(forceReload: true);
            RefreshPresetCollection();
            
            AppLog.Info(LogTag, $"ReloadPresetsAsync completed, {Presets.Count} presets loaded");
        }

        /// <summary>
        /// Resizes the selected window to the given preset dimensions.
        /// On success: optionally centers the window, activates it, then hides the UI.
        /// On failure: displays error status and logs warning.
        /// </summary>
        public async Task ResizeSelectedWindowAsync(PresetSize? preset)
        {
            // Validate inputs
            if (preset == null)
            {
                AppLog.Warn(LogTag, "ResizeSelectedWindowAsync called with null preset");
                return;
            }

            var target = SelectedWindow;
            if (target == null)
            {
                Status = "No window selected";
                AppLog.Warn(LogTag, "ResizeSelectedWindowAsync: no target window");
                return;
            }

            AppLog.Info(LogTag, $"Resizing '{target.Title}' to {preset.Name} ({preset.Width}x{preset.Height})");
            Status = $"Resizing {preset.Name}...";

            // Perform the resize operation
            var result = _windowResizer.Resize(target, preset.Width, preset.Height);

            if (!result.Success)
            {
                var errorMessage = result.ErrorMessage ?? "Resize failed";
                Status = errorMessage;
                AppLog.Warn(LogTag, $"Resize failed: {errorMessage}, ErrorCode={result.ErrorCode}");
                return;
            }

            // Resize succeeded - apply post-resize actions
            AppLog.Info(LogTag, $"Resize succeeded for '{target.Title}'");

            if (CenterOnResize)
            {
                WindowPositionService.CenterExternalWindow(target);
                AppLog.Info(LogTag, "Window centered on monitor");
            }

            _windowResizer.Activate(target);
            Status = $"Done {preset.Name}";

            // Delay before hiding to let user see the result
            await Task.Delay(PostResizeHideDelayMs);
            
            _dispatcherQueue.TryEnqueue(() => IsVisible = false);
        }

        /// <summary>
        /// Shows the ViewModel UI state.
        /// Note: CaptureTargetWindow() must be called BEFORE Show() to ensure
        /// the correct window is targeted.
        /// </summary>
        public void Show()
        {
            AppLog.Info(LogTag, "Show called");
            IsVisible = true;
        }

        /// <summary>
        /// Hides the ViewModel UI state and clears the selected window.
        /// The window is cleared so that the next Show() captures a fresh target.
        /// </summary>
        public void Hide()
        {
            AppLog.Info(LogTag, "Hide called");
            IsVisible = false;
            SelectedWindow = null;
        }

        private void RefreshPresetCollection()
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                Presets = new ObservableCollection<PresetSize>(_presets.Presets);
            });
        }
    }
}
