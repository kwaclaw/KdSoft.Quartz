using Quartz;
using Quartz.Impl.Triggers;
using Quartz.Spi;
using System;

namespace KdSoft.Quartz
{
  /// <summary>
  /// Marker interface for retry triggers.
  /// </summary>
  public interface IRetryTrigger: IMutableTrigger { }

    /// <summary>
    /// Trigger that allows retries.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class RetryTrigger<T>: AbstractTrigger, IRetryTrigger where T: RetryTrigger<T>
    {
        /// <seealso cref="AbstractTrigger()"/>
        protected RetryTrigger() { }

        /// <seealso cref="AbstractTrigger(string, string)"/>
        protected RetryTrigger(string name, string group) : base(name, group) { }

        /// <summary>
        /// Applies default retry settings.
        /// </summary>
        public abstract void ApplyDefaultSettings();

        // this property needs to be set by the concrete sub-type

        /// <summary>
        /// Applies retry settings from the original trigger that the retry trigger is based on.
        /// </summary>
        /// <param name="trigger">Trigger to copy settings from.</param>
        /// <returns><c>true</c> if applicable retry settings were found on the original trigger, <c>false</c> otherwise.</returns>
        public abstract bool ApplyOriginalTriggerSettings(ITrigger trigger);
    }
}
