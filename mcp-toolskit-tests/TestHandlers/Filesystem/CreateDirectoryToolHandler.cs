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
    public class TestCreateDirectoryToolHandler
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<CreateDirectoryToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly CreateDirectoryToolHandler _handler;
        private readonly string _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests");

        public class TestAppConfig : AppConfig
        {
            public override string ValidatePath(string path)
            {
                // Pour les tests, on retourne simplement le chemin complet
                return Path.GetFullPath(path);
            }
        }

        public TestCreateDirectoryToolHandler()
        {
            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<CreateDirectoryToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new CreateDirectoryToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );

            // Ensure test directory exists
            Directory.CreateDirectory(_testBasePath);
        }

        [Theory]
        [InlineData("testDir1")]
        [InlineData("path/to/nested/directory")]
        [InlineData("directory with spaces")]
        public async Task CreateDirectory_ShouldCreateDirectory(string directoryPath)
        {
            // Arrange
            var relativePath = Path.Combine(_testBasePath, directoryPath);
            var parameters = new CreateDirectoryParameters
            {
                Operation = CreateDirectoryOperation.CreateDirectory,
                Path = relativePath
            };

            // Cleanup before test
            if (Directory.Exists(relativePath))
                Directory.Delete(relativePath, true);

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully created directory {relativePath}", textContent.Text);
                Assert.True(Directory.Exists(relativePath));
            }
            finally
            {
                // Cleanup after test
                if (Directory.Exists(relativePath))
                    Directory.Delete(relativePath, true);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CreateDirectory_WithInvalidPath_ShouldThrowArgumentException(string directoryPath)
        {
            // Arrange
            var parameters = new CreateDirectoryParameters
            {
                Operation = CreateDirectoryOperation.CreateDirectory,
                Path = directoryPath
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task CreateDirectory_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var parameters = new CreateDirectoryParameters
            {
                Operation = (CreateDirectoryOperation)999, // Invalid operation
                Path = Path.Combine(_testBasePath, "test")
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