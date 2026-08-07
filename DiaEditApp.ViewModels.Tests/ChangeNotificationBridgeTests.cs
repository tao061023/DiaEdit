namespace DiaEditApp.ViewModels.Tests;

using DiaEditCore.Model;
using Xunit;

public class ChangeNotificationBridgeTests
{
    private sealed class FakeSubscriber : IAffectedByObjectId
    {
        public IReadOnlySet<ObjectId> ObservedIds { get; }
        public int OnAffectedCallCount { get; private set; }
        public Action? OnAffectedAction { get; set; }

        public FakeSubscriber(IReadOnlySet<ObjectId> observedIds) => ObservedIds = observedIds;

        public void OnAffected()
        {
            OnAffectedCallCount++;
            OnAffectedAction?.Invoke();
        }
    }

    [Fact]
    public void OnChanged_NotifiesOnlyOverlappingSubscribers()
    {
        var bridge = new ChangeNotificationBridge();
        var targetId = new StationObjectId(new StationId(1));
        var unrelatedId = new StationObjectId(new StationId(2));

        var matching = new FakeSubscriber(new HashSet<ObjectId> { targetId });
        var nonMatching = new FakeSubscriber(new HashSet<ObjectId> { unrelatedId });

        bridge.Subscribe(matching);
        bridge.Subscribe(nonMatching);

        bridge.OnChanged(new HashSet<ObjectId> { targetId });

        Assert.Equal(1, matching.OnAffectedCallCount);
        Assert.Equal(0, nonMatching.OnAffectedCallCount);
    }

    [Fact]
    public void OnChanged_NotifiesAllMatchingSubscribers()
    {
        var bridge = new ChangeNotificationBridge();
        var targetId = new StationObjectId(new StationId(1));

        var subA = new FakeSubscriber(new HashSet<ObjectId> { targetId });
        var subB = new FakeSubscriber(new HashSet<ObjectId> { targetId });

        bridge.Subscribe(subA);
        bridge.Subscribe(subB);

        bridge.OnChanged(new HashSet<ObjectId> { targetId });

        Assert.Equal(1, subA.OnAffectedCallCount);
        Assert.Equal(1, subB.OnAffectedCallCount);
    }

    [Fact]
    public void OnChanged_DoesNotNotifyUnsubscribedSubscriber()
    {
        var bridge = new ChangeNotificationBridge();
        var targetId = new StationObjectId(new StationId(1));
        var subscriber = new FakeSubscriber(new HashSet<ObjectId> { targetId });

        bridge.Subscribe(subscriber);
        bridge.Unsubscribe(subscriber);

        bridge.OnChanged(new HashSet<ObjectId> { targetId });

        Assert.Equal(0, subscriber.OnAffectedCallCount);
    }

    [Fact]
    public void Subscribe_SameSubscriberTwice_OnlyNotifiedOnce()
    {
        var bridge = new ChangeNotificationBridge();
        var targetId = new StationObjectId(new StationId(1));
        var subscriber = new FakeSubscriber(new HashSet<ObjectId> { targetId });

        bridge.Subscribe(subscriber);
        bridge.Subscribe(subscriber);

        bridge.OnChanged(new HashSet<ObjectId> { targetId });

        Assert.Equal(1, subscriber.OnAffectedCallCount);
    }

    [Fact]
    public void OnChanged_UnsubscribeDuringNotification_DoesNotThrow()
    {
        var bridge = new ChangeNotificationBridge();
        var targetId = new StationObjectId(new StationId(1));

        var victim = new FakeSubscriber(new HashSet<ObjectId> { targetId });
        var trigger = new FakeSubscriber(new HashSet<ObjectId> { targetId });
        trigger.OnAffectedAction = () => bridge.Unsubscribe(victim);

        bridge.Subscribe(victim);
        bridge.Subscribe(trigger);

        var exception = Record.Exception(() => bridge.OnChanged(new HashSet<ObjectId> { targetId }));

        Assert.Null(exception);
    }

    [Fact]
    public void OnChanged_EmptyAffectedIds_NotifiesNoOne()
    {
        var bridge = new ChangeNotificationBridge();
        var subscriber = new FakeSubscriber(new HashSet<ObjectId> { new StationObjectId(new StationId(1)) });
        bridge.Subscribe(subscriber);

        bridge.OnChanged(new HashSet<ObjectId>());

        Assert.Equal(0, subscriber.OnAffectedCallCount);
    }

    [Fact]
    public void OnChanged_NoSubscribers_DoesNotThrow()
    {
        var bridge = new ChangeNotificationBridge();

        var exception = Record.Exception(() => bridge.OnChanged(new HashSet<ObjectId> { new StationObjectId(new StationId(1)) }));

        Assert.Null(exception);
    }
}