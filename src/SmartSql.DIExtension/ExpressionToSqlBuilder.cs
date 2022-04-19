using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ServiceStack;
using SmartSql.Annotations;
using SmartSql.DataSource;
using SmartSql.DyRepository;

namespace SmartSql.DIExtension
{
    public class ExpressionToSqlBuilder<T, TRepository> where TRepository : IRepository
    {
        public static string Build(Expression<Func<T, bool>> expression, TRepository repository, int topNum)
        {
            var result = new ExpressionAnalyzer();
            //解析
            result.Analyze(expression);

            var data = result.ResultData;

            StringBuilder sb = new StringBuilder();

            var sType = typeof(T);

            string tableName = sType.Name;
            var tableAttr = sType.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
            {
                tableName = tableAttr.Name ?? tableName;
            }

            sb.Append($" select ");
            if (topNum > 0)
            {
                if (repository.SqlMapper.SmartSqlConfig.Database.DbProvider.Name == DbProvider.SQLSERVER)
                {
                    sb.Append($" top {topNum} ");
                }
            }

            sb.Append($" * from {tableName} {result.TableAlias} where ");

            foreach (var whereClip in data.StackList)
            {
                sb.Append(whereClip.Replace($"[{result.TableAlias}]", result.TableAlias) + " ");
            }
            
            if (topNum > 0)
            {
                if (repository.SqlMapper.SmartSqlConfig.Database.DbProvider.Name == DbProvider.MYSQL)
                {
                    sb.Append($" limit {topNum} ");
                }
            }

            string sql = sb.ToString();
            

            return sql;
        }
    }
}