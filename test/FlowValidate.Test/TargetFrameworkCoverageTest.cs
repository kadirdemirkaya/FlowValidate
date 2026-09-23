using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace FlowValidate.Test
{
    public class TargetFrameworkCoverageTest
    {
        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FlowValidate.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);

            return directory!.FullName;
        }

        private static string[] ReadTargetFrameworks(string relativeProjectPath)
        {
            var projectPath = Path.Combine(FindRepositoryRoot(), relativeProjectPath);
            var project = File.ReadAllText(projectPath);
            var match = Regex.Match(project, @"<TargetFrameworks?>([^<]+)</TargetFrameworks?>");

            Assert.True(match.Success, $"No target framework declaration found in {relativeProjectPath}.");

            return match.Groups[1].Value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        private static string ShortTargetFrameworkOf(Assembly assembly)
        {
            var frameworkName = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

            Assert.NotNull(frameworkName);

            var version = new FrameworkName(frameworkName!).Version;

            return $"net{version.Major}.{version.Minor}";
        }

        [Fact]
        public void TestProject_CoversEveryTargetFrameworkOfTheCorePackage()
        {
            // Act
            var packageFrameworks = ReadTargetFrameworks(Path.Combine("src", "FlowValidate", "FlowValidate.csproj"));
            var testFrameworks = ReadTargetFrameworks(Path.Combine("test", "FlowValidate.Test", "FlowValidate.Test.csproj"));

            // Assert
            var uncovered = packageFrameworks.Except(testFrameworks).ToArray();
            Assert.True(
                uncovered.Length == 0,
                $"The package targets {string.Join(", ", uncovered)} but the test project does not run there.");
        }

        [Fact]
        public void TestProject_CoversEveryTargetFrameworkOfTheAspNetCorePackage()
        {
            // Act
            var packageFrameworks = ReadTargetFrameworks(Path.Combine("src", "FlowValidate.AspNetCore", "FlowValidate.AspNetCore.csproj"));
            var testFrameworks = ReadTargetFrameworks(Path.Combine("test", "FlowValidate.Test", "FlowValidate.Test.csproj"));

            // Assert
            var uncovered = packageFrameworks.Except(testFrameworks).ToArray();
            Assert.True(
                uncovered.Length == 0,
                $"The package targets {string.Join(", ", uncovered)} but the test project does not run there.");
        }

        [Fact]
        public void LoadedCoreAssembly_IsTheBuildForTheRunningTargetFramework()
        {
            // Act
            var testFramework = ShortTargetFrameworkOf(typeof(TargetFrameworkCoverageTest).Assembly);
            var coreFramework = ShortTargetFrameworkOf(typeof(BaseValidator<>).Assembly);

            // Assert
            Assert.Equal(testFramework, coreFramework);
        }

        [Fact]
        public void RunningTargetFramework_IsDeclaredByTheTestProject()
        {
            // Act
            var testFrameworks = ReadTargetFrameworks(Path.Combine("test", "FlowValidate.Test", "FlowValidate.Test.csproj"));
            var runningFramework = ShortTargetFrameworkOf(typeof(TargetFrameworkCoverageTest).Assembly);

            // Assert
            Assert.Contains(runningFramework, testFrameworks);
        }
    }
}
