using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using Moq;

namespace mcp_toolskit_tests.TestHandlers.Filesystem
{
    [Collection("FileSystem")]  // Désactive la parallélisation
    public class TestWriteFileToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<WriteFileToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly WriteFileToolHandler _handler;
        private readonly string _testBasePath;
        private readonly object _lock = new object();

        public class TestAppConfig : AppConfig
        {
            public override string ValidatePath(string path)
            {
                // Pour les tests, on retourne simplement le chemin complet
                return Path.GetFullPath(path);
            }
        }

        public TestWriteFileToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-", ""));

            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<WriteFileToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new WriteFileToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );

            // Ensure test directory exists and is clean
            EnsureDirectoryExists(_testBasePath);
        }

        private void EnsureDirectoryExists(string path)
        {
            lock (_lock)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        private void CleanupDirectory(string path)
        {
            lock (_lock)
            {
                if (Directory.Exists(path))
                {
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch (IOException)
                    {
                        // Si le dossier est verrouillé, on attend un peu et on réessaie
                        Thread.Sleep(100);
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                        }
                    }
                }
            }
        }

        private string GetTestPath(string filename)
        {
            var path = Path.Combine(_testBasePath, filename);
            EnsureDirectoryExists(Path.GetDirectoryName(path));
            return path;
        }

        [Theory]
        [InlineData("test.txt", "Hello world", "")] // Écrit dans un nouveau fichier
        [InlineData("test.txt", "New content", "Old content")] // Remplace le contenu existant
        [InlineData("test.txt", "Special chars: ~@#$%", "Previous content")] // Caractères spéciaux
        [InlineData("test.txt", "Line 1\nLine 2", "Original")] // Contenu multiligne
        public async Task WriteFile_ShouldWriteContentCorrectly(string filename, string newContent, string initialContent)
        {
            // Arrange
            var filePath = GetTestPath(filename);

            // Write initial content if not empty
            if (!string.IsNullOrEmpty(initialContent))
            {
                await File.WriteAllTextAsync(filePath, initialContent);
            }

            var parameters = new WriteFileParameters
            {
                Operation = WriteFileOperation.WriteFile,
                Path = filePath,
                Content = newContent
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully wrote to {filePath}", textContent.Text);

                // Verify file content
                var actualContent = await File.ReadAllTextAsync(filePath);
                Assert.Equal(newContent, actualContent);
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (IOException)
                    {
                        // Ignore deletion errors during cleanup
                    }
                }
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task WriteFile_WithInvalidPath_ShouldThrowArgumentException(string filePath)
        {
            // Arrange
            var parameters = new WriteFileParameters
            {
                Operation = WriteFileOperation.WriteFile,
                Path = filePath,
                Content = "Test content"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData("test.txt", "")]
        [InlineData("test.txt", null)]
        public async Task WriteFile_WithInvalidContent_ShouldThrowArgumentException(string filename, string content)
        {
            // Arrange
            var filePath = GetTestPath(filename);
            var parameters = new WriteFileParameters
            {
                Operation = WriteFileOperation.WriteFile,
                Path = filePath,
                Content = content
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task WriteFile_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = GetTestPath("test.txt");
            var parameters = new WriteFileParameters
            {
                Operation = (WriteFileOperation)999, // Invalid operation
                Path = filePath,
                Content = "Test content"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        public void Dispose()
        {
            // Cleanup all test directories
            CleanupDirectory(Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests"));
        }
    }
}