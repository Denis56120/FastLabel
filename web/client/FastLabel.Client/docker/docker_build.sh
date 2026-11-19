#!/bin/bash

set -e

IMAGE_NAME="fastlabel-client"
WWWROOT_DIR="wwwroot"

echo "=== Construction de l'image Docker : $IMAGE_NAME ==="

# Vérifier si le dossier wwwroot est présent
if [ ! -d "$WWWROOT_DIR" ] || [ ! -f "$WWWROOT_DIR/index.html" ]; then
    echo "Erreur : Le dossier '$WWWROOT_DIR' (ou index.html) est introuvable dans ce dossier !"
    echo "Pense à publier et copier wwwroot ici avant de lancer le build."
    exit 1
fi

# Construction de l'image Docker
docker build -t "$IMAGE_NAME" .

echo "=== Image Docker construite avec succès ! ==="