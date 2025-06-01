# Image Service Provider

A robust Azure Functions-based service for managing images with efficient storage, retrieval, and caching capabilities.

## ✨ Features

*   **Image Upload:** Secure upload of images to Azure Blob Storage with metadata tracking.
*   **Image Retrieval:** Fast image access via unique identifiers with caching support.
*   **Image Deletion:** Complete removal of images from storage and database.
*   **Format Support:** Handles various image formats with special processing for SVG files.
*   **Content Type Detection:** Automatic handling of content types, including proper SVG content type resolution.
*   **Performance Optimized:** In-memory caching for frequently accessed images.

## 🛠️ Technical Stack

*   **Backend:** .NET 9.0 with C# 13.0
*   **Architecture:** Azure Functions v4 (isolated process model)
*   **Storage:**
    *   Azure Blob Storage for image files
    *   SQL Server via Entity Framework Core for metadata
*   **Caching:** In-memory caching system with configurable expiration
*   **API:** RESTful endpoints with OpenAPI support

## 🔌 API Endpoints

The following endpoints are available:

*   `POST /api/images` - Upload a new image
    *   **Request Body:** `multipart/form-data` containing the image file.
    *   **Response:** Details of the uploaded image, including its ID.
*   `GET /api/images/{imageId}` - Retrieve image by ID
    *   **Path Parameter:** `{imageId}` - The unique identifier of the image.
    *   **Response:** The image file with the appropriate content type.
*   `DELETE /api/images/{imageId}` - Delete an image
    *   **Path Parameter:** `{imageId}` - The unique identifier of the image.
    *   **Response:** Confirmation of deletion.

*(Note: The `/api/` prefix is common for Azure Functions but adjust if your routing is different.)*

## 🚀 Getting Started

To run this project locally, follow these steps:

### Prerequisites

*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
*   Azure Storage Emulator (e.g., [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite)) or an actual Azure Storage account.
*   SQL Server instance (e.g., SQL Server Express, Docker image).

### Setup

1.  **Clone the repository:**
    ```bash
    git clone <YOUR_REPOSITORY_URL>
    cd <PROJECT_DIRECTORY_NAME>
    ```

2.  **Configure Environment Variables:**
    Create a `local.settings.json` file in the root of your Azure Functions project (where the `host.json` file is located). This file is not typically committed to source control.

    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true", // Or your actual storage connection string for Functions runtime
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "SqlConnection": "YOUR_SQL_CONNECTION_STRING",
        "BlobStorage:ConnectionString": "YOUR_AZURE_STORAGE_CONNECTION_STRING", // Can be UseDevelopmentStorage=true if using Azurite
        "BlobStorage:ContainerName": "YOUR_BLOB_CONTAINER_NAME"
      }
    }
    ```
    *   Replace placeholders with your actual connection strings and container name.
    *   For `BlobStorage:ConnectionString`, if using Azurite, you can often use `UseDevelopmentStorage=true`.

3.  **Database Setup (if applicable):**
    *   Ensure your SQL Server database is running.
    *   Apply Entity Framework Core migrations (if you're using Code First):
        ```bash
        dotnet ef database update
        ```

4.  **Build and Run:**

    *   **Using Visual Studio 2022:**
        1.  Open the solution (`.sln`) file.
        2.  Set the Azure Functions project as the startup project.
        3.  Press `F5` or click the "Start" button.

    *   **Using .NET CLI:**
        Navigate to the Azure Functions project directory and run:
        ```bash
        dotnet build
        func start
        ```

## ✅ Testing

The service includes comprehensive testing strategies:

*   **Unit Tests:** Leveraging the Moq framework to isolate and test individual components and business logic.
*   **Integration Tests:** Employing Testcontainers with Azurite to simulate real Azure Storage interactions, ensuring reliable integration with external services.

To run the tests:

*   **Using Visual Studio Test Explorer:** Open the Test Explorer window and run all or selected tests.
*   **Using .NET CLI:**
    ```bash
    dotnet test
    ```

## 🧩 Key Dependencies

This project relies on several key libraries and frameworks:

*   Azure.Storage.Blobs
*   Microsoft.EntityFrameworkCore (and relevant provider, e.g., Microsoft.EntityFrameworkCore.SqlServer)
*   Microsoft.Azure.Functions.Worker
*   Microsoft.Azure.Functions.Worker.Extensions.* (various, e.g., Http, Storage)
*   Microsoft.Extensions.Caching.Memory
*   Moq (for unit testing)
*   Testcontainers.Azurite (for integration testing)

## Uploading Image Sequence Diagram

![BLF1RJ~1](https://github.com/user-attachments/assets/eea97ddd-e368-4515-b62d-a566c5ef2d9b)
