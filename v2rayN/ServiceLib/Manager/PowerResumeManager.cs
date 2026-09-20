using System.Net.NetworkInformation;

namespace ServiceLib.Manager;

/// <summary>
/// Monitors system sleep/wake and network interface changes to automatically
/// recover VPN tunnels and proxy configurations on macOS and other platforms.
/// </summary>
public class PowerResumeManager
{
    private static readonly Lazy<PowerResumeManager> _instance = new(() => new());
    public static PowerResumeManager Instance => _instance.Value;

    private readonly object _lock = new();
    private System.Threading.Timer? _heartbeatTimer;
    private CancellationTokenSource? _debounceCts;
    private DateTime _lastTickTime;
    private bool _isInitialized;
    private const string _tag = "PowerResumeManager";

    public void Init()
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            _lastTickTime = DateTime.UtcNow;

            // Heartbeat timer runs every 2 seconds.
            // Wall-clock time advances during sleep across all OSes.
            _heartbeatTimer = new System.Threading.Timer(OnHeartbeat, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            try
            {
                NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
    }

    private void OnHeartbeat(object? state)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = (now - _lastTickTime).TotalSeconds;
        _lastTickTime = now;

        // If elapsed time between 2-second ticks is > 6 seconds, system was in sleep mode
        if (elapsedSeconds > 6.0)
        {
            Logging.SaveLog($"{_tag} - System wake detected via time gap ({elapsedSeconds:F1}s). Scheduling reload...");
            ScheduleDebouncedResume();
        }
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        Logging.SaveLog($"{_tag} - NetworkAddressChanged event received. Scheduling reload check...");
        ScheduleDebouncedResume();
    }

    private void ScheduleDebouncedResume()
    {
        lock (_lock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait 2.5 seconds for network interface and DHCP routing table to stabilize
                    await Task.Delay(2500, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (CoreManager.Instance.HasRunningCore)
                    {
                        Logging.SaveLog($"{_tag} - Active core detected. Publishing SystemResumeRequested...");
                        AppEvents.SystemResumeRequested.Publish();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounced
                }
                catch (Exception ex)
                {
                    Logging.SaveLog(_tag, ex);
                }
            }, token);
        }
    }
}
