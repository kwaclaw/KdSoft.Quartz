using KdSoft.Quartz.AspNet;
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
        /// Add <see cref="JObject"/> to job data map.
        /// </summary>
        /// <param name="jdm">Job data ma  to use.</param>
        /// <param name="jobj">JSON object to add.</param>
        /// <param name="keyPrefix">Data map keys for JSON object properties will have this prefix, separated by ':'.</param>
        /// <param name="converters">JSON converters to use for serializing the object's properties.</param>
        public static void AddJObject(
            this JobDataMap jdm,
            JObject jobj,
            string keyPrefix,
            params JsonConverter[] converters
        ) {
            var sb = new StringBuilder(keyPrefix);
            sb.Append(':');
            int prefixLen = sb.Length;

            foreach (var prop in jobj.Properties()) {
                sb.Length = prefixLen;
                sb.Append(prop.Name);
                jdm.Add(sb.ToString(), prop.Value.ToString(Formatting.None, converters));
            }
        }

        /// <summary>
        /// Replaces <see cref="JObject"/> in job data map.
        /// </summary>
        /// <param name="jdm">Job data ma  to use.</param>
        /// <param name="jobj">Replacement JSON object.</param>
        /// <param name="keyPrefix">Data map keys for JSON object properties will have this prefix, separated by ':'.</param>
        /// <param name="converters">JSON converters to use for serializing the object's properties.</param>
        /// <remarks>This is a cumulative update, existing properties with the same prefix and name will not be removed
        /// if the new <see cref="JObject"/> does not include them.</remarks>
        public static void PutJObject(
            this JobDataMap jdm,
            JObject jobj,
            string keyPrefix,
            params JsonConverter[] converters
        ) {
            var sb = new StringBuilder(keyPrefix);
            sb.Append(':');
            int prefixLen = sb.Length;

            foreach (var prop in jobj.Properties()) {
                sb.Length = prefixLen;
                sb.Append(prop.Name);
                jdm.Put(sb.ToString(), prop.Value.ToString(Formatting.None, converters));
            }
        }


        /// <summary>
        /// Retrieves <see cref="JObject"/> from job data map.
        /// </summary>
        /// <param name="jdm">Job data map to use.</param>
        /// <param name="keyPrefix">All data map keys with this prefix will be used to identify the properties
        /// for building the return JSON object.</param>
        /// <param name="settings">JSON load settings to use for deserializing the object's properties.</param>
        public static JObject GetJObject(
            this JobDataMap jdm,
            string keyPrefix,
            JsonLoadSettings settings = null
        ) {
            var jobj = new JObject();
            var prefix = keyPrefix + ":";

            foreach (var entry in jdm) {
                if (!entry.Key.StartsWith(prefix))
                    continue;
                var key = entry.Key.Substring(prefix.Length);
                jobj[key] = JToken.Parse(entry.Value.ToString(), settings);
            }
            return jobj;
        }

        /// <summary>
        /// Schedules a new job, or conditionally replaces an existing job.
        /// </summary>
        /// <param name="scheduler">Scheduler instance to use.</param>
        /// <param name="jobKey">Job key to use.</param>
        /// <param name="triggerBuilder">Trigger builder to use. It has the trigger configuration.</param>
        /// <param name="jobBuilder">Job builder to use. It has the job configuration, excluding the job data map.</param>
        /// <param name="jobData">Job data added to the job data map as the single entry.
        /// The job data map key is <see cref="QuartzKeys.JObjectJobDataKey"/></param>
        /// <param name="overrideExisting">Indicates if an existing job should be replaced.</param>
        /// <returns>Job details and first scheduled run time.</returns>
        public static (IJobDetail, DateTimeOffset?) ScheduleJob(
            this IScheduler scheduler,
            JobKey jobKey,
            TriggerBuilder triggerBuilder,
            JobBuilder jobBuilder,
            JObject jobData,
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
            jdm.AddJObject(jobData, QuartzKeys.JObjectJobDataKey);

            var job = jobBuilder
                .WithIdentity(jobKey)
                .UsingJobData(jdm)
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
        /// <seealso cref="ScheduleJob(IScheduler, JobKey, TriggerBuilder, JobBuilder, JObject, bool)"/>
        /// <seealso cref="ScheduleJobsRequest{T}"/>
        public static (IEnumerable<(IJobDetail, DateTimeOffset?)>, IEnumerable<(JobKey, string)>) ScheduleJobs(
            this IScheduler scheduler,
            string groupName,
            string jobBaseName,
            string triggerBaseName,
            ScheduleJobsRequest<object> request  // object is supposed to mean JObject
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
                    var jobConfig = (JObject)request.JobDataItems[indx];
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
        /// Schedules multiple jobs using a shared group name, and a common job and trigger base name.
        /// The job configuration objects must be <see cref="Action{T}"/> instances where <c>T</c> 
        /// is of type <see cref="JobDataMap"/>.
        /// </summary>
        /// <param name="scheduler">Scheduler instance to use.</param>
        /// <param name="groupName">Shared name for job and trigger group.</param>
        /// <param name="jobBaseName">Base name for job, full names will have an auto-generated suffix.</param>
        /// <param name="triggerBaseName">Base name for trigger, full names will have an auto-generated suffix.</param>
        /// <param name="request">Holds scheduling parameters. The JobDataItems list must hold <see cref="Action{T}"/>
        /// instances where <c>T</c>  is of type <see cref="JobDataMap"/>.</param>
        /// <returns>Job and/or error information for scheduled jobs.</returns>
        /// <seealso cref="ScheduleJob(IScheduler, JobKey, TriggerBuilder, JobBuilder, Action{JobDataMap}, bool)"/>
        /// <seealso cref="ScheduleJobsRequest{T}"/>
        public static (IEnumerable<(IJobDetail, DateTimeOffset?)>, IEnumerable<(JobKey, string)>) ScheduleJobs(
            this IScheduler scheduler,
            string groupName,
            string jobBaseName,
            string triggerBaseName,
            ScheduleJobsRequest<Action<JobDataMap>> request
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
                    var jobResult = ScheduleJob(
                        scheduler,
                        jobKey,
                        trbuilder,
                        jobBuilder,
                        request.JobDataItems[indx],
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
        /// Updates the <see cref="JObject"/> related entry of the job's <see cref="JobDataMap"/>.
        /// </summary>
        /// <param name="scheduler">Scheduler to use.</param>
        /// <param name="jobKey">Job key.</param>
        /// <param name="jobConfigUpdate">JSON object to use for the <see cref="JObject"/> entry.
        /// The job data map key is <see cref="QuartzKeys.JObjectJobDataKey"/></param>.
        /// <returns>Updated job details.</returns>
        public static IJobDetail UpdateJobData(
            this IScheduler scheduler,
            JobKey jobKey,
            JObject jobConfigUpdate
        ) {
            var job = scheduler.GetJobDetail(jobKey);
            if (job == null)
                throw new ArgumentException(string.Format("Job '{0}' does not exist.", jobKey));

            var jdm = job.JobDataMap;
            jdm.PutJObject(jobConfigUpdate, QuartzKeys.JObjectJobDataKey);
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
    }
}
