# E4 - Eventing & Outbox Pattern Implementation

## Overview
E4 implements the **Outbox Pattern** and **Idempotent Consumers** to ensure reliable message delivery and prevent duplicate processing in our event-driven microservices architecture.

## Key Components

### 🔄 T4.1 - Outbox Table + Transactional Write
**Location**: `services/shared/Outbox/`

- **OutboxEvent**: Entity storing events to be published
- **IOutboxService**: Service for publishing events transactionally
- Events are stored in the same database transaction as business logic
- Ensures no events are lost during application restarts

```csharp
// Usage in services
await _outboxService.PublishEventAsync(
    "TransactionCreated",
    eventData,
    "Account", 
    accountId.ToString(),
    correlationId,
    causationId,
    userId
);
```

### 🚀 T4.2 - Outbox Dispatcher (Background Service)
**Location**: `services/shared/Outbox/OutboxDispatcherService.cs`

- Background service that polls unprocessed outbox events
- Publishes events to Kafka with proper headers
- Configurable batch size and polling interval
- Handles retries and marks events as processed

**Configuration**:
```json
{
  "Outbox": {
    "BatchSize": 50,
    "IntervalSeconds": 5,
    "MaxRetries": 5,
    "TopicPrefix": "ebanking"
  }
}
```

### 🔒 T4.3 - Idempotent Consumers
**Location**: `services/shared/Idempotency/` & `services/shared/Kafka/`

- **ProcessedEvent**: Tracks processed events per consumer
- **IdempotentKafkaConsumer**: Base class for all Kafka consumers
- Prevents duplicate processing of the same event
- Uses composite key: (EventId + ConsumerName)

**Key Features**:
- Automatic event ID extraction from headers or message offset
- Duplicate detection before processing
- Graceful handling of already processed events

### 🚨 T4.4 - Dead Letter Queue (DLQ) + Alerts
**Location**: `services/shared/DeadLetter/`

- **DeadLetterEvent**: Stores failed events with full context
- **IDeadLetterService**: Manages DLQ operations
- Events sent to DLQ after max retries exceeded
- Includes alerting mechanism (logging at CRITICAL level)

**DLQ Features**:
- Stores original payload, headers, and error details
- Tracks retry attempts
- Admin endpoints for DLQ management
- Resolution tracking with notes

## Implementation Status

### ✅ Services Updated

1. **Account Service**
   - ✅ OutboxEvent table added to DbContext
   - ✅ E4 services registered in Program.cs
   - ✅ AccountService updated to use Outbox pattern
   - ✅ Transactional event publishing implemented

2. **Audit Service**
   - ✅ E4 entities added to DbContext
   - ✅ KafkaAuditConsumer converted to IdempotentKafkaConsumer
   - ✅ ProcessedEvent and DeadLetterEvent tables configured
   - ✅ Integrity verification with hash chains

3. **Transfer Service** - *Ready for update*
4. **Payment Service** - *Ready for update*
5. **Notification Service** - *Ready for update*

## Database Schema Changes

Each service now includes these additional tables:

```sql
-- Outbox Pattern
CREATE TABLE OutboxEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    EventType NVARCHAR(100) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    AggregateType NVARCHAR(50) NOT NULL,
    AggregateId NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    ProcessedAt DATETIME2 NULL,
    IsProcessed BIT NOT NULL DEFAULT 0,
    -- ... additional fields
);

-- Idempotency
CREATE TABLE ProcessedEvents (
    EventId NVARCHAR(100) NOT NULL,
    ConsumerName NVARCHAR(100) NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    ProcessedAt DATETIME2 NOT NULL,
    PRIMARY KEY (EventId, ConsumerName)
);

-- Dead Letter Queue
CREATE TABLE DeadLetterEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OriginalTopic NVARCHAR(100) NOT NULL,
    ConsumerName NVARCHAR(100) NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    ErrorMessage NVARCHAR(MAX) NOT NULL,
    RetryCount INT NOT NULL,
    FailedAt DATETIME2 NOT NULL,
    IsResolved BIT NOT NULL DEFAULT 0
    -- ... additional fields
);
```

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "Kafka": "localhost:9092"
  },
  "Outbox": {
    "BatchSize": 50,
    "IntervalSeconds": 5,
    "MaxRetries": 5,
    "TopicPrefix": "ebanking"
  }
}
```

### Registration in Program.cs
```csharp
// Register E4 pattern for each service
builder.Services.AddE4EventingPattern<YourDbContext>(builder.Configuration);
```

## Event Flow

```
1. Business Transaction
   ├── Update Business Entity
   ├── Store Event in Outbox ─┐
   └── Commit Transaction      │
                               │
2. Outbox Dispatcher          │
   ├── Poll Outbox Events ────┘
   ├── Publish to Kafka
   └── Mark as Processed

3. Consumer Processing
   ├── Check if Already Processed (Idempotency)
   ├── Process Event
   ├── Mark as Processed
   └── On Failure → Retry → DLQ
```

## Benefits Achieved

### 🎯 DoD Compliance
- ✅ **No Lost Messages**: Outbox pattern ensures events survive restarts
- ✅ **No Duplicates**: Idempotent consumers ignore already processed events
- ✅ **Reliable Processing**: DLQ handles persistent failures
- ✅ **Observability**: Full audit trail and error tracking

### 🔧 Operational Benefits
- **Transactional Consistency**: Events and business data updated atomically
- **Retry Logic**: Configurable retry attempts with exponential backoff
- **Error Handling**: Failed events captured with full context
- **Monitoring**: Dead letter events trigger alerts
- **Recovery**: Manual resolution of DLQ items with admin tools

## Monitoring & Alerts

### Key Metrics to Monitor
- Outbox processing lag
- Dead letter queue size
- Consumer processing rates
- Duplicate event detection rates

### Alert Conditions
- DLQ events created (CRITICAL log level)
- High outbox processing lag
- Consumer group lag
- Integrity check failures

## Testing

### Unit Tests
- Outbox service event publishing
- Idempotency service duplicate detection
- Dead letter service error handling

### Integration Tests
- End-to-end event flow through outbox
- Consumer idempotency verification
- DLQ behavior under failure conditions

## Next Steps

1. **Complete remaining services** (Transfer, Payment, Notification)
2. **Add monitoring dashboards**
3. **Implement alerting integrations** (email, Slack)
4. **Performance optimization** for high-volume scenarios
5. **Add admin UI** for DLQ management

---

**E4 Status**: 🟡 **In Progress** - Account & Audit services complete, others pending
