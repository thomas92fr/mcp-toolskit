using mcp_toolskit.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace mcp_toolskit.Extentions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Génère une description complète pour l'énumération.
        /// </summary>
        /// <param name="enumType">Le type de l'énumération</param>
        /// <param name="title">Un titre optionnel pour la description générale</param>
        /// <returns>Une chaîne décrivant toutes les valeurs de l'énumération</returns>
        public static string GenerateFullDescription(
            this Type enumType,
            string title = "Available operations")
        {
            // Vérification que le type est bien une énumération
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("Le type doit être une énumération", nameof(enumType));
            }

            var operations = Enum.GetValues(enumType)
                .Cast<Enum>()
                .Select(enumValue => {
                    var memberInfo = enumType.GetMember(enumValue.ToString()).First();

                    // Récupère la description de l'attribut Description
                    var description = memberInfo.GetCustomAttribute<DescriptionAttribute>()?.Description
                        ?? "No description available";

                    // Récupère les descriptions de paramètres si l'attribut existe
                    var parametersDescription = memberInfo.GetCustomAttribute<ParametersAttribute>()?.ParameterDescriptions
                        ?? Array.Empty<string>();

                    // Construction de la description complète
                    var fullDescription = $"- {enumValue}: {description}";

                    // Ajoute les descriptions de paramètres si disponibles
                    if (parametersDescription.Any())
                    {
                        fullDescription += "\n  Parameters:\n    " +
                            string.Join("\n    ", parametersDescription);
                    }

                    return fullDescription;
                });

            return $"{title}:\n" + string.Join("\n", operations);
        }
    }
}
