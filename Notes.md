# Online Betting Data Capture and Analytics System Submission

## Overview
- API (.NET 8) accepts wager events and enqueues them (no DB work on request path).
- RabbitMQ buffers traffic.
- Consumer (.NET 8) drains queue and bulk-inserts to SQL Server via TVP.
- SQL schema + procs back fast paginated reads and top-spenders.

## Endpoints
- POST /api/player/casinowager → enqueue message, return 200 OK.
- GET /api/player/{playerId}/casino?page=&pageSize= → paginated wagers.
- GET /api/player/topSpenders?count= → top N by total spend.

## Architecture
-  API (enqueue) → BufferedPublisherService (batch publish + confirms) → RabbitMQ → Consumer (prefetch + batch) → SQL (TVP proc).

## RabbitMQ Publisher (API)
- Controller writes to bounded Channel (IPublishQueue).
- BackgroundService:
    - Single connection/channel, QueueDeclare once.
    - Batch publish (e.g., 500 messages).
    - Publisher confirms per batch (ConfirmSelect + WaitForConfirms).
    - Persistent messages to durable queue.
- Result: low API latency, high throughput, no channel contention.

## Consumer
- AsyncEventingBasicConsumer + BasicQos(prefetch) (e.g., 250–500).
- Buffers messages, then InsertBulk via TVP:
    - Upsert Players (MERGE).
    - Insert Wagers if not exists (WagerId PK) → idempotent.
- One DB round-trip per batch.

## Database
- Tables: casino.Players (PK AccountId), casino.Wagers (PK WagerId, FK → Players).
- Procs:
    - usp_IngestWager (single).
    - usp_IngestWagersBulk (TVP).
    - usp_GetPlayerWagersPaged, usp_GetTopSpenders.

## Key Issues Fixed
- Publisher timeouts / AlreadyClosedException → replaced per-request confirms with batched background publisher.
- NullReference in controller → registered IPublishQueue + background service; removed parameterless ctor.

## Run
1. SQL: run DatabaseGenerate.sql on localhost (Windows auth).
2. RabbitMQ: docker compose up -d (Rabbit only).
3. API: dotnet run --project src/OT.Assessment.App
4. Consumer: dotnet run --project src/OT.Assessment.Consumer
5. Run tester; watch queue drain and DB fill.