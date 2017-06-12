using KdSoft.Models.Shared.Scheduling;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Diagnostics;

namespace KdSoft.Quartz
{
    /// <summary> 
    /// A concrete <see cref="ITrigger" /> that is used to retry a failed <see cref="IJob" />.
    /// </summary>
    /// <seealso cref="ITrigger" />
    /// <seealso cref="ICronTrigger" />
    [Serializable]
    public class ExpBackoffRetryTrigger: RetryTrigger<ExpBackoffRetryTrigger>
    {
        /// <summary>
        /// Used to indicate the 'retry count' of the trigger is indefinite. Or in other words,
        /// the trigger should repeat continually until the trigger's ending timestamp.
        /// </summary>
        public const int RetryIndefinitely = -1;

        /// <summary>Limit for scheduling retries.</summary>
        public readonly int YearToGiveupSchedulingAt = 2299;

        DateTimeOffset? nextFireTimeUtc;
        DateTimeOffset? previousFireTimeUtc;
        int timesTriggered;

        /// <summary>
        /// Create a <see cref="ExpBackoffRetryTrigger" /> with no settings.
        /// </summary>
        public ExpBackoffRetryTrigger() { }

        /// <summary>
        /// Create a <see cref="ExpBackoffRetryTrigger" />.
        /// </summary>
        /// <param name="name">Name of trigger.</param>
        /// <param name="group">Name of group trigger belongs to.</param>
        /// <param name="retrySettings">Retry settings.</param>
        /// <param name="startTimeUtc">Time of first job execution.</param>
        public ExpBackoffRetryTrigger(
            string name,
            string group,
            ExpBackoffRetrySettings retrySettings,
            DateTimeOffset startTimeUtc
        ) : base(name, group) {
            this.RetrySettings = retrySettings ?? new ExpBackoffRetrySettings {
                MaxRetries = 4,
                PowerBase = 2.0,
                BackoffBaseInterval = TimeSpan.FromMinutes(5),
            };
            base.StartTimeUtc = startTimeUtc;
            this.YearToGiveupSchedulingAt = DateTimeOffset.UtcNow.Year + 290;
        }

        /// <summary>
        /// Create a <see cref="ExpBackoffRetryTrigger" /> in the default group.
        /// </summary>
        /// <param name="name">Name of trigger.</param>
        /// <param name="retrySettings">Retry settings.</param>
        /// <param name="startTimeUtc">Time of first job execution.</param>
        public ExpBackoffRetryTrigger(
            string name,
            ExpBackoffRetrySettings retrySettings,
            DateTimeOffset startTimeUtc
        ) : this(name, null, retrySettings, startTimeUtc) { }

        /// <summary>
        /// Create a <see cref="ExpBackoffRetryTrigger" /> that starts now.
        /// </summary>
        /// <param name="name">Name of trigger.</param>
        /// <param name="group">Name of group trigger belongs to.</param>
        /// <param name="retrySettings">Retry settings.</param>
        public ExpBackoffRetryTrigger(
            string name,
            string group,
            ExpBackoffRetrySettings retrySettings
        ) : this(name, group, retrySettings, SystemTime.UtcNow()) { }

        /// <summary>Retry settings.</summary>
        public ExpBackoffRetrySettings RetrySettings { get; private set; }

        /// <summary>
        /// Get or set the number of times the <see cref="ExpBackoffRetryTrigger" /> has already fired.
        /// </summary>
        public virtual int TimesTriggered { get; set; }

        /// <inheritdoc/>
        public override void ApplyDefaultSettings() {
            this.RetrySettings = new ExpBackoffRetrySettings {
                MaxRetries = 4,
                PowerBase = 2.0,
                BackoffBaseInterval = TimeSpan.FromMinutes(5),
            };
        }

        /// <inheritdoc/>
        public override bool ApplyOriginalTriggerSettings(ITrigger trigger) {
            string retrySettingsJson = trigger.JobDataMap.GetString(QuartzKeys.ExpBackoffRetrySettingsKey);
            if (retrySettingsJson != null) {
                var retrySettings = JsonConvert.DeserializeObject<ExpBackoffRetrySettings>(retrySettingsJson);
                this.RetrySettings = retrySettings;
                return true;
            }
            else {
                return false;
            }
        }

        /// <inheritdoc/>
        public override IScheduleBuilder GetScheduleBuilder() {
            var savedSettings = this.RetrySettings;
            Action<ExpBackoffRetryTrigger> applySettings = ebrt => {
                ebrt.RetrySettings = savedSettings.Clone();
            };
            var sb = RetryScheduleBuilder<ExpBackoffRetryTrigger>.Create().WithApplySettings(applySettings);

            switch (MisfireInstruction) {
                case global::Quartz.MisfireInstruction.IgnoreMisfirePolicy:
                    sb.WithMisfireHandlingInstructionIgnoreMisfires();
                    break;
            }

            return sb;
        }

        /// <inheritdoc/>
        public override DateTimeOffset? FinalFireTimeUtc {
            get {
                if (RetrySettings.MaxRetries == RetryIndefinitely) {
                    if (EndTimeUtc.HasValue)
                        return GetFireTimeBefore(EndTimeUtc);
                    else
                        return null;
                }

                DateTimeOffset lastTrigger = CalculateFireTime(RetrySettings.MaxRetries - 1);

                if (!EndTimeUtc.HasValue || lastTrigger < EndTimeUtc.Value) {
                    return lastTrigger;
                }
                else {
                    return GetFireTimeBefore(EndTimeUtc);
                }
            }
        }

        /// <inheritdoc/>
        public override bool HasMillisecondPrecision {
            get { return true; }
        }

        /// <inheritdoc/>
        protected override bool ValidateMisfireInstruction(int misfireInstruction) {
            if (misfireInstruction < global::Quartz.MisfireInstruction.IgnoreMisfirePolicy) {
                return false;
            }

            if (misfireInstruction > global::Quartz.MisfireInstruction.SmartPolicy) {
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override void UpdateAfterMisfire(ICalendar cal) {
            int instr = MisfireInstruction;
            if (instr == global::Quartz.MisfireInstruction.SmartPolicy) {
                nextFireTimeUtc = SystemTime.UtcNow();
            }
        }

        /// <inheritdoc/>
        public override void Triggered(ICalendar cal) {
            timesTriggered++;
            previousFireTimeUtc = nextFireTimeUtc;
            nextFireTimeUtc = GetFireTimeAfter(nextFireTimeUtc);
            string fmt = "Triggered {0}: {1} - {2}";
            Debug.WriteLine(string.Format(fmt, timesTriggered, previousFireTimeUtc?.ToLocalTime(), nextFireTimeUtc?.ToLocalTime()));

            while (nextFireTimeUtc.HasValue && cal != null && !cal.IsTimeIncluded(nextFireTimeUtc.Value)) {
                nextFireTimeUtc = GetFireTimeAfter(nextFireTimeUtc);

                if (!nextFireTimeUtc.HasValue) {
                    break;
                }

                //avoid infinite loop
                if (nextFireTimeUtc.Value.Year > YearToGiveupSchedulingAt) {
                    nextFireTimeUtc = null;
                }
            }
        }


        /// <inheritdoc/>
		public override void UpdateWithNewCalendar(ICalendar calendar, TimeSpan misfireThreshold) {
            nextFireTimeUtc = GetFireTimeAfter(previousFireTimeUtc);

            if (nextFireTimeUtc == null || calendar == null) {
                return;
            }

            DateTimeOffset now = SystemTime.UtcNow();
            while (nextFireTimeUtc.HasValue && !calendar.IsTimeIncluded(nextFireTimeUtc.Value)) {
                nextFireTimeUtc = GetFireTimeAfter(nextFireTimeUtc);

                if (!nextFireTimeUtc.HasValue) {
                    break;
                }

                //avoid infinite loop
                if (nextFireTimeUtc.Value.Year > YearToGiveupSchedulingAt) {
                    nextFireTimeUtc = null;
                }

                if (nextFireTimeUtc != null && nextFireTimeUtc.Value < now) {
                    TimeSpan diff = now - nextFireTimeUtc.Value;
                    if (diff >= misfireThreshold) {
                        nextFireTimeUtc = GetFireTimeAfter(nextFireTimeUtc);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override DateTimeOffset? ComputeFirstFireTimeUtc(ICalendar cal) {
            // assuming that StartTimeUtc is the time of the job failure, we wait
            // for one period before firing the retry trigger for the first time
            nextFireTimeUtc = StartTimeUtc + RetrySettings.BackoffBaseInterval;

            while (cal != null && !cal.IsTimeIncluded(nextFireTimeUtc.Value)) {
                nextFireTimeUtc = GetFireTimeAfter(nextFireTimeUtc);

                if (!nextFireTimeUtc.HasValue) {
                    break;
                }

                //avoid infinite loop
                if (nextFireTimeUtc.Value.Year > YearToGiveupSchedulingAt) {
                    return null;
                }
            }

            return nextFireTimeUtc;
        }

        /// <inheritdoc/>
        public override DateTimeOffset? GetNextFireTimeUtc() {
            return nextFireTimeUtc;
        }

        /// <inheritdoc/>
        public override void SetNextFireTimeUtc(DateTimeOffset? nextFireTime) {
            nextFireTimeUtc = nextFireTime;
        }

        /// <inheritdoc/>
        public override void SetPreviousFireTimeUtc(DateTimeOffset? previousFireTime) {
            previousFireTimeUtc = previousFireTime;
        }

        /// <inheritdoc/>
        public override DateTimeOffset? GetPreviousFireTimeUtc() {
            return previousFireTimeUtc;
        }

        struct FireTimes
        {
            public FireTimes(DateTimeOffset? before, DateTimeOffset? after) {
                this.Before = before;
                this.After = after;
            }

            public readonly DateTimeOffset? Before;
            public readonly DateTimeOffset? After;
        }

        DateTimeOffset CalculateFireTime(long triggerCount) {
            var factor = Math.Pow(RetrySettings.PowerBase, triggerCount);
            TimeSpan backoff = new TimeSpan((long)(RetrySettings.BackoffBaseInterval.Ticks * factor));
            return StartTimeUtc + backoff;
        }

        /// <summary>
        /// Calculates both, the fire times before and after the given time, constrained by StartTimeUtc and EndTimeUtc.
        /// Note: the resulting 'before' fire time may match the given time, but the 'after' fire time must be later than the given time.
        /// </summary>
        /// <param name="atTimeUtc">Point in time for which the fire times should be calculated.</param>
        /// <returns></returns>
        FireTimes GetCheckedFireTimes(DateTimeOffset atTimeUtc) {
            DateTimeOffset? before, after;

            var atTicks = atTimeUtc.Ticks;
            var endTicks = (EndTimeUtc ?? DateTimeOffset.MaxValue).Ticks;
            // our unit of time is BackoffBaseInterval; add small number to avoid truncation issues
            var deltaIntervals = ((atTicks + 10 - StartTimeUtc.Ticks) / RetrySettings.BackoffBaseInterval.Ticks);

            // if atTimeUtc is at or after EndTimeUtc then we won't have a next fire time
            if (endTicks <= atTicks) {
                before = EndTimeUtc;
                after = null;
            }
            // if atTimeUtc is after StartTimeUtc and before EndTimeUtc then we have both fire times
            else if (deltaIntervals > 0) {
                // add small number (< 1!) to avoid truncation issues due to precision loss
                var triggerCount = Math.Log(deltaIntervals + 0.5) / Math.Log(RetrySettings.PowerBase);
                before = CalculateFireTime((long)triggerCount);
                after = CalculateFireTime((long)triggerCount + 1);
            }
            else if (deltaIntervals == 0) {
                before = StartTimeUtc;
                after = CalculateFireTime(0);
            }
            // if atTimeUtc is before StartTimeUtc then we have no before fire time
            else {
                before = null;
                after = StartTimeUtc;
            }

            return new FireTimes(before, after);
        }

        /// <inheritdoc/>
        public override DateTimeOffset? GetFireTimeAfter(DateTimeOffset? afterTimeUtc) {
            if ((timesTriggered > RetrySettings.MaxRetries) && (RetrySettings.MaxRetries != RetryIndefinitely)) {
                return null;
            }

            var fireTimes = GetCheckedFireTimes(afterTimeUtc ?? SystemTime.UtcNow());
            return fireTimes.After;
        }

        /// <summary>
        /// Returns the last UTC time at which the <see cref="ISimpleTrigger" /> will fire, before the given time.
        /// If the trigger will not fire before the given time, <see langword="null" /> will be returned.
        /// Does not check MaxRetries.
        /// </summary>
        public virtual DateTimeOffset? GetFireTimeBefore(DateTimeOffset? endUtc) {
            var fireTimes = GetCheckedFireTimes(endUtc ?? SystemTime.UtcNow());
            return fireTimes.Before;
        }

        /// <inheritdoc/>
        public override bool GetMayFireAgain() {
            return GetNextFireTimeUtc().HasValue;
        }

        /// <inheritdoc/>
        public override void Validate() {
            base.Validate();

            if (RetrySettings.MaxRetries <= 0 && RetrySettings.MaxRetries != RetryIndefinitely) {
                throw new SchedulerException("MaxRetries must be greater than zero or have a value of 'RetryIndefinitely'.");
            }
            if (RetrySettings.PowerBase <= 1) {
                throw new SchedulerException("PowerBase must be greater than 1.0.");
            }
            if (RetrySettings.BackoffBaseInterval <= TimeSpan.Zero) {
                throw new SchedulerException("BackoffBaseInterval must be greater than zero.");
            }
        }
    }
}
