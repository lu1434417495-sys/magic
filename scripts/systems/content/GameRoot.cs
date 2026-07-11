using System;
using Godot;

/// <summary>
/// 统一组合根：持有 owning <see cref="GameSession"/> 的弱引用与正式内容读入口
/// <see cref="GameContentCatalog"/>。content catalog 借用 process snapshot，root 关闭时
/// 会原地使 catalog 失效；下游不应跨 root 生命周期缓存 catalog。
/// </summary>
public sealed class GameRoot : IDisposable
{
    private readonly GameContentCatalog _contentCatalog = new();
    private System.WeakReference<GameSession> _sessionRef;
    private bool _disposed;

    internal void BindSnapshot(
        GameSession session,
        ContentSnapshot snapshot,
        ILegacyEnemyContentCatalog legacyEnemyContent
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(legacyEnemyContent);
        _sessionRef = new System.WeakReference<GameSession>(session);
        try
        {
            _contentCatalog.BindSnapshot(session, snapshot, legacyEnemyContent);
        }
        catch
        {
            _contentCatalog.ClearSessionBinding();
            _sessionRef = null;
            throw;
        }
    }

    /// <summary>
    /// 释放本 root 拥有的运行期资源：解绑并使 content catalog 失效（清空 typed 快照、
    /// 自增 revision），以免仍持有旧 catalog 引用的下游读到 stale 内容。
    /// </summary>
    internal void DisposeOwnedRuntimeResources()
    {
        if (_disposed)
            return;
        _disposed = true;
        _contentCatalog.ClearSessionBinding();
        _sessionRef = null;
    }

    internal void ClearSnapshotBindingForRetry()
    {
        if (_disposed)
            return;
        _contentCatalog.ClearSessionBinding();
        _sessionRef = null;
    }

    public void Dispose() => DisposeOwnedRuntimeResources();

    public bool HasSessionTyped() => GetSessionTyped() != null;

    public GameSession GetSessionTyped()
    {
        if (
            _sessionRef == null
            || !_sessionRef.TryGetTarget(out GameSession session)
            || session == null
            || !GodotObject.IsInstanceValid(session)
        )
        {
            return null;
        }
        return session;
    }

    public GameContentCatalog GetContentCatalogTyped() => _contentCatalog;
}
