namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Key for Quartz job or Quartz trigger.
    /// </summary>
    public class QuartzKey
    {
        /// <summary>Name of job or trigger.</summary>
        public string Name { get; set; }
        /// <summary>Group name of job or trigger.</summary>
        public string Group { get; set; }
    }
}
