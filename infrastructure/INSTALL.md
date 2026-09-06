# Install Notes (Docker Desktop K8s)

## cert-manager
```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml
helm repo add jetstack https://charts.jetstack.io
helm upgrade --install cert-manager jetstack/cert-manager -n security --create-namespace --version v1.15.1
kubectl -n security create secret tls ebank-root-ca `
  --cert=rootCA.crt --key=rootCA.key
kubectl apply -f infrastructure/k8s/cert-manager/cluster-issuer.yaml
```

## ingress-nginx (DMZ)
```bash
kubectl create namespace dmz || true
kubectl create namespace ingress-nginx || true
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n dmz   --set controller.ingressClassResource.name=nginx   --set controller.ingressClass=nginx   --set controller.watchNamespace=dmz
```


## Apply base & platform

Prefer `./deploy.sh k8s-up` (or `./deploy.ps1 k8s-up`), which builds and loads the service
images and applies the full kustomization (`infrastructure/k8s`) in one step. To apply
manifests individually instead:
```bash
kubectl apply -f infrastructure/k8s/base/namespaces.yaml
kubectl apply -f infrastructure/k8s/network/default-deny.yaml
kubectl apply -f infrastructure/k8s/network/allow-dmz-to-gateway.yaml
kubectl apply -f infrastructure/k8s/keycloak/keycloak.yaml
kubectl apply -f infrastructure/k8s/mssql/unified-mssql-secret.yaml   # edit CHANGE_ME first
kubectl apply -f infrastructure/k8s/mssql/mssql.yaml
kubectl apply -f infrastructure/k8s/kafka/redpanda.yaml
kubectl apply -f infrastructure/k8s/ingress/ebank-ingress.yaml
```

## Hosts entry
Add to `/etc/hosts`:
```
127.0.0.1 ebank.local
```
