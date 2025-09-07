#!/usr/bin/env bash
set -euo pipefail

###############################################################################
# Config — tweak if needed
###############################################################################
HOST="ebank.local"
NS="svc"
CA_NAME="ebank-root-ca"         # Secret for the Root CA in Kubernetes
ISSUER_NAME="ebank-ca"          # cert-manager Issuer (namespaced)
CERT_NAME="ebank-local-cert"    # cert-manager Certificate resource
TLS_SECRET="ebank-tls"          # Secret that Ingress references
DAYS="3650"                     # 10 years

OUT_DIR="${OUT_DIR:-scripts/tls-localca}"
mkdir -p "${OUT_DIR}"

ROOT_KEY="${OUT_DIR}/rootCA.key"
ROOT_CRT="${OUT_DIR}/rootCA.crt"

###############################################################################
# Checks
###############################################################################
need() { command -v "$1" >/dev/null 2>&1 || { echo "[-] '$1' is required but not found in PATH."; exit 1; }; }

need openssl
need kubectl

# Check cert-manager CRDs
if ! kubectl get crd certificates.cert-manager.io >/dev/null 2>&1; then
  echo "cert-manager CRDs not found. Install cert-manager first (Phase 1 step)."
  echo "    Example:"
  echo "      kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.15.1/cert-manager.crds.yaml"
  echo "      helm repo add jetstack https://charts.jetstack.io"
  echo "      helm upgrade --install cert-manager jetstack/cert-manager -n security --create-namespace --version v1.15.1"
  exit 1
fi

# Ensure target namespace exists
kubectl get ns "${NS}" >/dev/null 2>&1 || kubectl create ns "${NS}"

###############################################################################
# Generate Root CA (only if not present)
###############################################################################
if [[ -f "${ROOT_KEY}" && -f "${ROOT_CRT}" ]]; then
  echo "Reusing existing Root CA: ${ROOT_CRT}"
else
  echo "Generating local Root CA (key + self-signed cert)..."
  openssl genrsa -out "${ROOT_KEY}" 4096
  openssl req -x509 -new -nodes -sha256 -days "${DAYS}" \
    -key "${ROOT_KEY}" \
    -subj "/CN=Local Ebank Root CA" \
    -out "${ROOT_CRT}"
  echo "[+] Root CA created:"
  echo "    - ${ROOT_KEY}"
  echo "    - ${ROOT_CRT}"
fi

###############################################################################
# Create/Update Kubernetes Secret with the Root CA keypair (namespaced)
###############################################################################
echo "Creating/Updating Root CA secret '${CA_NAME}' in namespace '${NS}'..."
kubectl -n "${NS}" create secret tls "${CA_NAME}" \
  --cert="${ROOT_CRT}" --key="${ROOT_KEY}" \
  --dry-run=client -o yaml | kubectl apply -f -

###############################################################################
# Create/Update cert-manager Issuer (uses the Root CA secret)
###############################################################################
echo "Applying cert-manager Issuer '${ISSUER_NAME}'..."
cat <<YAML | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Issuer
metadata:
  name: ${ISSUER_NAME}
  namespace: ${NS}
spec:
  ca:
    secretName: ${CA_NAME}
YAML

###############################################################################
# Create/Update Certificate for the host -> populates Secret ${TLS_SECRET}
###############################################################################
echo "Requesting Certificate '${CERT_NAME}' for DNS name '${HOST}'..."
cat <<YAML | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: ${CERT_NAME}
  namespace: ${NS}
spec:
  secretName: ${TLS_SECRET}
  dnsNames:
    - ${HOST}
  issuerRef:
    name: ${ISSUER_NAME}
    kind: Issuer
YAML

###############################################################################
# Wait until the Certificate is Ready
###############################################################################
echo "Waiting for Certificate to be Ready..."
kubectl -n "${NS}" wait --for=condition=Ready "certificate/${CERT_NAME}" --timeout=90s || {
  echo "[-] Certificate did not become Ready in time. Describe it for details:"
  echo "    kubectl -n ${NS} describe certificate ${CERT_NAME}"
  exit 1
}

echo "Certificate is Ready. Secret '${TLS_SECRET}' should now exist in namespace '${NS}'."
kubectl -n "${NS}" get secret "${TLS_SECRET}"

###############################################################################
# Zrust the Root CA on Windows (for a green lock in the browser)
###############################################################################
cat <<'NOTE'

===============================================================================
TRUST THE ROOT CA ON WINDOWS (one-time)
===============================================================================
1) Open the Microsoft Management Console:
   - Press Win+R, type: mmc  and press Enter
2) File -> Add/Remove Snap-in...
3) Select "Certificates" -> Add -> "Computer account" -> Next -> "Local computer" -> Finish -> OK
4) Expand "Certificates (Local Computer)" -> "Trusted Root Certification Authorities" -> "Certificates"
5) Right-click -> All Tasks -> Import...
6) Choose the file: scripts/tls-localca/rootCA.crt  (this repo path)
7) Complete the wizard. Close MMC (save if you want a console shortcut).

Hosts file reminder:
  Add/verify:  127.0.0.1  ebank.local

Ingress reminder:
  Ensure your Ingress in namespace 'svc' references:
    tls:
      - hosts: [ "ebank.local" ]
        secretName: ebank-tls

Test:
  Start your cluster/ingress, then open: https://ebank.local/
  The browser should trust the cert (issued by your Local Ebank Root CA).
NOTE
