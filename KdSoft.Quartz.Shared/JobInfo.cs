﻿using System;
using System.Collections.Generic;
using KdSoft.Utils;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Quartz job update, subset of <see cref="JobInfo"/>.
    /// Any <see cref="ValueWrapper{T}"/> property left <c>null</c> will be ignored for the update.
    /// </summary>
    public class JobUpdate
    {
        /// <inheritdoc cref="JobInfo.Description"/>
        public ValueWrapper<string> Description { get; set; }
        /// <inheritdoc cref="JobInfo.JobDataMap"/>
        public ValueWrapper<IDictionary<string, object>> JobDataMap { get; set; }
        /// <inheritdoc cref="JobInfo.Durable"/>
        public ValueWrapper<bool> Durable { get; set; }
        /// <inheritdoc cref="JobInfo.RequestRecovery"/>
        public ValueWrapper<bool> RequestRecovery { get; set; }
    }

    /// <summary>
    /// Quartz job information.
    /// </summary>
    public class JobInfo
    {
        /// <summary>Job key.</summary>
        public QuartzKey Key { get; set; }
        /// <summary>CLR type of Quartz job implementation.</summary>
        public Type JobType { get; set; }
        /// <summary>Job description, if any.</summary>
        public string Description { get; set; }
        /// <summary>Dictionary that holds state information for the Quartz job instance.</summary>
        public Dictionary<string, object> JobDataMap { get; set; }
        /// <summary>Whether or not the job should remain stored after it is orphaned (no triggers point to it).</summary>
        public bool Durable { get; set; }
        /// <summary>Instructs the scheduler whether or not the job should be re-executed
        /// if a 'recovery' or 'fail-over' situation is encountered.</summary>
        public bool RequestRecovery { get; set; }
        /// <summary>Indicates if the the scheduler should re-store the JobDataMap when job execution completes.
        /// This may be useful when the Quartz job updates the JobDataMap during execution.</summary>
        /// <remarks>Jobs that have this flag set should also seriously consider setting the <see cref="ConcurrentExecutionDisallowed"/>
        /// flag, to avoid data storage race conditions with concurrently executing job instances.</remarks>
        public bool PersistJobDataAfterExecution { get; set; }
        /// <summary>Indicates if multiple Job instances are allowed to be executed concurrently.</summary>
        public bool ConcurrentExecutionDisallowed { get; set; }
    }
}