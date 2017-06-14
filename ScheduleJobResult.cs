using System;

namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Result returned from scheduling a Quartz job.
    /// </summary>
    public class ScheduleJobResult
    {
        /// <summary></summary>
        public JobInfo JobInfo { get; set; }
        /// <summary>Next/first scheduled run time.</summary>
        public DateTimeOffset? RunAt { get; set; }
    }
}
