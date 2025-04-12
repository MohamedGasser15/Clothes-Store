using Clothes_Models.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_DataAccess.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<SecurityActivity> SecurityActivities { get; set; }

        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<OrderHeader> OrderHeaders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }




        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<CartItem>()
          .HasOne(ci => ci.Product)
          .WithMany()
          .HasForeignKey(ci => ci.ProductId)
          .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.User)
                .WithMany(u => u.CartItems)
                .HasForeignKey(ci => ci.UserId);
            modelBuilder.Entity<Category>().HasData(
            new Category
            {
                Category_Id = 1,
                Category_Name = "T-Shirts",
            },
            new Category 
            {
                Category_Id = 2,
                Category_Name = "Pantalon",
            },
            new Category
            {
                Category_Id = 3,
                Category_Name = "Shorts",
            }
            );
            modelBuilder.Entity<Brand>().HasData(
                new Brand
                {
                    Brand_Id = 1,
                    Brand_Name = "Nike",
                },
                new Brand
                {
                    Brand_Id = 2,
                    Brand_Name = "Adidas",
                },
                new Brand
                {
                    Brand_Id = 3,
                    Brand_Name = "Puma",
                }
                );
            modelBuilder.Entity<Product>().HasData(
                new Product {
                    Product_Id = 1,
                    Product_Name = "T-Shirt Nike",
                    Product_Description= "Black T-Shirt Nike",
                    imgUrl = "Nika.jpg",
                    Product_Size = "M",
                    Product_Color = "Black",
                    Product_Price = 100,
                    Category_Id = 1,
                    brand_Id = 1,
                },
                new Product
                {
                    Product_Id = 2,
                    Product_Name = "Pantalon Adidas",
                    Product_Description = "Red Pantalon Adidas",
                    imgUrl = "Adidas.jpg",
                    Product_Size = "L",
                    Product_Color = "Red",
                    Product_Price = 200,
                    Category_Id = 2,
                    brand_Id = 2,
                },
                new Product
                {
                    Product_Id = 3,
                    Product_Name = "Shorts Puma",
                    Product_Description = "Yellow Shorts Puma",
                    imgUrl = "Puma.jpg",
                    Product_Size = "S",
                    Product_Color = "Yellow",
                    Product_Price = 150,
                    Category_Id = 3,
                    brand_Id = 3,
                }
                );

        }
    }
}
