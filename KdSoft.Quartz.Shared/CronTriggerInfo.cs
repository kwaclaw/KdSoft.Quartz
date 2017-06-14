using System;
using System.Collections.Generic;
using KdSoft.Utils;

namespace KdSoft.Quartz
{
  /// <summary>
  /// Trigger update. Subset of <see cref="CronTriggerInfo"/>. Includes additional <see cref="CronExpressionString"/>
  /// and <see cref="TimeZoneId"/> properties for updating CRON triggers.
  /// Any <see cref="ValueWrapper{T}"/> property left <c>null</c> will be ignored for the update.
  /// </summary>
  public class CronTriggerUpdate
  {
    /// <inheritdoc cref="CronTriggerInfo.Description"/>
    public ValueWrapper<string> Description { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.JobDataMap"/>
    public ValueWrapper<IDictionary<string, object>> JobDataMap { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.StartTimeUtc"/>
    public ValueWrapper<DateTimeOffset> StartTimeUtc { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.EndTimeUtc"/>
    public ValueWrapper<DateTimeOffset?> EndTimeUtc { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.Priority"/>
    public ValueWrapper<int> Priority { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.CalendarName"/>
    public ValueWrapper<string> CalendarName { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.CronExpressionString"/>
    public ValueWrapper<string> CronExpressionString { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.TimeZoneId"/>
    public ValueWrapper<string> TimeZoneId { get; set; }
    /// <inheritdoc cref="CronTriggerInfo.MisfireInstruction"/>
    public ValueWrapper<int> MisfireInstruction { get; set; }
  }

  /// <summary>
  /// Trigger information (static), includes information for the trigger's associated job.
  /// If the trigger is a CRON trigger then the <see cref="CronExpressionString"/>
  /// and <see cref="TimeZoneId"/> properties will be set.
  /// </summary>
  public class CronTriggerInfo
  {
    /// <summary>Trigger key.</summary>
    public QuartzKey Key { get; set; }
    /// <summary>Associated job key.</summary>
    public QuartzKey JobKey { get; set; }
    /// <summary>Trigger description, if any.</summary>
    public string Description { get; set; }
    /// <summary><see cref="JobDataMap"/> associated with the Trigger. Changes made to this map during
    /// job execution are not re-persisted, and in fact typically result in an illegal state.</summary>
    public Dictionary<string, object> JobDataMap { get; set; }
    /// <summary>The time at which the trigger's scheduling should start. May or may not be the first actual fire time
    /// of the trigger, depending upon the type of trigger and the settings of the other properties of the trigger.
    /// However the first actual first time will not be before this date.</summary>
    /// <remarks>Setting a value in the past may cause a new trigger to compute a first fire time that is in the past,
    /// which may cause an immediate misfire of the trigger.</remarks>
    public DateTimeOffset StartTimeUtc { get; set; }
    /// <summary>The time on which the trigger must stop firing. This defines the final boundary for trigger firings.
    /// The trigger will not fire after to this date and time. If this value is null, no end time boundary is assumed,
    /// and the trigger can continue indefinitely.</summary>
    public DateTimeOffset? EndTimeUtc { get; set; }
    /// <summary>The priority of a Trigger acts as a tie breaker such that if two Triggers have the same scheduled fire time,
    /// then Quartz will do its best to give the one with the higher priority first access to a worker thread.</summary>
    /// <remarks>If not explicitly set, the default value is 5.</remarks>
    public int? Priority { get; set; }
    /// <summary>Get or set the Calendar with the given name on this Trigger.
    /// Use <c>null</c> when setting to dis-associate a Calendar.</summary>
    public string CalendarName { get; set; }
    /// <summary>Get or set the CRON expression, if this is a CRON trigger.</summary>
    public string CronExpressionString { get; set; }
    /// <summary>Get or set the time zone for which the CronExpressionString will be resolved.
    /// Applies to CRON triggers only.</summary>
    public string TimeZoneId { get; set; }
    /// <summary>Get or set the instruction the Scheduler should be given for handling misfire situations
    /// for this Trigger- the concrete Trigger type that you are using will have defined a set of additional
    /// <see cref="Quartz.MisfireInstruction"/> constants that may be set to this property.<br/>
    /// If not explicitly set, the default value is <see cref="Quartz.MisfireInstruction.InstructionNotSet"/>.</summary>
    /// <seealso cref="Quartz.MisfireInstruction"/>
    public int MisfireInstruction { get; set; }
    /// <summary>Returns the last UTC time at which the Trigger will fire, if the Trigger will repeat indefinitely,
    /// null will be returned. <br/>Note that the return time* may* be in the past.</summary>
    public DateTimeOffset? FinalFireTimeUtc { get; set; }
    /// <summary>Tells whether this Trigger instance can handle events in millisecond precision.</summary>
    public bool HasMillisecondPrecision { get; set; }
  }
}
