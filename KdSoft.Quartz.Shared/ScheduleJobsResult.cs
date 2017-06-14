using System.Collections.Generic;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Result returned from scheduling multiple Quartz jobs.
    /// </summary>
    public class ScheduleJobsResult
    {
        /// <summary />
        public IEnumerable<ScheduleJobResult> JobResults { get; set; }
        /// <summary />
        public IEnumerable<ScheduleJobError> Errors { get; set; }
    }

}
