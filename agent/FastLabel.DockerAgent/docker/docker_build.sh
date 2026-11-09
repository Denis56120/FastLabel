#!/bin/bash

# Arrêter le script en cas d'erreur
set -e

IMAGE_NAME="fastlabel-agent"

echo "=== Construction de l'image Docker : $IMAGE_NAME ==="

# Vérifier si le binaire est présent
if [ ! -f "FastLabel.DockerAgent" ]; then
    echo "Erreur : Le binaire 'FastLabel.DockerAgent' est introuvable dans ce dossier !"
    echo "Pense à compiler et copier ton binaire ici avant de lancer le build."
    exit 1
fi

# Construction de l'image Docker
docker build -t "$IMAGE_NAME" .

echo "=== Image Docker construite avec succès ! ==="