using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_DataAccess.Repo
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly ApplicationDbContext _db;
        internal DbSet<T> dbSet;
        public Repository(ApplicationDbContext db)
        {
            _db = db;
            this.dbSet = _db.Set<T>();
        }
        public async Task<T> GetById(int id)
        {
            return await dbSet.FindAsync(id);
        }
        public async Task<IEnumerable<T>> GetAll()
        {
            return await dbSet.ToListAsync();
        }
        public async Task Add(T entity)
        {
            dbSet.Add(entity);
            await _db.SaveChangesAsync();
        }
        public async Task Delete(int id)
        {
            dbSet.Remove(dbSet.Find(id));
            await _db.SaveChangesAsync();
        }
    }
}
