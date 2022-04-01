namespace SmartSql.Utils
{
    /// <summary>
    /// SmartSql 根据xml初始化配置
    /// </summary>
    public class SmartSqlOption
    {
        public string Alias { get; set; }

        public string ConnectionKey { get; set; }
        public string ConfigPath { get; set; }
        public string AssemblyString { get; set; }
        public string Filter { get; set; }
        public string ScopeTemplate { get; set; }

        /// <summary>
        /// Sql映射目录
        /// </summary>
        public string MapDir { get; set; }

        /// <summary>
        ///     <!--public const String SQLSERVER = "SqlServer";
        ///  public const String MS_SQLSERVER = "MsSqlServer";
        ///  public const String MYSQL = "MySql";
        ///  public const String MYSQL_CONNECTOR = "MySqlConnector";
        ///  public const String POSTGRESQL = "PostgreSql";
        ///  public const String ORACLE = "Oracle";
        ///  public const String SQLITE = "SQLite";-->
        /// </summary>
        public string DbProvider { get; set; }
    }
}