using Quartz;
using Quartz.Spi;
using System;

namespace KdSoft.Quartz
{
    public class RetryScheduleBuilder<T>: ScheduleBuilder<T> where T: RetryTrigger<T>, new()
    {
        int misfireInstruction = global::Quartz.MisfireInstruction.SmartPolicy;
        Action<T> applySettings;

        protected RetryScheduleBuilder() { }

        /// <summary>
        /// Create a RetryScheduleBuilder.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns>the new RetryScheduleBuilder</returns>
        public static RetryScheduleBuilder<T> Create() {
            return new RetryScheduleBuilder<T>();
        }

        /// <summary>
        /// Build the actual Trigger -- NOT intended to be invoked by end users,
        /// but will rather be invoked by a TriggerBuilder which this ScheduleBuilder is given to.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <seealso cref="TriggerBuilder.WithSchedule(IScheduleBuilder)" />
        public override IMutableTrigger Build() {
            var result = new T();
            result.ApplyDefaultSettings();
            applySettings?.Invoke(result);
            return result;
        }

        public RetryScheduleBuilder<T> WithApplySettings(Action<T> applySettings) {
            this.applySettings = applySettings;
            return this;
        }

        /// <summary>
        /// If the Trigger misfires, use the
        /// <see cref="MisfireInstruction.IgnoreMisfirePolicy" /> instruction.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns>the updated RetryScheduleBuilder</returns>
        ///  <seealso cref="MisfireInstruction.IgnoreMisfirePolicy" />
        public RetryScheduleBuilder<T> WithMisfireHandlingInstructionIgnoreMisfires() {
            misfireInstruction = MisfireInstruction.IgnoreMisfirePolicy;
            return this;
        }
    }

    public static class TriggerBuilderExtensions
    {
        public static TriggerBuilder WithRetrySchedule<T>(this TriggerBuilder builder, Action<RetryScheduleBuilder<T>> action = null) 
            where T: RetryTrigger<T>, new()
        {
            var rsb = RetryScheduleBuilder<T>.Create();
            action?.Invoke(rsb);
            return builder.WithSchedule(rsb);
        }
    }
}