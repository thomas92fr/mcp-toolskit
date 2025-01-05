# MCP-ToolsKit

MCP-ToolsKit est un serveur MCP (Model Context Protocol) qui fournit une suite d'outils pour ClaudeDesktop. Ce serveur est développé en C# et utilise .NET 9.0.

## Description

Ce serveur implémente le protocole MCP en se basant sur ModelContextProtocol.NET et expose différents outils utilitaires pour interagir avec :

- Système de fichiers (lecture, écriture, recherche, etc.)
- Calculatrice 
- Recherche web via l'API Brave
- Gestion Git
- Exécution de tests .NET
- Informations système

## Prérequis

- .NET 9.0 SDK
- Clé API Brave Search (pour les fonctionnalités de recherche) https://brave.com/search/api/
- Token GitHub (pour les fonctionnalités 'remotes' de Git) 

## Structure du Projet

Le projet est composé de deux parties principales :

- `mcp-toolskit/` : Le projet principal du serveur MCP
- `mcp-toolskit-tests/` : Les tests unitaires

### Gestionnaires d'outils disponibles

- **BraveSearch** : Recherche web et locale via l'API Brave
- **Calculator** : Opérations mathématiques basiques
- **DotNet** : Exécution de tests unitaires
- **Filesystem** : Opérations de fichiers (création, lecture, écriture, etc.)
- **Git** : Opérations Git basiques
- **Systems** : Informations système

## Configuration

La configuration du serveur se fait via le fichier `config.json`. Les paramètres importants incluent :

- Répertoires autorisés pour les opérations fichier
- Clé API Brave Search
- Autres paramètres spécifiques aux outils

Pour utiliser ce calculateur avec Claude Desktop, ajoutez la configuration suivante :
```json
{
  "mcpServers": {
    "Toolbox": {
      "command": "<chemin>/mcp-toolskit.exe"
    }
  }
}
```

## Logging

Le projet utilise Serilog pour la génération de logs détaillés des opérations effectuées.

## Tests

Le projet inclut une suite de tests unitaires dans le dossier `mcp-toolskit-tests/`. Les résultats des tests sont générés au format HTML dans le dossier `TestResults/`.

## Licence

Ce projet est sous licence Apache 2.0 - voir le fichier [LICENSE](LICENSE) pour plus de détails.