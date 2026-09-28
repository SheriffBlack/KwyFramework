using Microsoft.EntityFrameworkCore;
using Kwy.Data.Abstractions;

namespace Kwy.Data.EFCore;

public interface IEfCoreSqlBridge<TContext>
    where TContext : DbContext
{
    ISqlExecutor CreateExecutor(TContext dbContext);
}
