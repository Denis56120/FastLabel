# FastLabel Agent (Label-Flash)

Agent .NET 10 modulaire conçu pour exécuter des services d'impression d'étiquettes à distance via une architecture hybride (Windows Service + Tray Application) et un système de plugins chaînés par `AssemblyLoadContext`.

---

## 🏗️ Architecture du Projet

Le projet est divisé en plusieurs composants clés :
- **FastLabel.Service** : Le service Windows hôte qui gère le cycle de vie, la mise à jour automatique via ZIP et le chargement dynamique des plugins.
- **FastLabel.Tray** : L'application de bureau (Tray Icon) pour l'interaction utilisateur et l'état en temps réel.
- **FastLabel.Core** : Les interfaces et contrats partagés (`IFastLabelPlugin`).
- **FastLabel.Service.Plugin** : Le plugin de communication WebSocket sortante (`WSS`) vers le serveur cloud et de transmission TCP locale vers les imprimantes.

---

## 🚀 Fonctionnalités

* **Connexion sortante persistante** : L'agent initie une connexion WebSocket sécurisée (`WSS`) vers `fastlabel.com` pour contourner les pare-feux et NAT locaux.
* **Architecture de plugins à chaud** : Chargement, déchargement et mise à jour des plugins sans redémarrer le service Windows hôte.
* **Impression réseau locale** : Transmission de données brutes (ZPL/CPCL) vers les imprimantes connectées sur le réseau local (`TcpClient`).
* **Mise à jour automatisée** : Système de déploiement et d'application de mises à jour via archive ZIP.

---

## 🛠️ Prérequis techniques

* **.NET 10 SDK** (ou version compatible .NET 10)
* **Système d'exploitation** : Windows 10 / 11 ou Windows Server (pour le service Windows et la barre des tâches).

---

## 📦 Compilation et Publication

1. Cloner le dépôt :
   ```bash
git clone [https://github.com/ton-compte/fastlabel-agent.git](https://github.com/ton-compte/fastlabel-agent.git)
   ```

2. Compiler la solution :
   ```bash
dotnet build -c Release
   ```

3. Publier le service :
   ```bash
   dotnet publish FastLabel.Service -c Release -o ./publish
   ```

## ⚙️ Configuration

Le plugin se connecte par défaut au domaine configuré dans l'application ou passé dynamiquement via la méthode Launch(baseUrl). Assurez-vous que l'agent peut initier des connexions sortantes sur le port 443 vers votre instance fastlabel.com.

## 📝 Licence

Ce projet est propriétaire et réservé à l'usage de FastLabel.