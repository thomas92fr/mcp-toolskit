using mcp_toolskit.Handlers.DotNet;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using Moq;
using System.Text.RegularExpressions;

namespace mcp_toolskit_tests.TestHandlers.DotNet
{
    public class TestDotNetToolHandler
    {
        private readonly Mock<IServerContext> _mockServerContext;
        private readonly Mock<ISessionContext> _mockSessionContext;
        private readonly Mock<ILogger<DotNetToolHandler>> _mockLogger;
        private readonly TestAppConfig _appConfig;
        private readonly DotNetToolHandler _handler;
        private readonly string _testBasePath = Path.Combine(Path.GetTempPath(), "mcp-toolskit-tests", "dotnet");

        public class TestAppConfig : AppConfig
        {
            public override string ValidatePath(string path)
            {
                // Pour les tests, on retourne simplement le chemin complet
                return Path.GetFullPath(path);
            }
        }

        public TestDotNetToolHandler()
        {
            // Arrange - Setup mocks
            _mockServerContext = new Mock<IServerContext>();
            _mockSessionContext = new Mock<ISessionContext>();
            _mockLogger = new Mock<ILogger<DotNetToolHandler>>();

            // Setup AppConfig
            _appConfig = new TestAppConfig();

            // Create handler instance
            _handler = new DotNetToolHandler(
                _mockServerContext.Object,
                _mockSessionContext.Object,
                _mockLogger.Object,
                _appConfig
            );

            // Ensure test directory exists
            Directory.CreateDirectory(_testBasePath);
        }

        [Fact]
        public async Task RunTests_WithValidSolutionFile_ShouldReturnTestResults()
        {
            // Arrange
            // Créer un fichier solution temporaire pour les tests
            var tempSolutionFile = Path.Combine(_testBasePath, "TestSolution.sln");
            
            File.WriteAllText(tempSolutionFile, "Mock Solution File");

            var parameters = new DotNetParameters
            {
                Operation = DotNetOperation.RunTests,
                SolutionFile = tempSolutionFile
            };

            try 
            {
                // Act
                var result = await InvokeHandleAsync(parameters);

                // Assert
                Assert.NotNull(result);
                Assert.NotNull(result.Content);
                Assert.True(result.Content.Length > 0);

                var textContent = Assert.IsType<TextContent>(result.Content[0]);
                Assert.NotNull(textContent.Text);

                // Vérifier que les logs sont générés
                _mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Information, 
                        It.IsAny<EventId>(), 
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Running dotnet command")),
                        null, 
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()
                    ), 
                    Times.AtLeastOnce()
                );
            }
            finally 
            {
                // Cleanup
                if (File.Exists(tempSolutionFile))
                    File.Delete(tempSolutionFile);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task RunTests_WithInvalidSolutionFile_ShouldThrowArgumentException(string invalidPath)
        {
            // Arrange
            var parameters = new DotNetParameters
            {
                Operation = DotNetOperation.RunTests,
                SolutionFile = invalidPath
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await InvokeHandleAsync(parameters)
            );
        }

        [Fact]
        public async Task RunTests_WithNonExistentPath_ShouldThrowDirectoryNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testBasePath, "nonexistent", "Solution.sln");
            var parameters = new DotNetParameters
            {
                Operation = DotNetOperation.RunTests,
                SolutionFile = nonExistentPath
            };

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                async () => await InvokeHandleAsync(parameters)
            );
        }

        [Fact]
        public async Task RunTests_WithInvalidOperation_ShouldThrowArgumentException()
        {
            // Arrange
            var parameters = new DotNetParameters
            {
                Operation = (DotNetOperation)999, // Invalid operation
                SolutionFile = Path.Combine(_testBasePath, "TestSolution.sln")
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await InvokeHandleAsync(parameters)
            );
        }

        // Méthode d'aide pour invoquer la méthode protégée HandleAsync
        private async Task<CallToolResult> InvokeHandleAsync(DotNetParameters parameters)
        {
            // Utilisation de la réflexion pour appeler une méthode privée
            var method = typeof(DotNetToolHandler)
                .GetMethod("HandleAsync", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);

            if (method == null)
                throw new InvalidOperationException("Method HandleAsync not found");

            return await (Task<CallToolResult>)method.Invoke(_handler, new object[] { parameters, CancellationToken.None });
        }

        public void Dispose()
        {
            // Cleanup test directory if it exists
            if (Directory.Exists(_testBasePath))
                Directory.Delete(_testBasePath, true);
        }
    }
}