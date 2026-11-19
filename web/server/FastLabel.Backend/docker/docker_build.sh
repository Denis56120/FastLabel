#!/bin/bash

# Arrêter le script en cas d'erreur
set -e

IMAGE_NAME="fastlabel-backend"
BINARY_NAME="FastLabel.Backend"

echo "=== Construction de l'image Docker : $IMAGE_NAME ==="

# Vérifier si le binaire est présent
if [ ! -f "$BINARY_NAME" ]; then
    echo "Erreur : Le binaire '$BINARY_NAME' est introuvable dans ce dossier !"
    echo "Pense à compiler et copier ton binaire ici avant de lancer le build."
    exit 1
fi

# Construction de l'image Docker
docker build -t "$IMAGE_NAME" .

echo "=== Image Docker construite avec succès ! ==="