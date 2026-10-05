using System.Collections.Generic;
using MinecraftInRepo.Host;
using MinecraftInRepo.Net;
using Xunit;

namespace MinecraftInRepo.Tests
{
    public class ProtocolTests
    {
        private const string GuestJson =
            "{\"t\":\"gs\",\"seq\":41,\"x\":10.5,\"y\":64.0,\"z\":-3.25,\"px\":10.0,\"py\":64.0,\"pz\":-3.0," +
            "\"period\":50,\"yaw\":-90.0,\"pitch\":-15.0,\"eye\":1.62,\"fov\":70,\"cam\":1,\"camDist\":4.0," +
            "\"hp\":14,\"hpMax\":20,\"dead\":false,\"screen\":true,\"ack\":7,\"mode\":1,\"t\":123456}";

        [Fact]
        public void GuestStateParsesTheWireMessage()
        {
            Dictionary<string, object> msg = JsonLite.ParseObject(GuestJson);
            GuestState state;
            Assert.True(GuestState.TryParse(msg, out state));

            Assert.Equal(41L, state.Seq);
            Assert.Equal(10.5, state.X);
            Assert.Equal(64.0, state.Y);
            Assert.Equal(-3.25, state.Z);
            Assert.Equal(10.0, state.PX);
            Assert.Equal(-90f, state.Yaw);
            Assert.Equal(1.62f, state.EyeHeight);
            Assert.Equal(1, state.CameraMode);
            Assert.Equal(14f, state.Health);
            Assert.False(state.Dead);
            Assert.True(state.ScreenOpen);
            Assert.Equal(7L, state.TeleportAck);
            Assert.True(state.IsCreative);
        }

        [Fact]
        public void GuestStateAcceptsNumericBooleans()
        {
            Dictionary<string, object> msg = JsonLite.ParseObject(
                "{\"t\":\"gs\",\"seq\":1,\"dead\":1,\"screen\":0,\"mode\":0}");
            GuestState state;
            Assert.True(GuestState.TryParse(msg, out state));
            Assert.True(state.Dead);
            Assert.False(state.ScreenOpen);
            Assert.False(state.IsCreative);
        }

        [Fact]
        public void GuestStateFallsBackToSaneDefaults()
        {
            Dictionary<string, object> msg = JsonLite.ParseObject("{\"t\":\"gs\",\"seq\":1}");
            GuestState state;
            Assert.True(GuestState.TryParse(msg, out state));
            Assert.Equal(50f, state.TickPeriodMs);
            Assert.Equal(1.62f, state.EyeHeight);
            Assert.Equal(20f, state.MaxHealth);
        }

        [Fact]
        public void LerpPositionInterpolatesBetweenTicks()
        {
            Dictionary<string, object> msg = JsonLite.ParseObject(GuestJson);
            GuestState state;
            GuestState.TryParse(msg, out state);

            double x, y, z;
            state.LerpPosition(0.0, out x, out y, out z);
            Assert.Equal(10.0, x);

            state.LerpPosition(1.0, out x, out y, out z);
            Assert.Equal(10.5, x);

            state.LerpPosition(0.5, out x, out y, out z);
            Assert.Equal(10.25, x);
            Assert.Equal(64.0, y);
            Assert.Equal(-3.125, z);
        }

        [Fact]
        public void HostStateRoundTripsItsFields()
        {
            HostState state = new HostState
            {
                Seq = 12,
                TimestampMs = 99,
                Owner = Owner.GuestOwns,
                TeleportSeq = 3,
                Loading = true,
                MenuOpen = false,
                ViewportWidth = 1920,
                ViewportHeight = 1080,
                X = 1.5, Y = 2.5, Z = -3.5,
                Yaw = 12.5f,
                Pitch = -3.5f
            };

            Dictionary<string, object> msg = JsonLite.ParseObject(state.ToJson());
            Assert.Equal("hs", JsonLite.GetString(msg, "t"));
            Assert.Equal(12.0, JsonLite.GetNumber(msg, "seq"));
            Assert.Equal("GuestOwns", JsonLite.GetString(msg, "owner"));
            Assert.Equal(3.0, JsonLite.GetNumber(msg, "tseq"));
            Assert.True(JsonLite.GetBool(msg, "loading"));
            Assert.False(JsonLite.GetBool(msg, "menu"));
            Assert.Equal(1920.0, JsonLite.GetNumber(msg, "vpw"));
            Assert.Equal(-3.5, JsonLite.GetNumber(msg, "z"));
        }

        [Fact]
        public void GetBoolUnderstandsEveryWireShape()
        {
            Assert.True(JsonLite.GetBool(JsonLite.ParseObject("{\"a\":true}"), "a"));
            Assert.False(JsonLite.GetBool(JsonLite.ParseObject("{\"a\":false}"), "a"));
            Assert.True(JsonLite.GetBool(JsonLite.ParseObject("{\"a\":1}"), "a"));
            Assert.False(JsonLite.GetBool(JsonLite.ParseObject("{\"a\":0}"), "a"));
            Assert.True(JsonLite.GetBool(JsonLite.ParseObject("{\"a\":\"true\"}"), "a"));
            Assert.False(JsonLite.GetBool(JsonLite.ParseObject("{}"), "a"));
            Assert.True(JsonLite.GetBool(JsonLite.ParseObject("{}"), "a", true));
        }
    }
}
