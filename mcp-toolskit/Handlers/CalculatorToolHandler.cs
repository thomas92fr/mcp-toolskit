using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;

namespace mcp_toolskit.Handlers;

/// <summary>
/// Définit les paramètres nécessaires pour effectuer une opération de calculatrice.
/// </summary>
public class CalculatorParameters
{
    /// <summary>
    /// Type d'opération à effectuer.
    /// </summary>
    public required CalculatorOperation Operation { get; init; }

    /// <summary>
    /// Premier opérande de l'opération.
    /// </summary>
    public required double A { get; init; }

    /// <summary>
    /// Second opérande de l'opération.
    /// </summary>
    public required double B { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres de la calculatrice.
    /// </summary>
    /// <returns>Une chaîne contenant l'opération et les opérandes</returns>
    public override string ToString()
    {
        return $"Operation: {Operation}, A: {A}, B: {B}";
    }
}

/// <summary>
/// Énumère les opérations mathématiques disponibles dans la calculatrice.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CalculatorOperation>))]
public enum CalculatorOperation
{
    /// <summary>Addition de deux nombres</summary>
    Add,
    /// <summary>Soustraction de deux nombres</summary>
    Subtract,
    /// <summary>Multiplication de deux nombres</summary>
    Multiply,
    /// <summary>Division de deux nombres</summary>
    Divide,
    /// <summary>Élévation à la puissance</summary>
    Power,
    /// <summary>Racine carrée d'un nombre</summary>
    SquareRoot,
    /// <summary>Modulo (reste de la division)</summary>
    Modulo,
    /// <summary>Valeur absolue</summary>
    Abs,
    /// <summary>Logarithme</summary>
    Log,
    /// <summary>Sinus</summary>
    Sin,
    /// <summary>Cosinus</summary>
    Cos,
    /// <summary>Tangente</summary>
    Tan,
    /// <summary>Arrondi</summary>
    Round,
    /// <summary>Arrondi inférieur</summary>
    Floor,
    /// <summary>Arrondi supérieur</summary>
    Ceiling
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres de la calculatrice.<br/>
/// Généré partiellement par le compilateur pour optimiser la sérialisation/désérialisation.
/// </summary>
[JsonSerializable(typeof(CalculatorParameters))]
public partial class CalculatorParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations de la calculatrice implémentant l'interface outil du protocole MCP.
/// </summary>
/// <remarks>
/// Cette classe gère toutes les opérations mathématiques disponibles dans la calculatrice.<br/>
/// Elle valide les entrées, effectue les calculs et retourne les résultats dans le format approprié.
/// </remarks>
public class CalculatorToolHandler(
    IServerContext serverContext,
    ISessionContext sessionContext,
    ILogger<CalculatorToolHandler> logger
) : ToolHandlerBase<CalculatorParameters>(tool, serverContext, sessionContext)
{
    /// <summary>
    /// Définition statique de l'outil avec ses métadonnées.
    /// </summary>
    private static readonly Tool tool =
        new()
        {
            Name = "Calculator",
            Description = "Performs basic arithmetic operations",
            InputSchema =
                CalculatorParametersJsonContext.Default.CalculatorParameters.GetToolSchema()!
        };

    /// <summary>
    /// Information de type JSON pour la sérialisation/désérialisation des paramètres.
    /// </summary>
    public override JsonTypeInfo JsonTypeInfo =>
        CalculatorParametersJsonContext.Default.CalculatorParameters;

    /// <summary>
    /// Traite une requête d'opération de calculatrice.
    /// </summary>
    /// <param name="parameters">Paramètres de l'opération à effectuer</param>
    /// <param name="cancellationToken">Jeton d'annulation pour les opérations asynchrones</param>
    /// <returns>Le résultat de l'opération encapsulé dans un CallToolResult</returns>
    /// <remarks>
    /// Cette méthode :
    /// - Valide les paramètres d'entrée
    /// - Effectue l'opération mathématique demandée
    /// - Gère les cas d'erreur (division par zéro, racine carrée de nombre négatif, etc.)
    /// - Journalise le résultat
    /// - Retourne le résultat formaté
    /// </remarks>
    /// <exception cref="DivideByZeroException">Levée lors d'une tentative de division par zéro (Opérations : Divide, Modulo)</exception>
    /// <exception cref="ArgumentException">Levée pour des paramètres invalides (SquareRoot avec nombre négatif, Log avec paramètres invalides, Opération inconnue)</exception>
    protected override Task<CallToolResult> HandleAsync(
        CalculatorParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Query: {parameters}", parameters.ToString());

        var result = parameters.Operation switch
        {
            // Opérations de base
            CalculatorOperation.Add => parameters.A + parameters.B,
            CalculatorOperation.Subtract => parameters.A - parameters.B,
            CalculatorOperation.Multiply => parameters.A * parameters.B,
            CalculatorOperation.Divide when parameters.B != 0 => parameters.A / parameters.B,
            CalculatorOperation.Divide => throw new DivideByZeroException("Cannot divide by zero"),
            
            // Opérations avancées
            CalculatorOperation.Power => Math.Pow(parameters.A, parameters.B),
            CalculatorOperation.SquareRoot when parameters.A >= 0 => Math.Sqrt(parameters.A),
            CalculatorOperation.SquareRoot => throw new ArgumentException("Cannot calculate square root of negative number"),
            CalculatorOperation.Modulo when parameters.B != 0 => parameters.A % parameters.B,
            CalculatorOperation.Modulo => throw new DivideByZeroException("Cannot calculate modulo with zero"),
            CalculatorOperation.Abs => Math.Abs(parameters.A),
            
            // Fonctions trigonométriques et logarithmiques
            CalculatorOperation.Log when parameters.A > 0 => Math.Log(parameters.A, parameters.B),
            CalculatorOperation.Log => throw new ArgumentException("Invalid logarithm parameters"),
            CalculatorOperation.Sin => Math.Sin(parameters.A),
            CalculatorOperation.Cos => Math.Cos(parameters.A),
            CalculatorOperation.Tan => Math.Tan(parameters.A),
            
            // Fonctions d'arrondi
            CalculatorOperation.Round => Math.Round(parameters.A, (int)parameters.B),
            CalculatorOperation.Floor => Math.Floor(parameters.A),
            CalculatorOperation.Ceiling => Math.Ceiling(parameters.A),
            
            _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
        };

        var content = new TextContent { Text = result.ToString() };

        logger.LogInformation("Calculated with final content: {content}", content);

        return Task.FromResult(new CallToolResult { Content = new Annotated[] { content } });
    }
}