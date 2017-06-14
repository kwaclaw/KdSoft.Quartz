using System;

namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Settings for exponential backoff retry trigger.
    /// </summary>
    public class ExpBackoffRetrySettings
    {
        /// <summary>
        /// Base time interval to use for calculating retry delays. Also the initial retry delay.
        /// </summary>
        public TimeSpan BackoffBaseInterval { get; set; }

        /// <summary>Maximum number of retries.</summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// Power base to use for calculating exponentially increasing retry delays.
        /// </summary>
        public double PowerBase { get; set; }

        /// <summary>Clones the underlying instance.</summary>
        public ExpBackoffRetrySettings Clone() {
            return (ExpBackoffRetrySettings)MemberwiseClone();
        }
    }
}
