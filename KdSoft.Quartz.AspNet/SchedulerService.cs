using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using quartz = Quartz;
using quMatchers = Quartz.Impl.Matchers;
using quUtil = Quartz.Util;

namespace KdSoft.Quartz.AspNet
{
    /// <summary>
    /// Wrapper for Quartz scheduler.
    /// </summary>
    public class SchedulerService {
        readonly quartz.ISchedulerFactory SchedulerFactory;

        /// <summary>
        /// Quartz scheduler instance.
        /// </summary>
        public quartz.IScheduler Scheduler => SchedulerFactory.GetScheduler();

        /// <param name="serviceProvider">Service provider.</param>
        public SchedulerService(IServiceProvider serviceProvider) {
            this.SchedulerFactory = serviceProvider.GetService<quartz.ISchedulerFactory>();
        }

        static readonly IMapper mapper;

        static SchedulerService() {
            var config = new MapperConfiguration(cfg => {
                cfg.CreateMap<quartz.JobDataMap, JobDataDictionary>().ConvertUsing(jdm => jdm.Convert());
                cfg.CreateMap<IJobDataDictionary, quartz.JobDataMap>().ConvertUsing(jdd => jdd.Convert());
                cfg.CreateMap<quartz.IJobDetail, JobInfo>()
                    .ForMember(tgt => tgt.RequestRecovery, conf => conf.MapFrom(src => src.RequestsRecovery));
                cfg.CreateMap<QuartzKey, quartz.JobKey>();
                cfg.CreateMap<quartz.JobKey, QuartzKey>();
                cfg.CreateMap<QuartzKey, quartz.TriggerKey>();
                cfg.CreateMap<quartz.TriggerKey, QuartzKey>();
                cfg.CreateMap<quartz.ICronTrigger, CronTriggerInfo>()
                    .ForMember(tgt => tgt.TimeZoneId, conf => conf.MapFrom(src => src.TimeZone.Id));
                cfg.CreateMap<quartz.ITrigger, CronTriggerInfo>();
            });
            mapper = config.CreateMapper();
        }

        quMatchers.GroupMatcher<TKey> GetGroupMatcher<TKey>(string group, string matchType) where TKey : quUtil.Key<TKey> {
            quMatchers.GroupMatcher<TKey> matcher;
            if (group == null) {
                matcher = quMatchers.GroupMatcher<TKey>.AnyGroup();
            }
            else {
                switch ((matchType ?? "").ToUpper()) {
                    case GroupMatchTypes.Equal:
                        matcher = quMatchers.GroupMatcher<TKey>.GroupEquals(group);
                        break;
                    case GroupMatchTypes.StartsWith:
                        matcher = quMatchers.GroupMatcher<TKey>.GroupStartsWith(group);
                        break;
                    case GroupMatchTypes.EndsWith:
                        matcher = quMatchers.GroupMatcher<TKey>.GroupEndsWith(group);
                        break;
                    case GroupMatchTypes.Contains:
                        matcher = quMatchers.GroupMatcher<TKey>.GroupContains(group);
                        break;
                    default:
                        throw new ArgumentException("Invalid match type.", nameof(matchType));
                }
            }

            return matcher;
        }

        /// <summary>
        /// Returns version of Quartz implementation.
        /// </summary>
        public string Version() {
            var assem = this.Scheduler.GetType().Assembly;
            var informalAtt = assem.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return informalAtt != null ? informalAtt.InformationalVersion : assem.GetName().Version.ToString();
        }

        #region Jobs

        /// <seealso cref="quartz.IScheduler.GetJobGroupNames"/>
        public IList<string> GetJobGroups() {
            return Scheduler.GetJobGroupNames();
        }

        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.GetJobKeys"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public IEnumerable<QuartzKey> GetJobKeys(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.JobKey>(group, matchType);
            return Scheduler.GetJobKeys(matcher).Select(jk => mapper.Map<QuartzKey>(jk));
        }

        /// <summary>Returns job details for jobs matching the supplied group name as specified.</summary>
        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.GetJobKeys"/>
        /// <seealso cref="quartz.IScheduler.GetJobDetail"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public IEnumerable<JobInfo> GetJobs(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.JobKey>(group, matchType);
            var jobKeys = Scheduler.GetJobKeys(matcher);
            var jobs = jobKeys.Select(jk => mapper.Map<JobInfo>(Scheduler.GetJobDetail(jk)));
            return jobs;
        }

        /// <seealso cref="quartz.IScheduler.GetJobDetail"/>
        public JobInfo GetJob(QuartzKey key) {
            var job = Scheduler.GetJobDetail(mapper.Map<quartz.JobKey>(key));
            return mapper.Map<JobInfo>(job);
        }

        /// <summary>
        /// Updates selected job details and the <see cref="quartz.JobDataMap"/>.
        /// </summary>
        /// <param name="key">Job key.</param>
        /// <param name="update">Holds update parameters.</param>
        /// <returns>Updated job information.</returns>
        public JobInfo UpdateJob(QuartzKey key, JobUpdate update) {
            var jobKey = mapper.Map<quartz.JobKey>(key);
            var job = Scheduler.GetJobDetail(jobKey);
            if (job == null)
                throw new ArgumentException(string.Format("Job '{0}' does not exist.", jobKey));

            var builder = job.GetJobBuilder();
            if (update.Description != null)
                builder = builder.WithDescription(update.Description.Value);
            if (update.JobDataMap != null) {
                var jdm = job.JobDataMap;
                jdm.UpdateFrom(update.JobDataMap.Value);
                builder = builder.SetJobData(jdm);
            }
            if (update.Durable != null)
                builder = builder.StoreDurably(update.Durable.Value);
            if (update.RequestRecovery != null)
                builder = builder.RequestRecovery(update.RequestRecovery.Value);

            var newJob = builder.Build();
            Scheduler.AddJob(newJob, true);

            return mapper.Map<JobInfo>(newJob);
        }

        /// <seealso cref="quartz.IScheduler.DeleteJobs"/>
        public bool DeleteJobs(IEnumerable<QuartzKey> jobKeys) {
            return Scheduler.DeleteJobs(mapper.Map<List<quartz.JobKey>>(jobKeys));
        }

        /// <seealso cref="quartz.IScheduler.TriggerJob(quartz.JobKey)"/>
        public void RunJob(QuartzKey jobKey) {
            Scheduler.TriggerJob(mapper.Map<quartz.JobKey>(jobKey));
        }

        #endregion Jobs

        #region Triggers

        /// <seealso cref="quartz.IScheduler.GetTriggerGroupNames"/>
        public IList<string> GetTriggerGroups() {
            return Scheduler.GetTriggerGroupNames();
        }

        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.GetTriggerKeys"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public IEnumerable<QuartzKey> GetTriggerKeys(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.TriggerKey>(group, matchType);
            return Scheduler.GetTriggerKeys(matcher).Select(tk => mapper.Map<QuartzKey>(tk));
        }

        /// <returns>Trigger info. Includes CRON expression if applicable..</returns>
        /// <seealso cref="quartz.IScheduler.GetTriggersOfJob"/>
        public IEnumerable<CronTriggerInfo> GetTriggersOfJob(QuartzKey jobKey) {
            var triggers = Scheduler.GetTriggersOfJob(mapper.Map<quartz.JobKey>(jobKey));
            return triggers.Select(tr => {
                if (tr is quartz.ICronTrigger ctr)
                    return mapper.Map<CronTriggerInfo>(ctr);
                else
                    return mapper.Map<CronTriggerInfo>(tr);
            });
        }

        /// <returns>Trigger info. Includes CRON expression if applicable..</returns>
        /// <seealso cref="quartz.IScheduler.GetTrigger"/>
        public CronTriggerInfo GetTrigger(QuartzKey key) {
            var triggerKey = mapper.Map<quartz.TriggerKey>(key);
            var trigger = Scheduler.GetTrigger(triggerKey);
            if (trigger is quartz.ICronTrigger ctr)
                return mapper.Map<CronTriggerInfo>(ctr);
            else
                return mapper.Map<CronTriggerInfo>(trigger);
        }

        /// <seealso cref="quartz.IScheduler.GetTriggerState"/>
        public string GetTriggerState(QuartzKey key) {
            var triggerKey = mapper.Map<quartz.TriggerKey>(key);
            var triggerState = Scheduler.GetTriggerState(triggerKey);
            return triggerState.ToString();
        }

        #endregion Triggers

        #region Scheduling

        /// <seealso cref="SchedulerExtensions.ScheduleJobs(quartz.IScheduler, string, string, string, ScheduleJobsRequest)"/>
        /// <seealso cref="ScheduleJobsResult"/>
        public ScheduleJobsResult ScheduleJobs(
            string groupName,
            string jobBaseName,
            string triggerBaseName,
            ScheduleJobsRequest request
        ) {
            IEnumerable<(quartz.IJobDetail job, DateTimeOffset? runat)> jobResults;
            IEnumerable<(quartz.JobKey jobKey, string message)> errorResults;

            (jobResults, errorResults) = Scheduler.ScheduleJobs(
                    groupName,
                    jobBaseName,
                    triggerBaseName,
                    request
                );

            return new ScheduleJobsResult {
                JobResults = jobResults.Select(jr => new ScheduleJobResult {
                    JobInfo = mapper.Map<JobInfo>(jr.job),
                    RunAt = jr.runat
                }),
                Errors = errorResults.Select(er => new ScheduleJobError {
                    JobKey = mapper.Map<QuartzKey>(er.jobKey),
                    Message = er.message
                })
            };
        }

        /// <summary>
        /// Updates the <see cref="JObject"/> related entries of the job's <see cref="quartz.JobDataMap"/>.
        /// </summary>
        /// <param name="jobKey">Job key.</param>
        /// <param name="jobData">Job data, must be a <see cref="JObject"/> instance.</param>
        /// <param name="replace">Indicates if the update is a replacement, where the updated <see cref="JObject"/>
        /// will be identical to the <paramref name="jobData"/> argument. If <c>false</c> then only
        /// the properties passed in the argument will be updated and other properties will be unchanged.</param>
        /// <seealso cref="SchedulerExtensions.UpdateJobData(quartz.IScheduler, quartz.JobKey, JobDataDictionary, bool)"/>
        public JobInfo UpdateJobData(QuartzKey jobKey, JobDataDictionary jobData, bool replace = false) {
            var job = Scheduler.UpdateJobData(mapper.Map<quartz.JobKey>(jobKey), jobData, replace);
            return mapper.Map<JobInfo>(job);
        }

        /// <param name="jobKey">Job key.</param>
        /// <param name="jobDataKeys">Keys whose entries to remove from the job data dictionary.
        /// If this argument is <see langword="null"/> then all entries will be removed.
        /// If this argument is an empty collection then nothing will be removed.</param>
        /// <seealso cref="SchedulerExtensions.RemoveJobData(quartz.IScheduler, quartz.JobKey, IEnumerable{string})"/>
        public void RemoveJobData(QuartzKey jobKey, IEnumerable<string> jobDataKeys) {
            Scheduler.RemoveJobData(mapper.Map<quartz.JobKey>(jobKey), jobDataKeys);
        }

        /// <param name="delay">Time span to delay.</param>
        /// <param name="delaySeconds">Number of seconds to delay. Alternative delay specification,
        /// ignored if 'delay' argument is not <c>null</c>.</param>
        /// <seealso cref="quartz.IScheduler.StartDelayed"/>
        /// <seealso cref="quartz.IScheduler.Start"/>
        public void Start(TimeSpan? delay, int? delaySeconds) {
            if (delay == null && delaySeconds != null)
                delay = TimeSpan.FromSeconds(delaySeconds.Value);

            if (delay == null)
                Scheduler.Start();
            else
                Scheduler.StartDelayed(delay.Value);
        }

        /// <seealso cref="quartz.IScheduler.Standby"/>
        public void Pause() {
            Scheduler.Standby();
        }

        /// <summary>
        /// Reschedules job, including updates to the trigger's <see cref="quartz.JobDataMap"/>.
        /// </summary>
        /// <param name="triggerKey">Key of job's trigger.</param>
        /// <param name="update">Holds update parameters.</param>
        /// <returns>Updated trigger information, including associated job information.</returns>
        /// <remarks>Only CRON triggers are supported.</remarks>
        /// <seealso cref="quartz.CronScheduleBuilder"/>
        public RescheduleResult RescheduleJob(QuartzKey triggerKey, CronTriggerUpdate update) {
            var quartzTriggerKey = mapper.Map<quartz.TriggerKey>(triggerKey);
            var trigger = Scheduler.GetTrigger(quartzTriggerKey);
            if (trigger == null)
                throw new ArgumentException(string.Format("Trigger '{0}' does not exist.", quartzTriggerKey));
            var cronTrigger = trigger as quartz.ICronTrigger;
            if (cronTrigger == null)
                throw new ArgumentException(string.Format("Trigger '{0}' is not a CRON trigger.", quartzTriggerKey));

            var builder = cronTrigger.GetTriggerBuilder();
            if (update.Description != null)
                builder = builder.WithDescription(update.Description.Value);
            if (update.StartTimeUtc != null)
                builder = builder.StartAt(update.StartTimeUtc.Value);
            if (update.EndTimeUtc != null)
                builder = builder.EndAt(update.EndTimeUtc.Value);
            if (update.Priority != null)
                builder = builder.WithPriority(update.Priority.Value);

            quartz.JobDataMap jdm = null;
            if (update.RetrySettings != null) {
                var retrySettingsJson = JsonConvert.SerializeObject(update.RetrySettings.Value);
                jdm = jdm ?? new quartz.JobDataMap();
                jdm.Add(KdSoft.Quartz.QuartzKeys.ExpBackoffRetrySettingsKey, retrySettingsJson);
            }
            if (update.JobDataMap != null) {
                jdm = jdm ?? new quartz.JobDataMap();
                jdm.UpdateFrom(update.JobDataMap.Value);
            }
            if (jdm != null)
                builder = builder.UsingJobData(jdm);

            if (update.CalendarName != null)
                builder = builder.ModifiedByCalendar(update.CalendarName.Value);
            if (update.CronExpressionString != null) {
                var cronSchedule = quartz.CronScheduleBuilder.CronSchedule(update.CronExpressionString.Value);
                if (update.TimeZoneId != null) {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(update.TimeZoneId.Value);
                    cronSchedule = cronSchedule.InTimeZone(timeZone);
                    if (update.MisfireInstruction != null) {
                        switch (update.MisfireInstruction.Value) {
                            case MisfireInstruction.IgnoreMisfirePolicy:
                                cronSchedule = cronSchedule.WithMisfireHandlingInstructionIgnoreMisfires();
                                break;
                            case MisfireInstruction.CronTrigger.DoNothing:
                                cronSchedule = cronSchedule.WithMisfireHandlingInstructionDoNothing();
                                break;
                            case MisfireInstruction.CronTrigger.FireOnceNow:
                                cronSchedule = cronSchedule.WithMisfireHandlingInstructionFireAndProceed();
                                break;
                            case MisfireInstruction.InstructionNotSet: // same value as sched.MisfireInstruction.SmartPolicy
                                break;
                            default:
                                throw new ArgumentException("Invalid misfire instruction.", nameof(update));
                        }
                    }
                }
                builder = builder.WithSchedule(cronSchedule);
            }

            var newTrigger = (quartz.ICronTrigger)builder.Build();
            var runat = Scheduler.RescheduleJob(quartzTriggerKey, newTrigger);
            return new RescheduleResult { TriggerInfo = mapper.Map<CronTriggerInfo>(newTrigger), RunAt = runat };
        }

        /// <seealso cref="quartz.IScheduler.UnscheduleJobs"/>
        public bool UnscheduleJobs(IEnumerable<QuartzKey> triggerKeys) {
            return Scheduler.UnscheduleJobs(mapper.Map<List<quartz.TriggerKey>>(triggerKeys));
        }

        /// <seealso cref="quartz.IScheduler.PauseJob"/>
        public void PauseJob(QuartzKey key) {
            Scheduler.PauseJob(mapper.Map<quartz.JobKey>(key));
        }

        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.PauseJobs"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public void PauseJobs(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.JobKey>(group, matchType);
            Scheduler.PauseJobs(matcher);
        }

        /// <seealso cref="quartz.IScheduler.PauseTrigger"/>
        public void PauseTrigger(QuartzKey key) {
            Scheduler.PauseTrigger(mapper.Map<quartz.TriggerKey>(key));
        }

        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.PauseTriggers"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public void PauseTriggers(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.TriggerKey>(group, matchType);
            Scheduler.PauseTriggers(matcher);
        }

        /// <seealso cref="quartz.IScheduler.GetPausedTriggerGroups"/>
        public ICollection<string> GetPausedTriggerGroups() {
            return Scheduler.GetPausedTriggerGroups();
        }

        /// <seealso cref="quartz.IScheduler.PauseAll"/>
        public void PauseAll() {
            Scheduler.PauseAll();
        }

        /// <seealso cref="quartz.IScheduler.ResumeJob"/>
        public void ResumeJob(QuartzKey key) {
            Scheduler.ResumeJob(mapper.Map<quartz.JobKey>(key));
        }

        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.ResumeJobs"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public void ResumeJobs(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.JobKey>(group, matchType);
            Scheduler.ResumeJobs(matcher);
        }

        /// <seealso cref="quartz.IScheduler.GetCurrentlyExecutingJobs"/>
        public IEnumerable<QuartzKey> GetRunningJobs() {
            var contexts = Scheduler.GetCurrentlyExecutingJobs();
            return contexts.Select(ctx => mapper.Map<QuartzKey>(ctx.JobDetail.Key));
        }

        /// <seealso cref="quartz.IScheduler.ResumeTrigger"/>
        public void ResumeTrigger(QuartzKey key) {
            Scheduler.ResumeTrigger(mapper.Map<quartz.TriggerKey>(key));
        }

        /// <param name="group">Name of group to match. Can be a partial group name.</param>
        /// <param name="matchType">Specifies how group names must be <see cref="GroupMatchTypes">matched</see>
        /// against the specified 'group' argument.</param>
        /// <seealso cref="quartz.IScheduler.ResumeTriggers"/>
        /// <seealso cref="quMatchers.GroupMatcher{TKey}"/>
        public void ResumeTriggers(string group, string matchType = null) {
            var matcher = GetGroupMatcher<quartz.TriggerKey>(group, matchType);
            Scheduler.ResumeTriggers(matcher);
        }

        /// <seealso cref="quartz.IScheduler.ResumeAll"/>
        public void ResumeAll() {
            Scheduler.ResumeAll();
        }

        #endregion Scheduling
    }
}