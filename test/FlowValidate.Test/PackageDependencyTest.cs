namespace FlowValidate.Test
{
    public class PackageDependencyTest
    {
        private static string ReadDependencyManifest()
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FlowValidate.Test.deps.json"));
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.Mvc.ApiExplorer/")]
        [InlineData("Microsoft.AspNetCore.Mvc.Core/")]
        [InlineData("Microsoft.Extensions.DependencyModel/")]
        [InlineData("Microsoft.DotNet.PlatformAbstractions/")]
        public void CorePackage_DoesNotPullUnusedMvcPackages(string packagePrefix)
        {
            // Act
            var manifest = ReadDependencyManifest();

            // Assert
            Assert.DoesNotContain(packagePrefix, manifest);
        }
    }
}
