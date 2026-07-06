using Microsoft.EntityFrameworkCore;
using Kwy.Data.Sql;

namespace Kwy.Data.EFCore;

public interface IEfCoreSqlBridge<TContext>
    where TContext : DbContext
{
    ISqlExecutor CreateExecutor(TContext dbContext);
}
