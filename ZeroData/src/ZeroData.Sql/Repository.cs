using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroData.Sql
{
    /// <summary>
    /// Generic repository interface for clean architecture patterns.
    /// Wraps Table&lt;T&gt; operations behind a standardized interface.
    /// </summary>
    public interface IRepository<T> where T : class
    {
        T Find(params object[] keyValues);
        Task<T> FindAsync(CancellationToken ct = default, params object[] keyValues);
        List<T> GetAll();
        Task<List<T>> GetAllAsync(CancellationToken ct = default);
        List<T> Where(Expression<Func<T, bool>> predicate);
        Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
        T FirstOrDefault(Expression<Func<T, bool>> predicate);
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
        int Count(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
        bool Any(Expression<Func<T, bool>> predicate);
        void Insert(T entity);
        void Delete(T entity);
    }

    /// <summary>
    /// Base repository implementation backed by SqlContext.
    /// Provides standard CRUD operations. Override for custom queries.
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly SqlContext Context;

        public Repository(SqlContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        protected Table<T> Table => Context.GetTable<T>();

        public virtual T Find(params object[] keyValues) => Table.Find(keyValues);
        public virtual Task<T> FindAsync(CancellationToken ct = default, params object[] keyValues)
            => Table.FindAsync(ct, keyValues);
        public virtual List<T> GetAll() => Table.ToList();
        public virtual Task<List<T>> GetAllAsync(CancellationToken ct = default) => Table.ToListAsync(ct);
        public virtual List<T> Where(Expression<Func<T, bool>> predicate) => Table.Where(predicate);
        public virtual Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => Table.WhereAsync(predicate, ct);
        public virtual T FirstOrDefault(Expression<Func<T, bool>> predicate) => Table.FirstOrDefault(predicate);
        public virtual Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => Table.FirstOrDefaultAsync(predicate, ct);
        public virtual int Count(Expression<Func<T, bool>> predicate) => Table.Count(predicate);
        public virtual Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => Table.CountAsync(predicate, ct);
        public virtual bool Any(Expression<Func<T, bool>> predicate) => Table.Any(predicate);

        public virtual void Insert(T entity) => Context.GetTable<T>().InsertOnSubmit(entity);
        public virtual void Delete(T entity) => Context.GetTable<T>().DeleteOnSubmit(entity);
    }
}
