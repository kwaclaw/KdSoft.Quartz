using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KdSoft.Quartz.AspNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace KdSoft.Quartz.WebServices
{
    /// <seealso cref="SchedulerService"/>
    /// <summary>
    /// Controller that exposes functionality of Quartz scheduler.
    /// </summary>
    [ResponseCache(Duration = 0)]
    [Authorize("Administrator")]
    [Route("scheduler/[action]")]
    public class SchedulerController: Controller
    {
        protected readonly IServiceProvider ServiceProvider;

        /// <summary>Wrapper for Quartz scheduler.</summary>
        protected readonly SchedulerService Impl;

        /// <summary>MVC options.</summary>
        protected readonly IOptions<MvcOptions> MvcOptions;

        /// <param name="serviceProvider">Service provider.</param>
        /// <param name="mvcOptions">MVC options.</param>
        public SchedulerController(IServiceProvider serviceProvider, IOptions<MvcOptions> mvcOptions) {
            this.ServiceProvider = serviceProvider;
            this.MvcOptions = mvcOptions;
            this.Impl = new SchedulerService(serviceProvider);
        }

        /// <inheritdoc/>
        public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
            if (!context.ModelState.IsValid) {
                context.Result = new BadRequestObjectResult(context.ModelState);
            }
            return base.OnActionExecutionAsync(context, next);
        }

        /// <seealso cref="SchedulerService.Version"/>
        [HttpGet][AllowAnonymous]
        public string Version() {
            return Impl.Version();
        }

        #region Jobs

        /// <seealso cref="SchedulerService.GetJobGroups"/>
        [HttpGet]
        public virtual IList<string> GetJobGroups() {
            return Impl.GetJobGroups();
        }

        /// <seealso cref="SchedulerService.GetJobKeys(string, string)"/>
        [HttpGet]
        public virtual IEnumerable<QuartzKey> GetJobKeys(string group, string matchType = null) {
            return Impl.GetJobKeys(group, matchType);
        }

        /// <seealso cref="SchedulerService.GetJobs(string, string)"/>
        [HttpGet]
        public virtual IEnumerable<JobInfo> GetJobs(string group, string matchType = null) {
            return Impl.GetJobs(group, matchType);
        }

        /// <seealso cref="SchedulerService.GetJob(QuartzKey)"/>
        [HttpGet]
        public virtual JobInfo GetJob(QuartzKey key) {
            return Impl.GetJob(key);
        }

        /// <seealso cref="SchedulerService.UpdateJob(QuartzKey, JobUpdate)"/>
        [HttpPost]
        public virtual JobInfo UpdateJob(QuartzKey key, [FromBody]JobUpdate update) {
            return Impl.UpdateJob(key, update);
        }

        /// <seealso cref="SchedulerService.DeleteJobs(IEnumerable{QuartzKey})"/>
        [HttpPost]
        public virtual bool DeleteJobs([FromBody]IEnumerable<QuartzKey> jobKeys) {
            return Impl.DeleteJobs(jobKeys);
        }

        /// <seealso cref="SchedulerService.RunJob(QuartzKey)"/>
        [HttpPost]
        public virtual void RunJob(QuartzKey jobKey) {
            Impl.RunJob(jobKey);
        }

        #endregion Jobs

        #region Triggers

        /// <seealso cref="SchedulerService.GetTriggerGroups"/>
        [HttpGet]
        public virtual IList<string> GetTriggerGroups() {
            return Impl.GetTriggerGroups();
        }

        /// <seealso cref="SchedulerService.GetTriggerKeys(string, string)"/>
        [HttpGet]
        public virtual IEnumerable<QuartzKey> GetTriggerKeys(string group, string matchType = null) {
            return Impl.GetTriggerKeys(group, matchType);
        }

        /// <seealso cref="SchedulerService.GetTriggersOfJob(QuartzKey)"/>
        [HttpGet]
        public virtual IEnumerable<CronTriggerInfo> GetTriggersOfJob(QuartzKey jobKey) {
            return Impl.GetTriggersOfJob(jobKey);
        }

        /// <seealso cref="SchedulerService.GetTrigger(QuartzKey)"/>
        [HttpGet]
        public virtual CronTriggerInfo GetTrigger(QuartzKey key) {
            return Impl.GetTrigger(key);
        }

        /// <seealso cref="SchedulerService.GetTriggerState(QuartzKey)"/>
        [HttpGet]
        public virtual string GetTriggerState(QuartzKey key) {
            return Impl.GetTriggerState(key);
        }

        #endregion Triggers

        #region Scheduling

        /// <seealso cref="SchedulerService.ScheduleJobs(string, string, string, ScheduleJobsRequest{object})"/>
        [HttpPost]
        public virtual ScheduleJobsResult ScheduleJobs(
            string groupName,
            string jobBaseName,
            string triggerBaseName,
            [FromBody]ScheduleJobsRequest<object> request
        ) {
            return Impl.ScheduleJobs(groupName, jobBaseName, triggerBaseName, request);
        }

        /// <seealso cref="SchedulerService.UpdateJobData(QuartzKey, object, bool)"/>
        [HttpPost]
        public virtual JobInfo UpdateJobData(QuartzKey jobKey, [FromBody]object jobData, bool replace) {
            return Impl.UpdateJobData(jobKey, jobData, replace);
        }

        /// <seealso cref="SchedulerService.RemoveJobData(QuartzKey)"/>
        [HttpPost]
        public virtual void RemoveJobData(QuartzKey jobKey) {
            Impl.RemoveJobData(jobKey);
        }

        /// <seealso cref="SchedulerService.Start(TimeSpan?, int?)"/>
        [HttpGet]
        [HttpPost]
        public virtual void Start(TimeSpan? delay, int? delaySeconds) {
            Impl.Start(delay, delaySeconds);
        }

        /// <seealso cref="SchedulerService.Pause"/>
        [HttpGet]
        [HttpPost]
        public virtual void Pause() {
            Impl.Pause();
        }

        /// <seealso cref="SchedulerService.RescheduleJob(QuartzKey, CronTriggerUpdate)"/>
        [HttpPost]
        public virtual RescheduleResult RescheduleJob(QuartzKey triggerKey, [FromBody]CronTriggerUpdate update) {
            return Impl.RescheduleJob(triggerKey, update);
        }

        /// <seealso cref="SchedulerService.UnscheduleJobs(IEnumerable{QuartzKey})"/>
        [HttpPost]
        public virtual bool UnscheduleJobs([FromBody]IEnumerable<QuartzKey> triggerKeys) {
            return Impl.UnscheduleJobs(triggerKeys);
        }

        /// <seealso cref="SchedulerService.PauseJob(QuartzKey)"/>
        [HttpPost]
        public virtual void PauseJob(QuartzKey key) {
            Impl.PauseJob(key);
        }

        /// <seealso cref="SchedulerService.PauseJobs(string, string)"/>
        [HttpPost]
        public virtual void PauseJobs(string group, string matchType = null) {
            Impl.PauseJobs(group, matchType);
        }

        /// <seealso cref="SchedulerService.PauseTrigger(QuartzKey)"/>
        [HttpPost]
        public virtual void PauseTrigger(QuartzKey key) {
            Impl.PauseTrigger(key);
        }

        /// <seealso cref="SchedulerService.PauseTriggers(string, string)"/>
        [HttpPost]
        public virtual void PauseTriggers(string group, string matchType = null) {
            Impl.PauseTriggers(group, matchType);
        }

        /// <seealso cref="SchedulerService.GetPausedTriggerGroups"/>
        [HttpGet]
        public virtual ICollection<string> GetPausedTriggerGroups() {
            return Impl.GetPausedTriggerGroups();
        }

        /// <seealso cref="SchedulerService.PauseAll"/>
        [HttpPost]
        public virtual void PauseAll() {
            Impl.PauseAll();
        }

        /// <seealso cref="SchedulerService.ResumeJob(QuartzKey)"/>
        [HttpPost]
        public virtual void ResumeJob(QuartzKey key) {
            Impl.ResumeJob(key);
        }

        /// <seealso cref="SchedulerService.ResumeJobs(string, string)"/>
        [HttpPost]
        public virtual void ResumeJobs(string group, string matchType = null) {
            Impl.ResumeJobs(group, matchType);
        }

        /// <seealso cref="SchedulerService.GetRunningJobs"/>
        [HttpGet]
        public virtual IEnumerable<QuartzKey> GetRunningJobs() {
            return Impl.GetRunningJobs();
        }

        /// <seealso cref="SchedulerService.ResumeTrigger(QuartzKey)"/>
        [HttpPost]
        public virtual void ResumeTrigger(QuartzKey key) {
            Impl.ResumeTrigger(key);
        }

        /// <seealso cref="SchedulerService.ResumeTriggers(string, string)"/>
        [HttpPost]
        public virtual void ResumeTriggers(string group, string matchType = null) {
            Impl.ResumeTriggers(group, matchType);
        }

        /// <seealso cref="SchedulerService.ResumeAll"/>
        [HttpPost]
        public virtual void ResumeAll() {
            Impl.ResumeAll();
        }

        #endregion Scheduling
    }
}
