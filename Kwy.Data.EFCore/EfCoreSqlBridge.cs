using Kwy.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Kwy.Data.EFCore;

public sealed class EfCoreSqlBridge<TContext> : IEfCoreSqlBridge<TContext>
    where TContext : DbContext
{
    public ISqlExecutor CreateExecutor(TContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return new EfCoreSqlExecutor(dbContext);
    }
}
