using System.Threading.Tasks;

internal interface IApplicationShutdownHooks
{
    ValueTask QuiesceAsync(ShutdownReport report);
    ValueTask DrainRuntimeAsync(ShutdownReport report);
    ValueTask DrainSceneAsync(ShutdownReport report);
    bool CanReleaseProcessContent(ShutdownReport report, out string failure);
    ValueTask ReleaseContentAsync(ShutdownReport report);
    bool CanRunFinalizerBarrier(ShutdownReport report, out string failure);
    void RunFinalizerBarrier(ShutdownReport report);
}
