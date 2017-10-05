using System;
using System.Diagnostics.Tracing;
using System.Text;
using System.Threading;
using EtwStream;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace WebApplication1
{
    public class Program
    {
        public static void Main(string[] args) {
            BuildWebHost(args).Run();
        }

        //TODO configure logging

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureLogging(ConfigureLogging)
                .Build();

        static EventLevel ConvertLevel(LogLevel logLevel) {
            switch (logLevel) {
                case LogLevel.Critical:
                    return EventLevel.Critical;
                case LogLevel.Error:
                    return EventLevel.Error;
                case LogLevel.Warning:
                    return EventLevel.Warning;
                case LogLevel.Information:
                case LogLevel.Debug:
                    return EventLevel.Informational;
                case LogLevel.Trace:
                    return EventLevel.Verbose;
                default:
                    // this is really a level beyond critical, whose logging cannot be suppressed
                    return EventLevel.LogAlways;
            }
        }

        const string loggingEventSourceName = "Microsoft-Extensions-Logging";
        static CancellationTokenSource logFileCancelSource;
        static SubscriptionContainer logFileContainer;

        static void ConfigureLogging(WebHostBuilderContext context, ILoggingBuilder logBuilder) {
            var loggingSection = context.Configuration.GetSection("Logging");
            logBuilder.AddConfiguration(loggingSection);

            logBuilder.SetMinimumLevel(LogLevel.Trace);

            // we always log to ETW (Event Tracing for Windows)
            logBuilder.AddEventSourceLogger();

#if DEBUG
            logBuilder.AddDebug();
#endif

            if (Environment.UserInteractive) {
                logBuilder.AddConsole();
            }

            var eventLogLevel = loggingSection.GetValue<LogLevel>("EventLog:LogLevel:Default");
            if (eventLogLevel != LogLevel.None) {
                Func<string, LogLevel, bool> filter = (msg, lvl) => {
                    return lvl >= eventLogLevel;
                };

                logBuilder.AddEventLog(new EventLogSettings {
                    SourceName = loggingSection["EventLog:SourceName"],
                    Filter = filter
                });
            }

            var logFileLevel = loggingSection.GetValue<LogLevel>("EventSource:LogLevel:Default");
            if (logFileLevel != LogLevel.None) {
                logFileCancelSource = new CancellationTokenSource();
                logFileContainer = new SubscriptionContainer();

                var logFileTemplate = loggingSection["LogFileTemplate"];
                var rollFileSizeKB = loggingSection.GetValue<int>("RollingFileSizeKB");

                // configure in-process rolling file logger
                var keyWords = (EventKeywords)0x00000004;  // only process events tagged with Keywords.FormattedMessage
                Func<EventWrittenEventArgs, string> msgFormatter = x => {
                    var msg = x.DumpPayloadOrMessage();
                    return msg;
                };

                var observable = ObservableEventListener.FromEventSource(loggingEventSourceName, ConvertLevel(logFileLevel), keyWords)
                    .Buffer(TimeSpan.FromSeconds(5), 1000, logFileCancelSource.Token)
                    // fileNameSelector's DateTime is date of file open time, int is number sequence.
                    // timestampPattern's DateTime is write time of message. If pattern is different then roll new file.
                    // timestampPattern must be integer at last word.
                    .LogToRollingFile(
                        fileNameSelector: (dt, i) => string.Format(logFileTemplate, $@"-{dt.ToString("yyyyMMdd")}_{i.ToString("00")}"),
                        timestampPattern: x => x.ToString("yyyyMMdd"),
                        rollSizeKB: rollFileSizeKB,
                        messageFormatter: msgFormatter,
                        encoding: Encoding.UTF8,
                        autoFlush: false
                     );
                observable.AddTo(logFileContainer);
            }
        }
    }
}
