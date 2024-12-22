using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
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
[Description("Performs basic arithmetic operations")]
public enum CalculatorOperation
{
    /// <summary>Addition of two numbers</summary>
    [Description("Adds two numbers and returns their sum")]
    [Parameters("A: First number to add", "B: Second number to add")]
    Add,
    /// <summary>Subtraction of two numbers</summary>
    [Description("Subtracts the second number from the first and returns the difference")]
    [Parameters("A: Starting number", "B: Number to subtract")]
    Subtract,
    /// <summary>Multiplication of two numbers</summary>
    [Description("Multiplies two numbers and returns their product")]
    [Parameters("A: First factor", "B: Second factor")]
    Multiply,
    /// <summary>Division of two numbers</summary>
    [Description("Divides the first number by the second and returns the quotient")]
    [Parameters("A: Dividend (number to divide)", "B: Divisor (non-zero)")]
    Divide,
    /// <summary>Power operation</summary>
    [Description("Raises the first number to the power of the second")]
    [Parameters("A: Base", "B: Exponent")]
    Power,
    /// <summary>Square root of a number</summary>
    [Description("Calculates the square root of the first number")]
    [Parameters("A: Number (non-negative)", "B: Not used")]
    SquareRoot,
    /// <summary>Modulo (division remainder)</summary>
    [Description("Calculates the remainder of dividing the first number by the second")]
    [Parameters("A: Dividend", "B: Divisor (non-zero)")]
    Modulo,
    /// <summary>Absolute value</summary>
    [Description("Calculates the absolute value of the first number")]
    [Parameters("A: Number to transform", "B: Not used")]
    Abs,
    /// <summary>Logarithm</summary>
    [Description("Calculates the logarithm of the first number with the second as base")]
    [Parameters("A: Number (strictly positive)", "B: Logarithm base (strictly positive and not equal to 1)")]
    Log,
    /// <summary>Sine</summary>
    [Description("Calculates the sine of the first number (in radians)")]
    [Parameters("A: Angle in radians", "B: Not used")]
    Sin,
    /// <summary>Cosine</summary>
    [Description("Calculates the cosine of the first number (in radians)")]
    [Parameters("A: Angle in radians", "B: Not used")]
    Cos,
    /// <summary>Tangent</summary>
    [Description("Calculates the tangent of the first number (in radians)")]
    [Parameters("A: Angle in radians", "B: Not used")]
    Tan,
    /// <summary>Rounding</summary>
    [Description("Rounds the first number to the number of decimal places specified by the second")]
    [Parameters("A: Number to round", "B: Number of decimal places (integer)")]
    Round,
    /// <summary>Floor rounding</summary>
    [Description("Rounds down the first number to the nearest integer")]
    [Parameters("A: Number to round", "B: Not used")]
    Floor,
    /// <summary>Ceiling rounding</summary>
    [Description("Rounds up the first number to the nearest integer")]
    [Parameters("A: Number to round", "B: Not used")]
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
            Description = typeof(CalculatorOperation).GenerateFullDescription(),
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

    public  Task<CallToolResult> TestHandleAsync(
        CalculatorParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}