using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace KdSoft.Quartz
{
    public class ScheduleJobsRequest
    {
        public string QualifiedTypeName { get; set; }

        public IList<object> JobDataItems { get; set; }  // assumed to be JObject instances

        public string CronSchedule { get; set; }

        public ExpBackoffRetrySettings RetrySettings { get; set; }

        public bool OverrideExisting { get; set; }

        public bool Persistent { get; set; }

        public bool RequestRecovery { get; set; }
    }
}
