FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-cbl-mariner

WORKDIR /app

COPY FastLabel.Backend ./FastLabel.Backend

RUN chmod +x FastLabel.Backend

ENTRYPOINT ["./FastLabel.Backend"]