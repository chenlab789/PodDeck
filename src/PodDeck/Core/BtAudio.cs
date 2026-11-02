using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace PodDeck.Core;

/// <summary>
/// Connect / disconnect a paired Bluetooth audio device and steer default endpoints.
/// Connect and disconnect use the Bluetooth audio driver's KS "one-shot" properties,
/// which is the same thing the Windows sound flyout does.
/// </summary>
public static class BtAudio
{
    static readonly Guid KSPROPSETID_BtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    const uint KSPROPERTY_ONESHOT_RECONNECT = 0;
    const uint KSPROPERTY_ONESHOT_DISCONNECT = 1;
    const uint KSPROPERTY_TYPE_GET = 0x1;

    public static string NameMatch { get; set; } = "AirPods";

    static IEnumerable<MMDevice> Endpoints(DataFlow flow, DeviceState state = DeviceState.All)
    {
        using var en = new MMDeviceEnumerator();
        foreach (var d in en.EnumerateAudioEndPoints(flow, state))
        {
            string name;
            try { name = d.FriendlyName; } catch { continue; }
            if (name.Contains(NameMatch, StringComparison.OrdinalIgnoreCase)) yield return d;
        }
    }

    public static bool IsConnected() => Endpoints(DataFlow.Render, DeviceState.Active).Any();

    public static bool Connect() => SendOneShot(KSPROPERTY_ONESHOT_RECONNECT);
    public static bool Disconnect() => SendOneShot(KSPROPERTY_ONESHOT_DISCONNECT);

    public static Action<string>? Log { get; set; }

    static bool Ok(int hr, string step)
    {
        if (hr != 0) Log?.Invoke($"  {step} failed hr=0x{hr:X8}");
        return hr == 0;
    }

    static bool SendOneShot(uint id)
    {
        bool any = false;
        var endpoints = Endpoints(DataFlow.Render).ToList();
        var enumerator = CreateUniqueEnumerator();
        try
        {
        foreach (var ep in endpoints)
        {
            Log?.Invoke($"endpoint '{ep.FriendlyName}' state={ep.State} id={ep.ID}");
            try
            {
                if (!Ok(enumerator.GetDevice(ep.ID, out var dev), "GetDevice(endpoint)")) continue;
                var iidTopo = typeof(IDeviceTopology).GUID;
                if (!Ok(dev.Activate(ref iidTopo, 0x17, IntPtr.Zero, out var topoObj), "Activate(IDeviceTopology)")) continue;
                var topo = (IDeviceTopology)topoObj;
                if (!Ok(topo.GetConnector(0, out var conn), "GetConnector(0)")) continue;
                if (!Ok(conn.GetDeviceIdConnectedTo(out var filterId), "GetDeviceIdConnectedTo")) continue;
                Log?.Invoke($"  filter={filterId}");
                if (!Ok(enumerator.GetDevice(filterId, out var filterDev), "GetDevice(filter)")) continue;
                var iidKs = typeof(IKsControl).GUID;
                if (!Ok(filterDev.Activate(ref iidKs, 0x17, IntPtr.Zero, out var ksObj), "Activate(IKsControl)")) continue;
                var ks = (IKsControl)ksObj;

                var prop = new KSPROPERTY { Set = KSPROPSETID_BtAudio, Id = id, Flags = KSPROPERTY_TYPE_GET };
                int size = Marshal.SizeOf<KSPROPERTY>();
                IntPtr p = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(prop, p, false);
                    int hr = ks.KsProperty(p, (uint)size, IntPtr.Zero, 0, out _);
                    Log?.Invoke($"  KsProperty(id={id}) hr=0x{hr:X8}");
                    if (hr >= 0) any = true;
                }
                finally { Marshal.FreeHGlobal(p); }
            }
            catch (Exception ex) { Log?.Invoke($"  exception {ex.GetType().Name}: {ex.Message}"); }
        }
        }
        finally { Marshal.FinalReleaseComObject(enumerator); }
        return any;
    }

    /// <summary>
    /// The device enumerator is a process-wide singleton, so a normal RCW would be shared with
    /// NAudio's (typed as its own coclass) and break NAudio's casts. Use a private, uncached RCW.
    /// </summary>
    static IMMDeviceEnumeratorRaw CreateUniqueEnumerator()
    {
        var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        var iidUnknown = new Guid("00000000-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 0x17, ref iidUnknown, out var punk));
        try { return (IMMDeviceEnumeratorRaw)Marshal.GetUniqueObjectForIUnknown(punk); }
        finally { Marshal.Release(punk); }
    }

    [DllImport("ole32.dll")]
    static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint clsCtx, ref Guid iid, out IntPtr obj);

    // ---------- volume ----------

    public static float? GetVolume()
    {
        var ep = Endpoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(e => !IsHandsFree(e));
        return ep?.AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    public static void SetVolume(float v)
    {
        foreach (var ep in Endpoints(DataFlow.Render, DeviceState.Active))
            ep.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(v, 0f, 1f);
    }

    static bool IsHandsFree(MMDevice d) =>
        d.FriendlyName.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase) ||
        d.FriendlyName.StartsWith("Headset", StringComparison.OrdinalIgnoreCase);

    // ---------- keep stereo ----------

    /// <summary>
    /// Make AirPods stereo the default output for every role and move the default mic
    /// to a non-AirPods device, so apps stop opening the low-quality Hands-Free profile.
    /// Returns the name of the mic it picked, or null if none was found.
    /// </summary>
    public static string? KeepStereo()
    {
        var policy = (IPolicyConfig)new PolicyConfigClient();
        var stereo = Endpoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(e => !IsHandsFree(e));
        if (stereo is not null)
            for (int role = 0; role < 3; role++) policy.SetDefaultEndpoint(stereo.ID, role);

        using var en = new MMDeviceEnumerator();
        var mic = en.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(d => !d.FriendlyName.Contains(NameMatch, StringComparison.OrdinalIgnoreCase));
        if (mic is null) return null;
        for (int role = 0; role < 3; role++) policy.SetDefaultEndpoint(mic.ID, role);
        return mic.FriendlyName;
    }

    // ---------- COM interop ----------

    [StructLayout(LayoutKind.Sequential)]
    struct KSPROPERTY { public Guid Set; public uint Id; public uint Flags; }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumeratorRaw
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDeviceRaw device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDeviceRaw device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceRaw
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDeviceTopology
    {
        [PreserveSig] int GetConnectorCount(out uint count);
        [PreserveSig] int GetConnector(uint index, out IConnector connector);
    }

    [ComImport, Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IConnector
    {
        [PreserveSig] int GetType(out int type);
        [PreserveSig] int GetDataFlow(out int flow);
        [PreserveSig] int ConnectTo(IConnector other);
        [PreserveSig] int Disconnect();
        [PreserveSig] int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool connected);
        [PreserveSig] int GetConnectedTo(out IConnector other);
        [PreserveSig] int GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IKsControl
    {
        [PreserveSig] int KsProperty(IntPtr property, uint propertyLength, IntPtr data, uint dataLength, out uint returned);
    }

    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    class PolicyConfigClient { }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string id, out IntPtr format);
        [PreserveSig] int GetDeviceFormat(string id, bool def, out IntPtr format);
        [PreserveSig] int ResetDeviceFormat(string id);
        [PreserveSig] int SetDeviceFormat(string id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod(string id, bool def, out long defPeriod, out long minPeriod);
        [PreserveSig] int SetProcessingPeriod(string id, IntPtr period);
        [PreserveSig] int GetShareMode(string id, IntPtr mode);
        [PreserveSig] int SetShareMode(string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue(string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility(string id, bool visible);
    }
}
