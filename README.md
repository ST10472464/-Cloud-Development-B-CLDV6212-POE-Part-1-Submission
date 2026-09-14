# CoffeeNChill Canteen Management System

## Team Members
- Keyur Keshav
- Yadav Iserbelas (ST10472501)
- Saiyen Subban

## YouTube Demo
[Video Demo Link](#) <!-- Replace # with your actual YouTube URL -->

---

## Project Overview

CoffeeNChill is a cloud-enabled microservices system for managing a campus canteen. This project demonstrates Azure Storage, Serverless Functions, Docker Containerization, and Queue-based asynchronous processing.

---

## Part 1 — Azure Functions, Tables, Files & Docker Hub

### What Was Implemented
- **Azure Table Storage** for MenuItems with CRUD operations
- **HTTP Azure Functions** for menu management (Create, Read, Update, Delete)
- **Azure File Share** integration for staff document storage (Upload, List, Download)
- **Docker containerization** for local development with Azurite emulation

### Menu Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/menu` | Create a new menu item |
| GET | `/api/menu` | Get all menu items |
| GET | `/api/menu/category/{category}` | Get items by category |
| PUT | `/api/menu/{category}/{id}` | Update a menu item |
| DELETE | `/api/menu/{category}/{id}` | Delete a menu item |

### Document Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/documents/upload` | Upload staff document (multipart/form-data) |
| GET | `/api/documents` | List all documents with metadata |
| GET | `/api/documents/download/{fileName}` | Download document as file stream |

### Local Setup (Part 1)

1. **Install Docker Desktop** if not already installed

2. **Run Azurite Container:**
   ```bash
   docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite
   ```

3. **Create local.settings.json** in the project folder:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
     }
   }
   ```

4. **Run the Functions:**
   ```bash
   cd "Cloud Development B CLDV 6212- POE Part 1"
   func start
   ```

5. **Docker Commands (Part 1):**
   ```bash
   # Build the image
   docker build -t st10472501/coffeennchill-functions:v1.0 .

   # Run the container
   docker run -p 7058:80 st10472501/coffeennchill-functions:v1.0

   # Push to Docker Hub
   docker push st10472501/coffeennchill-functions:v1.0
   ```

---

## Part 2 — Queue Triggers & Docker Compose Orchestration

### Changelog
- Added `order-processing-queue` for asynchronous order processing
- Added `Orders` Azure Table for order tracking
- Added queue producer endpoint (`POST /api/orders/queue`)
- Added queue-triggered function (`ProcessOrderQueue`)
- Added order status transitions: Received → Preparing → Ready → Collected
- Added poison queue handling for failed messages
- Added `docker-compose.yml` for multi-container orchestration
- Added `Order` and `OrderEntity` models

### Order Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/orders/queue` | Queue a new order |
| GET | `/api/orders` | Get all orders |
| GET | `/api/orders/{date}` | Get orders by date |

### Queue Workflow
1. Student places order via `POST /api/orders/queue`
2. Order is validated, serialized to JSON, and Base64 encoded
3. Message pushed to `order-processing-queue`
4. `ProcessOrderQueue` function triggers automatically
5. Order written to `Orders` table with status "Received"
6. Status transitions: Received → Preparing → Ready → Collected
7. Failed messages sent to `order-processing-queue-poison`

### Local Setup (Part 2)

1. **Docker Compose (Recommended):**
   ```bash
   # Set your Docker Hub username
   export DOCKERHUB_USERNAME=st10472501

   # Start all services
   docker-compose up -d

   # Check logs
   docker-compose logs -f functions
   ```

2. **Manual Setup:**
   ```bash
   # Run Azurite
   docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 --name azurite mcr.microsoft.com/azure-storage/azurite

   # Run Functions
   cd "Cloud Development B CLDV 6212- POE Part 1"
   func start
   ```

### Docker Commands (Part 2)
   ```bash
   # Build the v2.0 image
   docker build -t st10472501/coffeennchill-functions:v2.0 .

   # Push to Docker Hub
   docker push st10472501/coffeennchill-functions:v2.0

   # Run with Docker Compose
   docker-compose up -d
   ```

---

## Postman Collection

Import the Postman collection from `/docs/CoffeeNChill.postman_collection.json` to test all endpoints.

### Testing the Queue
1. First, create some menu items using the Menu endpoints
2. Queue an order using `POST /api/orders/queue`
3. Wait 7-10 seconds for processing (status transitions take ~7 seconds)
4. Check order status using `GET /api/orders` - verify status is "Collected"

---

## Technology Stack
- **Runtime:** .NET 10, Azure Functions v4 (Isolated Worker)
- **Storage:** Azure Table Storage, Azure File Share, Azure Storage Queue
- **Containerization:** Docker, Docker Compose
- **Emulation:** Azurite for local Azure Storage

---

## Contributing
Each team member must make a minimum of 5 meaningful commits per part.

---

## License
Educational use only - CLDV6212 POE
