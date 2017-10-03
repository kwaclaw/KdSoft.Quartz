using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Helper routines for use with Quartz scheduler.
    /// </summary>
    public static class SchedulerExtensions
    {
        /// <summary>
        /// Schedules a new job, or conditionally replaces an existing job.
        /// </summary>
        /// <param name="scheduler">Scheduler instance to use.</param>
        /// <param name="jobKey">Job key to use.</param>
        /// <param name="triggerBuilder">Trigger builder to use. It has the trigger configuration.</param>
        /// <param name="jobBuilder">Job builder to use. It has the job configuration, excluding the job data map.</param>
        /// <param name="jobData">Job data.</param>
        /// <param name="overrideExisting">Indicates if an existing job should be replaced.</param>
        /// <returns>Job details and first scheduled run time.</returns>
        public static (IJobDetail, DateTimeOffset?) ScheduleJob(
            this IScheduler scheduler,
            JobKey jobKey,
            TriggerBuilder triggerBuilder,
            JobBuilder jobBuilder,
            JobDataMap jobData,
            bool overrideExisting
        ) {
            ICronTrigger trigger = (ICronTrigger)triggerBuilder.Build();

            if (overrideExisting) {
                bool exists = scheduler.DeleteJob(jobKey);
            }
            else {
                var oldJob = scheduler.GetJobDetail(jobKey);
                if (oldJob != null) {
                    return (oldJob, null);
                }
            }

            var job = jobBuilder
                .WithIdentity(jobKey)
                .UsingJobData(jobData)
                .Build();

            DateTimeOffset? runat = scheduler.ScheduleJob(job, trigger);

            return (job, runat);
        }

        /// <summary>
        /// Schedules a new job, or conditionally replaces an existing job.
        /// </summary>
        /// <param name="scheduler">Scheduler instance to use.</param>
        /// <param name="jobKey">Job key to use.</param>
        /// <param name="triggerBuilder">Trigger builder to use. It has the trigger configuration.</param>
        /// <param name="jobBuilder">Job builder to use. It has the job configuration, excluding the job data map.</param>
        /// <param name="addJobDataToMap">Callback to update the job data map.</param>
        /// <param name="overrideExisting">Indicates if an existing job should be replaced.</param>
        /// <returns>Job details and first scheduled run time.</returns>
        public static (IJobDetail, DateTimeOffset?) ScheduleJob(
            this IScheduler scheduler,
            JobKey jobKey,
            TriggerBuilder triggerBuilder,
            JobBuilder jobBuilder,
            Action<JobDataMap> addJobDataToMap,
            bool overrideExisting
        ) {
            ICronTrigger trigger = (ICronTrigger)triggerBuilder.Build();

            if (overrideExisting) {
                bool exists = scheduler.DeleteJob(jobKey);
            }
            else {
                var oldJob = scheduler.GetJobDetail(jobKey);
                if (oldJob != null) {
                    return (oldJob, null);
                }
            }

            var jdm = new JobDataMap();
            addJobDataToMap(jdm);

            var job = jobBuilder
                .WithIdentity(jobKey)
                .UsingJobData(jdm)
                .Build();

            DateTimeOffset? runat = scheduler.ScheduleJob(job, trigger);

            return (job, runat);
        }

        /// <summary>
        /// Schedules multiple jobs using a shared group name, and a common job and trigger base name.
        /// The job configuration objects must be <see cref="JObject"/> instances.
        /// </summary>
        /// <param name="scheduler">Scheduler instance to use.</param>
        /// <param name="groupName">Shared name for job and trigger group.</param>
        /// <param name="jobBaseName">Base name for job, full names will have an auto-generated suffix.</param>
        /// <param name="triggerBaseName">Base name for trigger, full names will have an auto-generated suffix.</param>
        /// <param name="request">Holds scheduling parameters. The JobDataItems list must hold <see cref="JObject"/> instances.</param>
        /// <returns>Job and/or error information for scheduled jobs.</returns>
        /// <seealso cref="ScheduleJob(IScheduler, JobKey, TriggerBuilder, JobBuilder, JobDataMap, bool)"/>
        /// <seealso cref="ScheduleJobsRequest"/>
        public static (IEnumerable<(IJobDetail, DateTimeOffset?)>, IEnumerable<(JobKey, string)>) ScheduleJobs(
            this IScheduler scheduler,
            string groupName,
            string jobBaseName,
            string triggerBaseName,
            ScheduleJobsRequest request  // object is supposed to mean JObject
        ) {
            var jobResults = new List<(IJobDetail, DateTimeOffset?)>();
            var errorResults = new List<(JobKey, string)>();

            var retrySettingsJson = JsonConvert.SerializeObject(request.RetrySettings);
            var triggerBuilder = TriggerBuilder.Create()
                .WithCronSchedule(request.CronSchedule)
                .UsingJobData(KdSoft.Quartz.QuartzKeys.ExpBackoffRetrySettingsKey, retrySettingsJson)
                .StartNow();

            var jobType = Type.GetType(request.QualifiedTypeName, true, true);
            var jobBuilder = JobBuilder.Create(jobType)
                .StoreDurably(request.Durable)
                .RequestRecovery(request.RequestRecovery);

            for (int indx = 0; indx < request.JobDataItems.Count; indx++) {
                var trbuilder = triggerBuilder.WithIdentity(triggerBaseName + "-" + indx, groupName);
                var jobKey = new JobKey(jobBaseName + "-" + indx, groupName);
                try {
                    var jobConfig = request.JobDataItems[indx]?.Convert();
                    var jobResult = ScheduleJob(
                        scheduler,
                        jobKey,
                        trbuilder,
                        jobBuilder,
                        jobConfig,
                        request.OverrideExisting
                    );
                    jobResults.Add(jobResult);
                }
                catch (Exception ex) {
                    errorResults.Add((jobKey, ex.Message));
                }
            }

            return (jobResults, errorResults);
        }

        /// <summary>
        /// Updates the <see cref="JObject"/> related entries of the job's <see cref="JobDataMap"/>.
        /// </summary>
        /// <param name="scheduler">Scheduler to use.</param>
        /// <param name="jobKey">Job key.</param>
        /// <param name="jobData">Job data dictionary to use for the update.</param>.
        /// <param name="replace">Indicates if the update is a replacement, where the updated job data
        /// will be identical to the <paramref name="jobData"/> argument. If <c>false</c> then only
        /// the properties passed in the argument will be updated and other properties will be unchanged.</param>
        /// <returns>Updated job details.</returns>
        public static IJobDetail UpdateJobData(
            this IScheduler scheduler,
            JobKey jobKey,
            JobDataDictionary jobData,
            bool replace = false
        ) {
            var job = scheduler.GetJobDetail(jobKey);
            if (job == null)
                throw new ArgumentException(string.Format("Job '{0}' does not exist.", jobKey));

            var jdm = job.JobDataMap;
            if (replace) {
                jdm.Clear();
            }
            jdm.UpdateFrom(jobData);
            scheduler.AddJob(job, true);

            return job;
        }

        /// <summary>
        /// Updates the job's <see cref="JobDataMap"/>.
        /// </summary>
        /// <param name="scheduler">Scheduler to use.</param>
        /// <param name="jobKey">Job key.</param>
        /// <param name="addJobDataToMap">Callback that updates the job data map.</param>.
        /// <returns>Updated job details.</returns>
        public static IJobDetail UpdateJobData(
            this IScheduler scheduler,
            JobKey jobKey,
            Action<JobDataMap> addJobDataToMap
        ) {
            var job = scheduler.GetJobDetail(jobKey);
            if (job == null)
                throw new ArgumentException(string.Format("Job '{0}' does not exist.", jobKey));

            var jdm = job.JobDataMap;
            addJobDataToMap(jdm);
            scheduler.AddJob(job, true);

            return job;
        }

        /// <summary>
        /// Removes the specified entries from the job's <see cref="JobDataMap"/>. 
        /// </summary>
        /// <param name="scheduler">Scheduler to use.</param>
        /// <param name="jobKey">Job key.</param>
        /// <param name="jobDataKeys">Keys whose entries to remove from the <see cref="JobDataMap"/>.
        /// If this argument is <see langword="null"/> then all entries will be removed.
        /// If this argument is an empty collection then nothing will be removed.</param>
        public static void RemoveJobData(this IScheduler scheduler, JobKey jobKey, IEnumerable<string> jobDataKeys) {
            var job = scheduler.GetJobDetail(jobKey);
            if (job == null)
                throw new ArgumentException(string.Format("Job '{0}' does not exist.", jobKey));

            var jdm = job.JobDataMap;
            if (jobDataKeys == null) {
                jdm.Clear();
            }
            else {
                foreach (var key in jobDataKeys) {
                    jdm.Remove(key);
                }
            }
            scheduler.AddJob(job, true);
        }
    }
}
