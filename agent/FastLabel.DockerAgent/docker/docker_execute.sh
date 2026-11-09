#!/bin/bash

set -e

CONTAINER_NAME="fastlabel-agent-instance"
IMAGE_NAME="fastlabel-agent"

# Configuration par défaut (tu peux les modifier ou les passer via l'environnement)
CLOUD_URL="${CloudUrl:-https://fastlabel.com}"
API_KEY="${ApiKey:-ta-cle-api-secrete}"

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
    -e CloudUrl="$CLOUD_URL" \
    -e ApiKey="$API_KEY" \
    "$IMAGE_NAME"

echo "=== Conteneur lancé avec succès ! ==="
echo "Pour suivre les logs en direct, tape : docker logs -f $CONTAINER_NAME"