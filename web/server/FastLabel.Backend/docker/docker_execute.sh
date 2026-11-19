#!/bin/bash

set -e

CONTAINER_NAME="fastlabel-backend-instance"
IMAGE_NAME="fastlabel-backend"

# Configuration par défaut (modifiable via l'environnement ou un fichier .env sourcé avant exécution)
ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://+:8080}"
ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
ConnectionStrings__DefaultConnection="${ConnectionStrings__DefaultConnection:-}"

echo "=== Lancement du conteneur : $CONTAINER_NAME ==="

# Supprimer l'ancien conteneur s'il existe déjà (qu'il soit arrêté ou en cours d'exécution)
if [ "$(docker ps -a -q -f name=^/${CONTAINER_NAME}$)" ]; then
    echo "Nettoyage de l'ancien conteneur..."
    docker stop "$CONTAINER_NAME" >/dev/null 2>&1 || true
    docker rm "$CONTAINER_NAME" >/dev/null 2>&1 || true
fi

# Lancement du nouveau conteneur
docker run -d \
    --name "$CONTAINER_NAME" \
    --restart always \
    -p 8080:8080 \
    -e ASPNETCORE_URLS="$ASPNETCORE_URLS" \
    -e ASPNETCORE_ENVIRONMENT="$ASPNETCORE_ENVIRONMENT" \
    -e ConnectionStrings__DefaultConnection="$ConnectionStrings__DefaultConnection" \
    "$IMAGE_NAME"

echo "=== Conteneur lancé avec succès ! ==="
echo "Pour suivre les logs en direct, tape : docker logs -f $CONTAINER_NAME"