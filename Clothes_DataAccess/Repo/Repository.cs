using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo.Interfaces;
using Clothes_Models.Models;
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
        }
        public async Task Delete(T entity)
        {
            dbSet.Remove(entity);
        }
        public async Task AdminActivityAsync(string userId, string activityType, string description, string ipAddress)
        {
            var activity = new AdminActivity
            {
                UserId = userId,
                ActivityType = activityType,
                Description = description,
                IpAddress = ipAddress
            };

            _db.AdminActivities.Add(activity);
            await _db.SaveChangesAsync();
        }
    }
}
