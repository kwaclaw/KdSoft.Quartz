using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Logging;
using Quartz;

namespace KdSoft.Quartz.WebServices
{
    public class EmptyJob: IJob
    {
        readonly ILog log = LogManager.GetLogger(typeof(EmptyJob));

        public void Execute(IJobExecutionContext context) {
            try {
                var dmp = context.MergedJobDataMap;
                log.Info(dmp.ToString());
            }
            catch (JobExecutionException) {
                throw;
            }
            catch (Exception ex) {
                throw new JobExecutionException(ex);
            }
        }
    }
}

