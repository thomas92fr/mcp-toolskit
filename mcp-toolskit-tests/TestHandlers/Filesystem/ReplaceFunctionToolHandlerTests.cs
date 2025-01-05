using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using Moq;

namespace mcp_toolskit_tests.TestHandlers.Filesystem
{
    [Collection("FileSystem")]  // Désactive la parallélisation
    public class TestReplaceFunctionToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<ReplaceFunctionToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly ReplaceFunctionToolHandler _handler;
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

        public TestReplaceFunctionToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-", ""));

            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<ReplaceFunctionToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new ReplaceFunctionToolHandler(
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

        [Fact]
        public async Task ReplaceFunction_ShouldReplaceCSharpFunction()
        {
            // Arrange
            var filePath = GetTestPath("test.cs");
            var initialContent = "public class TestClass { public void Test() { /* old */ } }";
            await File.WriteAllTextAsync(filePath, initialContent);

            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "public void Test()",
                StartMarkers = new[] { "{" },
                EndMarkers = new[] { "}" },
                NewFunctionCode = "{\n    /* new */\n}"
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Equal($"Successfully replaced function in {filePath}", textContent.Text);

                var actualContent = await File.ReadAllTextAsync(filePath);
                Assert.Equal("public class TestClass { public void Test() {\n    /* new */\n} }", actualContent);
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

        [Fact]
        public async Task ReplaceFunction_ShouldReplaceDelphiProcedure()
        {
            // Arrange
            var filePath = GetTestPath("test.pas");
            var initialContent = "unit MyUnit;\n\ninterface\n\ntype\n  TMyClass = class\n  public\n    procedure Test(AValue: Integer);\n  end;\n\nimplementation\n\nprocedure TMyClass.Test(AValue: Integer);\nbegin\n  // Old code\nend;";
            await File.WriteAllTextAsync(filePath, initialContent);

            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "procedure TMyClass.Test(AValue: Integer)",
                StartMarkers = new[] { "begin" },
                EndMarkers = new[] { "end;" },
                NewFunctionCode = "begin\n  // New code\nend;"
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Equal($"Successfully replaced function in {filePath}", textContent.Text);

                var actualContent = await File.ReadAllTextAsync(filePath);
                Assert.Equal("unit MyUnit;\n\ninterface\n\ntype\n  TMyClass = class\n  public\n    procedure Test(AValue: Integer);\n  end;\n\nimplementation\n\nprocedure TMyClass.Test(AValue: Integer);\nbegin\n  // New code\nend;", actualContent);
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
        public async Task ReplaceFunction_WithInvalidPath_ShouldThrowArgumentException(string filePath)
        {
            // Arrange
            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "test",
                StartMarkers = new[] { "{" },
                EndMarkers = new[] { "}" },
                NewFunctionCode = "test"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task ReplaceFunction_WithInvalidSignature_ShouldThrowArgumentException(string signature)
        {
            // Arrange
            var filePath = GetTestPath("test.cs");
            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = signature,
                StartMarkers = new[] { "{" },
                EndMarkers = new[] { "}" },
                NewFunctionCode = "test"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task ReplaceFunction_WithInvalidStartMarkers_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = GetTestPath("test.cs");
            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "test",
                StartMarkers = null,
                EndMarkers = new[] { "}" },
                NewFunctionCode = "test"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task ReplaceFunction_WithInvalidEndMarkers_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = GetTestPath("test.cs");
            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "test",
                StartMarkers = new[] { "{" },
                EndMarkers = null,
                NewFunctionCode = "test"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task ReplaceFunction_WithInvalidNewCode_ShouldThrowArgumentException(string newCode)
        {
            // Arrange
            var filePath = GetTestPath("test.cs");
            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "test",
                StartMarkers = new[] { "{" },
                EndMarkers = new[] { "}" },
                NewFunctionCode = newCode
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task ReplaceFunction_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var filePath = GetTestPath("nonexistent.txt");
            var parameters = new ReplaceFunctionParameters
            {
                Operation = ReplaceFunctionOperation.ReplaceFunction,
                Path = filePath,
                FunctionSignature = "test",
                StartMarkers = new[] { "{" },
                EndMarkers = new[] { "}" },
                NewFunctionCode = "test"
            };

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task ReplaceFunction_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = GetTestPath("test.txt");
            var parameters = new ReplaceFunctionParameters
            {
                Operation = (ReplaceFunctionOperation)999, // Invalid operation
                Path = filePath,
                FunctionSignature = "test",
                StartMarkers = new[] { "{" },
                EndMarkers = new[] { "}" },
                NewFunctionCode = "test"
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