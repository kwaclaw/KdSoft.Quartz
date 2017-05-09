using Quartz;
using Quartz.Impl.Triggers;
using Quartz.Spi;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Marker interface for retry triggers.
    /// </summary>
    public interface IRetryTrigger: IMutableTrigger { }

    public abstract class RetryTrigger<T>: AbstractTrigger, IRetryTrigger where T: RetryTrigger<T>
    {
        public RetryTrigger() { }

        public RetryTrigger(string name, string group) : base(name, group) { }

        public abstract void ApplyDefaultSettings();

        // this property needs to be set by the concrete sub-type
        public abstract bool ApplyOriginalTriggerSettings(ITrigger trigger);
    }
}
