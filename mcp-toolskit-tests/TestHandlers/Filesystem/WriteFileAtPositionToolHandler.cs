using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Server.Features.Tools;
using Moq;

namespace mcp_toolskit_tests.TestHandlers.Filesystem
{
    public class TestWriteFileAtPositionToolHandler
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<WriteFileAtPositionToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly WriteFileAtPositionToolHandler _handler;
        private readonly string _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests");

        public class TestAppConfig : AppConfig
        {
            public override string ValidatePath(string path)
            {
                // Pour les tests, on retourne simplement le chemin complet
                return Path.GetFullPath(path);
            }
        }

        public TestWriteFileAtPositionToolHandler()
        {
            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<WriteFileAtPositionToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new WriteFileAtPositionToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );

            // Ensure test directory exists
            Directory.CreateDirectory(_testBasePath);
        }

        [Theory]
        [InlineData("test.txt", "Hello", 0, "Hello world", "Hello world")]
        [InlineData("test.txt", " world", 5, "Hello", "Hello world")]
        [InlineData("test.txt", "Test", 10, "Hello", "Hello     Test")]
        public async Task WriteFileAtPosition_ShouldWriteContentCorrectly(string filename, string content, int position, string initialContent, string expectedFinalContent)
        {
            // Arrange
            var filePath = Path.Combine(_testBasePath, filename);

            // Write initial content if not empty
            if (!string.IsNullOrEmpty(initialContent))
            {
                await File.WriteAllTextAsync(filePath, initialContent);
            }

            var parameters = new WriteFileAtPositionParameters
            {
                Operation = WriteFileAtPositionOperation.WriteFileAtPosition,
                Path = filePath,
                Content = content,
                Position = position
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully wrote content at position {position} in {filePath}", textContent.Text);

                // Verify file content
                var actualContent = await File.ReadAllTextAsync(filePath);
                Assert.Equal(expectedFinalContent, actualContent);
            }
            finally
            {
                // Cleanup
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task WriteFileAtPosition_WithInvalidPath_ShouldThrowArgumentException(string filePath)
        {
            // Arrange
            var parameters = new WriteFileAtPositionParameters
            {
                Operation = WriteFileAtPositionOperation.WriteFileAtPosition,
                Path = filePath,
                Content = "Test content",
                Position = 0
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData("test.txt", "", 0)]
        [InlineData("test.txt", null, 0)]
        public async Task WriteFileAtPosition_WithInvalidContent_ShouldThrowArgumentException(string filename, string content, int position)
        {
            // Arrange
            var filePath = Path.Combine(_testBasePath, filename);
            var parameters = new WriteFileAtPositionParameters
            {
                Operation = WriteFileAtPositionOperation.WriteFileAtPosition,
                Path = filePath,
                Content = content,
                Position = position
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task WriteFileAtPosition_WithNegativePosition_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = Path.Combine(_testBasePath, "test.txt");
            var parameters = new WriteFileAtPositionParameters
            {
                Operation = WriteFileAtPositionOperation.WriteFileAtPosition,
                Path = filePath,
                Content = "Test content",
                Position = -1
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task WriteFileAtPosition_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = Path.Combine(_testBasePath, "test.txt");
            var parameters = new WriteFileAtPositionParameters
            {
                Operation = (WriteFileAtPositionOperation)999, // Invalid operation
                Path = filePath,
                Content = "Test content",
                Position = 0
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