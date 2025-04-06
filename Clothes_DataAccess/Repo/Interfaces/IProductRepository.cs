using Clothes_Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_DataAccess.Repo.Interfaces
{
    public interface IProductRepository : IRepository<Product>
    {
       Task UpdateAsync (Product product);
    }
}
