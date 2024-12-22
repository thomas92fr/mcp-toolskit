using Xunit;
using System.Threading.Tasks;
using mcp_toolskit.Handlers;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using Moq;
using System;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;

namespace mcp_toolskit_tests
{
    public class CalculatorTests
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<CalculatorToolHandler>> _mockLogger;
        private readonly CalculatorToolHandler _handler;

        public CalculatorTests()
        {
            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<CalculatorToolHandler>>();

            // Create handler instance
            _handler = new CalculatorToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object
            );
        }

        [Theory]
        [InlineData(2, 3, 5)]       // Test basique
        [InlineData(-1, 1, 0)]      // Test avec nombre négatif
        [InlineData(0, 0, 0)]       // Test avec zéros
        [InlineData(double.MaxValue, 1, double.MaxValue + 1)] // Test limite supérieure
        public async Task Add_ShouldReturnCorrectSum(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Add,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.HandleMessageAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var content = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), content.Text);
        }

        [Theory]
        [InlineData(10, 0)]     // Division par zéro
        public async Task Divide_ByZero_ShouldThrowException(double a, double b)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Divide,
                A = a,
                B = b
            };

            // Act & Assert
            await Assert.ThrowsAsync<DivideByZeroException>(
                async () => await _handler.HandleMessageAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData(-1)]    // Racine carrée d'un nombre négatif
        public async Task SquareRoot_NegativeNumber_ShouldThrowException(double a)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.SquareRoot,
                A = a,
                B = 0
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.HandleMessageAsync(parameters, default)
            );
        }
    }
}