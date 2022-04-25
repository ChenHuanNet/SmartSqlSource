using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using SmartSql.Annotations;
using SmartSql.Data;
using SmartSql.DyRepository;
using SmartSql.Utils;

namespace SmartSql.DIExtension
{
    public static class BaseRepositoryExtensions
    {
        #region 单表Linq查询

        public static List<TModel> Find<TModel>(this IRepository<TModel> repository,
            Expression<Func<TModel, bool>> expression)
        {
            string sql = ExpressionToSqlBuilder<TModel>.Build(expression, repository, 0);
            var context = new RequestContext()
            {
                RealSql = sql
            };
            return repository.SqlMapper.Query<TModel>(context).ToList();
        }


        /// <summary>
        /// 暂时仅支持 Sqlserver 和MySql 
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="expression"></param>
        /// <typeparam name="TModel"></typeparam>
        /// <typeparam name="TRepository"></typeparam>
        /// <returns></returns>
        public static TModel FindFirst<TModel>(this IRepository<TModel> repository,
            Expression<Func<TModel, bool>> expression)
        {
            string sql = ExpressionToSqlBuilder<TModel>.Build(expression, repository, 1);
            var context = new RequestContext()
            {
                RealSql = sql
            };
            return repository.SqlMapper.Query<TModel>(context).FirstOrDefault();
        }

        #endregion


        #region 批量插入

        /// <summary>
        /// 批量插入 chenh
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="list"></param>
        /// <param name="maxCount"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public static int BatchInsertV2<T1, T2>(this T2 repository, IList<T1> list, int maxCount = 100000)
            where T2 : IRepository
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

        static int BatchInsert<T1, T2>(IList<T1> list, T2 repository) where T2 : IRepository
        {
            StringBuilder sb = new StringBuilder();
            var sType = typeof(T1);

            string tableName = sType.Name;
            var tableAttr = sType.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
            {
                tableName = tableAttr.Name ?? tableName;
            }

            sb.Append($"  INSERT INTO {tableName} ");

            var properties = sType.GetProperties();

            StringBuilder colNameBuilder = new StringBuilder();
            foreach (var property in properties)
            {
                string colName = property.Name;
                var colAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (colAttr != null)
                {
                    colName = colAttr.Name ?? colName;
                    if (colAttr.IsPrimaryKey && colAttr.IsAutoIncrement)
                    {
                        continue;
                    }
                }

                colNameBuilder.Append($"{colName},");
            }

            string colNames = $" ({colNameBuilder.ToString().TrimEnd(',')}) ";

            sb.Append($" {colNames} values ");

            StringBuilder colValsBuilder = new StringBuilder();


            foreach (var data in list)
            {
                StringBuilder colValBuilder = new StringBuilder();
                foreach (var property in properties)
                {
                    var colAttr = property.GetCustomAttribute<ColumnAttribute>();
                    if (colAttr != null)
                    {
                        if (colAttr.IsPrimaryKey && colAttr.IsAutoIncrement)
                        {
                            continue;
                        }
                    }


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

            var context = new RequestContext()
            {
                RealSql = sb.ToString()
            };
            return repository.SqlMapper.Execute(context);
            //return repository.BatchInsert(sb.ToString());
        }

        #endregion

        #region 批量更新

        /// <summary>
        /// 批量插入 chenh
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="list"></param>
        /// <param name="maxCount"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public static int BatchUpdateV2<T1, T2>(this T2 repository, IList<T1> list, int maxCount = 100000)
            where T2 : IRepository
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
            where T2 : IRepository
        {
            StringBuilder sb = new StringBuilder();
            var sType = typeof(T1);

            var properties = sType.GetProperties();

            string tableName = sType.Name;
            var tableAttr = sType.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
            {
                tableName = tableAttr.Name ?? tableName;
            }

            sb.Append($" UPDATE {tableName} a inner join ");

            StringBuilder equalWhereClip = new StringBuilder();
            StringBuilder colSetBuilder = new StringBuilder();
            foreach (var property in properties)
            {
                string colName = property.Name;
                var colAttr = property.GetCustomAttribute<ColumnAttribute>();
                if (colAttr != null)
                {
                    colName = colAttr.Name ?? colName;
                    if (colAttr.IsPrimaryKey)
                    {
                        equalWhereClip.Append($"  b on a.{colName}=b.{colName} ");
                        continue;
                    }
                }

                colSetBuilder.Append($"a.{colName} = b.{colName},");
            }

            StringBuilder colValsBuilder = new StringBuilder();

            foreach (var data in list)
            {
                StringBuilder colValBuilder = new StringBuilder();
                foreach (var property in properties)
                {
                    var colName = property.Name;
                    var colAttr = (ColumnAttribute)property.GetCustomAttribute(typeof(ColumnAttribute));
                    if (colAttr != null && !string.IsNullOrWhiteSpace(colAttr.Name))
                    {
                        colName = colAttr.Name ?? colName;
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
            sb.Append(equalWhereClip.ToString());
            sb.Append($"  set {colSetBuilder.ToString().Trim().TrimEnd(',')}");

            var context = new RequestContext()
            {
                RealSql = sb.ToString()
            };
            return repository.SqlMapper.Execute(context);
        }

        #endregion
    }
}