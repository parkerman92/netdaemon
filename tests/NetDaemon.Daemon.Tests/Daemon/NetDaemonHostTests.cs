using JoySoftware.HomeAssistant.Client;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using NetDaemon.Common;
using Xunit;

namespace NetDaemon.Daemon.Tests.Daemon
{
    public class HostTestApp : NetDaemon.Common.NetDaemonApp
    {
    }

    public class NetDaemonTests : DaemonHostTestBase
    {
        public NetDaemonTests() : base()
        {
        }

        [Fact]
        public async Task EventShouldCallCorrectFunction()
        {
            // ARRANGE

            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);

            dynamic helloWorldDataObject = GetDynamicDataObject(HelloWorldData);

            DefaultHassClientMock.AddCustomEvent("CUSTOM_EVENT", helloWorldDataObject);

            var isCalled = false;
            var message = "";

            // ACT
            DefaultDaemonApp.ListenEvent("CUSTOM_EVENT", (ev, data) =>
            {
                isCalled = true;
                message = data.Test;
                return Task.CompletedTask;
            });

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.True(isCalled);
            Assert.Equal(HelloWorldData, message);
        }

        [Fact]
        public async Task AttributeServiceCallShouldFindCorrectFunction()
        {
            // ARRANGE
            var app = new AssmeblyDaemonApp();
            app.Id = "id";


            DefaultDaemonHost.InternalRunningAppInstances[app.Id] = app;
            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);

            // ACT
            await app.HandleAttributeInitialization(DefaultDaemonHost).ConfigureAwait(false);

            // ASSERT
            Assert.Single(app.DaemonCallBacksForServiceCalls);
        }

        [Fact]
        public void GetStateMissingEntityReturnsNull()
        {
            // ARRANGE

            // ACT
            var entity = DefaultDaemonHost.GetState("light.missing_entity");

            // ASSERT
            Assert.Null(entity);
        }

        [Fact]
        public async Task GetStateReturnsCorrectEntityState()
        {
            // ARRANGE

            // Fake what is coming from hass client
            var task = RunDefauldDaemonUntilCanceled();

            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);
            // ACT
            var entity = DefaultDaemonHost.GetState("light.correct_entity");

            await task;

            // ASSERT
            Assert.NotNull(entity);
            Assert.Equal(entity?.State, "on");
        }

        // Todo: Add tests to test objects and arrays from the dynamic conversion

        [Fact]
        public async Task OtherEventShouldNotCallCorrectFunction()
        {
            // ARRANGE
            dynamic dataObject = GetDynamicDataObject();

            DefaultHassClientMock.AddCustomEvent("CUSTOM_EVENT", dataObject);

            var isCalled = false;

            // ACT
            DefaultDaemonApp.ListenEvent("OTHER_EVENT", (ev, data) =>
            {
                isCalled = true;
                return Task.CompletedTask;
            });

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.False(isCalled);
        }

        [Fact]
        public async Task RunConnectedReturnsARunningTask()
        {
            // ARRANGE

            // ACT
            var (runTask, _) = ReturnRunningDefauldDaemonHostTask();
            await Task.Delay(10);

            // ASSERT
            Assert.False(runTask.IsCompleted || runTask.IsCanceled);
            await runTask;
        }

        [Fact]
        public async Task RunNotConnectedCompletesTask()
        {
            // ARRANGE

            // ACTION
            var (runTask, _) = ReturnRunningNotConnectedDaemonHostTask();
            await runTask;

            // ASSERT
            Assert.True(runTask.IsCompleted);
        }

        [Fact]
        public void RunNullReferenceToHassClientShouldThrowException()
        {
            // ARRANGE

            // ACT and ASSERT
            Assert.Throws<ArgumentNullException>(() =>
                { var DefaultDaemonHost = new NetDaemonHost(null, null); });
        }

        [Fact]
        public async Task SendEventShouldCallCorrectMethod()
        {
            // ARRANGE
            var eventData = GetDynamicDataObject();

            var task = RunDefauldDaemonUntilCanceled();

            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);
            // ACT
            await DefaultDaemonHost.SendEvent("test_event", eventData);
            await task;

            // ASSERT
            var expandoObject = (ExpandoObject)eventData;
            DefaultHassClientMock.Verify(n => n.SendEvent("test_event", expandoObject));
        }

        [Fact]
        public async Task SendEventWithNullDataShouldCallCorrectMethod()
        {
            // ARRANGE
            var task = RunDefauldDaemonUntilCanceled();
            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);
            // ACT
            await DefaultDaemonHost.SendEvent("test_event");
            await task;

            // ASSERT
            DefaultHassClientMock.Verify(n => n.SendEvent("test_event", null));
        }

        [Fact]
        public async Task SpeakShouldCallCorrectService()
        {
            // ARRANGE
            DefaultHassClientMock.FakeStates["media_player.fakeplayer"] = new HassState
            {
                EntityId = "media_player.fakeplayer",
            };

            DefaultDaemonHost.InternalDelayTimeForTts = 0; // For testing

            // ACT
            var (daemonTask, _) = ReturnRunningDefauldDaemonHostTask();

            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);

            DefaultDaemonHost.Speak("media_player.fakeplayer", "Hello test!");

            var (_, expObject) = GetDynamicObject(
                ("entity_id", "media_player.fakeplayer"),
                ("message", "Hello test!")
            );

            await Task.Delay(50);

            // ASSERT
            DefaultHassClientMock.Verify(n => n.CallService("tts", "google_cloud_say", expObject, true));

            await WaitUntilCanceled(daemonTask);
        }

        [Fact]
        public async Task SpeakShouldWaitUntilMediaPlays()
        {
            // ARRANGE

            // Get a running default Daemon
            var (daemonTask, _) = ReturnRunningDefauldDaemonHostTask(700);

            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);

            DefaultDaemonHost.InternalDelayTimeForTts = 0; // Allow now extra waittime

            // Expected data call service
            var (expectedAttruibutes, expectedAttributesExpObject) = GetDynamicObject(
                ("entity_id", "media_player.fakeplayer"),
                ("message", "Hello test!")
            );

            // Add the player that we want to fake having with the fake playing 0.1 second media duration
            dynamic currentStateAttributes = new ExpandoObject();
            currentStateAttributes.media_duration = 0.1;

            DefaultDaemonHost.InternalState["media_player.fakeplayer"] = new EntityState
            {
                EntityId = "media_player.fakeplayer",
                Attribute = currentStateAttributes
            };

            // await Task.Delay(100);

            // ACT
            DefaultDaemonHost.Speak("media_player.fakeplayer", "Hello test!");
            DefaultDaemonHost.Speak("media_player.fakeplayer", "Hello test!");

            // ASSERT

            await Task.Delay(50);

            DefaultHassClientMock.Verify(n => n.CallService("tts", "google_cloud_say", expectedAttributesExpObject, true), Times.Once);

            await Task.Delay(150);
            // Called twice
            DefaultHassClientMock.Verify(n => n.CallService("tts", "google_cloud_say", expectedAttributesExpObject, true), Times.Exactly(2));

            await WaitUntilCanceled(daemonTask);
        }

        [Fact]
        public async Task StopCallsCloseClient()
        {
            await DefaultDaemonHost.Stop();

            DefaultHassClientMock.Verify(n => n.CloseAsync(), Times.Once);
        }

        [Fact]
        public async Task SubscribeChangedStateForEntityWillMakeCorrectCallback()
        {
            // ARRANGE
            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            string? reportedState = "";

            // ACT
            DefaultDaemonApp.ListenState("binary_sensor.pir", (entityId, newState, oldState) =>
            {
                reportedState = newState?.State;

                return Task.CompletedTask;
            });

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.Equal("on", reportedState);
        }

        [Fact]
        public async Task SubscribeChangedStateForAllChangesWillMakeCorrectCallbacks()
        {
            // ARRANGE
            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");
            DefaultHassClientMock.AddChangedEvent("light.mylight", fromState: "on", toState: "off");

            int nrOfTimesCalled = 0;

            // ACT
            DefaultDaemonApp.ListenState("", (entityId, newState, oldState) =>
            {
                nrOfTimesCalled++;

                return Task.CompletedTask;
            });

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.Equal(2, nrOfTimesCalled);
        }

        [Fact]
        public async Task ChangedEventHaveNullDataShouldThrowException()
        {
            // ARRANGE
            DefaultHassClientMock.FakeEvents.Enqueue(new HassEvent
            {
                EventType = "state_changed",
                Data = null
            });

            // ACT
            await RunDefauldDaemonUntilCanceled();

            //ASSERT
            LoggerMock.AssertLogged(LogLevel.Error, Times.AtLeastOnce());
        }

        [Fact]
        public async Task CancelChangedStateForSubscriptionWillNotMakeCallback()
        {
            // ARRANGE
            bool isCalled = false;

            // ACT
            var id = DefaultDaemonApp.ListenState("binary_sensor.pir", (entityId, newState, oldState) =>
            {
                isCalled = true;

                return Task.CompletedTask;
            });

            DefaultDaemonApp.CancelListenState(id!);

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.False(isCalled);
            Assert.Empty(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public async Task CallServiceEventShouldCallCorrectFunction()
        {
            // ARRANGE
            var dynObject = GetDynamicDataObject(HelloWorldData);
            var daemonTask = await GetConnectedNetDaemonTask();


            var isCalled = false;
            string? message = "";

            // ACT
            DefaultDaemonHost.ListenServiceCall("custom_domain", "any_service", data =>
            {
                isCalled = true;
                message = data?.Test;
                return Task.CompletedTask;
            });

            DefaultHassClientMock.AddCallServiceEvent("custom_domain", "any_service", dynObject);

            await daemonTask;

            // ASSERT
            Assert.True(isCalled);
            Assert.Equal(HelloWorldData, message);
        }

        [Fact]
        public async Task CallServiceEventOtherShouldNotCallFunction()
        {
            // ARRANGE
            var dynObject = GetDynamicDataObject(HelloWorldData);

            DefaultHassClientMock.AddCallServiceEvent("custom_domain", "other_service", dynObject);

            var isCalled = false;
            string? message = "";

            DefaultDaemonHost.ListenServiceCall("custom_domain", "any_service", data =>
            {
                isCalled = true;
                message = data?.Test;
                return Task.CompletedTask;
            });

            await RunDefauldDaemonUntilCanceled();

            Assert.False(isCalled);
            Assert.True(string.IsNullOrEmpty(message));
        }

        [Fact]
        public async Task SetStateShouldCallCorrectFunction()
        {
            await DefaultDaemonHost.SetStateAsync("sensor.any_sensor", "on", ("attr", "value"));

            var (dynObj, expObj) = GetDynamicObject(
                ("attr", "value")
            );
            DefaultHassClientMock.Verify(n => n.SetState("sensor.any_sensor", "on", expObj));
        }

        [Fact]
        public async Task SetStateShouldReturnCorrectData()
        {
            await DefaultDaemonHost.SetStateAsync("sensor.any_sensor", "on", ("attr", "value"));

            var (dynObj, expObj) = GetDynamicObject(
                ("attr", "value")
            );
            DefaultHassClientMock.Verify(n => n.SetState("sensor.any_sensor", "on", expObj));
        }

        [Fact]
        public async Task DelayStateChangeShouldReturnTrue()
        {
            // ARRANGE

            // ACT
            using var delayResult = DefaultDaemonApp.DelayUntilStateChange(new string[] { "binary_sensor.pir" }, to: "on");

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.True(delayResult.Task.Result);
            Assert.Empty(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public async Task DelayStateChangeWithToAndFromShouldReturnTrue()
        {
            // ARRANGE

            // ACT
            using var delayResult = DefaultDaemonApp.DelayUntilStateChange(new string[] { "binary_sensor.pir" }, to: "on", from: "off");

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.True(delayResult.Task.Result);
            Assert.Empty(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public async Task DelayStateChangeWithToAndFromWrongShouldNotComplete()
        {
            // ARRANGE

            // ACT
            using var delayResult = DefaultDaemonApp.DelayUntilStateChange(new string[] { "binary_sensor.pir" }, to: "on", from: "unknown");

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.False(delayResult.Task.IsCompleted);
            Assert.Single(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public async Task DelayStateLambdaChangeShouldReturnTrue()
        {
            // ARRANGE

            // ACT
            using var delayResult = DefaultDaemonApp.DelayUntilStateChange(new string[] { "binary_sensor.pir" }, (n, o) => n?.State == "on");

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.True(delayResult.Task.Result);
            Assert.Empty(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public async Task DelayStateLambdaChangeShouldNotComplete()
        {
            // ARRANGE

            // ACT
            using var delayResult = DefaultDaemonApp.DelayUntilStateChange(new string[] { "binary_sensor.pir" }, (n, o) => n?.State == "on");

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "on", toState: "off");

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.False(delayResult.Task.IsCompleted);
            Assert.Single(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public async Task DelayStateChangeCancelShouldReturnFalse()
        {
            // ARRANGE

            // ACT
            using var delayResult = DefaultDaemonApp.DelayUntilStateChange(new string[] { "binary_sensor.pir" }, to: "on");

            delayResult.Cancel();

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.False(delayResult.Task.Result);
            Assert.Empty(DefaultDaemonApp.InternalStateActions);
        }

        [Fact]
        public void GetAppOnMissingAppShouldReturnNull()
        {
            // ARRANGE
            // ACT
            var theApp = DefaultDaemonHost.GetApp("noexist");

            // ASSERT
            Assert.Null(theApp);
        }

        [Fact]
        public async Task ClearShouldReturnNullGetApp()
        {
            // ARRANGE
            var theApp = DefaultDaemonHost.GetApp("app_id");
            Assert.NotNull(theApp);

            // ACT
            await DefaultDaemonHost.UnloadAllApps().ConfigureAwait(false);
            theApp = DefaultDaemonHost.GetApp("app_id");

            // ASSERT
            Assert.Null(theApp);
        }

        [Fact]
        public void EntityShouldReturCorrectValueForArea()
        {
            // ARRANGE
            DefaultDaemonHost._hassDevices["device_id"] = new HassDevice { AreaId = "area_id" };
            DefaultDaemonHost._hassAreas["area_id"] = new HassArea { Name = "Correct name", Id = "area_id" };
            DefaultDaemonHost._hassEntities["light.lamp"] = new HassEntity
            {
                EntityId = "light.lamp",
                DeviceId = "device_id"
            };
            // ACT
            var areaName = DefaultDaemonHost.GetAreaForEntityId("light.lamp");

            // ASSERT
            Assert.Equal("Correct name", areaName);
        }

        [Fact]
        public void EntityShouldReturNullForAreaNotExist()
        {
            // ARRANGE
            DefaultDaemonHost._hassDevices["device_id"] = new HassDevice { AreaId = "area_id" };
            DefaultDaemonHost._hassAreas["area_id"] = new HassArea { Name = "Correct name", Id = "area_id" };
            DefaultDaemonHost._hassEntities["light.lamp"] = new HassEntity
            {
                EntityId = "light.lamp",
                DeviceId = "device_id"
            };
            // ACT
            var areaName = DefaultDaemonHost.GetAreaForEntityId("light.not_exist_lamp");

            // ASSERT
            Assert.Null(areaName);
        }

        [Fact]
        public async Task StateChangeHasAreaInformation()
        {
            // ARRANGE
            DefaultDaemonHost._hassDevices["device_id"] = new HassDevice { AreaId = "area_id" };
            DefaultDaemonHost._hassAreas["area_id"] = new HassArea { Name = "Correct name", Id = "area_id" };
            DefaultDaemonHost._hassEntities["binary_sensor.pir"] = new HassEntity
            {
                EntityId = "binary_sensor.pir",
                DeviceId = "device_id"
            };

            DefaultHassClientMock.AddChangedEvent("binary_sensor.pir", fromState: "off", toState: "on");

            // ACT

            await RunDefauldDaemonUntilCanceled();

            // ASSERT
            Assert.Equal("Correct name", DefaultDaemonHost.InternalState["binary_sensor.pir"].Area);
        }

        [Fact]
        public async Task SetStateShouldKeepSameArea()
        {
            // ARRANGE
            var task = RunDefauldDaemonUntilCanceled();
            await WaitForDefaultDaemonToConnect(DefaultDaemonHost, CancellationToken.None);
            // ACT
            var state = await DefaultDaemonHost.SetStateAsync("light.ligth_in_area", "on", ("attr", "value"));
            await task;
            /// ASSERT
            Assert.Equal("Area", state?.Area);
        }

        [Theory]
        [InlineData(false, null, null, null, null)]
        [InlineData(true, null, 10, null, 10)]
        [InlineData(true, 10, null, 10, null)]
        [InlineData(true, "unavailable", 10, null, 10)]
        [InlineData(true, 10, "unavailable", 10, null)]
        [InlineData(true, 10, 11, 10, 11)]
        [InlineData(true, "hello", "world", "hello", "world")]
        [InlineData(true, (long)10, 10.0d, 10.0d, 10.0d)]
        [InlineData(true, 10.0d, (long)10, 10.0d, 10.0d)]
        public void FixStateTypesShouldReturnCorrectValues(
            bool result, dynamic? newState, dynamic? oldState, dynamic? expectedNewState, dynamic? expectedOldState)
        {
            HassStateChangedEventData state = new HassStateChangedEventData
            {
                NewState = new HassState
                {
                    State = newState
                },
                OldState = new HassState
                {
                    State = oldState
                }
            };

            bool res = NetDaemonHost.FixStateTypes(state);
            Assert.Equal(result, res);
            Assert.Equal(expectedNewState, state.NewState.State);
            Assert.Equal(expectedOldState, state.OldState.State);
        }
    }
}