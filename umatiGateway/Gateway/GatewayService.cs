using System.Collections.Concurrent;
using Opc.Ua;
using UmatiGateway.Mapping;
using UmatiGateway.Opc;
using UmatiGateway.Output;

namespace UmatiGateway.Gateway;

/// <summary>
/// Owns the OPC UA connection: connects to any OPC UA server, finds the machines in its Machines folder
/// (OPC 40001-1), starts one <see cref="MachineAdapter"/> per machine and keeps everything running across
/// connection losses. Controlled at runtime through the REST API / web page.
/// </summary>
public sealed class GatewayService : BackgroundService
{
    private readonly MessageHub _hub;
    private readonly ILogger<GatewayService> _log;
    private readonly ConcurrentDictionary<string, MachineAdapter> _machines = new();
    private readonly ConcurrentDictionary<string, string> _failures = new();
    private readonly SemaphoreSlim _wake = new(0);
    private volatile GatewayOptions _o;
    private volatile bool _want;
    private volatile bool _dropMachines;
    private CancellationTokenSource? _cycle;
    private TaskCompletionSource _manualRescan = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private MappingDefinition? _mapping;

    public string State { get; private set; } = "Disconnected";
    public string? Message { get; private set; }
    public string? ServerName { get; private set; }
    public DateTime? ConnectedSince { get; private set; }
    public bool MachinesFolderFound { get; private set; }

    public GatewayService(IConfiguration cfg, MessageHub hub, ILogger<GatewayService> log)
    {
        _hub = hub; _log = log;
        _o = GatewayOptions.Load(cfg);
        _want = _o.AutoConnect;
    }

    public GatewayOptions Options => _o;
    public IReadOnlyCollection<MachineAdapter> Machines => _machines.Values.OrderBy(m => m.Key, StringComparer.Ordinal).ToList();
    public IReadOnlyDictionary<string, string> Failures => _failures;
    public MappingDefinition? Mapping => _mapping;

    /// <summary>Finds a machine by machine id (encoded or not), ProductInstanceUri or browse name.</summary>
    public MachineAdapter? Find(string idOrName)
    {
        if (string.IsNullOrEmpty(idOrName)) return null;
        string decoded; try { decoded = Uri.UnescapeDataString(idOrName); } catch { decoded = idOrName; }
        string Norm(string s) { try { return Uri.UnescapeDataString(s); } catch { return s; } }
        return Machines.FirstOrDefault(m => m.Started && (Norm(m.MachineId) == decoded || m.MachineId == idOrName || m.ProductInstanceUri() == decoded))
            ?? Machines.FirstOrDefault(m => m.Key == decoded || m.DisplayName == decoded);
    }

    // ------------------------------------------------------------------ control (REST / web page)

    public void Connect() { _want = true; Cancel(false); _wake.Release(); }
    public void Disconnect() { _want = false; _dropMachines = true; Cancel(true); _wake.Release(); }
    public void Rescan() => _manualRescan.TrySetResult();

    public void ApplyOptions(GatewayOptions o, bool connect = true)
    {
        _o = o; _dropMachines = true; _want = connect;
        Cancel(true); _wake.Release();
    }

    private void Cancel(bool drop) { try { _cycle?.Cancel(); } catch (ObjectDisposedException) { } }

    // ------------------------------------------------------------------ main loop

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            if (_dropMachines) { await StopAllAsync(); _dropMachines = false; }
            if (!_want)
            {
                State = "Disconnected"; Message = null; ConnectedSince = null;
                try { await _wake.WaitAsync(stop); } catch (OperationCanceledException) { break; }
                continue;
            }

            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(stop);
            _cycle = cycle;
            var ct = cycle.Token;
            var o = _o;
            OpcUaConnection? conn = null;
            try
            {
                State = "Connecting"; Message = $"connecting to {o.OpcUaEndpoint}";
                _mapping = LoadMapping(o);
                conn = new OpcUaConnection(_log);
                await conn.ConnectAsync(o, ct);
                ServerName = conn.ServerName; ConnectedSince = DateTime.UtcNow;
                State = "Connected"; Message = null;
                while (!ct.IsCancellationRequested && !conn.Lost.IsCompleted)
                {
                    await DiscoverAsync(conn, o, ct);
                    var rescan = o.RescanInterval > 0 ? Task.Delay(o.RescanInterval * 1000, ct) : Task.Delay(Timeout.Infinite, ct);
                    await Task.WhenAny(conn.Lost, rescan, _manualRescan.Task);
                    if (_manualRescan.Task.IsCompleted) _manualRescan = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                if (!ct.IsCancellationRequested) { State = "Error"; Message = "OPC UA connection lost"; _log.LogWarning("OPC UA connection lost, reconnecting"); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                State = "Error"; Message = ex.Message;
                _log.LogWarning("OPC UA connection failed: {Msg}", ex.Message);
            }
            finally
            {
                ConnectedSince = null;
                foreach (var m in _machines.Values) m.OnSourceLost();
                if (conn is not null) await conn.DisposeAsync();
                _cycle = null;
            }
            if (stop.IsCancellationRequested) break;
            if (_want && !_dropMachines)
            {
                try { await _wake.WaitAsync(TimeSpan.FromSeconds(3), stop); } catch (OperationCanceledException) { break; }
            }
        }
        await StopAllAsync();
    }

    private MappingDefinition LoadMapping(GatewayOptions o)
    {
        var file = !string.IsNullOrEmpty(o.MappingFile) ? o.MappingFile : Path.Combine(AppContext.BaseDirectory, "mappings", "machinetool-default.json");
        var m = MappingDefinition.Load(file);
        _log.LogInformation("Mapping {File}: {N} elements, model {Ver}", file, m.Elements.Count, m.ModelVersion);
        return m;
    }

    private async Task DiscoverAsync(OpcUaConnection conn, GatewayOptions o, CancellationToken ct)
    {
        var folder = await conn.ResolvePathAsync(ObjectIds.ObjectsFolder, o.MachinesFolder, ct);
        if (folder is null)
        {
            MachinesFolderFound = false;
            Message = $"Machines folder '{o.MachinesFolder}' not found below the Objects folder";
            _log.LogWarning("{Msg}", Message);
            await RemoveMissingAsync(new HashSet<string>());
            return;
        }
        MachinesFolderFound = true; Message = null;
        conn.Invalidate(folder);
        var found = (await conn.BrowseAsync(folder, ct)).Where(c => c.NodeClass == NodeClass.Object).ToList();

        foreach (var child in found.OrderBy(c => c.BrowseName, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (_machines.TryGetValue(child.BrowseName, out var existing))
                {
                    if (existing.NeedsRebind)
                    {
                        _log.LogInformation("Machine {Key}: resynchronising after reconnect", child.BrowseName);
                        await existing.RebindAsync(conn, child, ct);
                    }
                    continue;
                }
                _log.LogInformation("Found machine {Name} ({Node})", child.BrowseName, child.NodeId);
                var type = await conn.TypeNameAsync(child.TypeDefinition, ct);
                var adapter = new MachineAdapter(child, type, conn, o, _mapping!, _hub, _log);
                try { await adapter.StartAsync(_machines.Values.Where(x => x.Started).Select(x => x.MachineId).ToHashSet(), ct); }
                catch { await adapter.DisposeAsync(); throw; }
                _machines[child.BrowseName] = adapter;
                _failures.TryRemove(child.BrowseName, out _);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _failures[child.BrowseName] = ex.Message;
                _log.LogError("Machine {Key}: {Msg}", child.BrowseName, ex.Message);
            }
        }
        await RemoveMissingAsync(found.Select(c => c.BrowseName).ToHashSet());
    }

    private async Task RemoveMissingAsync(HashSet<string> present)
    {
        foreach (var key in _machines.Keys.Where(k => !present.Contains(k)).ToList())
        {
            if (_machines.TryRemove(key, out var m))
            {
                _log.LogInformation("Machine {Key} disappeared from the Machines folder", key);
                await m.DisposeAsync();
            }
        }
        foreach (var f in _failures.Keys.Where(k => !present.Contains(k)).ToList()) _failures.TryRemove(f, out _);
    }

    private async Task StopAllAsync()
    {
        var all = _machines.Values.ToList();
        _machines.Clear(); _failures.Clear();
        foreach (var m in all) { try { await m.DisposeAsync(); } catch { /* ignore */ } }
        MachinesFolderFound = false;
    }
}
