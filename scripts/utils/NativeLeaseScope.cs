using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

internal sealed class NativeLeaseScope : IDisposable
{
    private sealed class OwnedEntry
    {
        internal OwnedEntry(IDisposable wrapper, string diagnosticId, string auditDomain)
        {
            Wrapper = wrapper;
            DiagnosticId = diagnosticId;
            AuditDomain = auditDomain;
        }

        internal IDisposable Wrapper { get; }
        internal string DiagnosticId { get; }
        internal string AuditDomain { get; set; }
    }

    private static long _nextScopeId;
    private static long _nextOwnerId;
    internal static readonly object OwnershipSync = new();

    private readonly string _ownerId;
    private readonly string _domainName;
    private readonly string _scopeDiagnosticId;
    private readonly bool _auditScope;
    private readonly List<OwnedEntry> _owned = new();
    private readonly Dictionary<object, OwnedEntry> _ownedByWrapper =
        new(GodotWrapperReferenceComparer.Instance);
    private bool _closed;
    private bool _disposeAttempted;

    internal NativeLeaseScope(string ownerId, LifetimeDomain domain)
        : this(ownerId, domain, auditScope: true) { }

    private NativeLeaseScope(string ownerId, LifetimeDomain domain, bool auditScope)
    {
        ValidateDomain(domain);
        _ownerId = string.IsNullOrWhiteSpace(ownerId) ? "unnamed" : ownerId.Trim();
        _domainName = domain.ToString();
        _auditScope = auditScope;
        long scopeId = Interlocked.Increment(ref _nextScopeId);
        _scopeDiagnosticId = $"native-scope:{_domainName}:{_ownerId}:{scopeId}";
        if (_auditScope)
        {
            LifecycleAuditRegistry.Shared.RegisterActive(
                LifecycleAuditActiveKind.Scope,
                _scopeDiagnosticId,
                _domainName,
                this
            );
        }
    }

    internal static NativeLeaseScope CreateProjectionOwner(
        string ownerId,
        LifetimeDomain domain
    ) => new(ownerId, domain, auditScope: false);

    internal bool IsClosed
    {
        get
        {
            lock (OwnershipSync)
                return _closed;
        }
    }

    internal bool Owns(object wrapper)
    {
        if (wrapper == null)
            return false;
        lock (OwnershipSync)
            return _ownedByWrapper.ContainsKey(wrapper);
    }

    internal int OwnedCount
    {
        get
        {
            lock (OwnershipSync)
                return _owned.Count;
        }
    }

    internal IReadOnlyList<IDisposable> SnapshotOwnedWrappers()
    {
        lock (OwnershipSync)
        {
            var result = new List<IDisposable>(_owned.Count);
            foreach (OwnedEntry entry in _owned)
                result.Add(entry.Wrapper);
            return result.AsReadOnly();
        }
    }

    internal T Own<T>(T wrapper, string reason)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        string label = Label(reason);

        lock (OwnershipSync)
        {
            ThrowIfClosed();
            ValidateWrapper(wrapper, label);

            if (
                !GodotWrapperOwnershipRegistry.TryClaimLeaseOwnership(
                    wrapper,
                    this,
                    label,
                    out string ownershipFailure
                )
            )
            {
                throw new InvalidOperationException(ownershipFailure);
            }

            string diagnosticId =
                $"native-owner:{_domainName}:{_ownerId}:{Interlocked.Increment(ref _nextOwnerId)}";
            var entry = new OwnedEntry(wrapper, diagnosticId, _domainName);
            bool auditRegistered = false;
            try
            {
                LifecycleAuditRegistry.Shared.RegisterActive(
                    LifecycleAuditActiveKind.Owner,
                    diagnosticId,
                    _domainName,
                    wrapper
                );
                auditRegistered = true;
                _owned.Add(entry);
                _ownedByWrapper.Add(wrapper, entry);
            }
            catch
            {
                if (auditRegistered)
                {
                    LifecycleAuditRegistry.Shared.UnregisterActive(
                        LifecycleAuditActiveKind.Owner,
                        diagnosticId,
                        _domainName
                    );
                }
                GodotWrapperOwnershipRegistry.TryReleaseLeaseOwnership(
                    wrapper,
                    this,
                    out _
                );
                throw;
            }
        }

        return wrapper;
    }

    internal T TransferTo<T>(NativeLeaseScope target, T wrapper, string reason)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wrapper);
        if (ReferenceEquals(this, target))
            throw new InvalidOperationException("A native lease cannot transfer to itself.");

        string label = target.Label(reason);
        lock (OwnershipSync)
        {
            ThrowIfClosed();
            target.ThrowIfClosed();
            ValidateWrapper(wrapper, label);
            if (!_ownedByWrapper.TryGetValue(wrapper, out OwnedEntry entry))
            {
                throw new InvalidOperationException(
                    $"The source native lease does not own the wrapper. owner={_ownerId}, type={wrapper.GetType().Name}"
                );
            }
            if (target._ownedByWrapper.ContainsKey(wrapper))
                throw new InvalidOperationException("The target native lease already owns the wrapper.");

            if (
                !GodotWrapperOwnershipRegistry.TryTransferLeaseOwnership(
                    wrapper,
                    this,
                    target,
                    label,
                    out string transferFailure
                )
            )
            {
                throw new InvalidOperationException(transferFailure);
            }

            if (
                !LifecycleAuditRegistry.Shared.TryTransferActiveDomain(
                    LifecycleAuditActiveKind.Owner,
                    entry.DiagnosticId,
                    entry.AuditDomain,
                    target._domainName,
                    out string auditFailure
                )
            )
            {
                GodotWrapperOwnershipRegistry.TryTransferLeaseOwnership(
                    wrapper,
                    target,
                    this,
                    Label("audit-transfer-rollback"),
                    out _
                );
                throw new InvalidOperationException(auditFailure);
            }

            int sourceIndex = _owned.IndexOf(entry);
            _owned.RemoveAt(sourceIndex);
            _ownedByWrapper.Remove(wrapper);
            try
            {
                target._owned.Add(entry);
                target._ownedByWrapper.Add(wrapper, entry);
            }
            catch
            {
                target._owned.Remove(entry);
                target._ownedByWrapper.Remove(wrapper);
                _owned.Insert(sourceIndex, entry);
                _ownedByWrapper.Add(wrapper, entry);
                LifecycleAuditRegistry.Shared.TryTransferActiveDomain(
                    LifecycleAuditActiveKind.Owner,
                    entry.DiagnosticId,
                    target._domainName,
                    entry.AuditDomain,
                    out _
                );
                GodotWrapperOwnershipRegistry.TryTransferLeaseOwnership(
                    wrapper,
                    target,
                    this,
                    Label("transfer-rollback"),
                    out _
                );
                throw;
            }

            entry.AuditDomain = target._domainName;
            LifecycleAuditRegistry.Shared.RecordTransferred();
        }

        return wrapper;
    }

    public void Dispose()
    {
        List<OwnedEntry> snapshot;
        lock (OwnershipSync)
        {
            if (_disposeAttempted)
                return;
            _disposeAttempted = true;
            _closed = true;
            snapshot = new List<OwnedEntry>(_owned);
        }

        var failures = new List<Exception>();
        for (int index = snapshot.Count - 1; index >= 0; index--)
        {
            OwnedEntry entry = snapshot[index];
            try
            {
                entry.Wrapper.Dispose();
                lock (OwnershipSync)
                {
                    if (
                        !GodotWrapperOwnershipRegistry.TryReleaseLeaseOwnership(
                            entry.Wrapper,
                            this,
                            out string releaseFailure
                        )
                    )
                    {
                        throw new InvalidOperationException(releaseFailure);
                    }
                    _owned.Remove(entry);
                    _ownedByWrapper.Remove(entry.Wrapper);
                    LifecycleAuditRegistry.Shared.UnregisterActive(
                        LifecycleAuditActiveKind.Owner,
                        entry.DiagnosticId,
                        entry.AuditDomain
                    );
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count == 0 && _auditScope)
        {
            LifecycleAuditRegistry.Shared.UnregisterActive(
                LifecycleAuditActiveKind.Scope,
                _scopeDiagnosticId,
                _domainName
            );
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"Native lease disposal failed. owner={_ownerId}, domain={_domainName}",
                failures
            );
        }
    }

    private static void ValidateDomain(LifetimeDomain domain)
    {
        if (
            !Enum.IsDefined(domain)
            || domain is LifetimeDomain.ProcessContent or LifetimeDomain.External
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(domain),
                domain,
                "Native/projection leases require a runtime-owned lifetime domain."
            );
        }
    }

    private static void ValidateWrapper(IDisposable wrapper, string reason)
    {
        if (wrapper is Node)
            throw new InvalidOperationException($"A native lease cannot own a Node. reason={reason}");
        if (wrapper is Resource resource && !string.IsNullOrEmpty(resource.ResourcePath))
        {
            throw new InvalidOperationException(
                $"A native lease cannot own a path-backed Resource. path={resource.ResourcePath}, reason={reason}"
            );
        }
        if (wrapper is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject))
            throw new ObjectDisposedException(wrapper.GetType().Name);
    }

    private void ThrowIfClosed()
    {
        if (_closed)
            throw new ObjectDisposedException(nameof(NativeLeaseScope), _ownerId);
    }

    private string Label(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? _ownerId : $"{_ownerId}:{reason.Trim()}";
}
