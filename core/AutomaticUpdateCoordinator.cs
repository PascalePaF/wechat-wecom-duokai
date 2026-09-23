using System;
using System.Threading;
using System.Threading.Tasks;

namespace WechatDuokai.Core
{
    /// <summary>
    /// Coordinates recurring checks without owning a timer or making network requests itself.
    /// The caller can poll it after startup, when a setting changes, and while the app stays open.
    /// </summary>
    public sealed class AutomaticUpdateCoordinator
    {
        private readonly Func<bool> _isEnabled;
        private readonly Func<bool> _isDue;
        private readonly Func<Task> _check;
        private int _running;
        private bool _startupDelayElapsed;

        public AutomaticUpdateCoordinator(Func<bool> isEnabled, Func<bool> isDue,
            Func<Task> check)
        {
            _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            _isDue = isDue ?? throw new ArgumentNullException(nameof(isDue));
            _check = check ?? throw new ArgumentNullException(nameof(check));
        }

        public void CompleteStartupDelay()
        {
            _startupDelayElapsed = true;
        }

        public async Task<bool> CheckIfDueAsync()
        {
            if (!_startupDelayElapsed || !_isEnabled() || !_isDue() ||
                Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return false;
            }

            try
            {
                // The setting may have changed while another event was awaiting the UI thread.
                if (!_isEnabled() || !_isDue()) return false;
                await _check().ConfigureAwait(true);
                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }
}
