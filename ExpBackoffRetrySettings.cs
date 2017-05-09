using System;

namespace KdSoft.Quartz
{
    public class ExpBackoffRetrySettings
    {
        public TimeSpan BackoffBaseInterval { get; set; }
        public int MaxRetries { get; set; }
        public double PowerBase { get; set; }
    }
}
