using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Hosting.Fakes;
using Microsoft.QualityTools.Testing.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AspNetBackgroundTasks.Internal;

namespace UnitTests
{
    [TestClass]
    public class BackgroundTaskManagerUnitTests
    {
        [TestMethod]
        public void BTM_RegistersWithHostEnvironment()
        {
            IRegisteredObject registeredObject = null;

            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = arg => { registeredObject = arg; };
                var instance = new RegisteredTasks();
                var mre = new ManualResetEvent(false);
                instance.Run(() => mre.WaitOne());
                mre.Set();
            }

            Assert.IsNotNull(registeredObject);
        }

        [TestMethod]
        public void BTM_BeforeShutdown_ShutdownNotSignaled()
        {
            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = _ => { };
                var instance = new RegisteredTasks();
                var mre = new ManualResetEvent(false);
                instance.Run(() => mre.WaitOne());
                mre.Set();
                Assert.IsFalse(instance.Shutdown.IsCancellationRequested);
            }
        }

        [TestMethod]
        public void BTM_AfterShutdownRequest_SyncTaskStillRunning_ShutdownIsSignaled()
        {
            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                IRegisteredObject registeredObject = null;
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = obj => { registeredObject = obj; };
                ShimHostingEnvironment.UnregisterObjectIRegisteredObject = _ => { };

                var instance = new RegisteredTasks();
                var mre = new ManualResetEvent(false);
                instance.Run(() => mre.WaitOne());

                registeredObject.Stop(false);
                Assert.IsTrue(instance.Shutdown.IsCancellationRequested);

                mre.Set();
            }
        }

        [TestMethod]
        public void BTM_AfterShutdownRequest_AsyncTaskStillRunning_ShutdownIsSignaled()
        {
            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                IRegisteredObject registeredObject = null;
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = obj => { registeredObject = obj; };
                ShimHostingEnvironment.UnregisterObjectIRegisteredObject = _ => { };

                var instance = new RegisteredTasks();
                var tcs = new TaskCompletionSource<object>();
                instance.Run(() => tcs.Task);

                registeredObject.Stop(false);
                Assert.IsTrue(instance.Shutdown.IsCancellationRequested);

                tcs.TrySetResult(null);
            }
        }

        [TestMethod]
        public void BTM_AfterBlockingShutdown_UnregistersFromHostEnvironment()
        {
            var mutex = new object();
            IRegisteredObject registeredObject = null;

            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = obj =>
                {
                    lock (mutex)
                        registeredObject = obj;
                };
                ShimHostingEnvironment.UnregisterObjectIRegisteredObject = obj =>
                {
                    lock (mutex)
                    {
                        Assert.AreSame(registeredObject, obj);
                        registeredObject = null;
                    }
                };

                var instance = new RegisteredTasks();
                var mre = new ManualResetEvent(false);
                instance.Run(() => mre.WaitOne());
                mre.Set();

                registeredObject.Stop(true);
                lock (mutex)
                {
                    Assert.IsNull(registeredObject);
                }
            }
        }

        [TestMethod]
        public void BTM_BlockingShutdown_WaitsForSyncTaskToExit()
        {
            var mutex = new object();
            IRegisteredObject registeredObject = null;

            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = obj =>
                {
                    lock (mutex)
                        registeredObject = obj;
                };
                ShimHostingEnvironment.UnregisterObjectIRegisteredObject = obj =>
                {
                    lock (mutex)
                    {
                        Assert.AreSame(registeredObject, obj);
                        registeredObject = null;
                    }
                };

                var instance = new RegisteredTasks();
                var mre = new ManualResetEvent(false);
                instance.Run(() => mre.WaitOne());

                var task = Task.Run(() => registeredObject.Stop(true));
                Assert.IsFalse(task.Wait(300));
                lock (mutex)
                    Assert.IsNotNull(registeredObject);
                mre.Set();
            }
        }

        [TestMethod]
        public void BTM_BlockingShutdown_WaitsForAsyncTaskToExit()
        {
            var mutex = new object();
            IRegisteredObject registeredObject = null;

            using (ShimsContext.Create())
            {
                ShimHostingEnvironment.BehaveAsNotImplemented();
                ShimHostingEnvironment.RegisterObjectIRegisteredObject = obj =>
                {
                    lock (mutex)
                        registeredObject = obj;
                };
                ShimHostingEnvironment.UnregisterObjectIRegisteredObject = obj =>
                {
                    lock (mutex)
                    {
                        Assert.AreSame(registeredObject, obj);
                        registeredObject = null;
                    }
                };

                var instance = new RegisteredTasks();
                var tcs = new TaskCompletionSource<object>();
                instance.Run(() => tcs.Task);

                var task = Task.Run(() => registeredObject.Stop(true));
                Assert.IsFalse(task.Wait(300));
                lock (mutex)
                    Assert.IsNotNull(registeredObject);
                tcs.TrySetResult(null);
            }
        }
    }
}
