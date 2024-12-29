using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Server.Features.Tools;
using Moq;
using System.Text.RegularExpressions;

namespace mcp_toolskit_tests.TestHandlers.Filesystem
{
    [Collection("FileSystem")]  // Désactive la parallélisation
    public class TestSearchPositionInFileWithRegexToolHandler : IDisposable
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<SearchPositionInFileWithRegexToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly SearchPositionInFileWithRegexToolHandler _handler;
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

        public TestSearchPositionInFileWithRegexToolHandler()
        {
            _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", Guid.NewGuid().ToString().Replace("-", ""));

            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<SearchPositionInFileWithRegexToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new SearchPositionInFileWithRegexToolHandler(
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
        [InlineData("test.txt", "Hello world", @"\w+", "Position: 0, Length: 5, Value: Hello\nPosition: 6, Length: 5, Value: world")] // Recherche de mots
        [InlineData("test.txt", "abc123def456", @"\d+", "Position: 3, Length: 3, Value: 123\nPosition: 9, Length: 3, Value: 456")] // Recherche de nombres
        [InlineData("test.txt", "test@example.com", @"[\w\.-]+@[\w\.-]+", "Position: 0, Length: 16, Value: test@example.com")] // Recherche d'email
        [InlineData("test.txt", "No matches here", @"[0-9]+", "No matches found")] // Aucune correspondance
        [InlineData("test.txt", "aaa bbb aaa", @"aaa", "Position: 0, Length: 3, Value: aaa\nPosition: 8, Length: 3, Value: aaa")] // Multiples occurrences identiques
        [InlineData("test.txt", "AB12CD34", @"[A-Z]{2}\d{2}", "Position: 0, Length: 4, Value: AB12\nPosition: 4, Length: 4, Value: CD34")] // Pattern complexe
        // C# - Recherche de classes/interfaces
        [InlineData("test.cs", "public class MyClass {}", @"public\s+class\s+\w+", "Position: 0, Length: 20, Value: public class MyClass")]
        [InlineData("test.cs", "public interface IMyInterface {}", @"public\s+interface\s+I\w+", "Position: 0, Length: 29, Value: public interface IMyInterface")]
        // C# - Propriétés
        [InlineData("test.cs", "public string Name { get; set; }", @"public\s+\w+\s+\w+\s*\{\s*get;\s*set;\s*\}", "Position: 0, Length: 32, Value: public string Name { get; set; }")]
        // C# - Méthodes asynchrones
        [InlineData("test.cs", "public async Task<string> GetDataAsync()", @"async\s+Task\<\w+\>\s+\w+Async", "Position: 7, Length: 31, Value: async Task<string> GetDataAsync")]
        // C# - Attributs
        [InlineData("test.cs", "[Serializable]\npublic class MyClass {}", @"\[\w+\]", "Position: 0, Length: 14, Value: [Serializable]")]
        // C# - Commentaires XML
        [InlineData("test.cs", "/// <summary>Test</summary>", @"///\s*<summary>[^<]*</summary>", "Position: 0, Length: 27, Value: /// <summary>Test</summary>")]
        // C# - LINQ
        [InlineData("test.cs", "var result = list.Where(x => x > 0).Select(x => x * 2);", @"\.Where\([^)]+\)\.Select\([^)]+\)", "Position: 17, Length: 37, Value: .Where(x => x > 0).Select(x => x * 2)")]
        // Delphi - Classes et interfaces
        [InlineData("test.pas", "type TMyClass = class(TObject)", @"type\s+T\w+\s*=\s*class", "Position: 0, Length: 21, Value: type TMyClass = class")]
        [InlineData("test.pas", "IMyInterface = interface", @"I\w+\s*=\s*interface", "Position: 0, Length: 24, Value: IMyInterface = interface")]
        // Delphi - Procédures et fonctions
        [InlineData("test.pas", "procedure DoSomething;", @"procedure\s+\w+;", "Position: 0, Length: 22, Value: procedure DoSomething;")]
        [InlineData("test.pas", "function GetValue: Integer;", @"function\s+\w+:\s*\w+;", "Position: 0, Length: 27, Value: function GetValue: Integer;")]
        // Delphi - Propriétés
        [InlineData("test.pas", "property Name: string read FName write FName;", @"property\s+\w+:\s*\w+\s+read\s+\w+\s+write\s+\w+", "Position: 0, Length: 44, Value: property Name: string read FName write FName")]
        // Delphi - Directives de compilation
        [InlineData("test.pas", "{$IFDEF DEBUG}\nShowMessage('Debug');\n{$ENDIF}", @"\{[$]IFDEF\s+\w+\}[\s\S]*\{[$]ENDIF\}", "Position: 0, Length: 45, Value: {$IFDEF DEBUG}\nShowMessage('Debug');\n{$ENDIF}")]
        // Delphi - Sections
        [InlineData("test.pas", "implementation\nuses SysUtils;", @"implementation\s*\n\s*uses\s+[\w,\s]+;", "Position: 0, Length: 29, Value: implementation\nuses SysUtils;")]
        public async Task SearchPositionInFileWithRegex_ShouldFindMatchesCorrectly(string filename, string content, string regex, string expectedResult)
        {
            // Arrange
            var filePath = GetTestPath(filename);
            await File.WriteAllTextAsync(filePath, content);

            var parameters = new SearchPositionInFileWithRegexParameters
            {
                Operation = SearchPositionInFileWithRegexOperation.SearchPositionInFileWithRegex,
                Path = filePath,
                Regex = regex
            };

            try
            {
                // Act
                var result = await _handler.TestHandleAsync(parameters, default);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.Content.Length > 0);
                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.Equal(expectedResult, textContent.Text);
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
        public async Task SearchPositionInFileWithRegex_WithInvalidPath_ShouldThrowArgumentException(string filePath)
        {
            // Arrange
            var parameters = new SearchPositionInFileWithRegexParameters
            {
                Operation = SearchPositionInFileWithRegexOperation.SearchPositionInFileWithRegex,
                Path = filePath,
                Regex = @"\w+"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task SearchPositionInFileWithRegex_WithInvalidRegex_ShouldThrowArgumentException(string regex)
        {
            // Arrange
            var filePath = GetTestPath("test.txt");
            var parameters = new SearchPositionInFileWithRegexParameters
            {
                Operation = SearchPositionInFileWithRegexOperation.SearchPositionInFileWithRegex,
                Path = filePath,
                Regex = regex
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task SearchPositionInFileWithRegex_WithInvalidRegexPattern_ShouldThrowRegexParseException()
        {
            // Arrange
            var filePath = GetTestPath("test.txt");
            await File.WriteAllTextAsync(filePath, "test content");

            var parameters = new SearchPositionInFileWithRegexParameters
            {
                Operation = SearchPositionInFileWithRegexOperation.SearchPositionInFileWithRegex,
                Path = filePath,
                Regex = @"["  // Invalid regex pattern
            };

            // Act & Assert
            await Assert.ThrowsAsync<RegexParseException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task SearchPositionInFileWithRegex_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var filePath = GetTestPath("nonexistent.txt");
            var parameters = new SearchPositionInFileWithRegexParameters
            {
                Operation = SearchPositionInFileWithRegexOperation.SearchPositionInFileWithRegex,
                Path = filePath,
                Regex = @"\w+"
            };

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await _handler.TestHandleAsync(parameters, default)
            );
        }

        [Fact]
        public async Task SearchPositionInFileWithRegex_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = GetTestPath("test.txt");
            var parameters = new SearchPositionInFileWithRegexParameters
            {
                Operation = (SearchPositionInFileWithRegexOperation)999, // Invalid operation
                Path = filePath,
                Regex = @"\w+"
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