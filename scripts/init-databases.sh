#!/bin/bash

# Database initialization script for E-banking services
# Creates databases and runs initial migrations

set -e

echo "🗄️ Initializing E-banking databases..."

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

print_step() {
    echo -e "${BLUE}==>${NC} $1"
}

print_success() {
    echo -e "${GREEN}✅${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠️${NC} $1"
}

print_error() {
    echo -e "${RED}❌${NC} $1"
}

# Wait for MSSQL to be ready
print_step "Waiting for MSSQL to be ready..."
kubectl wait --for=condition=ready pod -l app=mssql -n data --timeout=300s

# Get MSSQL pod name
MSSQL_POD=$(kubectl get pods -n data -l app=mssql -o jsonpath='{.items[0].metadata.name}')

if [ -z "$MSSQL_POD" ]; then
    print_error "MSSQL pod not found"
    exit 1
fi

print_success "MSSQL pod found: $MSSQL_POD"

# Function to execute SQL commands
execute_sql() {
    local sql_command="$1"
    local description="$2"
    
    echo "Executing: $description"
    kubectl exec -n data $MSSQL_POD -- /opt/mssql-tools/bin/sqlcmd \
        -S localhost \
        -U sa \
        -P 'YourStrong@Passw0rd' \
        -Q "$sql_command"
}

# Create databases
print_step "Creating databases..."

execute_sql "CREATE DATABASE AccountService;" "Account Service database"
execute_sql "CREATE DATABASE TransferService;" "Transfer Service database" 
execute_sql "CREATE DATABASE PaymentService;" "Payment Service database"
execute_sql "CREATE DATABASE NotificationService;" "Notification Service database"
execute_sql "CREATE DATABASE AuditService;" "Audit Service database"

print_success "Databases created successfully"

# Create database users (for production, use proper authentication)
print_step "Creating database users..."

execute_sql "CREATE LOGIN AccountServiceUser WITH PASSWORD = 'AccountService@2024!';" "Account Service user"
execute_sql "USE AccountService; CREATE USER AccountServiceUser FOR LOGIN AccountServiceUser; ALTER ROLE db_owner ADD MEMBER AccountServiceUser;" "Account Service permissions"

execute_sql "CREATE LOGIN TransferServiceUser WITH PASSWORD = 'TransferService@2024!';" "Transfer Service user"
execute_sql "USE TransferService; CREATE USER TransferServiceUser FOR LOGIN TransferServiceUser; ALTER ROLE db_owner ADD MEMBER TransferServiceUser;" "Transfer Service permissions"

execute_sql "CREATE LOGIN PaymentServiceUser WITH PASSWORD = 'PaymentService@2024!';" "Payment Service user"
execute_sql "USE PaymentService; CREATE USER PaymentServiceUser FOR LOGIN PaymentServiceUser; ALTER ROLE db_owner ADD MEMBER PaymentServiceUser;" "Payment Service permissions"

execute_sql "CREATE LOGIN NotificationServiceUser WITH PASSWORD = 'NotificationService@2024!';" "Notification Service user"
execute_sql "USE NotificationService; CREATE USER NotificationServiceUser FOR LOGIN NotificationServiceUser; ALTER ROLE db_owner ADD MEMBER NotificationServiceUser;" "Notification Service permissions"

execute_sql "CREATE LOGIN AuditServiceUser WITH PASSWORD = 'AuditService@2024!';" "Audit Service user"
execute_sql "USE AuditService; CREATE USER AuditServiceUser FOR LOGIN AuditServiceUser; ALTER ROLE db_owner ADD MEMBER AuditServiceUser;" "Audit Service permissions"

print_success "Database users created successfully"

# Initialize Account Service tables with sample data
print_step "Initializing Account Service with sample data..."

ACCOUNT_INIT_SQL="
USE AccountService;

-- Create Accounts table
CREATE TABLE Accounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(50) NOT NULL,
    AccountNumber NVARCHAR(20) NOT NULL UNIQUE,
    IBAN NVARCHAR(34) NOT NULL UNIQUE,
    Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'EUR',
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create Transactions table
CREATE TABLE Transactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccountId INT NOT NULL,
    Type NVARCHAR(10) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'EUR',
    Description NVARCHAR(500) NOT NULL,
    Reference NVARCHAR(100) NULL,
    CounterpartyName NVARCHAR(200) NULL,
    CounterpartyAccount NVARCHAR(34) NULL,
    TransactionDate DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CorrelationId NVARCHAR(50) NOT NULL,
    ExternalTransactionId NVARCHAR(100) NULL,
    FOREIGN KEY (AccountId) REFERENCES Accounts(Id)
);

-- Create Statements table
CREATE TABLE Statements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccountId INT NOT NULL,
    Year INT NOT NULL,
    Month INT NOT NULL,
    OpeningBalance DECIMAL(18,2) NOT NULL,
    ClosingBalance DECIMAL(18,2) NOT NULL,
    TotalDebits DECIMAL(18,2) NOT NULL,
    TotalCredits DECIMAL(18,2) NOT NULL,
    TransactionCount INT NOT NULL,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    GeneratedBy NVARCHAR(50) NOT NULL,
    FOREIGN KEY (AccountId) REFERENCES Accounts(Id),
    UNIQUE(AccountId, Year, Month)
);

-- Create indexes
CREATE INDEX IX_Accounts_UserId ON Accounts(UserId);
CREATE INDEX IX_Transactions_AccountId ON Transactions(AccountId);
CREATE INDEX IX_Transactions_TransactionDate ON Transactions(TransactionDate);
CREATE INDEX IX_Transactions_CorrelationId ON Transactions(CorrelationId);

-- Insert sample data
INSERT INTO Accounts (UserId, AccountNumber, IBAN, Balance, Currency) VALUES
('test-user-123', '1234567890', 'BA391234567890123456', 5000.00, 'EUR'),
('demo-user-456', '1234567891', 'BA391234567890123457', 3500.00, 'EUR');

-- Insert sample transactions
DECLARE @AccountId1 INT = (SELECT Id FROM Accounts WHERE UserId = 'test-user-123');
DECLARE @AccountId2 INT = (SELECT Id FROM Accounts WHERE UserId = 'demo-user-456');

INSERT INTO Transactions (AccountId, Type, Amount, Currency, Description, Reference, TransactionDate, CorrelationId) VALUES
(@AccountId1, 'CREDIT', 1000.00, 'EUR', 'Salary deposit', 'SAL001', DATEADD(day, -30, GETUTCDATE()), NEWID()),
(@AccountId1, 'DEBIT', 250.00, 'EUR', 'Grocery shopping', 'POS001', DATEADD(day, -25, GETUTCDATE()), NEWID()),
(@AccountId1, 'DEBIT', 100.00, 'EUR', 'ATM withdrawal', 'ATM001', DATEADD(day, -20, GETUTCDATE()), NEWID()),
(@AccountId1, 'CREDIT', 500.00, 'EUR', 'Transfer from friend', 'TRF001', DATEADD(day, -15, GETUTCDATE()), NEWID()),
(@AccountId1, 'DEBIT', 75.50, 'EUR', 'Online purchase', 'WEB001', DATEADD(day, -10, GETUTCDATE()), NEWID()),
(@AccountId1, 'DEBIT', 200.00, 'EUR', 'Utility bill', 'BILL001', DATEADD(day, -5, GETUTCDATE()), NEWID()),
(@AccountId2, 'CREDIT', 2000.00, 'EUR', 'Initial deposit', 'INIT001', DATEADD(day, -60, GETUTCDATE()), NEWID()),
(@AccountId2, 'DEBIT', 150.00, 'EUR', 'Restaurant', 'POS002', DATEADD(day, -30, GETUTCDATE()), NEWID()),
(@AccountId2, 'CREDIT', 300.00, 'EUR', 'Freelance payment', 'WORK001', DATEADD(day, -15, GETUTCDATE()), NEWID());
"

execute_sql "$ACCOUNT_INIT_SQL" "Account Service schema and sample data"

print_success "Account Service initialized with sample data"

# Show database status
print_step "Database status:"
execute_sql "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb');" "List of created databases"

echo ""
print_success "Database initialization completed!"
echo ""
echo "📊 Created databases:"
echo "   - AccountService (with sample data)"
echo "   - TransferService" 
echo "   - PaymentService"
echo "   - NotificationService"
echo "   - AuditService"
echo ""
echo "👥 Created users:"
echo "   - AccountServiceUser"
echo "   - TransferServiceUser"
echo "   - PaymentServiceUser" 
echo "   - NotificationServiceUser"
echo "   - AuditServiceUser"
echo ""
echo "🔗 Connection examples:"
echo "   Account Service: Server=mssql.data.svc.cluster.local,1433;Database=AccountService;User Id=AccountServiceUser;Password=AccountService@2024!;TrustServerCertificate=true;"
echo ""
echo "🧪 Test the Account Service:"
echo "   kubectl port-forward -n svc svc/account-service 8080:80"
echo "   curl http://localhost:8080/healthz"
