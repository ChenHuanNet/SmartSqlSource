using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartSql;
using SmartSql.ConfigBuilder;
using SmartSql.Configuration;
using SmartSql.DbSession;
using SmartSql.DIExtension;
using SmartSql.Utils;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SmartSqlDIExtensions
    {
        public static Dictionary<string, SmartSqlBuilder> CacheSmartSqlBuilders = new Dictionary<string, SmartSqlBuilder>();

        public static SmartSqlDIBuilder AddSmartSql(this IServiceCollection services,
            Func<IServiceProvider, SmartSqlBuilder> setup)
        {
            services.AddSingleton<SmartSqlBuilder>(sp => setup(sp).Build());
            AddOthers(services);
            return new SmartSqlDIBuilder(services);
        }

        public static SmartSqlDIBuilder AddSmartSql(this IServiceCollection services,
            Action<IServiceProvider, SmartSqlBuilder> setup)
        {
            return services.AddSmartSql(sp =>
            {
                var loggerFactory = sp.GetService<ILoggerFactory>();
                var smartSqlBuilder = new SmartSqlBuilder().UseLoggerFactory(loggerFactory);
                setup(sp, smartSqlBuilder);
                if (smartSqlBuilder.ConfigBuilder == null)
                {
                    var configPath = ResolveConfigPath(sp);
                    smartSqlBuilder.UseXmlConfig(SmartSql.ConfigBuilder.ResourceType.File, configPath);
                }

                return smartSqlBuilder;
            });
        }

        public static SmartSqlDIBuilder AddSmartSql(this IServiceCollection services,
            String alias = SmartSqlBuilder.DEFAULT_ALIAS)
        {
            return services.AddSmartSql((sp, builder) => { builder.UseAlias(alias); });
        }

        public static SmartSqlDIBuilder AddSmartSql(this IServiceCollection services, Action<SmartSqlBuilder> setup)
        {
            return services.AddSmartSql((sp, builder) => { setup(builder); });
        }

        private static string ResolveConfigPath(IServiceProvider sp)
        {
            var envStr = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configPath = SmartSqlBuilder.DEFAULT_SMARTSQL_CONFIG_PATH;
            if (!String.IsNullOrEmpty(envStr) &&
                !string.Equals(envStr, "Production", StringComparison.OrdinalIgnoreCase))
            {
                configPath = $"SmartSqlMapConfig.{envStr}.xml";
            }

            if (!ResourceUtil.FileExists(configPath))
            {
                configPath = SmartSqlBuilder.DEFAULT_SMARTSQL_CONFIG_PATH;
            }

            return configPath;
        }

        private static void AddOthers(IServiceCollection services)
        {
            services.AddSingleton<IDbSessionFactory>(sp => sp.GetRequiredService<SmartSqlBuilder>().DbSessionFactory);
            services.AddSingleton<ISqlMapper>(sp => sp.GetRequiredService<SmartSqlBuilder>().SqlMapper);
            services.AddSingleton<ITransaction>(sp => sp.GetRequiredService<SmartSqlBuilder>().SqlMapper);
            services.AddSingleton<IDbSessionStore>(sp =>
                sp.GetRequiredService<SmartSqlBuilder>().SmartSqlConfig.SessionStore);
        }


        /// <summary>
        /// 根据appsetting.json配置文件来注册SmartSql
        /// 下面是SmartSql的 Properties 必须有的配置节点
        /// <Properties>
        ///     <Property Name="ConnectionString" Value="${appsetting:ConnectionStrings:SqlConnection}"/>
        ///     <Property Name="MapDir" Value="${appsetting:SmarSqlOption:MapDir}"/>
        ///     <Property Name="DbProvider" Value="${appsetting:SmarSqlOption:DbProvider}"/>
        /// </Properties>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="smartSqlOptions"></param>
        /// <returns></returns>
        public static IServiceCollection AddSmartSqlServices(this IServiceCollection services,
            IConfiguration configuration, List<SmartSqlOption> smartSqlOptions)
        {
            foreach (var item in smartSqlOptions)
            {
                //xml配置文件只有初始化的时候有用到，后面不会用了，从外部导入配置，一个XML就够了
                IDictionary<string, string> connectionStrings = new Dictionary<string, string>();
                connectionStrings.Add("appsetting:ConnectionStrings:SqlConnection",
                    configuration[$"ConnectionStrings:{item.ConnectionKey}"]);
                connectionStrings.Add("appsetting:SmarSqlOption:MapDir", item.MapDir);
                connectionStrings.Add("appsetting:SmarSqlOption:DbProvider", item.DbProvider);
                services.AddSmartSql(builder =>
                {
                    var smartSqlBuilder = builder.UseAlias(item.Alias).UseProperties(connectionStrings)
                        .UseXmlConfig(ResourceType.File, item.ConfigPath);
                    if (!CacheSmartSqlBuilders.ContainsKey(item.Alias))
                        CacheSmartSqlBuilders.Add(item.Alias, smartSqlBuilder);
                }).AddRepositoryFromAssembly(options =>
                {
                    options.SmartSqlAlias = item.Alias;
                    options.AssemblyString = item.AssemblyString;
                    options.Filter = type => type.FullName.Contains(item.Filter);
                    options.ScopeTemplate = item.ScopeTemplate;
                });
            }

            return services;
        }


        public static SmartSqlBuilder GetSmartSqlBuilder(this IServiceProvider sp, string alias)
        {
            if (CacheSmartSqlBuilders.ContainsKey(alias))
            {
                return CacheSmartSqlBuilders[alias];
            }

            return null;
        }
    }
}