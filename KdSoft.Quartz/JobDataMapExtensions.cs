using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;

namespace KdSoft.Quartz
{
    /// <summary>
    /// Helper routines for use with Quartz scheduler.
    /// </summary>
    public static class JobDataMapExtensions
    {
        static bool IsQuartzKey(string str) {
            foreach (var key in QuartzKeys.JsonSet) {
                if (str.StartsWith(key, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Converts an instance of <see cref="JobDataMap"/> to an instance of <see cref="JobDataDictionary"/>.
        /// If an entry in the job data map matches one of the pre-defined entries in <see cref="QuartzKeys.JsonSet"/>
        /// then it will be deserialized as <see cref="JObject"/> instance.
        /// </summary>
        /// <param name="jdm">Instance to convert.</param>
        /// <returns>Converted instance.</returns>
        public static JobDataDictionary Convert(this JobDataMap jdm) {
            var result = new JobDataDictionary(jdm.Count);
            foreach (var entry in jdm) {
                if (IsQuartzKey(entry.Key)) {
                    var jobj = JsonConvert.DeserializeObject((string)entry.Value);
                    result[entry.Key] = jobj ?? entry.Value;
                }
                else {
                    result[entry.Key] = entry.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Updates an instance of <see cref="JobDataMap"/> from an instance of <see cref="IJobDataDictionary"/>.
        /// Only entries in the job data map with matching keys will be updated.
        /// If an entry in the job data dictionary matches one of the pre-defined entries in <see cref="QuartzKeys.JsonSet"/>,
        /// or if an entry is an instance of type <see cref="JObject"/>, then it will be serialized as JSON.
        /// </summary>
        /// <param name="jdm">Instance to update.</param>
        /// <param name="update">Update to use.</param>
        /// <param name="converters">JsonConverters to use, optional.</param>
        public static void UpdateFrom(this JobDataMap jdm, IJobDataDictionary update, params JsonConverter[] converters) {
            foreach (var entry in update) {
                if (IsQuartzKey(entry.Key)) {
                    if (entry.Value is JToken jtok)
                        jdm[entry.Key] = jtok.ToString(Formatting.None, converters);
                    else
                        jdm[entry.Key] = JsonConvert.SerializeObject(entry.Value, converters);
                }
                else if (entry.Value is JObject jobj) {
                    jdm[QuartzKeys.JObjectJobDataKey + ':' + entry.Key] = jobj.ToString(Formatting.None, converters);
                }
                else if (entry.Value is JArray jarr) {
                    jdm[QuartzKeys.JArrayJobDataKey + ':' + entry.Key] = jarr.ToString(Formatting.None, converters);
                }
                else if (entry.Value is JToken jtok) {
                    jdm[QuartzKeys.JTokenJobDataKey + ':' + entry.Key] = jtok.ToString(Formatting.None, converters);
                }
                else {
                    jdm[entry.Key] = entry.Value;
                }
            }
        }

        /// <summary>
        /// Converts an instance of <see cref="JobDataDictionary"/> to an instance of <see cref="JobDataMap"/>.
        /// If an entry in the job data dictionary matches one of the pre-defined entries in <see cref="QuartzKeys.JsonSet"/>,
        /// or if an entry is an instance of type <see cref="JObject"/>, then it will be serialized as JSON.
        /// </summary>
        /// <param name="jdd">Instance to convert.</param>
        /// <param name="converters">JsonConverters to use, optional.</param>
        /// <returns>Converted instance.</returns>
        public static JobDataMap Convert(this IJobDataDictionary jdd, params JsonConverter[] converters) {
            var result = new JobDataMap();
            result.UpdateFrom(jdd, converters);
            return result;
        }
    }
}
