using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using KdSoft.Quartz;
using KdSoft.Quartz.AspNet;
using KdSoft.Quartz.WebServices;
using KdSoft.Services.Json;
using KdSoft.Services.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;

namespace WebApplication1
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var mvc = services.AddMvc();

            //TODO Review JSON serializer settings based on the comments here:
            //https://github.com/aspnet/Mvc/issues/4562#issuecomment-226049509
            services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, JsonInputSerializerSetup>());
            mvc.AddJsonOptions(options => {
                ConfigureJsonOutput(options);
            });

            ConfigureSchedulerServices(services, mvc);

            services.Configure<IISOptions>(options => options.AutomaticAuthentication = true);

            services.AddAuthentication(IISDefaults.AuthenticationScheme);

            ConfigureMvcAuthorization(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }

            // Quartz scheduler needs logging infrastructure adapted
            ConfigureScheduler(loggerFactory);

            app.UseAuthentication();

            app.UseMvc();

            var scheduler = app.ApplicationServices.GetService<ISchedulerFactory>()?.GetScheduler();
            if (scheduler != null && schedulerAutoStart) {
                // if SchedulerAutoStart == false then one must call SchedulerController.Start() explicitly to start the IScheduler
                scheduler.Start();
            }
        }

        void ConfigureScheduler(ILoggerFactory loggerFactory) {
            // adapt Quartz to use the Asp.Net logging infrastructure
            var lfAdapter = new AspNetLoggerFactoryAdapter(loggerFactory, Common.Logging.LogLevel.All, false, false, false, null);
            Common.Logging.LogManager.Adapter = lfAdapter;
        }

        void ConfigureMvcAuthorization(IServiceCollection services) {
            var securityConfig = Configuration.GetSection("Security");
            var accounts = securityConfig.GetSection("Administration:AdAccounts").GetChildren();
            var adminAccounts = new HashSet<string>(accounts.Select(acct => acct.Value), StringComparer.OrdinalIgnoreCase);
            var groups = securityConfig.GetSection("Administration:AdGroups").GetChildren();
            var adminGroups = new HashSet<string>(groups.Select(grp => grp.Value), StringComparer.OrdinalIgnoreCase);

            services.AddAuthorization(options => {
                // this policy succeeds if the user has an admin account or is member of an admin group
                options.AddPolicy("Administrator", policy => {
                    policy.RequireAuthenticatedUser();
                    // cannot use policy.RequireClaim(...) because it makes case-sensitive comparison on the claim values
                    policy.RequireAssertion(context => {
                        var nameClaims = context.User.FindAll(System.Security.Claims.ClaimTypes.Name);
                        foreach (var claim in nameClaims) {
                            if (adminAccounts.Contains(claim.Value))
                                return true;
                        }

                        var adGroupClaims = context.User.FindAll(ClaimTypes.AdSecurityGroup); // this is case-insensitive!
                        foreach (var claim in adGroupClaims) {
                            if (adminGroups.Contains(claim.Value))
                                return true;
                        }

                        return false;
                    });
                });
            });
        }

        bool schedulerAutoStart;

        void ConfigureRetryListener(IScheduler scheduler) {
            Action<ExpBackoffRetryTrigger> applyFallbackSettings = trigger => {
                trigger.RetrySettings.MaxRetries = 5;
                trigger.RetrySettings.PowerBase = 2.0;
                trigger.RetrySettings.BackoffBaseInterval = TimeSpan.FromMinutes(10);
            };
            var listener = new RetryJobListener<ExpBackoffRetryTrigger>(applyFallbackSettings);
            scheduler.ListenerManager.AddJobListener(listener, GroupMatcher<JobKey>.AnyGroup());
        }

        void ConfigureSchedulerServices(IServiceCollection services, IMvcBuilder mvc) {
            mvc.AddApplicationPart(typeof(SchedulerController).Assembly);

            schedulerAutoStart = Configuration.GetValue<bool>("AutoStart");

            var quartzSection = Configuration.GetSection("Quartz");
            var quartzProps = new NameValueCollection();
            foreach (var entry in quartzSection.GetChildren()) {
                quartzProps.Add("quartz." + entry.Key, entry.Value);
            }

            services.AddSingleton<ISchedulerFactory>(sp => {
                var result = new StdSchedulerFactory(quartzProps);
                var scheduler = result.GetScheduler();
                scheduler.Context.Put(KdSoft.Quartz.AspNet.QuartzKeys.ServiceProviderKey, sp);
                ConfigureRetryListener(scheduler);
                return result;
            });
        }

        #region JSON and Protocol Buffers

        // we have special settings for input, but since we do not have the option to specify
        // separate input settings anymore, we need to replace the input formatters as a whole
        class JsonInputSerializerSetup: IConfigureOptions<MvcOptions>
        {
            readonly ILoggerFactory loggerFactory;
            readonly ArrayPool<char> charPool;
            readonly ObjectPoolProvider objectPoolProvider;

            public JsonInputSerializerSetup(ILoggerFactory loggerFactory, ArrayPool<char> charPool, ObjectPoolProvider objectPoolProvider) {
                this.loggerFactory = loggerFactory;
                this.charPool = charPool;
                this.objectPoolProvider = objectPoolProvider;
            }

            public void Configure(MvcOptions options) {
                options.InputFormatters.RemoveType<JsonInputFormatter>();
                options.InputFormatters.RemoveType<JsonPatchInputFormatter>();

                var jsonInputSettings = JsonSerializerSettingsProvider.CreateSerializerSettings();
                // make sure QLine.Services.Json.BaseTypes.Set gets initialized before creating (CamelCase)BaseTypeContractResolver
                jsonInputSettings.ContractResolver = new CamelCaseBaseTypeContractResolver {
                    NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false }
                };
                jsonInputSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.RoundtripKind;
                jsonInputSettings.DateParseHandling = Newtonsoft.Json.DateParseHandling.DateTimeOffset;
                jsonInputSettings.FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Decimal;
                jsonInputSettings.SerializationBinder = new KnownTypesSerializationBinder(KnownTypes.Map);
                jsonInputSettings.TypeNameHandling = TypeNameHandling.All;
                jsonInputSettings.Converters.Add(new TimeSpanISO8601Converter());
                jsonInputSettings.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;

                var logger = loggerFactory.CreateLogger<JsonInputFormatter>();
                var inputFormatter = new JsonInputFormatter(logger, jsonInputSettings, charPool, objectPoolProvider);
                options.InputFormatters.Add(inputFormatter);

                logger = loggerFactory.CreateLogger<JsonPatchInputFormatter>();
                inputFormatter = new JsonPatchInputFormatter(logger, jsonInputSettings, charPool, objectPoolProvider);
                options.InputFormatters.Add(inputFormatter);
            }
        }

        void ConfigureJsonOutput(MvcJsonOptions options) {
            var jsonOutputSettings = options.SerializerSettings;
            jsonOutputSettings.ContractResolver = new CamelCaseBaseTypeContractResolver {
                NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false }
            };
            jsonOutputSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.RoundtripKind;
            jsonOutputSettings.DateParseHandling = Newtonsoft.Json.DateParseHandling.DateTimeOffset;
            jsonOutputSettings.FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Decimal;
            jsonOutputSettings.Converters.Add(new TimeSpanISO8601Converter());
        }

        #endregion

    }
}
