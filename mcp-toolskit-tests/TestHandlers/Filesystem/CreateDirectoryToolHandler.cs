using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using Moq;

namespace mcp_toolskit_tests.TestHandlers.Filesystem
{
    [Collection("FileSystem")]  // Désactive la parallélisation
    public class TestCreateDirectoryToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<CreateDirectoryToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly CreateDirectoryToolHandler _handler;
        private readonly string _testBasePath;
        private readonly object _lock = new object();

        public class TestAppConfig : AppConfig
        {
            public override string ValidatePath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new ArgumentException("Path cannot be null or empty", nameof(path));
                }
                // Pour les tests, on retourne simplement le chemin complet
                return Path.GetFullPath(path);
            }
        }

        public TestCreateDirectoryToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-", ""));

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

            // Ensure test directory exists and is clean
            CleanupDirectory(_testBasePath);
            EnsureDirectoryExists(_testBasePath);
        }

        private void EnsureDirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

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
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

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

        private string GetTestPath(string directoryName)
        {
            if (string.IsNullOrEmpty(directoryName))
            {
                throw new ArgumentException("Directory name cannot be null or empty", nameof(directoryName));
            }
            return Path.Combine(_testBasePath, directoryName);
        }

        [Theory]
        [InlineData("simple_directory")] // Création d'un dossier simple
        [InlineData("nested/directory")] // Création d'un dossier avec un sous-dossier
        [InlineData("multiple/nested/directories")] // Création de plusieurs niveaux de dossiers
        [InlineData("space directory")] // Dossier avec espace
        [InlineData("special@#$directory")] // Dossier avec caractères spéciaux
        public async Task CreateDirectory_ShouldCreateDirectoryCorrectly(string directoryName)
        {
            //await Task.Delay(150);

            // Arrange
            var dirPath = GetTestPath(directoryName);
            var parameters = new CreateDirectoryParameters
            {
                Operation = CreateDirectoryOperation.CreateDirectory,
                Path = dirPath
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully created directory {dirPath}", textContent.Text);

                // Verify directory exists
                Assert.True(Directory.Exists(dirPath), $"Directory {dirPath} should exist");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(dirPath))
                    {
                        Directory.Delete(dirPath, true);
                    }
                }
                catch (IOException)
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }

        [Fact]
        public async Task CreateDirectory_WithExistingDirectory_ShouldNotThrowException()
        {
            // Arrange
            var dirPath = GetTestPath("existing_directory");
            Directory.CreateDirectory(dirPath);

            var parameters = new CreateDirectoryParameters
            {
                Operation = CreateDirectoryOperation.CreateDirectory,
                Path = dirPath
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully created directory {dirPath}", textContent.Text);
                Assert.True(Directory.Exists(dirPath));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(dirPath))
                    {
                        Directory.Delete(dirPath, true);
                    }
                }
                catch (IOException)
                {
                    // Ignore deletion errors during cleanup
                }
            }
        }

        [Fact]
        public async Task CreateDirectory_WithEmptyPath_ShouldThrowArgumentException()
        {
            // Arrange
            var parameters = new CreateDirectoryParameters
            {
                Operation = CreateDirectoryOperation.CreateDirectory,
                Path = string.Empty
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
            
            Assert.Contains("Path is required", exception.Message);
        }

        [Fact]
        public async Task CreateDirectory_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var dirPath = GetTestPath("test_directory");
            var parameters = new CreateDirectoryParameters
            {
                Operation = (CreateDirectoryOperation)999, // Invalid operation
                Path = dirPath
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
            
            Assert.Contains("Unknown operation", exception.Message);
        }

        public void Dispose()
        {
            // Cleanup all test directories
            CleanupDirectory(_testBasePath);
        }
    }
}