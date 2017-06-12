using Common.Logging.Configuration;
using Common.Logging.Simple;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace KdSoft.Quartz.AspNet
{
    public class AspNetLoggerFactoryAdapter: AbstractSimpleLoggerFactoryAdapter
    {
        readonly ILoggerFactory aspNetFactory;

        public AspNetLoggerFactoryAdapter(ILoggerFactory aspNetFactory): base(null) {
            this.aspNetFactory = aspNetFactory;
        }

        public AspNetLoggerFactoryAdapter(ILoggerFactory aspNetFactory, NameValueCollection properties): base(properties) {
            this.aspNetFactory = aspNetFactory;
        }

        public AspNetLoggerFactoryAdapter(ILoggerFactory aspNetFactory, Common.Logging.LogLevel level, bool showDateTime, bool showLogName, bool showLevel, string dateTimeFormat)
            : base(level, showDateTime, showLogName, showLevel, dateTimeFormat)
        {
            this.aspNetFactory = aspNetFactory;
        }

        /// <inheritdoc/>
        protected override Common.Logging.ILog CreateLogger(string name) {
            var aspNetLogger = aspNetFactory.CreateLogger(name);
            return new AspNetLoggerWrapper(aspNetLogger, name, this.Level, this.ShowLevel, this.ShowDateTime, this.ShowLogName, this.DateTimeFormat);
        }

        protected override Common.Logging.ILog CreateLogger(string name, Common.Logging.LogLevel level, bool showLevel, bool showDateTime, bool showLogName, string dateTimeFormat) {
            var aspNetLogger = aspNetFactory.CreateLogger(name);
            return new AspNetLoggerWrapper(aspNetLogger, name, level, showLevel, showDateTime, showLogName, dateTimeFormat);
        }
    }

    public class AspNetLoggerWrapper: AbstractSimpleLogger
    {
        readonly ILogger aspNetLogger;

        public AspNetLoggerWrapper(ILogger aspNetLogger, string logName, Common.Logging.LogLevel logLevel, bool showLevel, bool showDateTime, bool showLogName, string dateTimeFormat)
            : base(logName, logLevel, showLevel, showDateTime, showLogName, dateTimeFormat)
        {
            this.aspNetLogger = aspNetLogger;
        }

        protected override bool IsLevelEnabled(Common.Logging.LogLevel level) {
            return aspNetLogger.IsEnabled(Map2AspNetLogLevel(level));
        }

        protected override void WriteInternal(Common.Logging.LogLevel level, object message, Exception exception) {
            Func<object, Exception, string> formatMsg = (obj, ex) => {
                var sb = new StringBuilder();
                FormatOutput(sb, level, obj, ex);
                return sb.ToString();
            };
            aspNetLogger.Log(Map2AspNetLogLevel(level), 0, message, exception, formatMsg);
        }

        LogLevel Map2AspNetLogLevel(Common.Logging.LogLevel logLevel) {
            switch (logLevel) {
                case Common.Logging.LogLevel.Trace:
                    return LogLevel.Trace;
                case Common.Logging.LogLevel.Debug:
                    return LogLevel.Debug;
                case Common.Logging.LogLevel.Info:
                    return LogLevel.Information;
                case Common.Logging.LogLevel.Warn:
                    return LogLevel.Warning;
                case Common.Logging.LogLevel.Error:
                    return LogLevel.Error;
                case Common.Logging.LogLevel.Fatal:
                    return LogLevel.Critical;
                default:
                    return 0;
            }
        }
    }
}
