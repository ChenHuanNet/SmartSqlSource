using Microsoft.Extensions.Logging;
using SmartSql.Reflection.TypeConstants;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartSql.DyRepository
{
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly IDictionary<string, object> _cachedRepository = new Dictionary<string, object>();

        private readonly IRepositoryBuilder _repositoryBuilder;
        private readonly ILogger _logger;

        public RepositoryFactory(IRepositoryBuilder repositoryBuilder
            , ILogger logger
            )
        {
            _repositoryBuilder = repositoryBuilder;
            _logger = logger;
        }

        public object CreateInstance(Type interfaceType, ISqlMapper sqlMapper, string scope = "")
        {
            string key = interfaceType.FullName + "_" + sqlMapper.SmartSqlConfig.Alias;
            if (!_cachedRepository.ContainsKey(key))
            {
                lock (this)
                {
                    if (!_cachedRepository.ContainsKey(key))
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"RepositoryFactory.CreateInstance :InterfaceType.FullName:[{interfaceType.FullName}] Start");
                        }
                        var implType = _repositoryBuilder.Build(interfaceType, sqlMapper.SmartSqlConfig, scope);

                        var obj = sqlMapper.SmartSqlConfig.ObjectFactoryBuilder
                            .GetObjectFactory(implType, new Type[] { ISqlMapperType.Type })(new object[] { sqlMapper });
                        _cachedRepository.Add(key, obj);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug($"RepositoryFactory.CreateInstance :InterfaceType.FullName:[{interfaceType.FullName}],ImplType.FullName:[{implType.FullName}] End");
                        }
                    }
                }
            }
            return _cachedRepository[key];
        }
    }
}
