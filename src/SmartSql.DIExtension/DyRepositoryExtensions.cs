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
            var sqlMapper = sp.GetSqlMapper(alias);
            var factory = sp.GetRequiredService<IRepositoryFactory>();
            var data = (T) factory.CreateInstance(typeof(T), sqlMapper);
            return data;
        }

        /// <summary>
        /// 批量插入 chenh
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="list"></param>
        /// <param name="maxCount"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public static int BatchInsert<T1, T2>(this T2 repository, IList<T1> list, int maxCount = 100000)
            where T2 : IRepository, IBatchInsert<T1>
        {
            if (list.Count == 0)
            {
                return 0;
            }

            if (list.Count <= maxCount)
            {
                return BatchInsert(list, repository);
            }
            else
            {
                long pages = list.Count % maxCount == 0 ? list.Count / maxCount : list.Count / maxCount + 1;
                int result = 0;
                for (int i = 0; i < pages; i++)
                {
                    int length = maxCount;
                    var newList = list.Skip(i * length).Take(length).ToList();
                    result += BatchInsert(newList, repository);
                }

                return result;
            }
        }

        static int BatchInsert<T1, T2>(IList<T1> list, T2 repository) where T2 : IRepository, IBatchInsert<T1>
        {
            StringBuilder sb = new StringBuilder();
            var sType = typeof(T1);

            StringBuilder colValsBuilder = new StringBuilder();
            StringBuilder colValBuilder = new StringBuilder();

            foreach (var data in list)
            {
                foreach (var property in sType.GetProperties())
                {
                    object val = property.GetValue(data);
                    if (val == null)
                    {
                        colValBuilder.Append($"NULL,");
                        continue;
                    }

                    var vType = val.GetType();
                    if (vType == typeof(DateTime))
                    {
                        colValBuilder.Append($"'{val}',");
                    }
                    else if (vType.IsValueType)
                    {
                        colValBuilder.Append($"{val},");
                    }
                    else
                    {
                        if (val.ToString().Contains("'"))
                        {
                            val = val.ToString().Replace("'", "''");
                        }
                        colValBuilder.Append($"'{val}',");
                    }
                }

                string itemData = colValBuilder.ToString().Trim().TrimEnd(',');
                colValsBuilder.Append($" ({itemData}),");
            }

            string colValues = colValsBuilder.ToString().Trim().TrimEnd(',');

            sb.Append($" {colValues} ");

            return repository.BatchInsert(sb.ToString());
        }


        /// <summary>
        /// 批量插入 chenh
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="list"></param>
        /// <param name="maxCount"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public static int BatchUpdate<T1, T2>(this T2 repository, IList<T1> list, int maxCount = 100000)
            where T2 : IRepository, IBatchUpdate<T1>
        {
            if (list.Count == 0)
            {
                return 0;
            }

            if (list.Count <= maxCount)
            {
                return BatchUpdate(list, repository);
            }
            else
            {
                long pages = list.Count % maxCount == 0 ? list.Count / maxCount : list.Count / maxCount + 1;
                int result = 0;
                for (int i = 0; i < pages; i++)
                {
                    int length = maxCount;
                    var newList = list.Skip(i * length).Take(length).ToList();
                    result += BatchUpdate(newList, repository);
                }

                return result;
            }
        }


        static int BatchUpdate<T1, T2>(IList<T1> list, T2 repository)
            where T2 : IRepository, IBatchUpdate<T1>
        {
            StringBuilder sb = new StringBuilder();
            var sType = typeof(T1);

            StringBuilder colValsBuilder = new StringBuilder();

            foreach (var data in list)
            {
                StringBuilder colValBuilder = new StringBuilder();
                foreach (var property in sType.GetProperties())
                {
                    var colName = property.Name;
                    var colAttr = (ColumnAttribute) property.GetCustomAttribute(typeof(ColumnAttribute));
                    if (colAttr != null && !string.IsNullOrWhiteSpace(colAttr.Name))
                    {
                        colName = colAttr.Name;
                    }

                    object val = property.GetValue(data);
                    if (val == null)
                    {
                        colValBuilder.Append($"NULL as {colName},");
                        continue;
                    }

                    var vType = val.GetType();
                    if (vType == typeof(DateTime))
                    {
                        colValBuilder.Append($"'{val}' as {colName},");
                    }
                    else if (vType.IsValueType)
                    {
                        colValBuilder.Append($"{val} as {colName},");
                    }
                    else
                    {
                        if (val.ToString().Contains("'"))
                        {
                            val = val.ToString().Replace("'", "''");
                        }
                        colValBuilder.Append($"'{val}' as {colName},");
                    }
                }

                string itemData = colValBuilder.ToString().Trim().TrimEnd(',');
                colValsBuilder.Append($" select {itemData} union all");
            }

            string tmpColValues = colValsBuilder.ToString();
            string colValues =
                tmpColValues.Substring(0, tmpColValues.LastIndexOf("union all", StringComparison.Ordinal));
            sb.Append($" ({colValues}) ");


            return repository.BatchUpdate(sb.ToString());
        }
    }
}