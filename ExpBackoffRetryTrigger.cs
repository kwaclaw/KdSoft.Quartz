using Newtonsoft.Json;
using Quartz;
using System;
using System.Diagnostics;

namespace KdSoft.Quartz
{
    /// <summary> 
    /// A concrete <see cref="ITrigger" /> that is used to retry a failed <see cref="IJobDetail" />.
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

        public readonly int YearToGiveupSchedulingAt = 2299;

        DateTimeOffset? nextFireTimeUtc;
        DateTimeOffset? previousFireTimeUtc;
        int timesTriggered;

        /// <summary>
        /// Create a <see cref="ExpBackoffRetryTrigger" /> with no settings.
        /// </summary>
        public ExpBackoffRetryTrigger() { }

        public ExpBackoffRetryTrigger(
            string name,
            string group,
            int maxRetries,
            TimeSpan backoffBaseInterval,
            DateTimeOffset startTimeUtc,
            double exponent = 2.0
        ) : base(name, group) {
            this.MaxRetries = maxRetries;
            this.BackoffBaseInterval = backoffBaseInterval;
            base.StartTimeUtc = startTimeUtc;
            this.PowerBase = exponent;
            this.YearToGiveupSchedulingAt = DateTimeOffset.UtcNow.Year + 290;
        }

        public ExpBackoffRetryTrigger(
            string name,
            int maxRetries,
            TimeSpan backoffBaseInterval,
            DateTimeOffset startTimeUtc,
            double exponent = 2.0
        ) : this(name, null, maxRetries, backoffBaseInterval, startTimeUtc, exponent) { }

        public ExpBackoffRetryTrigger(
            string name,
            string group,
            int maxRetries,
            TimeSpan backoffBaseInterval
        ) : this(name, group, maxRetries, backoffBaseInterval, SystemTime.UtcNow(), 2.0) { }

        public ExpBackoffRetryTrigger(
            string name,
            int maxRetries,
            TimeSpan backoffBaseInterval
        ) : this(name, null, maxRetries, backoffBaseInterval, SystemTime.UtcNow(), 2.0) { }

        TimeSpan backoffBaseInterval;
        public TimeSpan BackoffBaseInterval {
            get { return backoffBaseInterval; }
            set { backoffBaseInterval = value; }
        }

        double powerBase;
        double baseLog;
        public double PowerBase {
            get { return powerBase; }
            set {
                powerBase = value;
                baseLog = Math.Log(value);
            }
        }

        int maxRetries;
        public int MaxRetries {
            get { return maxRetries; }
            set { maxRetries = value; }
        }

        /// <summary>
        /// Get or set the number of times the <see cref="ISimpleTrigger" /> has already
        /// fired.
        /// </summary>
        public virtual int TimesTriggered {
            get { return timesTriggered; }
            set { timesTriggered = value; }
        }

        public override void ApplyDefaultSettings() {
            MaxRetries = 4;
            PowerBase = 2.0;
            BackoffBaseInterval = TimeSpan.FromMinutes(5);
        }

        public override bool ApplyOriginalTriggerSettings(ITrigger trigger) {
            string retrySettingsJson = trigger.JobDataMap.GetString(QuartzKeys.ExpBackoffRetrySettingsKey);
            if (retrySettingsJson != null) {
                var retrySettings = JsonConvert.DeserializeObject<ExpBackoffRetrySettings>(retrySettingsJson);
                this.MaxRetries = retrySettings.MaxRetries;
                this.PowerBase = retrySettings.PowerBase;
                this.BackoffBaseInterval = retrySettings.BackoffBaseInterval;
                return true;
            }
            else {
                return false;
            }
        }

        public override IScheduleBuilder GetScheduleBuilder() {
            var savedMaxRetries = this.MaxRetries;
            var savedPowerBase = this.PowerBase;
            var savedBackoffBaseInterval = this.BackoffBaseInterval;
            Action<ExpBackoffRetryTrigger> applySettings = ebrt => {
                ebrt.MaxRetries = savedMaxRetries;
                ebrt.BackoffBaseInterval = savedBackoffBaseInterval;
                ebrt.PowerBase = savedPowerBase;
            };
            var sb = RetryScheduleBuilder<ExpBackoffRetryTrigger>.Create().WithApplySettings(applySettings);

            switch (MisfireInstruction) {
                case global::Quartz.MisfireInstruction.IgnoreMisfirePolicy:
                    sb.WithMisfireHandlingInstructionIgnoreMisfires();
                    break;
            }

            return sb;
        }

        /// <summary> 
        /// Returns the final UTC time at which the <see cref="ISimpleTrigger" /> will fire,
        /// if MaxRetries is 'RetryIndefinitely' and no end time is set, null will be returned.
        /// <para>
        /// Note that the return time may be in the past.
        /// </para>
        /// </summary>
        public override DateTimeOffset? FinalFireTimeUtc {
            get {
                if (maxRetries == RetryIndefinitely) {
                    if (EndTimeUtc.HasValue)
                        return GetFireTimeBefore(EndTimeUtc);
                    else
                        return null;
                }

                DateTimeOffset lastTrigger = CalculateFireTime(MaxRetries - 1);

                if (!EndTimeUtc.HasValue || lastTrigger < EndTimeUtc.Value) {
                    return lastTrigger;
                }
                else {
                    return GetFireTimeBefore(EndTimeUtc);
                }
            }
        }

        /// <summary>
        /// Tells whether this Trigger instance can handle events
        /// in millisecond precision.
        /// </summary>
        /// <value></value>
        public override bool HasMillisecondPrecision {
            get { return true; }
        }

        /// <summary>
        /// Validates the misfire instruction.
        /// </summary>
        /// <param name="misfireInstruction">The misfire instruction.</param>
        /// <returns></returns>
        protected override bool ValidateMisfireInstruction(int misfireInstruction) {
            if (misfireInstruction < global::Quartz.MisfireInstruction.IgnoreMisfirePolicy) {
                return false;
            }

            if (misfireInstruction > global::Quartz.MisfireInstruction.SmartPolicy) {
                return false;
            }

            return true;
        }

        public override void UpdateAfterMisfire(ICalendar cal) {
            int instr = MisfireInstruction;
            if (instr == global::Quartz.MisfireInstruction.SmartPolicy) {
                nextFireTimeUtc = SystemTime.UtcNow();
            }
        }

        /// <summary>
        /// Called when the <see cref="IScheduler" /> has decided to 'fire'
        /// the trigger (Execute the associated <see cref="IJob" />), in order to
        /// give the <see cref="ITrigger" /> a chance to update itself for its next
        /// triggering (if any).
        /// </summary>
        /// <seealso cref="JobExecutionException" />
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


        /// <summary>
        /// Updates the instance with new calendar.
        /// </summary>
        /// <param name="calendar">The calendar.</param>
        /// <param name="misfireThreshold">The misfire threshold.</param>
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

        /// <summary>
        /// Called by the scheduler at the time a <see cref="ITrigger" /> is first
        /// added to the scheduler, in order to have the <see cref="ITrigger" />
        /// compute its first fire time, based on any associated calendar.
        /// <para>
        /// After this method has been called, <see cref="GetNextFireTimeUtc" />
        /// should return a valid answer.
        /// </para>
        /// </summary>
        /// <returns> 
        /// The first time at which the <see cref="ITrigger" /> will be fired
        /// by the scheduler, which is also the same value <see cref="GetNextFireTimeUtc" />
        /// will return (until after the first firing of the <see cref="ITrigger" />).
        /// </returns>
        public override DateTimeOffset? ComputeFirstFireTimeUtc(ICalendar cal) {
            // assuming that StartTimeUtc is the time of the job failure, we wait
            // for one period before firing the retry trigger for the first time
            nextFireTimeUtc = StartTimeUtc + BackoffBaseInterval;

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

        /// <summary>
        /// Returns the next time at which the <see cref="ISimpleTrigger" /> will
        /// fire. If the trigger will not fire again, <see langword="null" /> will be
        /// returned. The value returned is not guaranteed to be valid until after
        /// the <see cref="ITrigger" /> has been added to the scheduler.
        /// </summary>
        public override DateTimeOffset? GetNextFireTimeUtc() {
            return nextFireTimeUtc;
        }

        public override void SetNextFireTimeUtc(DateTimeOffset? nextFireTime) {
            nextFireTimeUtc = nextFireTime;
        }

        public override void SetPreviousFireTimeUtc(DateTimeOffset? previousFireTime) {
            previousFireTimeUtc = previousFireTime;
        }

        /// <summary>
        /// Returns the previous time at which the <see cref="ISimpleTrigger" /> fired.
        /// If the trigger has not yet fired, <see langword="null" /> will be
        /// returned.
        /// </summary>
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
            var factor = Math.Pow(PowerBase, triggerCount);
            TimeSpan backoff = new TimeSpan((long)(BackoffBaseInterval.Ticks * factor));
            return StartTimeUtc + backoff;
        }

        /// <summary>
        /// Calculates both, the fire times before and after the given time, constrained
        /// by StartTimeUtc and EndTimeUtc.
        /// Note: the resulting 'before' fire time may match the given time, but the 'after' 
        /// fire time must be later than the given time.
        /// </summary>
        /// <param name="atTimeUtc"></param>
        /// <returns></returns>
        FireTimes GetCheckedFireTimes(DateTimeOffset atTimeUtc) {
            DateTimeOffset? before, after;

            var atTicks = atTimeUtc.Ticks;
            var endTicks = (EndTimeUtc ?? DateTimeOffset.MaxValue).Ticks;
            // our unit of time is BackoffBaseInterval; add small number to avoid truncation issues
            var deltaIntervals = ((atTicks + 10 - StartTimeUtc.Ticks) / BackoffBaseInterval.Ticks);

            // if atTimeUtc is at or after EndTimeUtc then we won't have a next fire time
            if (endTicks <= atTicks) {
                before = EndTimeUtc;
                after = null;
            }
            // if atTimeUtc is after StartTimeUtc and before EndTimeUtc then we have both fire times
            else if (deltaIntervals > 0) {
                // add small number (< 1!) to avoid truncation issues due to precision loss
                var triggerCount = Math.Log(deltaIntervals + 0.5) / baseLog;
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

        /// <summary> 
        /// Returns the next UTC time at which the <see cref="ISimpleTrigger" /> will
        /// fire, after the given UTC time. If the trigger will not fire after the given
        /// time, <see langword="null" /> will be returned. Checks MaxRetries.
        /// </summary>
        public override DateTimeOffset? GetFireTimeAfter(DateTimeOffset? afterTimeUtc) {
            if ((timesTriggered > maxRetries) && (maxRetries != RetryIndefinitely)) {
                return null;
            }

            var fireTimes = GetCheckedFireTimes(afterTimeUtc ?? SystemTime.UtcNow());
            return fireTimes.After;
        }

        /// <summary>
        /// Returns the last UTC time at which the <see cref="ISimpleTrigger" /> will
        /// fire, before the given time. If the trigger will not fire before the
        /// given time, <see langword="null" /> will be returned. Does not check MaxRetries.
        /// </summary>
        public virtual DateTimeOffset? GetFireTimeBefore(DateTimeOffset? endUtc) {
            var fireTimes = GetCheckedFireTimes(endUtc ?? SystemTime.UtcNow());
            return fireTimes.Before;
        }

        /// <summary> 
        /// Determines whether or not the <see cref="ISimpleTrigger" /> will occur
        /// again.
        /// </summary>
        public override bool GetMayFireAgain() {
            return GetNextFireTimeUtc().HasValue;
        }

        /// <summary>
        /// Validates whether the properties of the <see cref="IJobDetail" /> are
        /// valid for submission into a <see cref="IScheduler" />.
        /// </summary>
        public override void Validate() {
            base.Validate();

            if (MaxRetries <= 0 && MaxRetries != RetryIndefinitely) {
                throw new SchedulerException("MaxRetries must be greater than zero or have a value of 'RetryIndefinitely'.");
            }
            if (PowerBase <= 1) {
                throw new SchedulerException("PowerBase must be greater than 1.0.");
            }
            if (BackoffBaseInterval <= TimeSpan.Zero) {
                throw new SchedulerException("BackoffBaseInterval must be greater than zero.");
            }
        }
    }
}
