using System.Threading.Tasks;

namespace SmartSql.DyRepository
{
    public interface IBatchInsert<in TEntity>
    {
        int BatchInsert(string dataSql);
    }

    public interface IBatchInsertAsync<in TEntity>
    {

        Task<int> BatchInsertAsync(string dataSql);
    }
}