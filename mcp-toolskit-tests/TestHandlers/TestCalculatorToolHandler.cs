using mcp_toolskit.Handlers;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Server.Features.Tools;
using Moq;

namespace mcp_toolskit_tests.TestHandlers
{

    public class TestCalculatorToolHandler
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<CalculatorToolHandler>> _mockLogger;
        private readonly CalculatorToolHandler _handler;

        public TestCalculatorToolHandler()
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
        [InlineData(2, 3, 5)]
        [InlineData(-1, 1, 0)]
        [InlineData(0, 0, 0)]
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
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Content.Length > 0);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(5, 3, 2)]
        [InlineData(10, -5, 15)]
        [InlineData(0, 0, 0)]
        public async Task Subtract_ShouldReturnCorrectDifference(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Subtract,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(4, 2, 8)]
        [InlineData(-3, -2, 6)]
        [InlineData(0, 5, 0)]
        public async Task Multiply_ShouldReturnCorrectProduct(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Multiply,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(10, 2, 5)]
        [InlineData(-8, 2, -4)]
        [InlineData(0, 5, 0)]
        public async Task Divide_ShouldReturnCorrectQuotient(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Divide,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(10, 0)]
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
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData(2, 3, 8)]
        [InlineData(5, 2, 25)]
        [InlineData(0, 5, 0)]
        public async Task Power_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Power,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(16, 0, 4)]
        [InlineData(9, 0, 3)]
        [InlineData(0, 0, 0)]
        public async Task SquareRoot_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.SquareRoot,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(-1)]
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
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData(10, 3, 1)]
        [InlineData(17, 5, 2)]
        public async Task Modulo_ShouldReturnCorrectRemainder(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Modulo,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(10, 0)]
        public async Task Modulo_ByZero_ShouldThrowException(double a, double b)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Modulo,
                A = a,
                B = b
            };

            // Act & Assert
            await Assert.ThrowsAsync<DivideByZeroException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData(-5, 0, 5)]
        [InlineData(3.5, 0, 3.5)]
        [InlineData(0, 0, 0)]
        public async Task Abs_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Abs,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(Math.E, Math.E, 1)]
        [InlineData(8, 2, 3)]
        public async Task Log_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Log,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(-1, 2)]
        [InlineData(0, 2)]
        public async Task Log_InvalidInput_ShouldThrowException(double a, double b)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Log,
                A = a,
                B = b
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(Math.PI / 2, 0, 1)]
        [InlineData(Math.PI, 0, 0)]
        public async Task Sin_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Sin,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(Math.Round(expected, 10).ToString(), Math.Round(double.Parse(textContent.Text), 10).ToString());
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(Math.PI / 2, 0, 0)]
        [InlineData(Math.PI, 0, -1)]
        public async Task Cos_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Cos,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(Math.Round(expected, 10).ToString(), Math.Round(double.Parse(textContent.Text), 10).ToString());
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(Math.PI / 4, 0, 1)]
        public async Task Tan_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Tan,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(Math.Round(expected, 10).ToString(), Math.Round(double.Parse(textContent.Text), 10).ToString());
        }

        [Theory]
        [InlineData(3.7, 0, 4)]
        [InlineData(-3.7, 0, -4)]
        [InlineData(3.2, 0, 3)]
        public async Task Round_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Round,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }

        [Theory]
        [InlineData(3.7, 0, 3)]
        [InlineData(-3.7, 0, -4)]
        [InlineData(3.2, 0, 3)]
        public async Task Floor_ShouldReturnCorrectResult(double a, double b, double expected)
        {
            // Arrange
            var parameters = new CalculatorParameters
            {
                Operation = CalculatorOperation.Floor,
                A = a,
                B = b
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal(expected.ToString(), textContent.Text);
        }
    }
}