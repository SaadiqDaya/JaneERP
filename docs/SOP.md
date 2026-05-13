# JaneERP — Standard Operating Procedure (SOP)

**Version:** 1.1  
**Last Updated:** May 2026  
**Applies To:** All JaneERP users

---

## Table of Contents

1. [Overview](#1-overview)
2. [Getting Started](#2-getting-started)
3. [User Roles & Permissions](#3-user-roles--permissions)
4. [Main Menu & Navigation](#4-main-menu--navigation)
5. [Sales & Orders](#5-sales--orders)
6. [Purchase Orders & Receiving](#6-purchase-orders--receiving)
7. [Inventory Management](#7-inventory-management)
8. [Products & Parts](#8-products--parts)
9. [Manufacturing](#9-manufacturing)
10. [Reporting & Analytics](#10-reporting--analytics)
11. [Task Management](#11-task-management)
12. [System Administration](#12-system-administration)
13. [Data Import & Export](#13-data-import--export)
14. [Troubleshooting & FAQs](#14-troubleshooting--faqs)

---

## 1. Overview

JaneERP is the company's internal management system. It handles:

- **Sales orders** — both manual orders and Shopify store orders
- **Purchase orders** — buying stock from suppliers
- **Inventory** — tracking stock quantities across locations
- **Manufacturing** — tracking production work orders and costs
- **Reporting** — financial and operational reports
- **Team tasks** — assigning and tracking internal tasks

All users log in with a personal account. What you can see and do depends on your assigned role.

---

## 2. Getting Started

### 2.1 Logging In

1. Open the JaneERP application.
2. Enter your **Username** and **Password**.
3. Click **Login**.

> **Tip:** If "Remember Username" is enabled, your username will be pre-filled next time.

**If your account is locked:** Contact your administrator. Accounts lock after a set number of failed login attempts and unlock automatically after a cooldown period (typically 15 minutes).

**First time using the system:** Your administrator will create your account and give you your credentials. You should change your password on first login if prompted.

### 2.2 Session Timeout

The system automatically logs you out after **30 minutes of inactivity** to protect your account. You will be returned to the login screen. Any unsaved work may be lost — save frequently.

### 2.3 Logging Out

Close the main menu or use the logout option to end your session. Your login and logout times are recorded in the system audit log.

---

## 3. User Roles & Permissions

There are three roles in JaneERP. Your administrator assigns your role when they create your account.

| Role | What you can do |
|---|---|
| **Admin** | Full access: user management, settings, all modules, all data |
| **Editor** | Create and edit orders, inventory, manufacturing, tasks, reports |
| **Viewer** | Read-only: dashboards, reports, product search |

**Granular permissions** can also be set per-user for specific areas:

| Permission | Controls Access To |
|---|---|
| Inventory | Inventory adjustment, stock transfers, location management |
| Sales Orders | Creating and editing sales orders |
| Manufacturing | Work orders and manufacturing orders |
| Parts | Product and parts management |
| Cycle Count | Physical inventory counting |
| Tasks | Creating and managing team tasks |
| Logs | Viewing audit and login logs |

> If a button or menu item is greyed out or not visible, you likely do not have permission for that function. Contact your administrator.

---

## 4. Main Menu & Navigation

After logging in, you will see the **Main Menu**. This is the hub for all areas of the system.

### 4.1 Main Menu Sections

The main menu is divided into sections. Buttons may be hidden based on your role.

| Section | Buttons |
|---|---|
| **Sales** | Sales Dashboard, Create Order, Customers |
| **Purchasing** | Purchase Orders, Vendors, Reorder Report |
| **Inventory** | Inventory Snapshot, Cycle Count, Adjust Stock, Stock Transfer, Locations, Backorder Dashboard, Expiry Dashboard |
| **Products** | Products, Parts, Product Types, Attributes |
| **Manufacturing** | Manufacturing Dashboard, Work Orders, Batch Cooking |
| **Analytics** | KPI Dashboard, Reports, Breakeven Calculator |
| **Team** | Task Manager, Activity Log |
| **Admin** | User Management, Settings, Login Log |

### 4.2 Notification Badges

You may see small numbered badges on certain buttons:

- **Tasks badge** — number of unread mentions in task comments
- **Unverified Items badge** — products that have been added but not yet verified
- **Cycle Count badge** — locations overdue for a physical count

These are alerts that need your attention.

---

## 5. Sales & Orders

### 5.1 Viewing Orders (Sales Dashboard)

1. From the Main Menu, click **Sales Dashboard**.
2. Use the **Store** dropdown to filter by Shopify store or view all orders.
3. Set a **date range** using the From/To date pickers.
4. Use the **minimum amount** filter to narrow down by order value.
5. Click **Fetch Latest** to pull new orders from Shopify.
6. Click **Sync to ERP** to import Shopify orders into the database.

The order list shows order number, customer, date, total, and status.

### 5.2 Creating a Manual Sales Order

Use this for orders placed directly (not through Shopify).

1. From the Main Menu, click **Create Order**.
2. **Search for the customer** using the search box. Type part of the name or email and press Enter or click Search.
   - If the customer does not exist, you can enter their details manually.
3. Set the **Order Date**.
4. Select the **Currency** (e.g., ZAR, USD). All prices entered will be in this currency.
5. Set the **Order Type** (e.g., Wholesale, Retail).
6. **Add line items:**
   - Click **Add Item** or search for a product by SKU or name.
   - Enter the **quantity** and confirm the **unit price**.
   - Repeat for each product.
7. **Apply a discount** (optional):
   - If the customer has a discount tier, it may apply automatically.
   - You can also enter a fixed discount amount or a percentage.
8. Enter **shipping cost** if applicable.
9. Review the **order total** shown at the bottom (in both selected currency and home currency if different).
10. Set the **status** (Draft or Live).
11. Click **Save**.

> **Draft orders** are saved but not finalised. Set to **Live** once confirmed.

### 5.3 Customer Discount Tiers

Discount tiers allow you to apply automatic discounts to specific customers (e.g., wholesale accounts).

- Tiers are set up in **Settings > Customer Tiers** (Admin only).
- When a customer with a tier is selected on an order, the discount applies automatically.
- You can override the discount on any individual order.

### 5.4 Multi-Currency Orders

- Select the correct currency when creating the order.
- Exchange rates are configured by your administrator in Settings.
- The system will display the equivalent home currency total for reference.
- Reports and financial summaries convert all orders back to home currency.

### 5.5 Creating a Return (RMA)

Use this when a customer returns goods from a completed order.

1. From the Main Menu, click **Customers**.
2. Search for the customer and open their record.
3. Select the order you are creating a return against.
4. Click **Create Return**.
5. The return form shows all items from the original order.
6. For each item being returned, enter the **Return Qty**.
7. Set the **Condition** for each returned item:
   - **Resalable** — can be restocked
   - **Damaged** — write-off or quarantine
   - **Destroy** — to be disposed of
8. Enter a **Reason** for the return (required).
9. Add any **Notes** (optional).
10. Click **Submit Return**.

The return is created with status **Pending Approval**. An administrator must review and approve it before stock adjustments are made.

---

## 6. Purchase Orders & Receiving

### 6.1 Creating a Purchase Order (PO)

1. From the Main Menu, click **Purchase Orders**.
2. Click **New PO** or **Create PO**.
3. Select the **Supplier** from the dropdown.
4. Set the **Expected Delivery Date**.
5. Add line items: search for the product/part, enter quantity and unit cost.
6. Review the totals.
7. Click **Save**. The PO is saved as **Draft**.

### 6.2 PO Statuses

| Status | Meaning |
|---|---|
| Draft | PO created but not yet sent to the supplier |
| Sent | PO has been sent to the supplier |
| Partially Received | Some items have been received, others still outstanding |
| Received | All items received and inventory updated |
| Cancelled | PO was cancelled |

Update the PO status manually as it progresses (e.g., mark as Sent once you have emailed/called the supplier).

### 6.3 Receiving Items

When goods arrive from a supplier:

1. Open the relevant PO in **Purchase Orders**.
2. Click **Receive Items**.
3. The receive screen will show the expected items and quantities.
4. Enter the **actual quantities received** for each line.
5. Select the **destination location** (where the stock will go).
6. Click **Confirm Receipt**.

The system will:
- Update inventory quantities at the chosen location.
- Record the transaction in the inventory log.
- Update the PO status to Partially Received or Received.

> If you receive fewer items than ordered, the PO status becomes **Partially Received**. You can receive the remaining items later when they arrive.

### 6.4 Auto-Reorder Report

The Reorder Report identifies products that have fallen below their reorder point.

1. Click **Reorder Report** from the Main Menu.
2. Review the list of items that need restocking.
3. Select items to reorder.
4. Click **Generate PO** to automatically create a purchase order for the selected items and their default suppliers.

---

## 7. Inventory Management

### 7.1 Inventory Snapshot

The Inventory Snapshot gives a read-only overview of current stock levels across all locations.

1. Click **Inventory Snapshot** from the Main Menu.
2. Use the **status filter** to view:
   - **All** — every product
   - **Negative** — stock gone below zero (requires investigation)
   - **Zero** — items with no stock
   - **Low** — items below their reorder point
   - **OK** — items with sufficient stock
3. Items **expiring within 30 days** are highlighted — check these regularly.

> This screen is read-only. To adjust quantities, use Adjust Stock.

### 7.2 Adjusting Stock

Use this to correct quantities when there is a discrepancy not related to a sale, PO, or cycle count.

1. Click **Adjust Stock** from the Main Menu.
2. Search for the product.
3. Select the **location**.
4. Enter the **adjustment quantity** (positive to add, negative to remove).
5. Enter a **reason** for the adjustment.
6. Click **Save**.

All adjustments are logged with your username, date, and reason.

### 7.3 Transferring Stock Between Locations

1. Click **Stock Transfer** from the Main Menu.
2. Select the **source location** (where stock is coming from).
3. Select the **destination location** (where stock is going to).
4. Search for the product and enter the **quantity to transfer**.
5. Click **Transfer**.

The system will reduce stock at the source and increase it at the destination.

### 7.4 Cycle Count (Physical Stock Count)

A cycle count is a physical inventory check where you count stock on the shelf and compare it to the system quantity.

**When to do a cycle count:**
- On a regular schedule set by management (e.g., monthly per location)
- When you suspect a discrepancy
- After a large receiving or manufacturing run

**How to perform a cycle count:**

1. Click **Cycle Count** from the Main Menu.
2. Select the **Location** to count.
3. Optionally tick **Show uncounted only** to focus on items not yet counted.
4. For each product in the list:
   - The **System Qty** column shows what the database expects.
   - Enter the **Actual Qty** you physically counted.
5. Once all counts are entered, click **Verify All** to confirm all rows, or select individual rows and click **Verify Selected**.
6. The system records the variance (difference between system and actual) and updates quantities accordingly.
7. The **Last Verified** column will show your name and the date.

> **Negative variance** means less stock was found than the system shows. This could indicate theft, damage, or recording errors — investigate significant variances.

### 7.5 Unverified Items

New products added to the system that have not yet been physically verified will appear in the **Unverified Items** screen.

1. Click the **Unverified Items** badge or find it in the Inventory section.
2. Review each item.
3. Once you have confirmed the product and its initial stock count, click **Mark as Verified**.

### 7.6 Backorder Dashboard

The Backorder Dashboard shows open sales orders that could not be fully fulfilled due to insufficient stock.

1. Click **Backorder Dashboard** from the Main Menu.
2. The list shows each backordered item: order number, customer, product, backordered quantity, and the date the backorder was created.
3. When stock becomes available, select the relevant rows and click **Fulfil** to allocate stock and progress the order.

> Review the backorder dashboard regularly to ensure customers are not waiting unnecessarily.

### 7.7 Expiry Dashboard

The Expiry Dashboard tracks products that are approaching or past their expiry date.

1. Click **Expiry Dashboard** from the Main Menu.
2. The dashboard shows products grouped by expiry status:
   - **Expired** — past their expiry date (action required)
   - **Expiring Soon** — within 30 days (monitor closely)
   - **OK** — more than 30 days remaining
3. Use this screen to identify stock that should be prioritised for sale or written off.

---

## 8. Products & Parts

### 8.1 Adding a New Product

1. Click **Products** from the Main Menu.
2. Click **Add New** or **New Product**.
3. Fill in the required fields:
   - **SKU** — unique product code (required)
   - **Name** — product description
   - **Retail Price** — selling price to end customers
   - **Wholesale Price** — selling price to trade customers
   - **Reorder Point** — minimum quantity before a restock alert is triggered
4. Fill in optional fields as needed (description, product type, vendor, **Unit of Measure**, attributes).
5. Click **Save**.

### 8.2 Searching for a Product

1. Click **Products** or use the **Product Search** from any order screen.
2. Type part of the SKU or name.
3. Press **Enter** or click **Search**.
4. Click on a product to view its details.

> Viewers (read-only users) can search and view products but cannot edit them.

### 8.3 Product Types & Attributes

- **Product Types** — categories that group products (e.g., Clothing, Accessories). Managed under Products > Product Types.
- **Attributes** — custom options like size or colour. Managed under Products > Attribute Lists.

These are used to organise the product catalogue. Only Admins and Editors can add or change these.

### 8.4 Parts & Bills of Material (BOM)

Parts are the components used to build finished products (e.g., raw materials, packaging).

- **Parts Manager** — create and manage parts records. Each part can have a **Unit of Measure** (e.g., kg, L, each) set to match how it is measured and consumed.
- **BOM Explorer** — view and edit the Bill of Materials for each product (what components are used, and in what quantities).

Parts are consumed when a manufacturing work order is completed.

---

## 9. Manufacturing

### 9.1 Understanding Manufacturing Orders and Work Orders

- A **Manufacturing Order (MO)** is an instruction to produce a batch of products.
- Each MO contains one or more **Work Orders (WOs)** — one WO per product to be made.

### 9.2 Creating a Manufacturing Order

1. Click **Manufacturing Dashboard** from the Main Menu.
2. Click **New Manufacturing Order**.
3. Enter a name or reference for the MO.
4. Add each product to be manufactured:
   - Search for the product.
   - Enter the quantity to produce.
5. Click **Save**. Work orders are automatically created for each product line.

### 9.3 Processing Work Orders

1. Click **Work Orders** from the Main Menu.
2. Filter by date range if needed.
3. Find the work order you are working on.
4. Click **Mark In Progress** when production begins.
5. When production is complete, click **Complete**.
6. In the completion screen:
   - Confirm the **quantity produced**.
   - Enter the **cost of goods** (parts cost, labour, etc.).
   - Select the **destination location** where finished stock will go.
7. Click **Confirm**.

The system will:
- Add the produced quantity to inventory.
- Record the cost of goods for use in profit reports.
- Mark the work order as completed.

### 9.4 Batch Cooking

Batch Cooking allows you to combine multiple work orders into a single cook session, aggregate the ingredient list, and export production documentation.

**Starting a Batch Cook Session:**

1. Click **Batch Cooking** from the Main Menu (Manufacturing section).
2. The left panel shows all open work orders eligible for cooking.
3. **Tick the checkbox** next to each work order you want to include in this batch.
4. The right panel automatically updates to show the **aggregated ingredient list** — all parts required across the selected work orders, summed together, with on-hand quantities shown.
5. Review the ingredient list. Rows marked ✓ indicate sufficient stock on hand.
6. Enter a **Session Name** (optional) to identify this cook run.
7. Click **▶ Start Cook Session**.

**During a Cook Session (FormCookSession):**

Once started, the cook session screen opens. Here you work through each ingredient per work order:

1. The session shows each work order and its required ingredients.
2. As each ingredient batch is prepared, tick it off in the grid.
3. When all ingredients for a work order are done, you can mark it complete individually.
4. Once all work orders in the session are finished, click **Complete Session**.

**Exporting Production Documents:**

Before or after starting a session, you can export:

- **📄 Export Traveller CSV** — a batch traveller sheet listing all selected work orders, products, quantities, and aggregated ingredients. Use this as the physical document that travels with the batch through production.
- **🏷 Export Labels CSV** — a label sheet with one row per unit to be produced. Import into a label printer to generate product labels for the batch.

> Export the Traveller CSV before starting production so the team has a physical checklist to follow.

---

## 10. Reporting & Analytics

### 10.1 KPI Dashboard

The KPI Dashboard shows a live summary of the business at a glance.

Click **KPI Dashboard** from the Main Menu to see:

| KPI | What it shows |
|---|---|
| Today's Orders | Number of orders placed today |
| Today's Revenue | Total order value today |
| Pending Orders | Orders not yet fulfilled |
| In-Stock Products | Number of products with stock > 0 |
| Out of Stock / Low Stock | Products at zero or below reorder point |
| Open Work Orders | Manufacturing work orders in progress |
| Overdue Tasks | Tasks past their due date |
| Total Inventory Value | All stock quantities × retail price |

### 10.2 Reports

Click **Reports** from the Main Menu. There are five report tabs:

#### Stock on Hand
Shows current inventory quantities and values across all locations.
- Filter by location or SKU.
- Values shown at both retail and wholesale prices.
- Export to CSV for further analysis.

#### Sales by Period
Shows all orders within a chosen date range.
- Filter by date, customer, or store.
- Shows order totals in home currency.

#### COGS Summary
Shows cost of goods sold, pulled from completed work orders.
- Useful for understanding production costs over a period.

#### Cycle Count Variance
Shows discrepancies from past cycle counts.
- Helps identify recurring shrinkage or recording issues.

#### Gross Profit
Shows revenue minus COGS per product.
- Displays profit margin percentage.
- Helps identify most and least profitable products.

**To run any report:**
1. Select the report tab.
2. Set date range and any filters.
3. Click **Run** or **Refresh**.
4. Click **Export to CSV** to download the data, or **Print** to generate a PDF.

### 10.3 Breakeven Calculator

The Breakeven Calculator is a financial planning tool.

1. Click **Breakeven Calculator** from the Main Menu.
2. Enter your fixed costs (rent, salaries, etc.) and variable costs per unit.
3. Enter your average selling price.
4. The calculator shows how many units must be sold to break even.

---

## 11. Task Management

### 11.1 Creating a Task

1. Click **Task Manager** from the Main Menu.
2. Click **New Task**.
3. Enter:
   - **Title** — short description of the task
   - **Assigned To** — select a user from the dropdown
   - **Due Date** — when the task should be completed
   - **Details** — full description or instructions
4. Click **Save**.

### 11.2 Viewing and Managing Tasks

- The task list shows all tasks. Use the **filter by user** dropdown to see tasks assigned to a specific person.
- Overdue tasks are highlighted in red.
- To mark a task done: select it and click **Mark Done**, or select multiple tasks and use **Bulk Mark Done**.
- Click on a task to open the **Task Detail** screen and view the full history and comments.

### 11.3 Task Comments & Mentions

Inside a task's detail screen, you can add comments to discuss the task.

- Type your comment in the comment box.
- To notify a specific user, type **@username** in your comment (e.g., `@jane`). They will receive a badge notification on their Main Menu.
- Click **Add Comment** to post.

> Check your mention badges on the Main Menu regularly to stay up to date on tasks where you have been mentioned.

### 11.4 Task Statuses

| Status | Meaning |
|---|---|
| Open | Task created, not yet started |
| In Progress | Work has begun |
| Done | Task is complete |

---

## 12. System Administration

> The following sections apply to **Administrators only**.

### 12.1 Managing Users

1. Click **User Management** from the Main Menu.
2. To **add a new user**: click **New User**, enter their name, username, password, and assign their role and permissions. Click **Save**.
3. To **edit a user**: select them from the list and update their details or permissions. Click **Save**.
4. To **deactivate a user**: edit their record and disable their account. They will no longer be able to log in.

> User accounts should be created individually — do not share login credentials. Every action in the system is logged against the user account.

### 12.2 System Settings

Click **Settings** from the Main Menu to configure:

| Setting | Description |
|---|---|
| Company Logo | Upload the logo shown in the application |
| Currency | Set home currency and exchange rates |
| Password Policy | Set max failed login attempts and lockout duration |
| Backup Schedule | Set how often the database is backed up and where |
| Backup Folder | Set the destination folder for backup files |
| Accent Colour | Customise the application colour scheme |
| SMTP Email | Configure email settings for system notifications |
| Admin Contact | Set the admin name/phone shown on the login screen |
| Remember Username | Enable/disable the remember username option |
| Units of Measure | Manage the list of units (kg, L, each, etc.) available for products and parts |

### 12.3 Viewing Logs

**Activity Log** — shows a record of all data changes (creates, edits, deletes) made by all users. Use this to investigate discrepancies or track down who changed something.

**Login Log** — shows all login and logout events with timestamps and usernames. Use this to verify who accessed the system and when.

To access logs: click **Activity Log** or **Login Log** from the Main Menu (requires Log permission or Admin role).

---

## 13. Data Import & Export

### 13.1 Importing Data

Bulk data can be imported via CSV files.

1. Click **Imports** from the Main Menu.
2. Select the data type to import (e.g., Products, Customers, Inventory).
3. Download the **CSV template** to see the required format.
4. Prepare your CSV file following the template exactly.
5. Click **Browse**, select your file.
6. Click **Import**.
7. Review any errors or warnings shown after the import.

> Always review the template before creating your import file. Incorrect formatting will cause errors. Test with a small batch first.

### 13.2 Exporting Data

1. Click **Exports** from the Main Menu.
2. Select the data type to export.
3. Apply any filters (e.g., date range).
4. Click **Export to CSV**.
5. Choose where to save the file.

Reports also have their own export buttons — see Section 10.

---

## 14. Troubleshooting & FAQs

### My account is locked
Contact your administrator. Accounts lock after repeated failed login attempts. The lockout will clear automatically after the configured period, or an admin can reset it manually.

### I can't see a button or screen
Your role or permissions may not include access to that area. Contact your administrator if you believe you should have access.

### The system logged me out automatically
The session timeout is 30 minutes of inactivity. Log back in. If you were in the middle of data entry that was not saved, you will need to re-enter it.

### Stock quantities look wrong
1. Check the **Inventory Snapshot** to confirm current quantities.
2. Check the **Activity Log** to see if a recent adjustment was made.
3. Run a **Cycle Count** on the affected location to verify physical quantities.
4. If the discrepancy cannot be explained, use **Adjust Stock** with a written reason.

### A purchase order is stuck on "Partially Received"
This means not all items on the PO have been received yet. When the remaining items arrive, open the PO, click **Receive Items**, and complete the receipt. The status will update to **Received** automatically once all quantities are confirmed.

### A Shopify order is not appearing in the Sales Dashboard
1. Click **Fetch Latest** to pull fresh data from Shopify.
2. Check your date range filter — the order may fall outside the selected period.
3. Check the minimum amount filter is not hiding low-value orders.
4. If the order still does not appear, contact your administrator to check the Shopify sync configuration.

### How do I correct a mistake on an order?
Open the order and edit it. All changes are logged in the Activity Log. If an order cannot be edited (e.g., it is in a finalised status), contact your administrator.

### I cannot find a product when searching
- Try searching by SKU instead of name, or vice versa.
- Check that the product exists — go to Products and confirm.
- The product may have a different name or SKU than expected. Ask the person who added it.

---

*For technical issues or system errors, contact your system administrator.*

*This document should be reviewed and updated whenever significant changes are made to the software.*
