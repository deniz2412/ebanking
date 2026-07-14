#!/bin/bash

# Platform testing script for E-banking
# Tests all E1 components are working correctly

set -e

echo "🧪 Testing E-banking Platform (Epic 1)..."

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

# Test variables
TESTS_PASSED=0
TESTS_FAILED=0

run_test() {
    local test_name="$1"
    local test_command="$2"
    
    echo -n "Testing $test_name... "
    
    if eval "$test_command" &>/dev/null; then
        echo -e "${GREEN}PASS${NC}"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}FAIL${NC}"
        ((TESTS_FAILED++))
    fi
}

# Test 1: Kubernetes cluster connectivity
print_step "Testing Kubernetes connectivity..."
run_test "Kubernetes cluster" "kubectl cluster-info"

# Test 2: Namespace existence
print_step "Testing namespaces..."
run_test "DMZ namespace" "kubectl get namespace dmz"
run_test "SVC namespace" "kubectl get namespace svc"
run_test "DATA namespace" "kubectl get namespace data"
run_test "Security namespace" "kubectl get namespace security"

# Test 3: Core services status
print_step "Testing core services..."
run_test "Cert-manager pods" "kubectl get pods -n security -l app.kubernetes.io/name=cert-manager --field-selector=status.phase=Running"
run_test "Ingress controller" "kubectl get pods -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx --field-selector=status.phase=Running"
run_test "MSSQL database" "kubectl get pods -n data -l app=mssql --field-selector=status.phase=Running"
run_test "Keycloak" "kubectl get pods -n security -l app=keycloak --field-selector=status.phase=Running"
run_test "Vault" "kubectl get pods -n security -l app=vault --field-selector=status.phase=Running"
run_test "Redpanda" "kubectl get pods -n svc -l app=redpanda --field-selector=status.phase=Running"

# Test 4: Application services
print_step "Testing application services..."
run_test "API Gateway" "kubectl get pods -n svc -l app=api-gateway --field-selector=status.phase=Running"
run_test "Account Service" "kubectl get pods -n svc -l app=account-service --field-selector=status.phase=Running"

# Test 5: Ingress and TLS
print_step "Testing ingress and TLS..."
run_test "Main ingress" "kubectl get ingress -n svc ebank-ingress"
run_test "Keycloak ingress" "kubectl get ingress -n security keycloak-ingress"
run_test "TLS certificate" "kubectl get certificate -n svc ebank-tls"

# Test 6: Network policies
print_step "Testing network policies..."
run_test "Default deny policy" "kubectl get networkpolicy -n svc default-deny"
run_test "Ingress to gateway policy" "kubectl get networkpolicy -n svc allow-ingress-to-gateway"

# Test 7: HTTP endpoints (requires port forwarding)
print_step "Testing HTTP endpoints..."

# Test Gateway health
echo "Setting up port forwards for testing..."
kubectl port-forward -n svc svc/api-gateway 8081:80 &
GATEWAY_PID=$!

kubectl port-forward -n svc svc/account-service 8082:80 &
ACCOUNT_PID=$!

kubectl port-forward -n security svc/keycloak 8083:8080 &
KEYCLOAK_PID=$!

kubectl port-forward -n security svc/vault 8084:8200 &
VAULT_PID=$!

# Wait for port forwards to establish
sleep 5

run_test "Gateway health endpoint" "curl -s http://localhost:8081/healthz | grep -q healthy"
run_test "Account service health" "curl -s http://localhost:8082/healthz | grep -q healthy"
run_test "Keycloak health" "curl -s http://localhost:8083/health/ready"
run_test "Vault health" "curl -s http://localhost:8084/v1/sys/health"

# Clean up port forwards
kill $GATEWAY_PID $ACCOUNT_PID $KEYCLOAK_PID $VAULT_PID 2>/dev/null || true

# Test 8: Database connectivity
print_step "Testing database connectivity..."

MSSQL_POD=$(kubectl get pods -n data -l app=mssql -o jsonpath='{.items[0].metadata.name}')
if [ -n "$MSSQL_POD" ]; then
    run_test "MSSQL connection" "kubectl exec -n data $MSSQL_POD -- /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -Q 'SELECT 1'"
    run_test "AccountService database" "kubectl exec -n data $MSSQL_POD -- /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -Q 'SELECT name FROM sys.databases WHERE name = \"AccountService\"'"
fi

# Test 9: Kafka connectivity
print_step "Testing Kafka connectivity..."
REDPANDA_POD=$(kubectl get pods -n svc -l app=redpanda -o jsonpath='{.items[0].metadata.name}')
if [ -n "$REDPANDA_POD" ]; then
    run_test "Redpanda admin API" "kubectl exec -n svc $REDPANDA_POD -- curl -s http://localhost:9644/v1/status/ready"
fi

# Test 10: Secrets and ConfigMaps
print_step "Testing secrets and configuration..."
run_test "MSSQL secret" "kubectl get secret -n data mssql-secret"
run_test "Account service secret" "kubectl get secret -n svc account-service-secrets"
run_test "Keycloak realm ConfigMap" "kubectl get configmap -n security keycloak-realm"

# Summary
echo ""
echo "🏁 Test Summary:"
echo "=================="
echo -e "Tests passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Tests failed: ${RED}$TESTS_FAILED${NC}"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}🎉 All tests passed! Platform is ready.${NC}"
    echo ""
    echo "🔗 Access points:"
    echo "   - Main app: https://ebank.local"
    echo "   - Keycloak: https://ebank.local/auth (admin/admin)"
    echo "   - Swagger UI: kubectl port-forward -n svc svc/account-service 8080:80 → http://localhost:8080"
    echo ""
    echo "📋 Next steps:"
    echo "   1. Initialize Vault secrets: bash scripts/setup-vault.sh"
    echo "   2. Test authentication flow"
    echo "   3. Deploy additional microservices (Transfer, Payment, etc.)"
    echo "   4. Deploy frontend application"
    exit 0
else
    echo -e "${RED}❌ Some tests failed. Check the failed components.${NC}"
    echo ""
    echo "🔧 Troubleshooting:"
    echo "   - Check pod status: kubectl get pods -A"
    echo "   - Check pod logs: kubectl logs -n <namespace> <pod-name>"
    echo "   - Check ingress: kubectl describe ingress -A"
    echo "   - Check network policies: kubectl get networkpolicies -A"
    exit 1
fi
