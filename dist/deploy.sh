#!/bin/bash
set -e

BASE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "========================================================"
echo " Deploiement complet FastLabel (Backend / Client / Agent)"
echo "========================================================"

deploy_component() {
    local name="$1"
    local dir="$BASE_DIR/$2"

    echo ""
    echo ">>> [$name] Preparation des scripts..."
    cd "$dir"
    sed -i 's/\r$//' build-docker.sh docker_execute.sh
    chmod +x build-docker.sh docker_execute.sh

    echo ">>> [$name] Build de l'image Docker..."
    ./build-docker.sh

    echo ">>> [$name] Lancement du conteneur..."
    ./docker_execute.sh
}

deploy_component "BACKEND" "backend"
deploy_component "CLIENT"  "client"
deploy_component "AGENT"   "agent"

echo ""
echo "========================================================"
echo " [SUCCES] Les 3 services ont ete reconstruits et relances !"
echo "========================================================"
docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}"