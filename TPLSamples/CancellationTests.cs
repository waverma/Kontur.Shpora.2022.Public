﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Cancellation
{
    [TestFixture]
    public class CancellationTests
    {
        [Test]
        //CancelBefore
        //CancelAfter
        public void CancelNotStarted()
        {
            using (var cts = new CancellationTokenSource())
            {
                var task = Task.Run(() => { }, cts.Token);
                Thread.Sleep(10);
                cts.Cancel();
                task.Wait();
                Console.WriteLine(task.Status);
            }
        }

        [Test]
        //WaitForCurrent
        //Exception -> Faulted
        public void CancelWhenAll()
        {
            var tasks = new Task[10];
            using (var cts = new CancellationTokenSource())
            {
                tasks[0] = Task.Run(() =>
                                    {
                                        Thread.Sleep(500);
                                        //throw new NotImplementedException();
                                    }, cts.Token);
                for (int i = 1; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                                        {
                                            Thread.Sleep(1000);
                                        }, cts.Token);
                }

                var aggregatedTask = Task.WhenAll(tasks);
                var sw = Stopwatch.StartNew();
                cts.Cancel();
                aggregatedTask.IgnoreExceptions().Wait();
                Console.WriteLine(aggregatedTask.Status);
                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }

        [Test]
        public void ContinueWithDoesntWork()
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                var task = Task.Run(() =>
                                    {
                                        for (int i = 0; i < 10; i++)
                                        {
                                            Thread.Sleep(100);
                                            token.ThrowIfCancellationRequested();
                                        }
                                    }, token);
                var continuation = task.ContinueWith(t =>
                                                     {
                                                         for (int i = 0; i < 10; i++)
                                                         {
                                                             Thread.Sleep(100);
                                                             token.ThrowIfCancellationRequested();
                                                         }
                                                     }, token);
                var sw = Stopwatch.StartNew();

                Thread.Sleep(10);
                cts.Cancel();
                try
                {
                    Task.WaitAll(task, continuation);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                Console.WriteLine(task.Status);
                Console.WriteLine(continuation.Status);
                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }

        private Task CreateCancellableTask(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    Console.WriteLine(i);
                                    Thread.Sleep(100);
                                    if(cancellationToken.IsCancellationRequested)
                                        throw new OperationCanceledException();
                                    //cancellationToken.ThrowIfCancellationRequested();
                                }
                            }, cancellationToken);
        }
        
        [Test]
        //ThrowIf...
        //TaskStatus(Faulted!)
        //Resharper
        //IsCancellationRequested
        //cancel finished task
        //cancel waiting
        public void Polling()
        {
            using (var cts = new CancellationTokenSource())
            {
                var task = CreateCancellableTask(cts.Token);
                Thread.Sleep(600);
                cts.Cancel();
                task.IgnoreExceptions().Wait();
                Console.WriteLine(task.Status);
            }
        }

        [Test]
        public void UsingOldToken()
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                cts.Cancel();
                var task = Task.Run(() => { }, token);
                task.IgnoreExceptions().Wait();
                Console.WriteLine(task.Status);
            }
        }

        [Test]
        public void TestMultipleTokens()
        {
            using (var firstCTS = new CancellationTokenSource())
            using (var secondCTS = new CancellationTokenSource())
            {
                var firstTask = Task.Run(() =>
                                         {
                                             Thread.Sleep(1000);
                                             secondCTS.Token.ThrowIfCancellationRequested();
                                         }, firstCTS.Token);
                Thread.Sleep(100);
                secondCTS.Cancel();
                firstTask.IgnoreExceptions().Wait();
                Console.WriteLine(firstTask.Status);
            }
        }

        [Test]
        //throw in Register
        //long running
        public void Register()
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                var task = Task.Run(() =>
                         {
                             using (token.Register(() =>
                                                       {
                                                           Thread.Sleep(1000);
                                                           Console.WriteLine("Handled cancellation!");
                                                       }))
                             {
                                 Thread.Sleep(100);
                                 token.ThrowIfCancellationRequested();
                             }
                             Thread.Sleep(100);
                             token.ThrowIfCancellationRequested();
                             Console.WriteLine("Finished!");
                         }, token);

                Thread.Sleep(50);
                var sw = Stopwatch.StartNew();
                cts.Cancel();
                Console.WriteLine("Cancelled in {0} ms", sw.ElapsedMilliseconds);
                task.IgnoreExceptions().Wait();
                Console.WriteLine(task.Status);
            }
        }

        private readonly Queue<string> statuses = new Queue<string>();

        private void LogAction(string message, Action action)
        {
            lock (statuses)
            {
                action();
                statuses.Enqueue(message);
            }
        }

        [Test, Ignore]
        //CancellationTokenRegistration.Dispose
        public void CancellationDeadlock()
        {

            using (var cts = new CancellationTokenSource())
            {
                var ct = cts.Token;

                var task = Task.Run(() =>
                {
                    var ctr = default(CancellationTokenRegistration);

                    LogAction("cancellation registered",
                        () => ctr = ct.Register(() =>
                                                {
                                                    LogAction("cancelling", () => { });
                                                }));

                    Thread.Sleep(20); //emulating some real work

                    LogAction("finished",
                        () =>
                        {
                            Thread.Sleep(50);   //emulating some other disposing action
                            ctr.Dispose();
                        });
                }, ct);

                Thread.Sleep(30);
                cts.Cancel();
                task.IgnoreExceptions().Wait();
            }
        }

        private void DoTask(string workerName, Stopwatch workingTime, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => { Console.WriteLine("{0} was interrupted after {1} ms", workerName, workingTime.ElapsedMilliseconds); }))
            {
                Console.WriteLine("{0} worker is concentrating...", workerName);
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(100);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                Console.WriteLine("{0} worker has finished his task!", workerName);
            }
        }

        [Test]
        public void LinkedToken()
        {
            using (var incomingPhoneCall = new CancellationTokenSource())
            using (var incomingEmail = new CancellationTokenSource())
            using (var incomingPhoneOrEmail = CancellationTokenSource.CreateLinkedTokenSource(incomingPhoneCall.Token, incomingEmail.Token))
            {
                var sw = Stopwatch.StartNew();

                var goodWorker = Task.Run(() =>
                {
                    DoTask("good", sw, incomingPhoneCall.Token);
                }, incomingPhoneCall.Token);
                var badWorker = Task.Run(() =>
                {
                    DoTask("bad", sw, incomingPhoneOrEmail.Token);
                }, incomingPhoneOrEmail.Token);

                //Thread.Sleep(300);
                //incomingEmail.Cancel();
                Thread.Sleep(300);
                incomingPhoneCall.Cancel();
                Task.WaitAll(goodWorker.IgnoreExceptions(), badWorker.IgnoreExceptions());
                Console.WriteLine("All tasks finished in {0}. Statuses: good - {1}, bad - {2}", sw.ElapsedMilliseconds, goodWorker.Status, badWorker.Status);
            }
        }
    }

    public static class TaskExtensions
    {
        public static Task IgnoreExceptions(this Task t)
        {
            return t.ContinueWith(_ => { });
        }
    }
}