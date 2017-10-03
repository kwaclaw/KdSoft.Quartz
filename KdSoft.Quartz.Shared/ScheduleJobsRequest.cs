using System.Collections.Generic;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Arguments to schedule multiple Quartz jobs with the same job type and trigger settings.
    /// </summary>
    public class ScheduleJobsRequest
    {
        /// <summary>Namespace qualified type name of job class.</summary>
        public string QualifiedTypeName { get; set; }

        /// <summary>Job configuration objects.</summary>
        public IList<JobDataDictionary> JobDataItems { get; set; }

        /// <summary>CRON schedule expression.</summary>
        public string CronSchedule { get; set; }

        /// <summary>Trigger retry settings.</summary>
        public ExpBackoffRetrySettings RetrySettings { get; set; }

        /// <summary>Indicates if existing jobs (same job key) should be replaced.</summary>
        public bool OverrideExisting { get; set; }

        /// <summary>
        /// Whether or not the job should remain stored after it is orphaned (no triggers point to it).
        /// </summary>
        public bool Durable { get; set; }

        /// <summary>
        /// Instructs the scheduler whether or not the job should be re-executed if
        /// a 'recovery' or 'fail-over' situation is encountered.
        /// </summary>
        public bool RequestRecovery { get; set; }
    }
}
