using System;
using System.Collections.Generic;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Job data map keys. They indicate objects serialized to a JSON string.
    /// </summary>
    public static class QuartzKeys
    {
        /// <summary />
        public const string ExpBackoffRetrySettingsKey = "kds:ExpBackoffRetrySettings";
        /// <summary />
        public const string JTokenJobDataKey = "kds:JTokenJobData";
        /// <summary />
        public const string JObjectJobDataKey = "kds:JObjectJobData";
        /// <summary />
        public const string JArrayJobDataKey = "kds:JArrayJobData";

        static readonly ISet<string> jsonSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ExpBackoffRetrySettingsKey,
            JObjectJobDataKey,
            JArrayJobDataKey
        };

        /// <summary>
        /// Set of job data map keys to be used with JSON serialization in the current appliation instance. Not case-sensitive.
        /// </summary>
        public static ISet<string> JsonSet => jsonSet;
    }
}
