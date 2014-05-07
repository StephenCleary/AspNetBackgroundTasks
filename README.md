![logo](https://raw.githubusercontent.com/StephenCleary/AspNetBackgroundTasks/master/icon.png)

Important Note
===

The [.NET framework 4.5.2 introduced `HostingEnvironment.QueueBackgroundWorkItem`](http://msdn.microsoft.com/en-us/library/ms171868(v=vs.110).aspx#v452), which effectively rendered this project obsolete. I recommend you upgrade to 4.5.2 and use `HostingEnvironment.QueueBackgroundWorkItem` instead of the `BackgroundTaskManager.Run` provided by this library.

This project is placed into maintenance mode only as of 2014-05-07, since it is only useful on .NET 4.5.0 and 4.5.1. I will leave the code up indefinitely since it is one of the few examples of how to register work with the ASP.NET runtime; however, I do not plan on adding any features.

Fire and Forget
===

This is one solution for a "fire and forget" scenario on ASP.NET: when you have enough data to send the response, but you still have additional work to do.

You want to return the response right away, but you also need to start the additional work in the background.

**Important: "fire and forget" on ASP.NET is *dangerous!* Please read the discussion below to understand why!**

How to Use
===

Download and install the [Nito.AspNetBackgroundTasks NuGet package](https://www.nuget.org/packages/Nito.AspNetBackgroundTasks/).

You can start background work by calling `BackgroundTaskManager.Run`:

````C#
BackgroundTaskManager.Run(() =>
{
  MyWork();
});
````

`Run` also understands asynchronous methods:

````C#
BackgroundTaskManager.Run(async () =>
{
  await MyWorkAsync();
});
````

If your background work is long-running or asynchronous, then use the `BackgroundTaskManager.Shutdown` cancellation token. This token is set when the ASP.NET runtime asks to shut down the application.

````C#
BackgroundTaskManager.Run(async () =>
{
  await MyWorkAsync(BackgroundTaskManager.Shutdown);
});
````

Finally, note that *all exceptions are ignored*. If you want to log exceptions, you'll have to do it yourself:

````C#
BackgroundTaskManager.Run(() =>
{
  try
  {
    MyWork();
  }
  catch (Exception ex)
  {
    Log(ex);
  }
});
````

The Problem with Fire and Forget
===

ASP.NET lives around a request lifecycle. If there are no active requests, ASP.NET may decide to unload your application. This is *perfectly normal*; by default ASP.NET will recycle your application every so often just to keep things clean.

This causes a problem for "fire and forget": ASP.NET isn't even aware that the background work is running. `BackgroundTaskManager` will register the background work with the ASP.NET runtime so it is aware of it. However, it's still not a perfectly reliable solution.

The Reliable Solution
===

The **reliable** way to handle this is complicated:

* Add the background work to a reliable queue, such as an [Azure queue](http://azure.microsoft.com/en-us/documentation/articles/storage-dotnet-how-to-use-queues-20/) or [MSMQ](http://msdn.microsoft.com/en-us/library/ms71147.aspx).
* Have an independent worker process that executes the work in the queue, such as an [Azure WebJob](http://azure.microsoft.com/en-us/documentation/articles/web-sites-create-web-jobs/), an [Azure Worker Role](http://msdn.microsoft.com/en-us/library/azure/jj155995.aspx), or a [Win32 Service](http://msdn.microsoft.com/en-us/library/y817hyb6.aspx).
* Determine some means to notify the end-user that the background processing has completed, such as email or [SignalR](http://signalr.net/).

One good, reliable solution is [HangFire](http://hangfire.io).

The Unreliable Solution
===

This solution is simpler, but dangerous: push the background work off to another thread within the ASP.NET process.

This solution is **dangerous** because it is **not reliable**; there is *no guarantee* that the background work will ever be done.

If your system **needs** the background work to be done, then you *must* use the proper, reliable solution above. There is no other way.

However, if you are perfectly OK with occasionally *losing* background work without a trace, then you can use the dangerous solution. For example, you have an on-disk cache that you want to update, but you don't want to slow down the requests. You want to send the response immediately and *then* update the on-disk cache in the background.

To reiterate: "fire and forget" on ASP.NET *actually means* "I don't care whether this work actually gets done or not".

Minimizing Unreliability
===

Just because the solution is unreliable, doesn't mean it must be *totally* unreliable.

AspNetBackgroundTasks is a NuGet package that allows you to register "fire and forget" work with the ASP.NET runtime. The unreliability is minimized; the danger is mitigated. It **is** still unreliable and dangerous, however.

Futher Reading
===

[Phil Haack: "The Dangers of Implementing Recurring Background Tasks in ASP.NET"](http://haacked.com/archive/2011/10/16/the-dangers-of-implementing-recurring-background-tasks-in-asp-net.aspx/)

[Stephen Cleary: "Returning Early from ASP.NET Requests"](http://blog.stephencleary.com/2012/12/returning-early-from-aspnet-requests.html)