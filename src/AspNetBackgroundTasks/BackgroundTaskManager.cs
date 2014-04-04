using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using Nito.AsyncEx;

namespace Nito.AspNetBackgroundTasks
{
    /// <summary>
    /// A type that tracks background operations and notifies ASP.NET that they are still in progress.
    /// </summary>
    public sealed class BackgroundTaskManager : IRegisteredObject
    {
        /// <summary>
        /// A cancellation token that is set when ASP.NET is shutting down the app domain.
        /// </summary>
        private readonly CancellationTokenSource _shutdown;

        /// <summary>
        /// A countdown event that is incremented each time a task is registered and decremented each time it completes. When it reaches zero, we are ready to shut down the app domain. 
        /// </summary>
        private readonly AsyncCountdownEvent _count;

        /// <summary>
        /// A task that completes after <see cref="_count"/> reaches zero and the object has been unregistered.
        /// </summary>
        private readonly Task _done;

        private BackgroundTaskManager()
        {
            // Start the count at 1 and decrement it when ASP.NET notifies us we're shutting down.
            _shutdown = new CancellationTokenSource();
            _count = new AsyncCountdownEvent(1);
            _shutdown.Token.Register(() => _count.Signal(), useSynchronizationContext: false);

            // Register the object and unregister it when the count reaches zero.
            HostingEnvironment.RegisterObject(this);
            _done = _count.WaitAsync().ContinueWith(
                _ => HostingEnvironment.UnregisterObject(this),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        void IRegisteredObject.Stop(bool immediate)
        {
            _shutdown.Cancel();
            if (immediate)
                _done.Wait();
        }

        /// <summary>
        /// Registers a task with the ASP.NET runtime.
        /// </summary>
        /// <param name="task">The task to register.</param>
        private void Register(Task task)
        {
            _count.AddCount();
            task.ContinueWith(
                _ => _count.Signal(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// The background task manager for this app domain.
        /// </summary>
        private static readonly BackgroundTaskManager Instance = new BackgroundTaskManager();

        /// <summary>
        /// Gets a cancellation token that is set when ASP.NET is shutting down the app domain.
        /// </summary>
        public static CancellationToken Shutdown { get { return Instance._shutdown.Token; } }

        /// <summary>
        /// Executes an <c>async</c> background operation, registering it with ASP.NET.
        /// </summary>
        /// <param name="operation">The background operation.</param>
        public static void Run(Func<Task> operation)
        {
            Instance.Register(Task.Run(operation));
        }

        /// <summary>
        /// Executes a background operation, registering it with ASP.NET.
        /// </summary>
        /// <param name="operation">The background operation.</param>
        public static void Run(Action operation)
        {
            Instance.Register(Task.Run(operation));
        }
    }
}
