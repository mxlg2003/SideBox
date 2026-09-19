using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace WinDock.Providers;

/// <summary>
/// Bluetooth connect/disconnect should refresh power immediately; the idle timer is 15s.
/// </summary>
internal sealed class BluetoothPresenceWatcher : IDisposable
{
    private const string Classic = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string LowEnergy = "{bb7bb05e-9e57-4d01-8970-1b89b7295685}";

    private readonly Action _changed;
    private readonly List<DeviceWatcher> _watchers = [];
    private int _disposed;

    public BluetoothPresenceWatcher(Action changed)
    {
        _changed = changed;
        TryWatch(
            $"(System.Devices.Aep.ProtocolId:=\"{Classic}\" OR System.Devices.Aep.ProtocolId:=\"{LowEnergy}\")",
            ["System.Devices.Aep.IsConnected"],
            DeviceInformationKind.AssociationEndpoint);
        TryWatch(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
        TryWatch(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        foreach (var watcher in _watchers)
        {
            watcher.Added -= OnAdded;
            watcher.Updated -= OnUpdated;
            watcher.Removed -= OnUpdated;
            try
            {
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                {
                    watcher.Stop();
                }
            }
            catch (Exception)
            {
                // Watcher already stopped.
            }
        }
    }

    private void TryWatch(string selector, IEnumerable<string>? properties = null, DeviceInformationKind? kind = null)
    {
        try
        {
            var watcher = kind is DeviceInformationKind value
                ? DeviceInformation.CreateWatcher(selector, properties, value)
                : properties is null
                    ? DeviceInformation.CreateWatcher(selector)
                    : DeviceInformation.CreateWatcher(selector, properties);
            watcher.Added += OnAdded;
            watcher.Updated += OnUpdated;
            watcher.Removed += OnUpdated;
            watcher.Start();
            _watchers.Add(watcher);
        }
        catch (Exception)
        {
            // Optional; the power timer still polls.
        }
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update) => Notify(sender);

    private void OnAdded(DeviceWatcher sender, DeviceInformation info) => Notify(sender);

    private void Notify(DeviceWatcher sender)
    {
        if (Volatile.Read(ref _disposed) == 1
            || sender.Status is not DeviceWatcherStatus.EnumerationCompleted)
        {
            return;
        }

        _changed();
    }
}
