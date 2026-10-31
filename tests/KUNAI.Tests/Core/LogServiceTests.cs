using KUNAI.Core.Services;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Core;

public class LogServiceTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 1, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    [Fact]
    public void Add_AppendsEntriesInOrder_WithFormattedTimestamp()
    {
        var time = new FixedTimeProvider();
        var log = new LogService(new ImmediateUiDispatcher(), 1000, time);

        log.Add("Connected");
        time.Now = time.Now.AddSeconds(4);
        log.Add("Layer1");

        Assert.Equal(new[] { "[12:00:01] Connected", "[12:00:05] Layer1" }, log.Entries.Select(e => e.ToString()));
    }

    [Fact]
    public void Add_BeyondMax_RemovesOldestEntries()
    {
        var log = new LogService(new ImmediateUiDispatcher(), 3, TimeProvider.System);

        for (var i = 1; i <= 5; i++)
        {
            log.Add($"m{i}");
        }

        Assert.Equal(new[] { "m3", "m4", "m5" }, log.Entries.Select(e => e.Message));
    }

    [Fact]
    public void DefaultMax_Is1000()
    {
        var log = new LogService(new ImmediateUiDispatcher());

        for (var i = 0; i < 1500; i++)
        {
            log.Add($"m{i}");
        }

        Assert.Equal(1000, log.Entries.Count);
        Assert.Equal("m500", log.Entries[0].Message);
        Assert.Equal("m1499", log.Entries[^1].Message);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var log = new LogService(new ImmediateUiDispatcher());
        log.Add("a");

        log.Clear();

        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Entries_AreOnlyUpdatedWhenDispatcherRuns()
    {
        var dispatcher = new QueueingUiDispatcher();
        var log = new LogService(dispatcher);

        log.Add("a");
        log.Add("b");
        Assert.Empty(log.Entries);

        dispatcher.RunAll();
        Assert.Equal(new[] { "a", "b" }, log.Entries.Select(e => e.Message));
    }

    [Fact]
    public void Timestamp_IsCapturedAtAddTime_NotAtDispatchTime()
    {
        var time = new FixedTimeProvider();
        var dispatcher = new QueueingUiDispatcher();
        var log = new LogService(dispatcher, 10, time);

        log.Add("a");
        time.Now = time.Now.AddMinutes(10);
        dispatcher.RunAll();

        Assert.Equal("[12:00:01] a", log.Entries.Single().ToString());
    }

    [Fact]
    public void Entries_RaiseCollectionChanged_ForUiBinding()
    {
        var log = new LogService(new ImmediateUiDispatcher());
        var raised = 0;
        log.Entries.CollectionChanged += (_, _) => raised++;

        log.Add("a");

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new LogService(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogService(new ImmediateUiDispatcher(), 0, TimeProvider.System));
        Assert.Throws<ArgumentNullException>(() => new LogService(new ImmediateUiDispatcher(), 10, null!));
    }

    [Fact]
    public void Add_NullMessage_Throws()
    {
        var log = new LogService(new ImmediateUiDispatcher());

        Assert.Throws<ArgumentNullException>(() => log.Add(null!));
    }
}
