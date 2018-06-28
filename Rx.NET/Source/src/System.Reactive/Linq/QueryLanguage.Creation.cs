﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace System.Reactive.Linq
{
    using ObservableImpl;

    internal partial class QueryLanguage
    {
        #region - Create -

        public virtual IObservable<TSource> Create<TSource>(Func<IObserver<TSource>, IDisposable> subscribe)
        {
            return new CreateWithDisposableObservable<TSource>(subscribe);
        }

        sealed class CreateWithDisposableObservable<TSource> : ObservableBase<TSource>
        {
            readonly Func<IObserver<TSource>, IDisposable> subscribe;

            public CreateWithDisposableObservable(Func<IObserver<TSource>, IDisposable> subscribe)
            {
                this.subscribe = subscribe;
            }

            protected override IDisposable SubscribeCore(IObserver<TSource> observer)
            {
                return subscribe(observer) ?? Disposable.Empty;
            }
        }

        public virtual IObservable<TSource> Create<TSource>(Func<IObserver<TSource>, Action> subscribe)
        {
            return new CreateWithActionDisposable<TSource>(subscribe);
        }

        sealed class CreateWithActionDisposable<TSource> : ObservableBase<TSource>
        {
            readonly Func<IObserver<TSource>, Action> subscribe;

            public CreateWithActionDisposable(Func<IObserver<TSource>, Action> subscribe)
            {
                this.subscribe = subscribe;
            }

            protected override IDisposable SubscribeCore(IObserver<TSource> observer)
            {
                var a = subscribe(observer);
                return a != null ? Disposable.Create(a) : Disposable.Empty;
            }
        }

        #endregion

        #region - CreateAsync -

        public virtual IObservable<TResult> Create<TResult>(Func<IObserver<TResult>, CancellationToken, Task> subscribeAsync)
        {
            return new CreateWithTaskTokenObservable<TResult>(subscribeAsync);
        }

        sealed class CreateWithTaskTokenObservable<TResult> : ObservableBase<TResult>
        {
            readonly Func<IObserver<TResult>, CancellationToken, Task> subscribeAsync;

            public CreateWithTaskTokenObservable(Func<IObserver<TResult>, CancellationToken, Task> subscribeAsync)
            {
                this.subscribeAsync = subscribeAsync;
            }

            protected override IDisposable SubscribeCore(IObserver<TResult> observer)
            {
                var cancellable = new CancellationDisposable();

                var taskObservable = subscribeAsync(observer, cancellable.Token).ToObservable();
                var taskCompletionObserver = new TaskCompletionObserver(observer);
                var subscription = taskObservable.Subscribe(taskCompletionObserver);

                return StableCompositeDisposable.Create(cancellable, subscription);
            }

            sealed class TaskCompletionObserver : IObserver<Unit>
            {
                readonly IObserver<TResult> observer;

                public TaskCompletionObserver(IObserver<TResult> observer)
                {
                    this.observer = observer;
                }

                public void OnCompleted()
                {
                    observer.OnCompleted();
                }

                public void OnError(Exception error)
                {
                    observer.OnError(error);
                }

                public void OnNext(Unit value)
                {
                    // deliberately ignored
                }
            }
        }

        public virtual IObservable<TResult> Create<TResult>(Func<IObserver<TResult>, Task> subscribeAsync)
        {
            return Create<TResult>((observer, token) => subscribeAsync(observer));
        }

        public virtual IObservable<TResult> Create<TResult>(Func<IObserver<TResult>, CancellationToken, Task<IDisposable>> subscribeAsync)
        {
            return new CreateWithTaskDisposable<TResult>(subscribeAsync);
        }

        sealed class CreateWithTaskDisposable<TResult> : ObservableBase<TResult>
        {
            readonly Func<IObserver<TResult>, CancellationToken, Task<IDisposable>> subscribeAsync;

            public CreateWithTaskDisposable(Func<IObserver<TResult>, CancellationToken, Task<IDisposable>> subscribeAsync)
            {
                this.subscribeAsync = subscribeAsync;
            }

            protected override IDisposable SubscribeCore(IObserver<TResult> observer)
            {
                var cancellable = new CancellationDisposable();

                var taskObservable = subscribeAsync(observer, cancellable.Token).ToObservable();

                var taskCompletionObserver = new TaskDisposeCompletionObserver(observer);

                //
                // We don't cancel the subscription below *ever* and want to make sure the returned resource gets disposed eventually.
                // Notice because we're using the AnonymousObservable<T> type, we get auto-detach behavior for free.
                //
                taskObservable.Subscribe(taskCompletionObserver);

                return StableCompositeDisposable.Create(cancellable, taskCompletionObserver);
            }

            sealed class TaskDisposeCompletionObserver : IObserver<IDisposable>, IDisposable
            {
                readonly IObserver<TResult> observer;

                IDisposable disposable;

                public TaskDisposeCompletionObserver(IObserver<TResult> observer)
                {
                    this.observer = observer;
                }

                public void Dispose()
                {
                    Disposable.TryDispose(ref disposable);
                }

                public void OnCompleted()
                {
                    observer.OnCompleted();
                }

                public void OnError(Exception error)
                {
                    observer.OnError(error);
                }

                public void OnNext(IDisposable value)
                {
                    Disposable.SetSingle(ref disposable, value);
                }
            }
        }

        public virtual IObservable<TResult> Create<TResult>(Func<IObserver<TResult>, Task<IDisposable>> subscribeAsync)
        {
            return Create<TResult>((observer, token) => subscribeAsync(observer));
        }

        public virtual IObservable<TResult> Create<TResult>(Func<IObserver<TResult>, CancellationToken, Task<Action>> subscribeAsync)
        {
            return new CreateWithTaskActionObservable<TResult>(subscribeAsync);
        }

        sealed class CreateWithTaskActionObservable<TResult> : ObservableBase<TResult>
        {
            readonly Func<IObserver<TResult>, CancellationToken, Task<Action>> subscribeAsync;

            public CreateWithTaskActionObservable(Func<IObserver<TResult>, CancellationToken, Task<Action>> subscribeAsync)
            {
                this.subscribeAsync = subscribeAsync;
            }

            protected override IDisposable SubscribeCore(IObserver<TResult> observer)
            {
                var cancellable = new CancellationDisposable();

                var taskObservable = subscribeAsync(observer, cancellable.Token).ToObservable();

                var taskCompletionObserver = new TaskDisposeCompletionObserver(observer);

                //
                // We don't cancel the subscription below *ever* and want to make sure the returned resource gets disposed eventually.
                // Notice because we're using the AnonymousObservable<T> type, we get auto-detach behavior for free.
                //
                taskObservable.Subscribe(taskCompletionObserver);

                return StableCompositeDisposable.Create(cancellable, taskCompletionObserver);
            }

            sealed class TaskDisposeCompletionObserver : IObserver<Action>, IDisposable
            {
                readonly IObserver<TResult> observer;

                Action disposable;

                static readonly Action DisposedAction = () => { };

                public TaskDisposeCompletionObserver(IObserver<TResult> observer)
                {
                    this.observer = observer;
                }

                public void Dispose()
                {
                    Interlocked.Exchange(ref disposable, DisposedAction)?.Invoke();
                }

                public void OnCompleted()
                {
                    observer.OnCompleted();
                }

                public void OnError(Exception error)
                {
                    observer.OnError(error);
                }

                public void OnNext(Action value)
                {
                    if (Interlocked.CompareExchange(ref disposable, value, null) != null)
                    {
                        value?.Invoke();
                    }
                }
            }
        }

        public virtual IObservable<TResult> Create<TResult>(Func<IObserver<TResult>, Task<Action>> subscribeAsync)
        {
            return Create<TResult>((observer, token) => subscribeAsync(observer));
        }

        #endregion

        #region + Defer +

        public virtual IObservable<TValue> Defer<TValue>(Func<IObservable<TValue>> observableFactory)
        {
            return new Defer<TValue>(observableFactory);
        }

        #endregion

        #region + DeferAsync +

        public virtual IObservable<TValue> Defer<TValue>(Func<Task<IObservable<TValue>>> observableFactoryAsync)
        {
            return Defer(() => StartAsync(observableFactoryAsync).Merge());
        }

        public virtual IObservable<TValue> Defer<TValue>(Func<CancellationToken, Task<IObservable<TValue>>> observableFactoryAsync)
        {
            return Defer(() => StartAsync(observableFactoryAsync).Merge());
        }

        #endregion

        #region + Empty +

        public virtual IObservable<TResult> Empty<TResult>()
        {
            return EmptyDirect<TResult>.Instance;
        }

        public virtual IObservable<TResult> Empty<TResult>(IScheduler scheduler)
        {
            return new Empty<TResult>(scheduler);
        }

        #endregion

        #region + Generate +

        public virtual IObservable<TResult> Generate<TState, TResult>(TState initialState, Func<TState, bool> condition, Func<TState, TState> iterate, Func<TState, TResult> resultSelector)
        {
            return new Generate<TState, TResult>.NoTime(initialState, condition, iterate, resultSelector, SchedulerDefaults.Iteration);
        }

        public virtual IObservable<TResult> Generate<TState, TResult>(TState initialState, Func<TState, bool> condition, Func<TState, TState> iterate, Func<TState, TResult> resultSelector, IScheduler scheduler)
        {
            return new Generate<TState, TResult>.NoTime(initialState, condition, iterate, resultSelector, scheduler);
        }

        #endregion

        #region + Never +

        public virtual IObservable<TResult> Never<TResult>()
        {
            return ObservableImpl.Never<TResult>.Default;
        }

        #endregion

        #region + Range +

        public virtual IObservable<int> Range(int start, int count)
        {
            return Range_(start, count, SchedulerDefaults.Iteration);
        }

        public virtual IObservable<int> Range(int start, int count, IScheduler scheduler)
        {
            return Range_(start, count, scheduler);
        }

        private static IObservable<int> Range_(int start, int count, IScheduler scheduler)
        {
            var longRunning = scheduler.AsLongRunning();
            if (longRunning != null)
            {
                return new RangeLongRunning(start, count, longRunning);
            }
            return new RangeRecursive(start, count, scheduler);
        }

        #endregion

        #region + Repeat +

        public virtual IObservable<TResult> Repeat<TResult>(TResult value)
        {
            return Repeat_(value, SchedulerDefaults.Iteration);
        }

        public virtual IObservable<TResult> Repeat<TResult>(TResult value, IScheduler scheduler)
        {
            return Repeat_(value, scheduler);
        }

        private IObservable<TResult> Repeat_<TResult>(TResult value, IScheduler scheduler)
        {
            var longRunning = scheduler.AsLongRunning();
            if (longRunning != null)
            {
                return new Repeat<TResult>.ForeverLongRunning(value, longRunning);
            }
            return new Repeat<TResult>.ForeverRecursive(value, scheduler);
        }

        public virtual IObservable<TResult> Repeat<TResult>(TResult value, int repeatCount)
        {
            return Repeat_(value, repeatCount, SchedulerDefaults.Iteration);
        }

        public virtual IObservable<TResult> Repeat<TResult>(TResult value, int repeatCount, IScheduler scheduler)
        {
            return Repeat_(value, repeatCount, scheduler);
        }

        private IObservable<TResult> Repeat_<TResult>(TResult value, int repeatCount, IScheduler scheduler)
        {
            var longRunning = scheduler.AsLongRunning();
            if (longRunning != null)
            {
                return new Repeat<TResult>.CountLongRunning(value, repeatCount, longRunning);
            }
            return new Repeat<TResult>.CountRecursive(value, repeatCount, scheduler);
        }

        #endregion

        #region + Return +

        public virtual IObservable<TResult> Return<TResult>(TResult value)
        {
            return new Return<TResult>(value, SchedulerDefaults.ConstantTimeOperations);
        }

        public virtual IObservable<TResult> Return<TResult>(TResult value, IScheduler scheduler)
        {
            return new Return<TResult>(value, scheduler);
        }

        #endregion

        #region + Throw +

        public virtual IObservable<TResult> Throw<TResult>(Exception exception)
        {
            return new Throw<TResult>(exception, SchedulerDefaults.ConstantTimeOperations);
        }

        public virtual IObservable<TResult> Throw<TResult>(Exception exception, IScheduler scheduler)
        {
            return new Throw<TResult>(exception, scheduler);
        }

        #endregion

        #region + Using +

        public virtual IObservable<TSource> Using<TSource, TResource>(Func<TResource> resourceFactory, Func<TResource, IObservable<TSource>> observableFactory) where TResource : IDisposable
        {
            return new Using<TSource, TResource>(resourceFactory, observableFactory);
        }

        #endregion

        #region - UsingAsync -

        public virtual IObservable<TSource> Using<TSource, TResource>(Func<CancellationToken, Task<TResource>> resourceFactoryAsync, Func<TResource, CancellationToken, Task<IObservable<TSource>>> observableFactoryAsync) where TResource : IDisposable
        {
            return Observable.FromAsync<TResource>(resourceFactoryAsync)
                .SelectMany(resource =>
                    Observable.Using<TSource, TResource>(
                        () => resource,
                        resource_ => Observable.FromAsync<IObservable<TSource>>(ct => observableFactoryAsync(resource_, ct)).Merge()
                    )
                );
        }

        #endregion
    }
}
