using System.Threading.Tasks;

internal interface IApplicationShutdownParticipant
{
    string ShutdownParticipantId { get; }
    ApplicationShutdownParticipantStage ShutdownStage { get; }
    int ShutdownOrder { get; }
    ValueTask CloseForApplicationShutdownAsync(ShutdownReport report);
}
