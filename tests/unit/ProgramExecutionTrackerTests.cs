using UmatiGateway.Mapping;
using Xunit;
using static UmatiGateway.Mapping.ProgramExecutionTracker;

namespace umatiGateway.Tests;

public class ProgramExecutionTrackerTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
    private static ProgramExecutionTracker NewTracker() { var n = 0; return new ProgramExecutionTracker(() => $"exec-{++n}"); }

    [Fact]
    public void FirstObservationWithoutRunningProgramGivesNullSnapshot()
    {
        var t = NewTracker();
        var r = t.Update(new Input("Ended", "P1"), T0);
        Assert.NotNull(r.Snapshot);
        Assert.Null(r.Snapshot!.Value);
        Assert.Empty(r.Events);
        Assert.Null(t.CurrentExecutionId);
    }

    [Fact]
    public void RunningAtStartIsAdoptedSilently()
    {
        var t = NewTracker();
        var r = t.Update(new Input("Running", "P1"), T0);
        Assert.Empty(r.Events);
        Assert.Equal("exec-1", r.Snapshot!.CorrelationId);
        Assert.Equal("Running", r.Snapshot.Value!["State"]);
        Assert.Equal("exec-1", t.CurrentExecutionId);
    }

    [Fact]
    public void FullLifecycleProducesEventsAndSums()
    {
        var t = NewTracker();
        t.Update(new Input("Ended", null), T0);

        var started = t.Update(new Input("Running", "P1", null, new Counters(100, 10)), T0.AddSeconds(10));
        Assert.Equal("Production.ProgramExecutionStarted", Assert.Single(started.Events).Event);
        Assert.Equal("exec-1", t.CurrentExecutionId);

        var interrupted = t.Update(new Input("Interrupted", "P1"), T0.AddSeconds(20));
        Assert.Equal("Production.ProgramExecutionInterrupted", Assert.Single(interrupted.Events).Event);

        var resumed = t.Update(new Input("Running", "P1"), T0.AddSeconds(30));
        Assert.Equal("Production.ProgramExecutionResumed", Assert.Single(resumed.Events).Event);

        var ended = t.Update(new Input("Ended", "P1", null, new Counters(160, 15)), T0.AddSeconds(70));
        var ev = Assert.Single(ended.Events);
        Assert.Equal("Production.ProgramExecutionEnded", ev.Event);
        Assert.Equal(60.0, ev.Value["Duration"]);                 // includes the interruption
        Assert.Equal(60.0, ev.Value["EnergyElectricity"]);
        Assert.Equal(5.0, ev.Value["EnergyCompressedAir"]);
        Assert.Equal("Ended", ended.Snapshot!.Value!["State"]);   // snapshot stays after the end
        Assert.Equal("exec-1", ended.Snapshot.CorrelationId);
        Assert.Null(t.CurrentExecutionId);                         // no live execution any more
    }

    [Fact]
    public void AbortedIsReportedAsAborted()
    {
        var t = NewTracker();
        t.Update(new Input("Ended", null), T0);
        t.Update(new Input("Running", "P1"), T0.AddSeconds(1));
        var r = t.Update(new Input("Aborted", "P1"), T0.AddSeconds(5));
        Assert.Equal("Production.ProgramExecutionAborted", Assert.Single(r.Events).Event);
    }

    [Fact]
    public void SameStateAgainIsNoChange()
    {
        var t = NewTracker();
        t.Update(new Input("Ended", null), T0);
        var r = t.Update(new Input("Ended", null), T0.AddSeconds(5));
        Assert.Null(r.Snapshot);
        Assert.Empty(r.Events);
    }

    [Fact]
    public void NewExecutionGetsNewId()
    {
        var t = NewTracker();
        t.Update(new Input("Ended", null), T0);
        t.Update(new Input("Running", "A"), T0.AddSeconds(1));
        t.Update(new Input("Ended", "A"), T0.AddSeconds(2));
        t.Update(new Input("Running", "B"), T0.AddSeconds(3));
        Assert.Equal("exec-2", t.CurrentExecutionId);
    }
}
