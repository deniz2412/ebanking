#!/usr/bin/env bash
set -euo pipefail

echo "Deploying Keycloak realm + Keycloak ..."
kubectl apply -f infrastructure/k8s/keycloak/keycloak-realm.yaml
kubectl apply -f infrastructure/k8s/keycloak/keycloak.yaml

echo "Deploying Ocelot gateway ..."
kubectl apply -f infrastructure/k8s/gateway/gateway-ocelot-configmap.yaml
kubectl apply -f infrastructure/k8s/gateway/api-gateway.yaml

echo "Deploying placeholder frontend + ingress ..."
kubectl apply -f infrastructure/k8s/frontend/frontend.yaml

echo "Deploying ingresses ..."
kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml
kubectl apply -f infrastructure/k8s/ingress/keycloak-ingress.yaml

echo "Applying NetworkPolicies ..."
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-gateway.yaml
kubectl apply -f infrastructure/k8s/network/allow-ingress-to-keycloak.yaml

echo "Waiting for rollouts ..."
kubectl -n security rollout status deploy/keycloak
kubectl -n svc rollout status deploy/api-gateway

echo "Phase 2 base deployed. Test:"
echo "  curl -k https://ebank.local/healthz"
echo "  open https://ebank.local/auth/"
