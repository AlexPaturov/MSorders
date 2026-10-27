# Technical Debt

1. **Missing Docker Compose configuration**

   The project does not provide a `docker-compose.yml` file for local development or CI pipelines.  Without it, developers must manually build the image and run containers with individual `docker run` commands, which is error‑prone and hard to maintain.

   *Solution:* Add a minimal Docker Compose definition that builds the service from the repository root, exposes the required port, and mounts a named volume for data persistence.

   ```yaml
   version: "3.8"

   services:
     orders:
       build: .
       container_name: orders-container
       ports:
         - "8080:8080"
       volumes:
         - orders-data:/app/data

   volumes:
     orders-data:
   ```
