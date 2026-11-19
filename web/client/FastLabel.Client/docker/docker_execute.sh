#!/bin/bash

set -e

CONTAINER_NAME="fastlabel-client-instance"
IMAGE_NAME="fastlabel-client"
HOST_PORT="${HOST_PORT:-8081}"

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
    -p "$HOST_PORT":80 \
    "$IMAGE_NAME"

echo "=== Conteneur lancé avec succès ! ==="
echo "Pour suivre les logs en direct, tape : docker logs -f $CONTAINER_NAME"