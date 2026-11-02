using Windows.Devices.Bluetooth.Advertisement;

namespace PodDeck.Core;

/// <summary>
/// Listens to BLE advertisements and keeps the latest AirPods state.
/// Raises events on the thread pool; UI code must marshal to the dispatcher.
/// </summary>
public sealed class PodWatcher : IDisposable
{
    const ushort AppleCompanyId = 0x004C;
    // AirPods stop advertising a few seconds after the lid closes.
    static readonly TimeSpan GoneAfter = TimeSpan.FromSeconds(15);

    readonly BluetoothLEAdvertisementWatcher _watcher = new() { ScanningMode = BluetoothLEScanningMode.Active };
    readonly object _lock = new();
    readonly System.Threading.Timer _goneTimer;

    PodState? _current;
    bool _present;
    bool _lidOpen;
    int _inEar = -1;

    public PodState? Current { get { lock (_lock) return _current; } }
    public bool Present { get { lock (_lock) return _present; } }

    /// <summary>Fires for every decoded advertisement (for the live monitor).</summary>
    public event Action<PodState>? Advert;
    /// <summary>Fires on every decoded change of battery / ear / lid state.</summary>
    public event Action<PodState>? StateChanged;
    /// <summary>Case lid went from closed (or silent) to open.</summary>
    public event Action<PodState>? LidOpened;
    /// <summary>Case lid closed.</summary>
    public event Action? LidClosed;
    /// <summary>(before, after) number of pods in ears.</summary>
    public event Action<int, int>? EarCountChanged;
    /// <summary>No advertisement for a while: AirPods are in a closed case or out of range.</summary>
    public event Action? Gone;

    public PodWatcher()
    {
        _watcher.Received += OnReceived;
        _goneTimer = new System.Threading.Timer(_ => CheckGone(), null, 3000, 3000);
    }

    public void Start() => _watcher.Start();

    void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        // Other people's AirPods are much farther away.
        if (args.RawSignalStrengthInDBm < -80) return;
        foreach (var m in args.Advertisement.ManufacturerData)
        {
            if (m.CompanyId != AppleCompanyId) continue;
            var bytes = new byte[m.Data.Length];
            using (var r = Windows.Storage.Streams.DataReader.FromBuffer(m.Data)) r.ReadBytes(bytes);
            var s = PodState.TryParse(bytes, args.RawSignalStrengthInDBm);
            if (s is not null) Handle(s);
        }
    }

    void Handle(PodState s)
    {
        bool fireLidOpen = false, fireLidClosed = false, fireChange;
        int earBefore = -1, earAfter = -1;
        lock (_lock)
        {
            if (s.LidOpen && !_lidOpen) fireLidOpen = true;
            if (!s.LidOpen && _lidOpen && s.InCase) fireLidClosed = true;
            _lidOpen = s.LidOpen;
            _present = true;

            if (_inEar >= 0 && s.InEarCount != _inEar) { earBefore = _inEar; earAfter = s.InEarCount; }
            _inEar = s.InEarCount;

            fireChange = _current is null || !SameVisible(_current, s);
            if (_current is null || _current.Status != s.Status || _current.LidByte != s.LidByte)
                Log.Write($"adv status=0x{s.Status:X2} lid=0x{s.LidByte:X2} inEar={s.InEarCount} inCase={s.InCase} L={s.Left} R={s.Right} C={s.Case} rssi={s.Rssi}");
            _current = s;
        }
        Advert?.Invoke(s);
        if (fireChange) StateChanged?.Invoke(s);
        if (fireLidOpen) LidOpened?.Invoke(s);
        if (fireLidClosed) LidClosed?.Invoke();
        if (earBefore >= 0) EarCountChanged?.Invoke(earBefore, earAfter);
    }

    static bool SameVisible(PodState a, PodState b) =>
        a.Left == b.Left && a.Right == b.Right && a.Case == b.Case &&
        a.LeftCharging == b.LeftCharging && a.RightCharging == b.RightCharging && a.CaseCharging == b.CaseCharging &&
        a.InEarCount == b.InEarCount && a.LidOpen == b.LidOpen;

    void CheckGone()
    {
        bool fire = false;
        lock (_lock)
        {
            if (_present && _current is not null && DateTime.Now - _current.SeenAt > GoneAfter)
            {
                _present = false;
                _lidOpen = false;
                _inEar = -1;
                fire = true;
            }
        }
        if (fire) Gone?.Invoke();
    }

    public void Dispose()
    {
        _goneTimer.Dispose();
        _watcher.Stop();
    }
}
