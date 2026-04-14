# JaneERP

A Windows desktop ERP application built for VanGo Production. Manages inventory, orders, purchasing, manufacturing, Shopify sync, and more.

## Tech Stack

- .NET 10 Windows Forms
- SQL Server Express (primary database)
- SQLite (local cache via Entity Framework Core)
- Dapper, CsvHelper

## Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server Express (local instance named `SQLEXPRESS`)

## Setup

**1. Clone the repo**
```
git clone https://github.com/SaadiqDaya/JaneERP.git
cd JaneERP
```

**2. Create your App.config**

Copy the example file and fill in your database credentials:
```
cp App.config.example App.config
```

Edit `App.config` and replace `YOUR_DB_USER` and `YOUR_DB_PASSWORD` with your SQL Server credentials.

**3. Set up the database**

Create the `JaneERP` database in SQL Server and ensure the user in your connection string has access. The app runs migrations automatically on first launch.

**4. Build and run**
```
dotnet run
```

## Features

- Inventory management with location/bin tracking
- Purchase orders and receiving
- Sales orders and Shopify store sync
- Manufacturing orders and work orders
- Cycle counts and stock transfers
- KPI dashboard and reporting
- User management with role-based permissions
- CSV import/export
