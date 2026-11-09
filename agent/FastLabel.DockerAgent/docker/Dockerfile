FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-cbl-mariner

WORKDIR /app

# Mettre l'exécutable linux dans le même dossier que le dockerfile
COPY FastLabel.DockerAgent ./FastLabel.DockerAgent

RUN chmod +x FastLabel.DockerAgent

ENTRYPOINT ["./FastLabel.DockerAgent"]