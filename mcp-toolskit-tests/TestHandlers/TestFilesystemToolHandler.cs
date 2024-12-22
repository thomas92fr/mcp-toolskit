using mcp_toolskit.Handlers;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using Moq;

namespace mcp_toolskit_tests.TestHandlers
{
    public class TestFilesystemToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<FilesystemToolHandler>> _mockLogger;
        private readonly FilesystemToolHandler _handler;
        private readonly string _testDirectory;
        private readonly AppConfig _appConfig;

        public TestFilesystemToolHandler()
        {
            // Créer un répertoire temporaire pour les tests
            _testDirectory = Path.Combine(Path.GetTempPath(), "FilesystemToolHandlerTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Setup configuration
            _appConfig = new AppConfig
            {
                AllowedDirectories = new[] { _testDirectory }
            };

            // Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<FilesystemToolHandler>>();

            // Create handler instance
            _handler = new FilesystemToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );
        }

        public void Dispose()
        {
            // Nettoyer le répertoire de test après les tests
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public async Task WriteFile_ShouldCreateFileWithContent()
        {
            // Arrange
            string testFileName = "test.txt";
            string testContent = "Hello, World!";
            string testFilePath = Path.Combine(_testDirectory, testFileName);

            var parameters = new FilesystemParameters
            {
                Operation = FilesystemOperation.WriteFile,
                Path = testFilePath,
                Content = testContent
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Equal($"Successfully wrote to {testFilePath}", textContent.Text);

            // Vérifier que le fichier existe et contient le bon contenu
            Assert.True(File.Exists(testFilePath));
            string actualContent = await File.ReadAllTextAsync(testFilePath);
            Assert.Equal(testContent, actualContent);
        }

        [Fact]
        public async Task WriteFile_ShouldThrowException_WhenPathOutsideAllowedDirectories()
        {
            // Arrange
            string testFilePath = Path.Combine(Path.GetTempPath(), "unauthorized.txt");
            var parameters = new FilesystemParameters
            {
                Operation = FilesystemOperation.WriteFile,
                Path = testFilePath,
                Content = "Test content"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task WriteFile_ShouldThrowException_WhenPathIsNull()
        {
            // Arrange
            var parameters = new FilesystemParameters
            {
                Operation = FilesystemOperation.WriteFile,
                Path = null,
                Content = "Test content"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task WriteFile_ShouldThrowException_WhenContentIsNull()
        {
            // Arrange
            var parameters = new FilesystemParameters
            {
                Operation = FilesystemOperation.WriteFile,
                Path = Path.Combine(_testDirectory, "test.txt"),
                Content = null
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }
    }
}