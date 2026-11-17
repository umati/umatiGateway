using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.ComplexTypes;
using Opc.Ua.Configuration;

namespace UmatiGateway.Opc;

/// <summary>A browsed child node.</summary>
public sealed record OpcChild(NodeId NodeId, string BrowseName, NodeClass NodeClass, string DisplayName, NodeId? TypeDefinition);

/// <summary>
/// OPC UA client connection to one server: connect, browse by name, read, subscribe.
/// Connection loss is signalled through <see cref="Lost"/>; the owner reconnects by creating a new instance.
/// </summary>
public sealed class OpcUaConnection : IAsyncDisposable
{
    private readonly ILogger _log;
    private readonly Dictionary<string, List<OpcChild>> _browseCache = new();
    private readonly SemaphoreSlim _browseLock = new(1, 1);
    private Session? _session;
    private readonly TaskCompletionSource _lost = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public OpcUaConnection(ILogger log) => _log = log;

    public Session Session => _session ?? throw new InvalidOperationException("not connected");
    public bool Connected => _session is { Connected: true };
    /// <summary>completes when the keep-alive detects that the session is gone</summary>
    public Task Lost => _lost.Task;
    public string? ServerName { get; private set; }

    public async Task ConnectAsync(GatewayOptions o, CancellationToken ct)
    {
        var pki = Path.GetFullPath(o.PkiPath);
        var config = new ApplicationConfiguration
        {
            ApplicationName = "umatiGateway",
            ApplicationType = ApplicationType.Client,
            ApplicationUri = $"urn:{Utils.GetHostName()}:umatiGateway",
            ProductUri = "urn:org:umati:umatigateway",
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pki, "own"),
                    SubjectName = "CN=umatiGateway, O=umati, DC=localhost",
                },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = CertificateStoreType.Directory, StorePath = Path.Combine(pki, "trusted") },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = CertificateStoreType.Directory, StorePath = Path.Combine(pki, "issuer") },
                RejectedCertificateStore = new CertificateTrustList { StoreType = CertificateStoreType.Directory, StorePath = Path.Combine(pki, "rejected") },
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = true,
            },
            TransportQuotas = new TransportQuotas { OperationTimeout = 20000, MaxStringLength = 1048576, MaxByteStringLength = 1048576, MaxArrayLength = 65535, MaxMessageSize = 4194304 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
            DisableHiResClock = true,
        };
        await config.ValidateAsync(ApplicationType.Client, ct);
        config.CertificateValidator.CertificateValidation += (_, e) => e.Accept = true; // demo gateway: accept any server certificate

        var app = new ApplicationInstance { ApplicationName = config.ApplicationName, ApplicationType = ApplicationType.Client, ApplicationConfiguration = config };
        try { await app.CheckApplicationInstanceCertificatesAsync(false, ct: ct); }
        catch (Exception ex) { _log.LogWarning("Could not create/check client certificate ({Msg}); continuing (fine for SecurityMode None)", ex.Message); }

        _log.LogInformation("Connecting to OPC UA server {Endpoint}", o.OpcUaEndpoint);
        var ep = CoreClientUtils.SelectEndpoint(config, o.OpcUaEndpoint, o.OpcUaUseSecurity);
        var configured = new ConfiguredEndpoint(null, ep, EndpointConfiguration.Create(config));
        var identity = string.IsNullOrEmpty(o.OpcUaUser) ? new UserIdentity() : new UserIdentity(o.OpcUaUser, o.OpcUaPassword ?? "");

        var session = await Session.Create(config, configured, false, false, "umatiGateway", 60000, identity, null, ct);
        session.KeepAlive += (s, e) =>
        {
            if (ServiceResult.IsBad(e.Status))
            {
                _log.LogWarning("OPC UA keep-alive failed: {Status}", e.Status);
                _lost.TrySetResult();
            }
        };
        session.SessionClosing += (_, _) => _lost.TrySetResult();
        _session = session;
        ServerName = ep.Server?.ApplicationName?.Text is { Length: > 0 } sn ? sn : ep.Server?.ApplicationUri;
        _log.LogInformation("OPC UA session established ({Server}, security {Mode})", ServerName, ep.SecurityMode);

        try
        {
            // lets the client decode custom structures (e.g. AcActivePowerPe)
            var cts = new ComplexTypeSystem(session);
            await cts.LoadAsync(ct: ct);
        }
        catch (Exception ex) { _log.LogWarning("Could not load custom data types: {Msg}", ex.Message); }
    }

    // ------------------------------------------------------------------ browsing

    /// <summary>Forget the cached children of a node (used to see new machines when rescanning the Machines folder).</summary>
    public void Invalidate(NodeId node) { lock (_browseCache) _browseCache.Remove(node.ToString()); }

    public async Task<IReadOnlyList<OpcChild>> BrowseAsync(NodeId node, CancellationToken ct = default)
    {
        var key = node.ToString();
        await _browseLock.WaitAsync(ct);
        try
        {
            if (_browseCache.TryGetValue(key, out var cached)) return cached;
            var list = new List<OpcChild>();
            var desc = new BrowseDescription
            {
                NodeId = node,
                BrowseDirection = BrowseDirection.Forward,
                ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                IncludeSubtypes = true,
                NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                ResultMask = (uint)BrowseResultMask.All,
            };
            var resp = await Session.BrowseAsync(null, null, 0, new BrowseDescriptionCollection { desc }, ct);
            var result = resp.Results[0];
            Add(list, result.References);
            var cp = result.ContinuationPoint;
            while (cp is { Length: > 0 })
            {
                var next = await Session.BrowseNextAsync(null, false, new ByteStringCollection { cp }, ct);
                Add(list, next.Results[0].References);
                cp = next.Results[0].ContinuationPoint;
            }
            _browseCache[key] = list;
            return list;
        }
        finally { _browseLock.Release(); }
    }

    private void Add(List<OpcChild> list, ReferenceDescriptionCollection? refs)
    {
        if (refs is null) return;
        foreach (var r in refs)
        {
            var id = ExpandedNodeId.ToNodeId(r.NodeId, Session.NamespaceUris);
            if (id is null) continue;
            list.Add(new OpcChild(id, r.BrowseName.Name, r.NodeClass, r.DisplayName?.Text ?? r.BrowseName.Name,
                NodeId.IsNull(r.TypeDefinition) ? null : ExpandedNodeId.ToNodeId(r.TypeDefinition, Session.NamespaceUris)));
        }
    }

    /// <summary>Resolves "A/B/C" (browse names, namespace-agnostic) below <paramref name="start"/>; depth first over all matches.</summary>
    public async Task<NodeId?> ResolvePathAsync(NodeId start, string path, CancellationToken ct = default)
    {
        var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        async Task<NodeId?> Walk(NodeId node, int i)
        {
            if (i == segs.Length) return node;
            foreach (var c in await BrowseAsync(node, ct))
            {
                if (c.BrowseName != segs[i]) continue;
                var found = await Walk(c.NodeId, i + 1);
                if (found is not null) return found;
            }
            return null;
        }
        return await Walk(start, 0);
    }

    public async Task<string?> TypeNameAsync(NodeId? typeDefinition, CancellationToken ct = default)
    {
        if (typeDefinition is null) return null;
        try
        {
            var node = await Session.ReadNodeAsync(typeDefinition, ct);
            return node.DisplayName?.Text;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------ read / subscribe

    public async Task<Dictionary<string, Sample>> ReadAsync(IReadOnlyDictionary<string, NodeId> nodes, CancellationToken ct = default)
    {
        var res = new Dictionary<string, Sample>();
        if (nodes.Count == 0) return res;
        var keys = nodes.Keys.ToList();
        var toRead = new ReadValueIdCollection(keys.Select(k => new ReadValueId { NodeId = nodes[k], AttributeId = Attributes.Value }));
        var resp = await Session.ReadAsync(null, 0, TimestampsToReturn.Both, toRead, ct);
        for (var i = 0; i < keys.Count; i++) res[keys[i]] = OpcValue.FromDataValue(resp.Results[i]);
        return res;
    }

    private Subscription? _shared;
    private readonly SemaphoreSlim _subGate = new(1, 1);

    /// <summary>
    /// Adds monitored items to the one subscription of this session (servers limit the number of
    /// subscriptions per session, so all machines share it). Dispose the handle to remove the items again.
    /// </summary>
    public async Task<IDisposable> SubscribeAsync(IReadOnlyDictionary<string, NodeId> nodes, int publishingMs, int samplingMs,
        Action<string, Sample> onChange, CancellationToken ct = default)
    {
        await _subGate.WaitAsync(ct);
        try
        {
            if (_shared is null)
            {
                _shared = new Subscription(Session.DefaultSubscription)
                {
                    PublishingInterval = publishingMs,
                    KeepAliveCount = 10,
                    LifetimeCount = 120,
                    PublishingEnabled = true,
                    DisplayName = "umatiGateway",
                };
                Session.AddSubscription(_shared);
                await _shared.CreateAsync(ct);
            }
            var items = new List<MonitoredItem>();
            foreach (var (key, nodeId) in nodes)
            {
                var item = new MonitoredItem(_shared.DefaultItem)
                {
                    StartNodeId = nodeId,
                    AttributeId = Attributes.Value,
                    DisplayName = key,
                    SamplingInterval = samplingMs,
                    QueueSize = 1,
                    DiscardOldest = true,
                    MonitoringMode = MonitoringMode.Reporting,
                };
                item.Notification += (mi, args) =>
                {
                    foreach (var dv in mi.DequeueValues()) onChange(key, OpcValue.FromDataValue(dv));
                };
                _shared.AddItem(item);
                items.Add(item);
            }
            await _shared.ApplyChangesAsync(ct);
            return new ItemsHandle(this, items);
        }
        finally { _subGate.Release(); }
    }

    private sealed class ItemsHandle : IDisposable
    {
        private readonly OpcUaConnection _c; private readonly List<MonitoredItem> _items;
        public ItemsHandle(OpcUaConnection c, List<MonitoredItem> items) { _c = c; _items = items; }
        public void Dispose()
        {
            try
            {
                if (!_c.Connected || _c._shared is null) return;
                Task.Run(async () =>
                {
                    await _c._subGate.WaitAsync();
                    try { _c._shared.RemoveItems(_items); await _c._shared.ApplyChangesAsync(); }
                    finally { _c._subGate.Release(); }
                }).Wait(TimeSpan.FromSeconds(5));
            }
            catch { /* session may be gone */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lost.TrySetResult();
        try { if (_session is not null) await _session.CloseAsync(); } catch { /* ignore */ }
        _session?.Dispose();
        _session = null;
    }
}
