using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace ResizeMe.Helpers
{
    internal class StatusManager
    {
        private readonly TextBlock _statusText;
        private readonly TimeSpan _defaultClearDelay = TimeSpan.FromSeconds(3);
        public StatusManager(TextBlock statusText)
        {
            _statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
        }

        public void SetStatus(string text, TimeSpan? clearAfter = null)
        {
            if (_statusText.DispatcherQueue == null)
            {
                _statusText.Text = text;
                return;
            }
            _statusText.DispatcherQueue.TryEnqueue(() => _statusText.Text = text);
            if (clearAfter.HasValue)
            {
                _ = ClearAfterAsync(clearAfter.Value);
            }
            else if (_defaultClearDelay > TimeSpan.Zero)
            {
                _ = ClearAfterAsync(_defaultClearDelay);
            }
        }

        private async Task ClearAfterAsync(TimeSpan delay)
        {
            await Task.Delay(delay);
            if (_statusText.DispatcherQueue == null) return;
            _statusText.DispatcherQueue.TryEnqueue(() => _statusText.Text = "Ready");
        }
    }
}
