using System.Threading.Tasks;
using SmartSql.DyRepository.Annotations;

namespace SmartSql.DyRepository
{
    public interface IBatchUpdate<in TEntity>
    {
        int BatchUpdate(string dataSql);
    }
    public interface IBatchUpdateAsync<in TEntity>
    {
        Task<int> BatchUpdateAysnc(string dataSql);
    }
}