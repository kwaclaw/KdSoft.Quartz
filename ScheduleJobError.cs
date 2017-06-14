namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Error information returned when scheduling a Quartz job fails.
    /// </summary>
    public class ScheduleJobError
    {
        /// <summary></summary>
        public QuartzKey JobKey { get; set; }
        /// <summary>Error message.</summary>
        public string Message { get; set; }
    }
}
