using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImageSim.ViewModels
{
    public static class TaskExtensions
    {
        public static Task ForEachAsync<TSource>(this IEnumerable<TSource> source, Func<TSource, Task> itemProcessor, int max_processes)
        {
            var semaphore = new SemaphoreSlim(max_processes, max_processes);
            return Task.WhenAll(source.Select(x => ProcessItem(x, itemProcessor, semaphore)));
        }

        private static async Task ProcessItem<TSource>(TSource item, Func<TSource, Task> itemProcessor, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await itemProcessor(item);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static Task<TResult[]> ForEachAsync<TSource, TResult>(
            this IEnumerable<TSource> source, 
            Func<TSource, Task<TResult>> itemProcessor, 
            int max_processes)
        {
            var semaphore = new SemaphoreSlim(max_processes, max_processes);
            return Task.WhenAll(source.Select(x => ProcessItem(x, itemProcessor, semaphore)));
        }

        private static async Task<TResult> ProcessItem<TSource, TResult>(
            TSource item, 
            Func<TSource, Task<TResult>> itemProcessor, 
            SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                return await itemProcessor(item);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public static Action Debounce(this Action func, int milliseconds = 300)
        {
            CancellationTokenSource cancelTokenSource = null;

            return () =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            func();
                        }
                    }, TaskScheduler.Default);
            };
        }

        public static Action<T> Debounce<T>(this Action<T> func, int milliseconds = 300)
        {
            CancellationTokenSource cancelTokenSource = null;

            return arg =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            func(arg);
                        }
                    }, TaskScheduler.Default);
            };
        }
    }
}
