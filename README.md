# ⚡ FastLabel

FastLabel est une solution moderne et performante de gestion d'impression d'étiquettes industrielles et bureautiques (compatible ZPL, imprimantes réseau et agents locaux). Le système repose sur un backend ASP.NET Core découplé, une base de données SQLite embarquée, un client Web Blazor et un Agent Windows modulaire communiquant en temps réel via WebSockets.

---

## 🏗️ Architecture Globale

Le projet se divise en trois composants principaux :

1. **Backend (`FastLabel.Backend`)**
   - **Framework :** .NET 10.0 (C#)
   - **Base de données :** SQLite (`FastLabel.Server.db`) via Entity Framework Core
   - **Authentification :** JWT (JSON Web Tokens) & Hachage sécurisé des mots de passe
   - **Communication Temps Réel :** WebSockets (`/agent-ws`) pour le pilotage et le suivi des jobs d'impression par les agents locaux
   - **API :** Combinaison de Minimal APIs et de Contrôleurs REST (`/api/auth`, `/api/users`, `/api/printers`, `/api/templates`, `/api/print`)

2. **Client Web (Blazor / Front-end)**
   - Application Web configurée pour interagir avec l'API REST et gérer les modèles d'étiquettes et imprimantes.
   - Ressources statiques et d'interface logées dans `wwwroot` (styles Bootstrap, scripts JS, favicons).
   - Configuration de connexion backend gérée via `appsettings.json`.

3. **FastLabel Agent (Label-Flash)**
   - Agent .NET 10 modulaire conçu pour exécuter des services d'impression d'étiquettes à distance via une architecture hybride (Windows Service + Tray Application) et un système de plugins chaînés par `AssemblyLoadContext`.
   - **FastLabel.Service :** Le service Windows hôte qui gère le cycle de vie, la mise à jour automatique via ZIP et le chargement dynamique des plugins.
   - **FastLabel.Tray :** L'application de bureau (Tray Icon) pour l'interaction utilisateur et l'état en temps réel.
   - **FastLabel.Core :** Les interfaces et contrats partagés (`IFastLabelPlugin`).
   - **FastLabel.Service.Plugin :** Le plugin de communication WebSocket sortante (`WSS`) vers le serveur cloud et de transmission TCP locale vers les imprimantes.

---

## 🚀 Fonctionnalités Clés

- **Connexion sortante persistante :** L'agent initie une connexion WebSocket sécurisée (`WSS`) vers le serveur backend pour contourner les pare-feux et NAT locaux.
- **Architecture de plugins à chaud :** Chargement, déchargement et mise à jour des plugins sans redémarrer le service Windows hôte.
- **Impression réseau locale :** Transmission de données brutes (ZPL/CPCL) vers les imprimantes connectées sur le réseau local (`TcpClient`).
- **Mise à jour automatisée :** Système de déploiement et d'application de mises à jour de l'agent via archive ZIP.

---

## 🛠️ Prérequis Techniques

- **.NET 10 SDK** (ou version compatible .NET 10)
- **Système d'exploitation :** Windows 10 / 11 ou Windows Server (pour le service Windows et l'application Tray de l'agent).

---

## 📦 Compilation et Démarrage Rapide

### 1. Démarrage du Backend

```bash
cd server/FastLabel.Backend
dotnet run
```

Par défaut, le serveur démarre et écoute sur **`http://localhost:6000`**. La base de données SQLite (`FastLabel.Server.db`) est automatiquement créée et initialisée au premier lancement avec des jeux de données de test.

### 2. Configuration du Client Web

Dans le dossier `wwwroot/` de votre application client, assurez-vous que le fichier `appsettings.json` pointe vers l'URL de votre serveur API :

```json
{
  "BackendUrl": "http://localhost:6000"
}
```

### 3. Compilation et Publication de l'Agent

```bash
# Cloner le dépôt
git clone https://github.com/ton-compte/fastlabel-agent.git

# Compiler la solution
dotnet build -c Release

# Publier le service
dotnet publish FastLabel.Service -c Release -o ./publish
```

---

## ⚙️ Configuration & Sécurité

### Authentification API Client & Backend
- Toutes les requêtes vers les endpoints protégés (`/api/users/me`, etc.) exigent un en-tête d'autorisation avec le jeton JWT :
  ```http
  Authorization: Bearer <VOTRE_TOKEN_JWT>
  ```

### Connexion Agent <-> Backend
- L'agent se connecte au WebSocket d'impression du backend en fournissant sa clé API dans la query string :
  ```text
  ws://localhost:6000/agent-ws?apiKey=<CLÉ_API_AGENT>
  ```
- Le plugin se connecte par défaut au domaine configuré dans l'application ou passé dynamiquement via la méthode `Launch(baseUrl)`. Assurez-vous que l'agent peut initier des connexions sortantes sur le port 443 vers votre instance `fastlabel.com` (ou port 6000 en dev local).

---

## 📂 Structure des Répertoires

```text
FastLabel/
├── client/                      # Application Client Web (Blazor)
│   └── wwwroot/                 # Ressources statiques (CSS, JS, favicon.png, appsettings.json)
├── server/
│   └── FastLabel.Backend/       # Backend ASP.NET Core
│       ├── Controllers/         # Contrôleurs REST (Auth, Profils Utilisateurs)
│       ├── Data/                # DbContext EF Core (AppDbContext.cs)
│       ├── DTOs/                # Data Transfer Objects
│       ├── EndPoints/           # Minimal API Endpoints (Printers, Templates)
│       ├── Models/              # Modèles de données (User, Agent, Printer, PrintJob, etc.)
│       ├── Services/            # Services métier (TokenService JWT)
│       ├── Program.cs           # Point d'entrée de l'application & WebSockets
│       └── FastLabel.Server.db  # Base de données SQLite (runtime)
└── agent/                       # Agent Windows et Service
    ├── FastLabel.Service/       # Hôte Service Windows
    ├── FastLabel.Tray/          # App barre des tâches (Tray Icon)
    ├── FastLabel.Core/          # Interfaces partagées
    └── FastLabel.Service.Plugin/# Plugin WS / TCP Impression
```

---

## 📝 Licence

Ce projet est propriétaire et réservé à l'usage de FastLabel. Tous droits réservés.