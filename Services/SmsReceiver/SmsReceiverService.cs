using System.Net;
using System.Text.RegularExpressions;

namespace Yolcu360Otomasyon.Services;

public sealed partial class SmsReceiverService : IAsyncDisposable
{
    private static readonly Regex OtpRegex = new(@"\b\d{4,8}\b", RegexOptions.Compiled);

    private readonly object _sync = new();
    private readonly List<TaskCompletionSource<string>> _waiters = [];
    private readonly int _preferredPort;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private string? _latestCode;

    public event Action<string>? SmsReceived;

    public int Port { get; private set; }

    public SmsReceiverService(int port = 5000)
    {
        _preferredPort = port;
        Port = port;
    }

    public Task StartAsync()
    {
        if (_listener?.IsListening == true)
            return Task.CompletedTask;

        var listener = new HttpListener();
        try
        {
            listener.Prefixes.Add($"http://+:{Port}/");
            listener.Start();
        }
        catch
        {
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://*:{Port}/");
                listener.Start();
            }
            catch
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{Port}/");
                listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                listener.Start();
            }
        }

        _listener = listener;
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public string GetStatusMessage()
    {
        var addresses = GetLocalIpAddresses().ToArray();
        var primaryAddress = addresses.FirstOrDefault() ?? "127.0.0.1";
        var alternatives = addresses.Length > 1
            ? $" Alternatif IP: {string.Join(", ", addresses.Skip(1))}"
            : string.Empty;

        return $"SMS alıcısı hazır. MacroDroid URL: http://{primaryAddress}:{Port}/sms?message={{sms_message}}{alternatives}";
    }

    public static string GetPreferredLocalIpAddress() => GetLocalIpAddresses().FirstOrDefault() ?? "127.0.0.1";

    public static IEnumerable<string> GetLocalIpAddresses()
    {
        var addresses = new List<string>();

        foreach (var networkInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType is not (System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 or System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                continue;

            var properties = networkInterface.GetIPProperties();
            foreach (var address in properties.UnicastAddresses)
            {
                var ip = address.Address;
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    addresses.Add(ip.ToString());
            }
        }

        if (addresses.Count > 0)
            return addresses.Distinct();

        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                .Select(ip => ip.ToString())
                .Distinct();
        }
        catch
        {
            return [];
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            if (_listener?.IsListening == true)
                _listener.Stop();
            _listener?.Close();

            if (_listenerTask is not null)
                await _listenerTask;
        }
        catch
        {
            // no-op
        }
        finally
        {
            _cts?.Dispose();
        }
    }
}
