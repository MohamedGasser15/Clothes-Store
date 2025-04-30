# LushThreads - E-Commerce Web App

A full-featured and responsive **E-Commerce web application** built with **ASP.NET MVC**, **Entity Framework**, **Tailwind CSS**, and **Bootstrap**, integrating **Stripe** for secure payment processing.

---

## Live Demo

[https://lushthreads.runasp.net](https://lushthreads.runasp.net)

---

## Screenshots

Coming soon...

---

## Features

- Responsive Design (Mobile, Tablet, Desktop)
- User Authentication & Authorization (Register, Login, Logout)
- Email Confirmation via ASP.NET Identity
- Shopping Cart with quantity management
- Stripe Payment Integration
- Product Catalog with filtering by brand and category
- Product Details Page
- Order Summary and Stripe Receipt Email
- Admin Panel:
  - Manage Products (Create, Edit, Delete)
  - Manage Categories & Brands
  - View Registered Users
- Identity-based authentication system
- Clean architecture using SOLID principles

> Note: The admin panel is protected. You need to log in as an admin user or create one manually in the database.

---

## Tech Stack

| Layer            | Technologies                                    |
|------------------|-------------------------------------------------|
| Backend          | ASP.NET MVC, C#, Entity Framework, LINQ        |
| Frontend         | Razor Views, Bootstrap, Tailwind CSS           |
| Database         | SQL Server, Code-First Migrations              |
| Authentication   | ASP.NET Identity, Email Verification           |
| Payment Gateway  | Stripe                                          |

---

## Project Architecture

- Modular solution structure:
  - `Clothes_Store`: Main MVC application (views, controllers)
  - `Clothes_DataAccess`: Entity Framework context and repository layer
  - `Clothes_Models`: Data models and view models
  - `Clothes_Utilities`: Helpers and utility services
- MVC Pattern (Models, Views, Controllers)
- Repository + Unit of Work pattern
- Separation of concerns (Services, Helpers, etc.)
- Code-first Entity Framework migrations

---

## Folder Structure (Overview)

```
Clothes-Store/
├── Clothes_Store/         # ASP.NET MVC Web Application
├── Clothes_DataAccess/    # Data Access Layer
├── Clothes_Models/        # Data Models
├── Clothes_Utilities/     # Utility Classes
├── Clothes Store.sln      # Visual Studio Solution File
├── .gitignore
└── README.md
```

---

## How to Run Locally

1. Clone the repository:

```bash
git clone https://github.com/MohamedGasser15/Clothes-Store.git
```

2. Open `Clothes Store.sln` in **Visual Studio**.

3. Set `Clothes_Store` as the startup project.

4. Update the connection string in `appsettings.json`.

5. Apply migrations:

```bash
Update-Database
```

6. Run the project (IIS Express or Kestrel).

---

## Packages Used

- Microsoft.EntityFrameworkCore
- Microsoft.AspNetCore.Identity
- Stripe.Net
- Tailwind via CDN / custom
- Bootstrap 5

---

## What I Learned

- Building scalable applications using ASP.NET MVC
- Using Entity Framework Code-First approach with clean architecture
- Integrating third-party payment systems like Stripe
- Handling user authentication and email confirmation securely
- Creating responsive UI using Tailwind and Bootstrap

---

## Contact

Feel free to reach out or connect on [LinkedIn](https://www.linkedin.com/in/mohamed-gasser-4b3ab1333)  
If you like the project, consider giving it a star.

---

## License

This project is licensed under the MIT License.
