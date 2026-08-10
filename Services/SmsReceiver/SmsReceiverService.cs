using System.Net;
using System.Text.RegularExpressions;

namespace Yolcu360Otomasyon.Services;

public sealed partial class SmsReceiverService : IAsyncDisposable
{
    private static readonly Regex OtpRegex = new(@"\b\d{4,8}\b", RegexOptions.Compiled);
    private const int MaxPortAttempts = 20;

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

        var started = false;

        for (var offset = 0; offset < MaxPortAttempts && !started; offset++)
        {
            var candidatePort = _preferredPort + offset;

            // Attempt 1: Wildcard +
            try
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://+:{candidatePort}/");
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
                break;
            }
            catch
            {
            }

            // Attempt 2: Wildcard *
            try
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://*:{candidatePort}/");
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
                break;
            }
            catch
            {
            }

            // Attempt 3: Localhost & 127.0.0.1
            try
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://localhost:{candidatePort}/");
                candidateListener.Prefixes.Add($"http://127.0.0.1:{candidatePort}/");
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
                break;
            }
            catch
            {
            }
        }

        if (!started || _listener is null)
            throw new InvalidOperationException("SMS alıcısı için uygun port bulunamadı.");

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
