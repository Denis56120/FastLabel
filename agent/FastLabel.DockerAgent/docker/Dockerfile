FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-cbl-mariner

WORKDIR /app

COPY FastLabel.DockerAgent ./FastLabel.DockerAgent

RUN chmod +x FastLabel.DockerAgent

ENTRYPOINT ["./FastLabel.DockerAgent"]