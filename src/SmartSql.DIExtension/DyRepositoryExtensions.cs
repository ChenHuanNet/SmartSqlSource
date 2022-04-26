using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartSql;
using SmartSql.DIExtension;
using SmartSql.DyRepository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SmartSql.Annotations;
using SmartSql.Exceptions;
using SmartSql.Utils;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DyRepositoryExtensions
    {
        /// <summary>
        /// 注入SmartSql仓储工厂
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="scope_template">Scope模板，默认：I{Scope}Repository</param>
        /// <param name="sqlIdNamingConvert">SqlId命名转换</param>
        /// <returns></returns>
        public static SmartSqlDIBuilder AddRepositoryFactory(this SmartSqlDIBuilder builder, string scope_template = "",
            Func<Type, MethodInfo, String> sqlIdNamingConvert = null)
        {
            builder.Services.TryAddSingleton<IRepositoryBuilder>((sp) =>
            {
                var loggerFactory = sp.GetService<ILoggerFactory>() ?? Logging.Abstractions.NullLoggerFactory.Instance;
                var logger = loggerFactory.CreateLogger<EmitRepositoryBuilder>();
                return new EmitRepositoryBuilder(scope_template, sqlIdNamingConvert, logger);
            });
            builder.Services.TryAddSingleton<IRepositoryFactory>((sp) =>
            {
                var loggerFactory = sp.GetService<ILoggerFactory>() ?? Logging.Abstractions.NullLoggerFactory.Instance;
                var logger = loggerFactory.CreateLogger<RepositoryFactory>();
                var repositoryBuilder = sp.GetRequiredService<IRepositoryBuilder>();
                return new RepositoryFactory(repositoryBuilder, logger);
            });
            return builder;
        }

        /// <summary>
        /// 注入单个仓储接口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="smartSqlAlias"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        public static SmartSqlDIBuilder AddRepository<T>(this SmartSqlDIBuilder builder, string smartSqlAlias,
            string scope = "") where T : class
        {
            builder.AddRepositoryFactory();
            builder.Services.AddSingleton<T>(sp =>
            {
                ISqlMapper sqlMapper = sp.GetRequiredService<ISqlMapper>();
                ;
                if (!String.IsNullOrEmpty(smartSqlAlias))
                {
                    sqlMapper = sp.EnsureSmartSql(smartSqlAlias).SqlMapper;
                }

                var factory = sp.GetRequiredService<IRepositoryFactory>();
                return factory.CreateInstance(typeof(T), sqlMapper, scope) as T;
            });
            return builder;
        }

        /// <summary>
        /// 注入仓储结构 By 程序集
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="setupOptions"></param>
        /// <returns></returns>
        public static SmartSqlDIBuilder AddRepositoryFromAssembly(this SmartSqlDIBuilder builder,
            Action<AssemblyAutoRegisterOptions> setupOptions)
        {
            builder.AddRepositoryFactory();
            var options = new AssemblyAutoRegisterOptions
            {
                Filter = (type) => type.IsInterface
            };
            setupOptions(options);
            ScopeTemplateParser templateParser = new ScopeTemplateParser(options.ScopeTemplate);
            var allTypes = TypeScan.Scan(options);
            foreach (var type in allTypes)
            {
                builder.Services.AddSingleton(type, sp =>
                {
                    ISqlMapper sqlMapper = sp.GetRequiredService<ISqlMapper>();
                    ;
                    if (!String.IsNullOrEmpty(options.SmartSqlAlias))
                    {
                        sqlMapper = sp.EnsureSmartSql(options.SmartSqlAlias).SqlMapper;
                    }

                    var factory = sp.GetRequiredService<IRepositoryFactory>();
                    var scope = string.Empty;
                    if (!String.IsNullOrEmpty(options.ScopeTemplate))
                    {
                        scope = templateParser.Parse(type.Name);
                    }

                    return factory.CreateInstance(type, sqlMapper, scope);
                });
            }

            return builder;
        }


        /// <summary>
        /// 推荐采用这个方法获取多库  chenh
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="alias"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetRepository<T>(this IServiceProvider sp, string alias) where T : IRepository
        {
            var sqlMapper = sp.GetSmartSqlBuilder(alias)?.SqlMapper;
            if (sqlMapper == null)
            {
                throw new Exception($"没有找到相关的 {alias} 的SmartSql配置");
            }

            var factory = sp.GetRequiredService<IRepositoryFactory>();
            var data = (T)factory.CreateInstance(typeof(T), sqlMapper);
            return data;
        }
    }
}