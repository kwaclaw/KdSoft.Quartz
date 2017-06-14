using System;

namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Result returned from re-scheduling a Quartz job.
    /// </summary>
    public class RescheduleResult
    {
        /// <summary></summary>
        public CronTriggerInfo TriggerInfo { get; set; }
        /// <summary>Next/first scheduled run time.</summary>
        public DateTimeOffset? RunAt { get; set; }
    }
}
