using Quartz;
using Quartz.Spi;
using System;

namespace KdSoft.Quartz
{
    /// <summary>
    /// <see cref="IScheduleBuilder"/> for <see cref="RetryTrigger{T}">retry triggers</see>.
    /// </summary>
    /// <typeparam name="T">Type of retry trigger.</typeparam>
    public class RetryScheduleBuilder<T>: ScheduleBuilder<T> where T: RetryTrigger<T>, new()
    {
        int misfireInstruction = global::Quartz.MisfireInstruction.SmartPolicy;
        Action<T> applySettings;

        /// <inheritdoc/>
        protected RetryScheduleBuilder() { }

        /// <summary>
        /// Factory method.
        /// </summary>
        public static RetryScheduleBuilder<T> Create() {
            return new RetryScheduleBuilder<T>();
        }

        /// <inheritdoc/>
        /// <seealso cref="TriggerBuilder.WithSchedule(IScheduleBuilder)" />
        public override IMutableTrigger Build() {
            var result = new T();
            result.ApplyDefaultSettings();
            applySettings?.Invoke(result);
            result.MisfireInstruction = misfireInstruction;
            return result;
        }

        /// <summary>
        /// Sets "apply settings" callback in fluent style.
        /// </summary>
        /// <param name="applySettings">Callback to apply settings.</param>
        /// <returns>Updated instance.</returns>
        public RetryScheduleBuilder<T> WithApplySettings(Action<T> applySettings) {
            this.applySettings = applySettings;
            return this;
        }

        /// <summary>
        /// If the Trigger misfires, use the <see cref="MisfireInstruction.IgnoreMisfirePolicy" /> instruction.
        /// </summary>
        /// <returns>Updated instance.</returns>
        /// <seealso cref="MisfireInstruction.IgnoreMisfirePolicy" />
        public RetryScheduleBuilder<T> WithMisfireHandlingInstructionIgnoreMisfires() {
            misfireInstruction = MisfireInstruction.IgnoreMisfirePolicy;
            return this;
        }
    }

    /// <summary>
    /// <see cref="TriggerBuilder"/> extensions.
    /// </summary>
    public static class TriggerBuilderExtensions
    {
        /// <summary>
        /// Set the <see cref="RetryScheduleBuilder{T}"/> that will be used to define the Trigger's schedule.
        /// </summary>
        /// <typeparam name="T">Type of retry trigger.</typeparam>
        /// <param name="builder">TriggerBuilder to update.</param>
        /// <param name="action">Delegate to invoke on the <see cref="RetryScheduleBuilder{T}"/>. Optional.</param>
        /// <returns>Updated TriggerBuilder instance.</returns>
        /// <seealso cref="TriggerBuilder.WithSchedule(IScheduleBuilder)" />
        public static TriggerBuilder WithRetrySchedule<T>(this TriggerBuilder builder, Action<RetryScheduleBuilder<T>> action = null) 
            where T: RetryTrigger<T>, new()
        {
            var rsb = RetryScheduleBuilder<T>.Create();
            action?.Invoke(rsb);
            return builder.WithSchedule(rsb);
        }
    }
}