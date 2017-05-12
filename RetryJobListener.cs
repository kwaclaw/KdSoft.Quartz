using Quartz;
using Quartz.Listener;
using System;
using System.Diagnostics;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Schedules a RetryTrigger{T} when a job fails, based on the original trigger's settings or configured fallback settings.
    /// Unschedules that trigger once the retried job succeeds. The orginal trigger is not affected or modified.
    /// </summary>
    /// <typeparam name="T">Type of retry trigger to use.</typeparam>
    public class RetryJobListener<T>: JobListenerSupport where T: RetryTrigger<T>, new()
    {
        readonly Action<T> applyFallbackSettings;

        public RetryJobListener(Action<T> applyFallbackSettings = null) {
            this.applyFallbackSettings = applyFallbackSettings;
        }

        public override string Name { get { return "RetryListener"; } }

        // the retry trigger key is derived from the original trigger key
        static TriggerKey RetryTriggerKey(ITrigger trig) {
            return new TriggerKey(trig.Key.Name + "#RETRY", trig.Key.Group);
        }

        public override void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException) {
            var trig = context.Trigger;
            if (JobFailed(jobException)) {
                if (trig is IRetryTrigger) {
                    return;  // we are in retry mode, let it continue
                }

                var retryKey = RetryTriggerKey(trig);
                if (context.Scheduler.GetTrigger(retryKey) == null) {
                    var now = DateTimeOffset.UtcNow;

                    Action<T> applySettings = rt => {
                        if (!rt.ApplyOriginalTriggerSettings(trig)) {
                            applyFallbackSettings?.Invoke(rt);
                        }
                    };

                    var tbuilder = TriggerBuilder.Create()
                        .WithIdentity(retryKey)
                        .WithRetrySchedule<T>(rsb => rsb.WithApplySettings(applySettings))
                        .ForJob(context.JobDetail)  // necessary for adding the trigger to the job
                        .StartAt(now)
                        .EndAt(trig.GetFireTimeAfter(now));
                    var retryTrigger = tbuilder.Build();

                    // we want the old trigger to keep running, so this is the way to add a trigger to the job
                    var scheduledAt = context.Scheduler.ScheduleJob(retryTrigger);
                    Debug.WriteLine("Retry scheduled - " + DateTimeOffset.Now);
                }
            }
            // if the job was successful, then we need to unschedule the current trigger if it is a retry trigger
            else if (trig is IRetryTrigger) {
                var unscheduled = context.Scheduler.UnscheduleJob(trig.Key);
                if (unscheduled)
                    Debug.WriteLine("Retry unscheduled - " + DateTimeOffset.Now);
            }
            // if the job got fired successfully while a retry trigger is active, let's unschedule the retry trigger
            else {
                var retryKey = RetryTriggerKey(trig);
                var unscheduled = context.Scheduler.UnscheduleJob(retryKey);
                if (unscheduled)
                    Debug.WriteLine("Retry unscheduled - " + DateTimeOffset.Now);
            }
        }

        public override void JobToBeExecuted(IJobExecutionContext context) {
            //
        }

        static bool JobFailed(JobExecutionException jobException) {
            return jobException != null;
        }
    }
}
