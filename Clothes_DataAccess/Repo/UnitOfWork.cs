using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_DataAccess.Repo
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _db;
        public IBrandRepository Brands { get; private set; }
        public ICategoryRepository Categories { get; private set; }
        public IProductRepository Products { get; private set; }
        public UnitOfWork(ApplicationDbContext db)
        {
            _db = db;
            Brands = new BrandRepository(db);
            Categories = new CategoryRepository(db);
            Products = new ProductRepository(db);
        }
        public async Task SaveAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
