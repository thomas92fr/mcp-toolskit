# mcp-toolskit

Ma liste d'outils utilisables dans claude desktop

Ce projet C# est un serveur MCP (Model Context Protocol (MCP) server) qui met des outils a disposition de ClaudeDesktop.

Il utilise .net v9.0
Il utilise Serilog pour la génération de logs
Il est basé sur ModelContextProtocol.NET: ´https://github.com/salty-flower/ModelContextProtocol.NET´ pour la partie serveur MCP

# Configuration dans Claude

Pour utiliser ce calculateur avec Claude, ajoutez la configuration suivante :

```json
{
  "mcpServers": {
    "calculator": {
      "command": "I:/CSharp Projects/mcp-toolskit/bin/Debug/net9.0/mcp-toolskit.exe"
    }
  }
}
```