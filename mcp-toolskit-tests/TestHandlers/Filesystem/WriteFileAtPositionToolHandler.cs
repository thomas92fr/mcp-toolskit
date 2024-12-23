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
    public class TestWriteFileAtPositionToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<WriteFileAtPositionToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly WriteFileAtPositionToolHandler _handler;
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

        public TestWriteFileAtPositionToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-",""));

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
        [InlineData("test.txt", "Hello", 0, "Hello world", "Hello world")] // Pas de changement, contenu identique
        [InlineData("test.txt", "totos", 0, "Hello world", "totos world")] // Remplace au début
        [InlineData("test.txt", " world", 5, "Hello", "Hello world")] // Insère au milieu
        [InlineData("test.txt", "Test", 10, "Hello", "Hello     Test")] // Ajoute à la fin avec espaces
        [InlineData("test.txt", " awesome", 5, "Hello world!", "Hello awesome world!")] // Insère dans une phrase
        // Cas supplémentaires
        [InlineData("test.txt", " ", 6, "Hello!", "Hello! ")] // Insère un espace à la fin
        [InlineData("test.txt", "New", 0, "", "New")] // Insère dans un fichier vide        
        [InlineData("test.txt", "Middle", 7, "1234567890", "1234567Middle890")] // Insère en milieu d'une chaîne numérique
        [InlineData("test.txt", "Extra", 0, "Extra", "Extra")] // Contenu déjà au début
        [InlineData("test.txt", "Padding", 3, "ABCDEF", "ABCPaddingDEF")] // Insère à une position spécifique
        [InlineData("test.txt", "Append", 10, "Existing", "Existing  Append")] // Insère avec des espaces lorsque la position dépasse de 2 caractères
        [InlineData("test.txt", "Multi", 3, "A B C D E F", "A BMulti C D E F")] // Contenu avec espaces
        [InlineData("test.txt", "Special!", 3, "Chars~@$#", "ChaSpecial!rs~@$#")] // Caractères spéciaux

        public async Task WriteFileAtPosition_ShouldWriteContentCorrectly(string filename, string content, int position, string initialContent, string expectedFinalContent)
        {
            await Task.Delay(100);  
            
            // Arrange
            var filePath = GetTestPath(filename);

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
            var filePath = GetTestPath(filename);
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
            var filePath = GetTestPath("test.txt");
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
            var filePath = GetTestPath("test.txt");
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
            // Cleanup all test directories
            CleanupDirectory(Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests"));
        }
    }
}