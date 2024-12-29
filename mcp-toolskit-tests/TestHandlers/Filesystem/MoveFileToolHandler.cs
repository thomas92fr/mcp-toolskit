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
    [Collection("FileSystem")]  // Désactive la parallélisation
    public class TestMoveFileToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<MoveFileToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly MoveFileToolHandler _handler;
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

        public TestMoveFileToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-",""));

            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<MoveFileToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new MoveFileToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );

            // Ensure test directory exists and is clean
            EnsureDirectoryExists(_testBasePath);
        }

        private void EnsureDirectoryExists(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            lock (_lock)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        private void CleanupDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
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

        private string GetTestPath(string filename)
        {
            var path = Path.Combine(_testBasePath, filename);
            EnsureDirectoryExists(Path.GetDirectoryName(path));
            return path;
        }

        [Theory]
        [InlineData("source.txt", "destination.txt", "File content")]
        [InlineData("subfolder/source.txt", "newsubfolder/destination.txt", "Another file content")]
        [InlineData("特殊文字のファイル.txt", "移動したファイル.txt", "Unicode content")]
        public async Task MoveFile_ValidFile_ShouldMoveSuccessfully(string sourceFilename, string destFilename, string fileContent)
        {
            // Arrange
            var sourcePath = GetTestPath(sourceFilename);
            var destPath = GetTestPath(destFilename);

            // Ensure directory for source and destination exist
            EnsureDirectoryExists(Path.GetDirectoryName(sourcePath));
            EnsureDirectoryExists(Path.GetDirectoryName(destPath));

            // Create source file with content
            await File.WriteAllTextAsync(sourcePath, fileContent);

            var parameters = new MoveFileParameters
            {
                Operation = MoveFileOperation.MoveFile,
                Source = sourcePath,
                Destination = destPath
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Contains($"Successfully moved {sourcePath} to {destPath}", textContent.Text);

                // Verify files
                Assert.False(File.Exists(sourcePath), "Original file should not exist after move");
                Assert.True(File.Exists(destPath), "Destination file should exist");
                var movedFileContent = await File.ReadAllTextAsync(destPath);
                Assert.Equal(fileContent, movedFileContent);
            }
            finally
            {
                // Additional cleanup of potentially created files/directories
                if (File.Exists(sourcePath)) File.Delete(sourcePath);
                if (File.Exists(destPath)) File.Delete(destPath);
            }
        }

        [Theory]
        [InlineData("test-source.txt", "")]
        [InlineData("", "test-dest.txt")]
        public async Task MoveFile_MissingSourceOrDestination_ShouldThrowArgumentException(string sourcePath, string destPath)
        {
            // Arrange
            sourcePath = sourcePath == "" ? "" : GetTestPath(sourcePath);
            destPath = destPath == "" ? "" : GetTestPath(destPath);

            var parameters = new MoveFileParameters
            {
                Operation = MoveFileOperation.MoveFile,
                Source = sourcePath,
                Destination = destPath
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task MoveFile_SourceFileNotExists_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var sourcePath = GetTestPath("non_existent_source.txt");
            var destPath = GetTestPath("destination.txt");

            var parameters = new MoveFileParameters
            {
                Operation = MoveFileOperation.MoveFile,
                Source = sourcePath,
                Destination = destPath
            };

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task MoveFile_DestinationFileAlreadyExists_ShouldThrowIOException()
        {
            // Arrange
            var sourcePath = GetTestPath("source.txt");
            var destPath = GetTestPath("destination.txt");

            // Create source and destination files
            await File.WriteAllTextAsync(sourcePath, "Source content");
            await File.WriteAllTextAsync(destPath, "Existing destination content");

            var parameters = new MoveFileParameters
            {
                Operation = MoveFileOperation.MoveFile,
                Source = sourcePath,
                Destination = destPath
            };

            // Act & Assert
            await Assert.ThrowsAsync<IOException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task MoveFile_InvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var sourcePath = GetTestPath("source.txt");
            var destPath = GetTestPath("destination.txt");

            var parameters = new MoveFileParameters
            {
                Operation = (MoveFileOperation)999, // Invalid operation
                Source = sourcePath,
                Destination = destPath
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