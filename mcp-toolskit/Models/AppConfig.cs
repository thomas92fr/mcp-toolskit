using System.Text.Json;

namespace mcp_toolskit.Models
{
    /// <summary>
    /// Configuration spécifique pour GIT 
    /// </summary>
    public class GitConfig
    {
        /// <summary>
        /// Nom de l'utilisateur , utilisé pour les oérations GIT
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Email de l'utilisateur , utilisé pour les oérations GIT
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// Mot de passe de l'utilisateur , utilisé pour les oérations GIT (en remote)
        /// </summary>
        public string UserPassword { get; set; } = string.Empty;

    }    

    /// <summary>
    /// Configuration spécifique pour BraveSearch API
    /// </summary>
    public class BraveSearchConfig
    {
        /// <summary>
        /// Token a utiliser pour les appels a l'API BraveSearch
        /// </summary>
         public string ApiKey { get; set; } = string.Empty;
        /// <summary>
        /// Indique si on ignore ou non les erreurs SSL lors des appels a l'API BraveSearch
        /// </summary>
        public bool IgnoreSSLErrors { get; set; } = false;
    }

    /// <summary>
    /// Classe de configuration de l'application.
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Chemin des fichiers de logs.
        /// </summary>
        public string LogPath { get; set; } = AppContext.BaseDirectory;

        /// <summary>
        /// Liste des répertoires autorisés pour les opérations sur le système de fichiers.
        /// </summary>
        public string[] AllowedDirectories { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Liste des outils interdits.
        /// </summary>
        public string[] ForbiddenTools { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Configuration spécifique pour BraveSearch API
        /// </summary>
        public BraveSearchConfig BraveSearch { get; set; } = new BraveSearchConfig();

        /// <summary>
        /// Configuration spécifique pour GIT
        /// </summary>
        public GitConfig Git { get; set; } = new GitConfig();

        /// <summary>
        /// Crée une nouvelle instance de la configuration avec les valeurs par défaut.
        /// </summary>
        public AppConfig()
        {
            NormalizePathProperties();
        }

        /// <summary>
        /// Convertit les chemins relatifs en chemins absolus par rapport au répertoire de l'application.
        /// </summary>
        private void NormalizePathProperties()
        {
            // Normalise le chemin des logs
            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                LogPath = Path.GetFullPath(LogPath, AppContext.BaseDirectory);
            }

            // Normalise les chemins des répertoires autorisés
            if (AllowedDirectories != null)
            {
                AllowedDirectories = AllowedDirectories.Select(dir =>
                    Path.GetFullPath(dir, AppContext.BaseDirectory)).ToArray();
            }
        }

        /// <summary>
        /// Charge la configuration depuis un fichier JSON.
        /// </summary>
        /// <param name="path">Chemin du fichier de configuration JSON</param>
        /// <returns>Une instance de AppConfig avec les valeurs chargées</returns>
        /// <exception cref="FileNotFoundException">Si le fichier n'existe pas</exception>
        /// <exception cref="JsonException">Si le fichier JSON est mal formaté</exception>
        public static AppConfig LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Le fichier de configuration n'existe pas.", path);

            string jsonContent = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(jsonContent) ?? new AppConfig();
            config.NormalizePathProperties();
            
            return config;
        }

        /// <summary>
        /// Tente de charger la configuration depuis un fichier JSON.
        /// Si aucun chemin n'est spécifié, essaie de charger config.json depuis le répertoire de l'application.
        /// En cas d'erreur, retourne une configuration avec les valeurs par défaut.
        /// </summary>
        /// <param name="configPath">Chemin du fichier de configuration JSON (optionnel)</param>
        /// <param name="errorMessage">Message d'erreur en cas d'échec du chargement</param>
        /// <returns>Une instance de AppConfig, soit chargée du fichier soit avec les valeurs par défaut</returns>
        public static AppConfig GetConfiguration(string? configPath, out string? errorMessage)
        {
            errorMessage = null;

            // Si aucun chemin n'est spécifié, essaie de charger config.json
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            }

            try
            {
                // Tente de charger la configuration
                return LoadFromFile(configPath);
            }
            catch (FileNotFoundException)
            {
                // Si c'est le fichier config.json par défaut qui n'a pas été trouvé, on retourne silencieusement la config par défaut
                if (configPath == Path.Combine(AppContext.BaseDirectory, "config.json"))
                {
                    return new AppConfig();
                }
                
                errorMessage = $"Le fichier de configuration '{configPath}' n'existe pas. Utilisation de la configuration par défaut.";
            }
            catch (JsonException ex)
            {
                errorMessage = $"Erreur de format dans le fichier de configuration '{configPath}': {ex.Message}. Utilisation de la configuration par défaut.";
            }
            catch (Exception ex)
            {
                errorMessage = $"Erreur lors du chargement de la configuration '{configPath}': {ex.Message}. Utilisation de la configuration par défaut.";
            }

            // En cas d'erreur, retourne la configuration par défaut
            return new AppConfig();
        }

        /// <summary>
        /// Retourne une représentation JSON indentée de la configuration.
        /// </summary>
        /// <returns>La configuration au format JSON indenté</returns>
        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        /// <summary>
        /// Valide qu'un chemin est dans un répertoire autorisé
        /// </summary>
        public virtual string ValidatePath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!AllowedDirectories.Any(dir => fullPath.StartsWith(Path.GetFullPath(dir))))
            {
                throw new UnauthorizedAccessException($"Access denied - path outside allowed directories: {fullPath}");
            }
            return fullPath;
        }

        /// <summary>
        /// Valide qu'un nom d'outils est autorisé
        /// </summary>
        public virtual bool ValidateTool(string tool_name)
        {
           
            if (ForbiddenTools.Any(tool => tool?.ToLower() == tool_name?.ToLower()))
            {
                return false;
            }
            return true;
        }
    }
}