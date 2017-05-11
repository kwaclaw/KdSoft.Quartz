using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;

namespace KdSoft.Quartz
{
    public static class SchedulerExtensions
    {
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

        public static (IJobDetail, DateTimeOffset?) ScheduleJob(
            this IScheduler scheduler,
            JobKey jobKey,
            TriggerBuilder triggerBuilder,
            JobBuilder jobBuilder,
            JObject jobConfig,
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
            jdm.AddJObject(jobConfig, QuartzKeys.JObjectJobDataKey);

            var job = jobBuilder
                .WithIdentity(jobKey)
                .UsingJobData(jdm)
                .Build();

            DateTimeOffset? runat = scheduler.ScheduleJob(job, trigger);

            return (job, runat);
        }

        public static (IEnumerable<(IJobDetail, DateTimeOffset?)>, IEnumerable<(JobKey, string)>) ScheduleJobs(
            this IScheduler scheduler,
            string groupName,
            string jobBaseName,
            string triggerBaseName,
            ScheduleJobsRequest request
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
                .StoreDurably(request.Persistent)
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
    }
}
