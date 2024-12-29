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
    public class TestReadMultipleFilesToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<ReadMultipleFilesToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly ReadMultipleFilesToolHandler _handler;
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

        public TestReadMultipleFilesToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-",""));

            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<ReadMultipleFilesToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new ReadMultipleFilesToolHandler(
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

        public static IEnumerable<object[]> ReadMultipleFiles_TestData()
        {
            yield return new object[] { 
                new[] { "file1.txt", "file2.txt" }, 
                new[] { "Hello", "World" } 
            };
            yield return new object[] { 
                new[] { "test.txt" }, 
                new[] { "Single file test" } 
            };
            yield return new object[] { 
                new[] { "empty.txt", "content.txt" }, 
                new[] { "", "Some content" } 
            };
            yield return new object[] { 
                new[] { "special-chars.txt", "unicode.txt" }, 
                new[] { "Special!@#$%^&*()", "Unicodeêàçè" } 
            };
        }

        [Theory]
        [MemberData(nameof(ReadMultipleFiles_TestData))]
        public async Task ReadMultipleFiles_ShouldReadFilesCorrectly(string[] filenames, string[] contents)
        {
            //await Task.Delay(100);  
            
            // Arrange
            var filePaths = filenames.Select(GetTestPath).ToList();
            
            try 
            {
                // Create test files
                for (int i = 0; i < filePaths.Count; i++)
                {
                    await File.WriteAllTextAsync(filePaths[i], contents[i]);
                }

                var parameters = new ReadMultipleFilesParameters
                {
                    Operation = ReadMultipleFilesOperation.ReadMultipleFiles,
                    Paths = filePaths
                };

                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                
                // Verify each file was read correctly
                for (int i = 0; i < filePaths.Count; i++)
                {
                    Assert.Contains($"{filePaths[i]}:\n{contents[i]}", textContent.Text);
                }
            }
            finally
            {
                // Cleanup
                foreach (var filePath in filePaths)
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
        }

        public static IEnumerable<object[]> EmptyPaths_TestData()
        {
            yield return new object[] { null };
            yield return new object[] { new string[0] };
        }

        [Theory]
        [MemberData(nameof(EmptyPaths_TestData))]
        public async Task ReadMultipleFiles_WithEmptyPaths_ShouldThrowArgumentException(string[] paths)
        {
            // Arrange
            var parameters = new ReadMultipleFilesParameters
            {
                Operation = ReadMultipleFilesOperation.ReadMultipleFiles,
                Paths = paths?.ToList() ?? new List<string>()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task ReadMultipleFiles_WithNonExistentFile_ShouldReportError()
        {
            // Arrange
            var nonExistentPath = GetTestPath("nonexistent.txt");
            var parameters = new ReadMultipleFilesParameters
            {
                Operation = ReadMultipleFilesOperation.ReadMultipleFiles,
                Paths = new List<string> { nonExistentPath }
            };

            // Act
            var result = await _handler.TestHandleAsync(parameters, default);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Content.Length > 0);
            var textContent = Assert.IsType<TextContent>(result.Content[0]);
            Assert.Contains($"{nonExistentPath}: Error", textContent.Text);
        }

        [Fact]
        public async Task ReadMultipleFiles_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = GetTestPath("test.txt");
            var parameters = new ReadMultipleFilesParameters
            {
                Operation = (ReadMultipleFilesOperation)999, // Invalid operation
                Paths = new List<string> { filePath }
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