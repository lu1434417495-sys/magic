using System;
using System.Collections.Generic;
using System.Threading;

internal sealed class GodotProjectionLease<T> : IDisposable
    where T : class, IDisposable
{
    private sealed record BorrowEntry(string DiagnosticId, WeakReference<object> Target);

    private static long _nextLeaseId;
    private static long _nextBorrowerId;

    private readonly string _ownerId;
    private readonly string _domainName;
    private readonly string _leaseDiagnosticId;
    private readonly NativeLeaseScope _ownerScope;
    private readonly List<BorrowEntry> _borrowers = new();
    private T _value;
    private bool _closed;

    private GodotProjectionLease(
        T root,
        string ownerId,
        LifetimeDomain domain,
        string reason
    )
    {
        ArgumentNullException.ThrowIfNull(root);
        _ownerId = string.IsNullOrWhiteSpace(ownerId) ? "unnamed" : ownerId.Trim();
        _domainName = domain.ToString();
        long leaseId = Interlocked.Increment(ref _nextLeaseId);
        _leaseDiagnosticId = $"projection-lease:{_domainName}:{_ownerId}:{leaseId}";
        _ownerScope = NativeLeaseScope.CreateProjectionOwner(
            $"projection:{_ownerId}:{leaseId}",
            domain
        );

        LifecycleAuditRegistry.Shared.RegisterActive(
            LifecycleAuditActiveKind.Lease,
            _leaseDiagnosticId,
            _domainName,
            this
        );
        try
        {
            _value = _ownerScope.Own(root, reason);
        }
        catch
        {
            _closed = true;
            _ownerScope.Dispose();
            LifecycleAuditRegistry.Shared.UnregisterActive(
                LifecycleAuditActiveKind.Lease,
                _leaseDiagnosticId,
                _domainName
            );
            throw;
        }
    }

    internal static GodotProjectionLease<T> CreateOwnedRoot(
        T root,
        string ownerId,
        LifetimeDomain domain,
        string reason
    ) => new(root, ownerId, domain, reason);

    internal T Value
    {
        get
        {
            lock (NativeLeaseScope.OwnershipSync)
            {
                ThrowIfClosed();
                return _value;
            }
        }
    }

    internal TOwned Own<TOwned>(TOwned wrapper, string reason)
        where TOwned : class, IDisposable
    {
        lock (NativeLeaseScope.OwnershipSync)
        {
            ThrowIfClosed();
            return _ownerScope.Own(wrapper, reason);
        }
    }

    internal TBorrowed Borrow<TBorrowed>(TBorrowed wrapper, string reason)
        where TBorrowed : class
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        lock (NativeLeaseScope.OwnershipSync)
        {
            ThrowIfClosed();
            string diagnosticId =
                $"content-borrower:{_domainName}:{_ownerId}:{Interlocked.Increment(ref _nextBorrowerId)}";
            LifecycleAuditRegistry.Shared.RegisterActive(
                LifecycleAuditActiveKind.ContentBorrower,
                diagnosticId,
                _domainName,
                wrapper
            );
            try
            {
                _borrowers.Add(new BorrowEntry(diagnosticId, new WeakReference<object>(wrapper)));
            }
            catch
            {
                LifecycleAuditRegistry.Shared.UnregisterActive(
                    LifecycleAuditActiveKind.ContentBorrower,
                    diagnosticId,
                    _domainName
                );
                throw;
            }
            return wrapper;
        }
    }

    public void Dispose()
    {
        List<BorrowEntry> borrowers;
        lock (NativeLeaseScope.OwnershipSync)
        {
            if (_closed)
                return;
            _closed = true;
            _value = null;
            borrowers = new List<BorrowEntry>(_borrowers);
            _borrowers.Clear();
        }

        foreach (BorrowEntry borrower in borrowers)
        {
            LifecycleAuditRegistry.Shared.UnregisterActive(
                LifecycleAuditActiveKind.ContentBorrower,
                borrower.DiagnosticId,
                _domainName
            );
        }

        _ownerScope.Dispose();
        LifecycleAuditRegistry.Shared.UnregisterActive(
            LifecycleAuditActiveKind.Lease,
            _leaseDiagnosticId,
            _domainName
        );
    }

    private void ThrowIfClosed()
    {
        if (_closed)
            throw new ObjectDisposedException(nameof(GodotProjectionLease<T>), _ownerId);
    }
}
