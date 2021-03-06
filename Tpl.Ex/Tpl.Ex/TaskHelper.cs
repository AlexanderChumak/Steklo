﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Tpl.Ex
{
    /// <summary>
    /// Utility methods for <see cref="System.Threading.Tasks.Task"/>.
    /// </summary>
    public static class TaskHelper
    {
        private static readonly Task _defaultCompleted = FromResult<AsyncVoid>(default(AsyncVoid));

        private static readonly Task<object> _completedTaskReturningNull = FromResult<object>(null);

        /// <summary>
        /// Returns a task which retries the task returned by the specified task provider.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="taskProvider">The task returning function.</param>
        /// <param name="maxAttemps">The maximum number of retries.</param>
        /// <param name="shouldRetry">A predicate function which determines whether an exception should cause a retry. The default returns true for all exceptions.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public static Task<TResult> Retry<TResult>(Func<Task<TResult>> taskProvider, int maxAttemps = 5,
            Func<Exception, bool> shouldRetry = null)
        {
            if (shouldRetry == null)
                shouldRetry = ex => true;
            return taskProvider()
                .ContinueWith(task => RetryContinuation(task, taskProvider, maxAttemps, shouldRetry));
        }

        /// <summary>
        /// A task continuation which attempts a retry if a task is faulted.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="task"></param>
        /// <param name="taskProvider"></param>
        /// <param name="attemptsRemaining"></param>
        /// <param name="shouldRetry"></param>
        /// <returns></returns>
        private static TResult RetryContinuation<TResult>(Task<TResult> task, Func<Task<TResult>> taskProvider,
            int attemptsRemaining, Func<Exception, bool> shouldRetry)
        {
            if (task.IsFaulted)
            {
                if (task.Exception != null && (attemptsRemaining > 0 && shouldRetry(task.Exception.InnerException)))
                {
                    return taskProvider()
                        .ContinueWith(
                            retryTask => RetryContinuation(retryTask, taskProvider, --attemptsRemaining, shouldRetry))
                        .Result;
                }
                return task.Result;
            }
            return task.Result;
        }

        // <summary>
        // Returns a canceled Task. The task is completed, IsCanceled = True, IsFaulted = False.
        // </summary>
        public static Task Canceled()
        {
            return CancelCache<AsyncVoid>.Canceled;
        }

        // <summary>
        // Returns a canceled Task of the given type. The task is completed, IsCanceled = True, IsFaulted = False.
        // </summary>
        public static Task<TResult> Canceled<TResult>()
        {
            return CancelCache<TResult>.Canceled;
        }

        // <summary>
        // Returns a completed task that has no result. 
        // </summary>        
        public static Task Completed()
        {
            return _defaultCompleted;
        }

        // <summary>
        // Returns an error task. The task is Completed, IsCanceled = False, IsFaulted = True
        // </summary>
        public static Task FromError(Exception exception)
        {
            return FromError<AsyncVoid>(exception);
        }

        // <summary>
        // Returns an error task of the given type. The task is Completed, IsCanceled = False, IsFaulted = True
        // </summary>
        // <typeparam name="TResult"></typeparam>
        public static Task<TResult> FromError<TResult>(Exception exception)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.SetException(exception);
            return tcs.Task;
        }

        // <summary>
        // Returns an error task of the given type. The task is Completed, IsCanceled = False, IsFaulted = True
        // </summary>
        public static Task FromErrors(IEnumerable<Exception> exceptions)
        {
            return FromErrors<AsyncVoid>(exceptions);
        }

        // <summary>
        // Returns an error task of the given type. The task is Completed, IsCanceled = False, IsFaulted = True
        // </summary>
        public static Task<TResult> FromErrors<TResult>(IEnumerable<Exception> exceptions)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.SetException(exceptions);
            return tcs.Task;
        }

        // <summary>
        // Returns a successful completed task with the given result.  
        // </summary>        
        public static Task<TResult> FromResult<TResult>(TResult result)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.SetResult(result);
            return tcs.Task;
        }

        public static Task<object> NullResult()
        {
            return _completedTaskReturningNull;
        }

        // <summary>
        // Replacement for Task.Factory.StartNew when the code can run synchronously. 
        // We run the code immediately and avoid the thread switch. 
        // This is used to help synchronous code implement task interfaces.
        // </summary>
        // <param name="action">action to run synchronouslyt</param>
        // <param name="token">cancellation token. This is only checked before we run the task, and if cancelled, we immediately return a cancelled task.</param>
        // <returns>a task who result is the result from Func()</returns>
        // <remarks>
        // Avoid calling Task.Factory.StartNew.         
        // This avoids gotchas with StartNew:
        // - ensures cancellation token is checked (StartNew doesn't check cancellation tokens).
        // - Keeps on the same thread. 
        // - Avoids switching synchronization contexts.
        // Also take in a lambda so that we can wrap in a try catch and honor task failure semantics.        
        // </remarks>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "The caught exception type is reflected into a faulted task.")]
        public static Task RunSynchronously(Action action, CancellationToken token = default(CancellationToken))
        {
            if (token.IsCancellationRequested)
            {
                return Canceled();
            }

            try
            {
                action();
                return Completed();
            }
            catch (Exception e)
            {
                return FromError(e);
            }
        }

        // <summary>
        // Replacement for Task.Factory.StartNew when the code can run synchronously. 
        // We run the code immediately and avoid the thread switch. 
        // This is used to help synchronous code implement task interfaces.
        // </summary>
        // <typeparam name="TResult">type of result that task will return.</typeparam>
        // <param name="func">function to run synchronously and produce result</param>
        // <param name="cancellationToken">cancellation token. This is only checked before we run the task, and if cancelled, we immediately return a cancelled task.</param>
        // <returns>a task who result is the result from Func()</returns>
        // <remarks>
        // Avoid calling Task.Factory.StartNew.         
        // This avoids gotchas with StartNew:
        // - ensures cancellation token is checked (StartNew doesn't check cancellation tokens).
        // - Keeps on the same thread. 
        // - Avoids switching synchronization contexts.
        // Also take in a lambda so that we can wrap in a try catch and honor task failure semantics.        
        // </remarks>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "The caught exception type is reflected into a faulted task.")]
        public static Task<TResult> RunSynchronously<TResult>(Func<TResult> func, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Canceled<TResult>();
            }

            try
            {
                return FromResult(func());
            }
            catch (Exception e)
            {
                return FromError<TResult>(e);
            }
        }

        // <summary>
        // Overload of RunSynchronously that avoids a call to Unwrap(). 
        // This overload is useful when func() starts doing some synchronous work and then hits IO and 
        // needs to create a task to finish the work. 
        // </summary>
        // <typeparam name="TResult">type of result that Task will return</typeparam>
        // <param name="func">function that returns a task</param>
        // <param name="cancellationToken">cancellation token. This is only checked before we run the task, and if cancelled, we immediately return a cancelled task.</param>
        // <returns>a task, created by running func().</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "The caught exception type is reflected into a faulted task.")]
        public static Task<TResult> RunSynchronously<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Canceled<TResult>();
            }

            try
            {
                return func();
            }
            catch (Exception e)
            {
                return FromError<TResult>(e);
            }
        }

        // <summary>
        // Update the completion source if the task failed (cancelled or faulted). No change to completion source if the task succeeded. 
        // </summary>
        // <typeparam name="TResult">result type of completion source</typeparam>
        // <param name="tcs">completion source to update</param>
        // <param name="source">task to update from.</param>
        // <returns>true on success</returns>
        public static bool SetIfTaskFailed<TResult>(this TaskCompletionSource<TResult> tcs, Task source)
        {
            switch (source.Status)
            {
                case TaskStatus.Canceled:
                case TaskStatus.Faulted:
                    return tcs.TrySetFromTask(source);
            }

            return false;
        }

        // <summary>
        // Set a completion source from the given Task.
        // </summary>
        // <typeparam name="TResult">result type for completion source.</typeparam>
        // <param name="tcs">completion source to set</param>
        // <param name="source">Task to get values from.</param>
        // <returns>true if this successfully sets the completion source.</returns>
        [SuppressMessage("Microsoft.Web.FxCop", "MW1201:DoNotCallProblematicMethodsOnTask", Justification = "This is a known safe usage of Task.Result, since it only occurs when we know the task's state to be completed.")]
        public static bool TrySetFromTask<TResult>(this TaskCompletionSource<TResult> tcs, Task source)
        {
            if (source.Status == TaskStatus.Canceled)
            {
                return tcs.TrySetCanceled();
            }

            if (source.Status == TaskStatus.Faulted)
            {
                return tcs.TrySetException(source.Exception.InnerExceptions);
            }

            if (source.Status == TaskStatus.RanToCompletion)
            {
                Task<TResult> taskOfResult = source as Task<TResult>;
                return tcs.TrySetResult(taskOfResult == null ? default(TResult) : taskOfResult.Result);
            }

            return false;
        }

        // <summary>
        // Set a completion source from the given Task. If the task ran to completion and the result type doesn't match
        // the type of the completion source, then a default value will be used. This is useful for converting Task into
        // Task{AsyncVoid}, but it can also accidentally be used to introduce data loss (by passing the wrong
        // task type), so please execute this method with care.
        // </summary>
        // <typeparam name="TResult">result type for completion source.</typeparam>
        // <param name="tcs">completion source to set</param>
        // <param name="source">Task to get values from.</param>
        // <returns>true if this successfully sets the completion source.</returns>
        [SuppressMessage("Microsoft.Web.FxCop", "MW1201:DoNotCallProblematicMethodsOnTask", Justification = "This is a known safe usage of Task.Result, since it only occurs when we know the task's state to be completed.")]
        public static bool TrySetFromTask<TResult>(this TaskCompletionSource<Task<TResult>> tcs, Task source)
        {
            if (source.Status == TaskStatus.Canceled)
            {
                return tcs.TrySetCanceled();
            }

            if (source.Status == TaskStatus.Faulted)
            {
                return tcs.TrySetException(source.Exception.InnerExceptions);
            }

            if (source.Status == TaskStatus.RanToCompletion)
            {
                // Sometimes the source task is Task<Task<TResult>>, and sometimes it's Task<TResult>.
                // The latter usually happens when we're in the middle of a sync-block postback where
                // the continuation is a function which returns Task<TResult> rather than just TResult,
                // but the originating task was itself just Task<TResult>. An example of this can be
                // found in TaskExtensions.CatchImpl().
                Task<Task<TResult>> taskOfTaskOfResult = source as Task<Task<TResult>>;
                if (taskOfTaskOfResult != null)
                {
                    return tcs.TrySetResult(taskOfTaskOfResult.Result);
                }

                Task<TResult> taskOfResult = source as Task<TResult>;
                if (taskOfResult != null)
                {
                    return tcs.TrySetResult(taskOfResult);
                }

                return tcs.TrySetResult(TaskHelper.FromResult(default(TResult)));
            }

            return false;
        }

        // <summary>
        // This class is a convenient cache for per-type cancelled tasks
        // </summary>
        private static class CancelCache<TResult>
        {
            public static readonly Task<TResult> Canceled = GetCancelledTask();
            private static Task<TResult> GetCancelledTask()
            {
                TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
                tcs.SetCanceled();
                return tcs.Task;
            }
        }

        // <summary>
        // Used as the T in a "conversion" of a Task into a Task{T}
        // </summary>
        private struct AsyncVoid
        {
        }
    }
}
