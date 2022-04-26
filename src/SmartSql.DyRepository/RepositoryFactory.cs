using Microsoft.Extensions.Logging;
using SmartSql.Reflection.TypeConstants;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartSql.DyRepository
{
    public class RepositoryFactory : IRepositoryFactory
    {
        private static readonly IDictionary<string, object> CachedRepository = new Dictionary<string, object>();

        private readonly IRepositoryBuilder _repositoryBuilder;
        private readonly ILogger _logger;

        public RepositoryFactory(IRepositoryBuilder repositoryBuilder
            , ILogger logger
        )
        {
            _repositoryBuilder = repositoryBuilder;
            _logger = logger;
        }

        public object GetInstance(Type interfaceType, string alias, ISqlMapper sqlMapper)
        {
            string key = interfaceType.FullName + "_" + alias;
            var impl = CachedRepository[key];
            if (impl == null)
            {
                impl = CreateInstance(interfaceType, sqlMapper);
            }

            return impl;
        }

        public object CreateInstance(Type interfaceType, ISqlMapper sqlMapper, string scope = "")
        {
            string key = interfaceType.FullName + "_" + sqlMapper.SmartSqlConfig.Alias;
            if (!CachedRepository.ContainsKey(key))
            {
                lock (this)
                {
                    if (!CachedRepository.ContainsKey(key))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"RepositoryFactory.CreateInstance :InterfaceType.FullName:[{interfaceType.FullName}] Start");
                        }

                        var implType = _repositoryBuilder.Build(interfaceType, sqlMapper.SmartSqlConfig, scope);

                        var obj = sqlMapper.SmartSqlConfig.ObjectFactoryBuilder
                            .GetObjectFactory(implType, new Type[] { ISqlMapperType.Type })(new object[] { sqlMapper });
                        CachedRepository.Add(key, obj);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"RepositoryFactory.CreateInstance :InterfaceType.FullName:[{interfaceType.FullName}],ImplType.FullName:[{implType.FullName}] End");
                        }
                    }
                }
            }

            return CachedRepository[key];
        }
    }
}