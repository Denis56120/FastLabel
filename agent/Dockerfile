# --- Stage de Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copie des fichiers projets depuis leurs dossiers respectifs
COPY ["FastLabel.Core/FastLabel.Core.csproj", "FastLabel.Core/"]
COPY ["FastLabel.Service.Plugin/FastLabel.Service.Plugin.csproj", "FastLabel.Service.Plugin/"]
COPY ["FastLabel.DockerAgent/FastLabel.DockerAgent.csproj", "FastLabel.DockerAgent/"]

RUN dotnet restore "FastLabel.DockerAgent/FastLabel.DockerAgent.csproj"

# Copie de tout le reste du code source
COPY . .
WORKDIR "/src/FastLabel.DockerAgent"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- Stage Final (Runtime Linux léger) ---
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FastLabel.DockerAgent.dll"]