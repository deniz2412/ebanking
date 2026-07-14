#!/usr/bin/env bash
set -euo pipefail

echo "Ensure namespaces exist (dmz, svc, data, security, ebanking-backend)..."
kubectl get ns dmz >/dev/null 2>&1 || kubectl create ns dmz
kubectl get ns svc >/dev/null 2>&1 || kubectl create ns svc
kubectl get ns data >/dev/null 2>&1 || kubectl create ns data
kubectl get ns security >/dev/null 2>&1 || kubectl create ns security
kubectl get ns ebanking-backend >/dev/null 2>&1 || kubectl create ns ebanking-backend

echo "Apply base network policies (default-deny)..."
kubectl apply -f infrastructure/k8s/network/default-deny.yaml

echo "Install/upgrade cert-manager..."
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml
helm repo add jetstack https://charts.jetstack.io >/dev/null 2>&1 || true
helm upgrade --install cert-manager jetstack/cert-manager -n security --create-namespace --version v1.15.1

echo "Apply ClusterIssuer (dev-ca)..."
kubectl apply -f infrastructure/k8s/cert-manager/cluster-issuer.yaml

echo "Install/upgrade ingress-nginx controller..."
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx >/dev/null 2>&1 || true
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n dmz --create-namespace   --set controller.ingressClassResource.name=nginx   --set controller.ingressClass=nginx

echo "Deploying API Gateway (placeholder echo on :80) and allow traffic from ingress controller..."
kubectl apply -f infrastructure/k8s/gateway/api-gateway.yaml
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-gateway.yaml

echo "Deploying Ingress for host ebank.local (TLS via dev-ca)..."
kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml

echo "Deploying Core Services"
kubectl apply -f infrastructure/k8s/mssql/secret.yaml
kubectl apply -f infrastructure/k8s/mssql/mssql.yaml
kubectl apply -f infrastructure/k8s/kafka/redpanda.yaml
kubectl apply -f infrastructure/k8s/keycloak/keycloak.yaml

echo "Wait for ingress and gateway to be ready..."
kubectl -n dmz rollout status deploy/ingress-nginx-controller
kubectl -n svc rollout status deploy/api-gateway

echo ""
echo "Phase 1 rollout is ready. Add '127.0.0.1 ebank.local' to /etc/hosts and open: https://ebank.local/"
