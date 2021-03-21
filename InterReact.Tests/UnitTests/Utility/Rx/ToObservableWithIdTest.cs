﻿using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using InterReact.Interfaces;
using InterReact.Messages;
using InterReact.Utility.Rx;
using InterReact.Tests.Utility;
using Xunit;
using Xunit.Abstractions;

namespace InterReact.Tests.UnitTests.Rx
{
    public class ToObservableWithIdTest : BaseReactiveTest
    {
        private const int Id = 42;
        private int subscribeCalls, unsubscribeCalls;
        private readonly Subject<object> subject = new Subject<object>();

        public class SomeClass : IHasRequestId
        {
            public int RequestId { get; } = Id;
            public long Ticks { get; } = Stopwatch.GetTimestamp();
        }
        public class SomeClassEnd : IHasRequestId
        {
            public int RequestId { get; } = Id;
        }

        public ToObservableWithIdTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Test_Ok()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id, requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref subscribeCalls);
                    subject.OnNext(new SomeClass());
                }, requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref unsubscribeCalls);
                });

            var n1 = await observable.Materialize().ToList();
            Assert.Equal(2, n1.Count);
            Assert.Equal(NotificationKind.OnNext, n1[0].Kind);
            Assert.Equal(NotificationKind.OnCompleted, n1[1].Kind);

            var n2 = await observable.Materialize().ToList();
            Assert.Equal(2, n2.Count);
            Assert.Equal(NotificationKind.OnNext, n2[0].Kind);
            Assert.Equal(NotificationKind.OnCompleted, n2[1].Kind);

            Assert.NotEqual(n1[0].Value.Ticks, n2[0].Value.Ticks);
            Assert.Equal(2, subscribeCalls);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Fact]
        public async Task Test_Multi_Ok()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref subscribeCalls);
                    subject.OnNext(new SomeClass());
                    subject.OnNext(new SomeClass());
                    subject.OnNext(new SomeClassEnd());
                },
                requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref unsubscribeCalls);
                }, 
                m => m is SomeClassEnd);
            var list = await observable.ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal(1, subscribeCalls);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Fact]
        public async Task Test_Alert()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id, requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref subscribeCalls);
                    subject.OnNext(new Alert(requestId, 1, "some error"));
                }, requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref unsubscribeCalls);
                });
            var alert = await Assert.ThrowsAsync<Alert>(async () => await observable);
            Assert.Equal(Id, alert.RequestId);
            Assert.Equal(1, subscribeCalls);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Fact]
        public async Task Test_Alert_multi()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref subscribeCalls);
                    subject.OnNext(new Alert(requestId, 1, "some error"));
                },
                requestId =>
                {
                    Assert.Equal(Id, requestId);
                    Interlocked.Increment(ref unsubscribeCalls);
                },
                m => m is SomeClassEnd);
            var alert = await Assert.ThrowsAsync<Alert>(async () => await observable);
            Assert.Equal(Id, alert.RequestId);
            Assert.Equal(1, subscribeCalls);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Fact]
        public async Task Test_Subscribe_Error()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId => { throw new BarrierPostPhaseException();},
                requestId => Interlocked.Increment(ref unsubscribeCalls));
            await Assert.ThrowsAsync<BarrierPostPhaseException>(async () => await observable);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Fact]
        public async Task Test_Source_Error()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId => subject.OnError(new BarrierPostPhaseException()),
                requestId => Interlocked.Increment(ref unsubscribeCalls));
            await Assert.ThrowsAsync<BarrierPostPhaseException>(async () => await observable);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Fact]
        public async Task Test_Source_Completed()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId => subject.OnCompleted(),
                requestId => Interlocked.Increment(ref unsubscribeCalls));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await observable);
            Assert.Equal(0, unsubscribeCalls);
        }

        [Theory,
        InlineData(-1),
        InlineData(0),
        InlineData(1),
        InlineData(10),
        InlineData(100)]
        public async Task Test_Timeout(int ticks)
        {
            var ts = ticks == -1 ? TimeSpan.Zero : TimeSpan.FromTicks(ticks);

            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId => Interlocked.Increment(ref subscribeCalls),
                requestId => Interlocked.Increment(ref unsubscribeCalls));

            await Assert.ThrowsAsync<TimeoutException>(async () => await observable.Timeout(ts));
            await Task.Delay(1);
            Assert.Equal(1, subscribeCalls);
            Assert.Equal(1, unsubscribeCalls);
        }

        [Fact]
        public void Test_Unsubscribe_Error()
        {
            var observable = subject.ToObservable<SomeClass>(() => Id,
                requestId => Interlocked.Increment(ref subscribeCalls), 
                requestId => { throw new BarrierPostPhaseException(); });

            //Assert.Throws<BarrierPostPhaseException>(() => observable.Subscribe().Dispose());
            observable.Subscribe().Dispose();
            Assert.Equal(1, subscribeCalls);
        }
    }

}
