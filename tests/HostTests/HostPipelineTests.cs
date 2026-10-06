using MinecraftInRepo.Host;
using Xunit;

namespace MinecraftInRepo.Tests
{
    public class HostPipelineTests
    {
        private static GuestState StateAt(long seq, double x, float period = 50f)
        {
            return new GuestState
            {
                Seq = seq,
                X = x, PX = x - 1.0, Y = 64.0, PY = 64.0, Z = 0.0, PZ = 0.0,
                TickPeriodMs = period
            };
        }

        [Fact]
        public void TickTrackerInterpolatesFromTheArrivalTime()
        {
            TickTracker tracker = new TickTracker();
            GuestState state = StateAt(1, 10.0);

            tracker.Observe(state, 1000);
            Assert.True(tracker.Primed);
            Assert.Equal(0.0, tracker.Alpha(state, 1000));

            Assert.Equal(0.5, tracker.Alpha(state, 1025), 3);
            Assert.Equal(1.0, tracker.Alpha(state, 1050));
            Assert.Equal(1.0, tracker.Alpha(state, 9000));   // clamped
        }

        [Fact]
        public void TickTrackerRestartsWhenANewTickArrives()
        {
            TickTracker tracker = new TickTracker();
            tracker.Observe(StateAt(1, 10.0), 1000);
            Assert.Equal(1.0, tracker.Alpha(StateAt(1, 10.0), 2000));

            tracker.Observe(StateAt(2, 11.0), 2000);
            Assert.Equal(0.0, tracker.Alpha(StateAt(2, 11.0), 2000));
            Assert.Equal(0.0, tracker.Alpha(StateAt(2, 11.0), 2010));
        }

        [Fact]
        public void HealthScalesBetweenTheTwoGames()
        {
            Assert.Equal(5f, HealthScale.ToGuest(25, 100));       // 25 of 100 -> 5 hearts
            Assert.Equal(20f, HealthScale.ToGuest(100, 100));
            Assert.Equal(0f, HealthScale.ToGuest(0, 100));
            Assert.Equal(2f, HealthScale.ToGuest(1, 10));

            Assert.Equal(50, HealthScale.ToHost(10f, 100, false));
            Assert.Equal(100, HealthScale.ToHost(20f, 100, false));
            Assert.Equal(0, HealthScale.ToHost(20f, 100, true));   // dead is dead
            Assert.Equal(1, HealthScale.ToHost(0.2f, 100, false)); // never let the host kill us
            Assert.Equal(100, HealthScale.ToHost(999f, 100, false));
        }

        [Fact]
        public void SchedulerFillsTheNearestRegionFirstAndKnowsWhenItIsReady()
        {
            RegionScheduler scheduler = new RegionScheduler(1, 1);
            Assert.False(scheduler.Ready);
            Assert.True(scheduler.SetCentre(4, 0, -2));
            Assert.False(scheduler.SetCentre(4, 0, -2));   // same centre: no change
            Assert.Equal(27, scheduler.Count);             // 3 x 3 x 3 regions

            for (int i = 0; i < 27; i++)
            {
                RegionKey next;
                Assert.True(scheduler.TryNext(0, out next));
                if (i == 0)
                {
                    Assert.Equal(new RegionKey(4, 0, -2), next);   // the centre comes first
                }
                scheduler.MarkSent(next, 0, 4000);
            }

            Assert.True(scheduler.Ready);
            RegionKey none;
            Assert.False(scheduler.TryNext(0, out none));          // everything is fresh
            Assert.True(scheduler.TryNext(4001, out none));        // ...until it is due again
        }

        [Fact]
        public void SchedulerRefreshesTheCentreOftenAndTheEdgesRarely()
        {
            RegionScheduler scheduler = new RegionScheduler(1, 1);
            scheduler.SetCentre(0, 0, 0);

            RegionKey centre;
            Assert.True(scheduler.TryNext(0, out centre));
            scheduler.MarkSent(centre, 0, 4000);

            RegionKey far;
            Assert.True(scheduler.TryNext(0, out far));
            scheduler.MarkSent(far, 0, 30000);

            // The centre is due again in 4 s, the far region only after 30 s.
            RegionKey due;
            Assert.False(scheduler.TryNext(3999, out due));
            Assert.True(scheduler.TryNext(4001, out due));
            Assert.Equal(centre, due);
        }

        [Fact]
        public void MovingTheCentreResetsReadiness()
        {
            RegionScheduler scheduler = new RegionScheduler(0, 0);
            scheduler.SetCentre(0, 0, 0);
            RegionKey only;
            Assert.True(scheduler.TryNext(0, out only));
            scheduler.MarkSent(only, 0, 4000);
            Assert.True(scheduler.Ready);

            Assert.True(scheduler.SetCentre(1, 0, 0));
            Assert.False(scheduler.Ready);
        }
    }
}
