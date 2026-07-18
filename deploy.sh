#!/usr/bin/env bash
# eBanking Secure — one deployment entry point (Docker Compose or Kubernetes).
#
#   ./deploy.sh up [--frontend]     start the stack with Docker Compose (build + run)
#   ./deploy.sh down                stop the compose stack
#   ./deploy.sh logs [service]      tail logs
#   ./deploy.sh ps                  show status
#   ./deploy.sh clean               stop and remove volumes (wipes data)
#   ./deploy.sh k8s-up              build images + deploy to the current kube-context
#   ./deploy.sh k8s-down            remove the Kubernetes resources
#
# Browser login prerequisite (once): map the Keycloak hostname to localhost so the browser
# and the containers resolve the same issuer:  echo "127.0.0.1 keycloak" | sudo tee -a /etc/hosts
set -euo pipefail
cd "$(dirname "$0")"

SERVICES=(account transfer payment notification audit gateway)

compose() { docker compose "$@"; }

hosts_hint() {
  if ! grep -qE '^\s*127\.0\.0\.1\s+keycloak\b' /etc/hosts 2>/dev/null; then
    echo "  note: add '127.0.0.1 keycloak' to your hosts file for browser login." >&2
  fi
}

case "${1:-}" in
  up)
    profile=""
    [[ "${2:-}" == "--frontend" ]] && profile="--profile frontend"
    compose $profile up -d --build
    hosts_hint
    echo "up — gateway http://localhost:5000  keycloak http://localhost:8180  kafka-ui http://localhost:8080"
    [[ -n "$profile" ]] && echo "     SPA http://localhost:4200 (login: testuser / password123)"
    ;;
  down)  compose --profile frontend down ;;
  logs)  compose logs -f "${2:-}" ;;
  ps|status) compose ps ;;
  clean) compose --profile frontend down -v ;;

  k8s-up)
    echo "Building service images…"
    docker build -f Dockerfile.gateway       -t ebanking/api-gateway:latest .
    for s in account transfer payment notification audit; do
      docker build -f "Dockerfile.$s" -t "ebanking/${s}-service:latest" .
    done
    docker build -f frontend/web/Dockerfile  -t ebanking/frontend:latest frontend/web
    imgs=(api-gateway account-service transfer-service payment-service notification-service audit-service frontend)
    # kind / minikube run their own container runtime — load images in if present.
    if command -v kind >/dev/null && kind get clusters >/dev/null 2>&1; then
      for img in "${imgs[@]}"; do kind load docker-image "ebanking/${img}:latest" 2>/dev/null || true; done
    elif command -v minikube >/dev/null; then
      for img in "${imgs[@]}"; do minikube image load "ebanking/${img}:latest" 2>/dev/null || true; done
    fi
    echo "Applying manifests…"
    kubectl apply -k infrastructure/k8s
    echo "k8s-up — check: kubectl get pods -A | grep ebanking"
    ;;
  k8s-down) kubectl delete -k infrastructure/k8s --ignore-not-found ;;

  *)
    grep -E '^#( |$)' "$0" | sed 's/^# \{0,1\}//' | head -20
    exit 1 ;;
esac
