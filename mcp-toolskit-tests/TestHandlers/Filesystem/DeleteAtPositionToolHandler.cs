using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Features.Tools;
using Moq;

namespace mcp_toolskit_tests.TestHandlers.Filesystem
{
    public class TestDeleteAtPositionToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<DeleteAtPositionToolHandler>> _mockLogger;
        private readonly AppConfig _appConfig;
        private readonly DeleteAtPositionToolHandler _handler;
        private readonly string _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests");
        private readonly string _testFilePath;

        public TestDeleteAtPositionToolHandler()
        {
            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<DeleteAtPositionToolHandler>>();

            // Setup AppConfig with test directory
            _appConfig = new AppConfig 
            { 
                AllowedDirectories = new string[] { _testBasePath }
            };

            // Create handler instance
            _handler = new DeleteAtPositionToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );

            // Ensure test directory exists
            Directory.CreateDirectory(_testBasePath);
            _testFilePath = Path.Combine(_testBasePath, "test.txt");
        }

        [Theory]
        [InlineData("Hello World!", 6, 5, false, "Hello !")] // Delete "World"
        [InlineData("Hello World!", 6, 5, true, "Hello      !")] // Replace "World" with spaces
        [InlineData("Test Content", 0, 4, false, " Content")] // Delete from start
        public async Task DeleteAtPosition_ShouldModifyFileCorrectly(
            string initialContent,
            int position,
            int length,
            bool preserveLength,
            string expectedContent)
        {
            // Arrange
            await File.WriteAllTextAsync(_testFilePath, initialContent);

            var parameters = new DeleteAtPositionParameters
            {
                Operation = DeleteAtPositionOperation.DeleteAtPosition,
                Path = _testFilePath,
                Position = position,
                Length = length,
                PreserveLength = preserveLength
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully deleted", textContent.Text);
                
                var actualContent = await File.ReadAllTextAsync(_testFilePath);
                Assert.Equal(expectedContent, actualContent);
            }
            finally
            {
                if (File.Exists(_testFilePath))
                    File.Delete(_testFilePath);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task DeleteAtPosition_WithInvalidPath_ShouldThrowArgumentException(string filePath)
        {
            // Arrange
            var parameters = new DeleteAtPositionParameters
            {
                Operation = DeleteAtPositionOperation.DeleteAtPosition,
                Path = filePath,
                Position = 0,
                Length = 1,
                PreserveLength = false
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData(-1, 5)] // Negative position
        [InlineData(0, 0)] // Zero length
        [InlineData(0, -1)] // Negative length
        public async Task DeleteAtPosition_WithInvalidParameters_ShouldThrowArgumentException(
            int position,
            int length)
        {
            // Arrange
            await File.WriteAllTextAsync(_testFilePath, "Test Content");

            var parameters = new DeleteAtPositionParameters
            {
                Operation = DeleteAtPositionOperation.DeleteAtPosition,
                Path = _testFilePath,
                Position = position,
                Length = length,
                PreserveLength = false
            };

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(
                    async () => await _handler.TestHandleAsync(parameters, default)
                );
            }
            finally
            {
                if (File.Exists(_testFilePath))
                    File.Delete(_testFilePath);
            }
        }

        [Fact]
        public async Task DeleteAtPosition_WithPositionBeyondFileEnd_ShouldThrowArgumentException()
        {
            // Arrange
            var content = "Short text";
            await File.WriteAllTextAsync(_testFilePath, content);

            var parameters = new DeleteAtPositionParameters
            {
                Operation = DeleteAtPositionOperation.DeleteAtPosition,
                Path = _testFilePath,
                Position = content.Length + 1, // Beyond end of file
                Length = 5,
                PreserveLength = false
            };

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(
                    async () => await _handler.TestHandleAsync(parameters, default)
                );
            }
            finally
            {
                if (File.Exists(_testFilePath))
                    File.Delete(_testFilePath);
            }
        }

        [Fact]
        public async Task DeleteAtPosition_WithUnauthorizedPath_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var unauthorizedPath = Path.Combine(Path.GetTempPath(), "unauthorized", "test.txt");
            var parameters = new DeleteAtPositionParameters
            {
                Operation = DeleteAtPositionOperation.DeleteAtPosition,
                Path = unauthorizedPath,
                Position = 0,
                Length = 5,
                PreserveLength = false
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task DeleteAtPosition_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var parameters = new DeleteAtPositionParameters
            {
                Operation = (DeleteAtPositionOperation)999, // Invalid operation
                Path = _testFilePath,
                Position = 0,
                Length = 5,
                PreserveLength = false
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        public void Dispose()
        {
            // Cleanup test directory if it exists
            if (Directory.Exists(_testBasePath))
                Directory.Delete(_testBasePath, true);
        }
    }
}