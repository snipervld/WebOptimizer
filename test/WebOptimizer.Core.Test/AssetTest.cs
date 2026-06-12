using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using WebOptimizer.Core.Test.Mocks;
using Xunit;

namespace WebOptimizer.Test
{
    public class AssetTest
    {
        [Fact2]
        public void AssetCreate_Success()
        {
            string route = "route";
            string contentType = "text/css";
            var sourcefiles = new[] { "file1.css" };
            var logger = new Mock<ILogger<Asset>>();

            var asset = new Asset(route, contentType, sourcefiles, logger.Object);

            Assert.Equal(route, asset.Route);
            Assert.Equal(contentType, asset.ContentType);
            Assert.Equal(sourcefiles, asset.SourceFiles);
            Assert.Empty(asset.Processors);
        }

        [Fact2]
        public void AssetCreateMultipleSourceFiles_Success()
        {
            string route = "route";
            string contentType = "text/css";
            var sourcefiles = new[] { "file1.css", "file2.css" };
            var logger = new Mock<ILogger<Asset>>();

            var asset = new Asset(route, contentType, sourcefiles, logger.Object);

            Assert.Equal(route, asset.Route);
            Assert.Equal(contentType, asset.ContentType);
            Assert.Equal(sourcefiles, asset.SourceFiles);
            Assert.Empty(asset.Processors);
        }


        [Fact2]
        public void GenerateCacheKey_Success()
        {
            string route = "route";
            string contentType = "text/css";
            var sourcefiles = new[] { "file1.css" };
            var logger = new Mock<ILogger<Asset>>();

            var context = new Mock<HttpContext>().SetupAllProperties();
            var options = new WebOptimizerOptions() { EnableCaching = true };
            var env = new Mock<IWebHostEnvironment>();
            var cache = new Mock<IMemoryCache>();
            var fileProvider = new PhysicalFileProvider(Path.GetTempPath());

            var asset = new Asset(route, contentType, sourcefiles, logger.Object);
            asset.Items.Add("PhysicalFiles", new string[0]);

            StringValues ae = "gzip, deflate";
            context.SetupSequence(c => c.Request.Headers.TryGetValue("Accept-Encoding", out ae))
                   .Returns(false)
                   .Returns(true);

            context.Setup(c => c.RequestServices.GetService(typeof(IWebHostEnvironment)))
                   .Returns(env.Object);

            context.Setup(c => c.RequestServices.GetService(typeof(IMemoryCache)))
                   .Returns(cache.Object);

            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            // Check non-gzip value
            string key = asset.GenerateCacheKey(context.Object, options);
            Assert.Equal("_BZuuBNh_zEXnNPIPaO_4Ii4UdM", key);

            // Check gzip value
            string gzipKey = asset.GenerateCacheKey(context.Object, options);
            Assert.Equal("SvH6WGVAapgMXiPenaOGnKS_oMI", gzipKey);
        }

        [Fact2]
        public void AssetToString()
        {
            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/route", "content/type", [], logger.Object);

            Assert.Equal(asset.Route, asset.ToString());
        }

        /// <summary>
        /// Tests that asset's <see cref="Asset.ExecuteAsync"/> correctly reads all files,
        /// including those in nested directories, when using a glob pattern.
        ///
        /// Also, tests <see cref="GlobbingUrlBuilder"/> from <see cref="Asset.ExpandGlobs"/> and checks
        /// if it works nicely with nested directories.
        /// </summary>
        [Fact2]
        public async Task ExecuteAsync_GlobMatchesNestedDirectories_ReadsAllFiles()
        {
            var date = new DateTime(2017, 1, 1);
            var root =
                MockFileInfo.CreateDirectory("", date,
                [
                    new MockFileInfo("file1.css", date, Encoding.UTF8.GetBytes("body { background-color: red; }")),
                    new MockFileInfo("file2.css", date, Encoding.UTF8.GetBytes("body { background-color: blue; }")),
                    MockFileInfo.CreateDirectory("sub", date,
                    [
                        new MockFileInfo("file3.css", date, Encoding.UTF8.GetBytes("body { background-color: green; }")),
                    ]),
                ]);
            var fileProvider = MockFileProvider.Create(root);

            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/all.css", "text/css", ["**/*.css"], logger.Object);
            asset.Concatenate();

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            var cache = new MemoryCache(new MemoryCacheOptions());

            var context = new Mock<HttpContext>();
            context.SetupAllProperties();
            context.Setup(c => c.RequestServices.GetService(typeof(IWebHostEnvironment)))
                .Returns(env.Object);
            context.Setup(c => c.RequestServices.GetService(typeof(IMemoryCache)))
                .Returns(cache);
            context.Setup(c => c.Response.Headers)
                .Returns(new HeaderDictionary());

            var options = new WebOptimizerOptions();

            byte[] result = await asset.ExecuteAsync(context.Object, options);
            string content = Encoding.UTF8.GetString(result);

            Assert.Contains("background-color: red", content);
            Assert.Contains("background-color: blue", content);
            Assert.Contains("background-color: green", content);
        }

        /// <summary>
        /// Same scenario as <see cref="ExecuteAsync_GlobMatchesNestedDirectories_ReadsAllFiles"/> but
        /// backed by a real <see cref="PhysicalFileProvider"/> over a temporary directory tree
        /// instead of a mocked file provider.
        /// </summary>
        [Fact2]
        public async Task ExecuteAsync_GlobMatchesNestedDirectories_PhysicalFileProvider_ReadsAllFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "WebOptimizerTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(Path.Combine(root, "sub"));
                File.WriteAllText(Path.Combine(root, "file1.css"), "body { background-color: red; }");
                File.WriteAllText(Path.Combine(root, "file2.css"), "body { background-color: blue; }");
                File.WriteAllText(Path.Combine(root, "sub", "file3.css"), "body { background-color: green; }");

                var fileProvider = new PhysicalFileProvider(root);

                var logger = new Mock<ILogger<Asset>>();
                var asset = new Asset("/all.css", "text/css", ["**/*.css"], logger.Object);
                asset.Concatenate();

                var env = new Mock<IWebHostEnvironment>();
                env.Setup(e => e.WebRootFileProvider)
                    .Returns(fileProvider);

                var cache = new MemoryCache(new MemoryCacheOptions());

                var context = new Mock<HttpContext>();
                context.SetupAllProperties();
                context.Setup(c => c.RequestServices.GetService(typeof(IWebHostEnvironment)))
                    .Returns(env.Object);
                context.Setup(c => c.RequestServices.GetService(typeof(IMemoryCache)))
                    .Returns(cache);
                context.Setup(c => c.Response.Headers)
                    .Returns(new HeaderDictionary());

                var options = new WebOptimizerOptions();

                byte[] result = await asset.ExecuteAsync(context.Object, options);
                string content = Encoding.UTF8.GetString(result);

                Assert.Contains("background-color: red", content);
                Assert.Contains("background-color: blue", content);
                Assert.Contains("background-color: green", content);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Fact2]
        public async Task ExecuteAsync_ExplicitSourceFiles_PreservesSourceFilesOrder()
        {
            var date = new DateTime(2017, 1, 1);
            var root = MockFileInfo.CreateDirectory("", date,
            [
                new MockFileInfo("a.css", date, Encoding.UTF8.GetBytes(".a { color: red; }")),
                new MockFileInfo("b.css", date, Encoding.UTF8.GetBytes(".b { color: blue; }")),
            ]);
            var fileProvider = MockFileProvider.Create(root);

            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/all.css", "text/css", ["b.css", "a.css"], logger.Object);
            asset.Concatenate();

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            var cache = new MemoryCache(new MemoryCacheOptions());

            var context = new Mock<HttpContext>();
            context.SetupAllProperties();
            context.Setup(c => c.RequestServices.GetService(typeof(IWebHostEnvironment)))
                .Returns(env.Object);
            context.Setup(c => c.RequestServices.GetService(typeof(IMemoryCache)))
                .Returns(cache);
            context.Setup(c => c.Response.Headers)
                .Returns(new HeaderDictionary());

            var options = new WebOptimizerOptions();

            byte[] result = await asset.ExecuteAsync(context.Object, options);
            string content = Encoding.UTF8.GetString(result);

            int indexB = content.IndexOf(".b {", StringComparison.Ordinal);
            int indexA = content.IndexOf(".a {", StringComparison.Ordinal);
            Assert.True(indexB >= 0 && indexA >= 0, "both files must be bundled");
            Assert.True(indexB < indexA, "b.css must come before a.css, matching SourceFiles order");
        }

        [Fact2]
        public async Task ExecuteAsync_MultipleGlobPatterns_PreservesPatternOrder()
        {
            var date = new DateTime(2017, 1, 1);
            var root = MockFileInfo.CreateDirectory("", date,
            [
                new MockFileInfo("root.css", date, Encoding.UTF8.GetBytes(".root { color: red; }")),
                MockFileInfo.CreateDirectory("sub", date,
                [
                    new MockFileInfo("nested.css", date, Encoding.UTF8.GetBytes(".nested { color: blue; }")),
                ]),
            ]);
            var fileProvider = MockFileProvider.Create(root);

            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/all.css", "text/css", ["sub/*.css", "*.css"], logger.Object);
            asset.Concatenate();

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            var cache = new MemoryCache(new MemoryCacheOptions());

            var context = new Mock<HttpContext>();
            context.SetupAllProperties();
            context.Setup(c => c.RequestServices.GetService(typeof(IWebHostEnvironment)))
                .Returns(env.Object);
            context.Setup(c => c.RequestServices.GetService(typeof(IMemoryCache)))
                .Returns(cache);
            context.Setup(c => c.Response.Headers)
                .Returns(new HeaderDictionary());

            var options = new WebOptimizerOptions();

            byte[] result = await asset.ExecuteAsync(context.Object, options);
            string content = Encoding.UTF8.GetString(result);

            int indexNested = content.IndexOf(".nested {", StringComparison.Ordinal);
            int indexRoot = content.IndexOf(".root {", StringComparison.Ordinal);
            Assert.True(indexNested >= 0 && indexRoot >= 0, "both files must be bundled");
            Assert.True(indexNested < indexRoot, "sub/*.css pattern is listed first, so its file must come first");
        }

        [Fact2]
        public void ExpandGlobs_ExplicitSourceFiles_PreservesSourceFilesOrder()
        {
            var date = new DateTime(2017, 1, 1);
            var root = MockFileInfo.CreateDirectory("", date,
            [
                new MockFileInfo("a.css", date, Encoding.UTF8.GetBytes(".a { color: red; }")),
                new MockFileInfo("b.css", date, Encoding.UTF8.GetBytes(".b { color: blue; }")),
            ]);
            var fileProvider = MockFileProvider.Create(root);

            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/all.css", "text/css", ["b.css", "a.css"], logger.Object);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            var cache = new MemoryCache(new MemoryCacheOptions());

            IEnumerable<string> files = Asset.ExpandGlobs(asset, env.Object, cache);

            Assert.Equal(new[] { "b.css", "a.css" }, files);
        }

        [Fact2]
        public void ExpandGlobs_MultipleGlobPatterns_PreservesPatternOrder()
        {
            var date = new DateTime(2017, 1, 1);
            var root = MockFileInfo.CreateDirectory("", date,
            [
                new MockFileInfo("root.css", date, Encoding.UTF8.GetBytes(".root { color: red; }")),
                MockFileInfo.CreateDirectory("sub", date,
                [
                    new MockFileInfo("nested.css", date, Encoding.UTF8.GetBytes(".nested { color: blue; }")),
                ]),
            ]);
            var fileProvider = MockFileProvider.Create(root);

            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/all.css", "text/css", ["sub/*.css", "*.css"], logger.Object);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            var cache = new MemoryCache(new MemoryCacheOptions());

            IEnumerable<string> files = Asset.ExpandGlobs(asset, env.Object, cache);

            Assert.Equal(new[] { "sub/nested.css", "root.css" }, files);
        }

        /// <summary>
        /// <see cref="Asset.ExpandGlobs"/> maps every <see cref="IAsset.SourceFiles"/> entry to a group of
        /// matched files and keeps those groups in entry order. The order *within* a glob group is an
        /// implementation detail and is not asserted.
        /// </summary>
        [Fact2]
        public void ExpandGlobs_MixedExplicitAndGlobPatterns_KeepsEntryGroupOrder()
        {
            var date = new DateTime(2017, 1, 1);
            var root = MockFileInfo.CreateDirectory("", date,
            [
                new MockFileInfo("alpha.css", date, Encoding.UTF8.GetBytes(".alpha { color: red; }")),
                new MockFileInfo("zeta.css", date, Encoding.UTF8.GetBytes(".zeta { color: blue; }")),
                new MockFileInfo("style-a.css", date, Encoding.UTF8.GetBytes(".style-a { color: #FF0000; }")),
                new MockFileInfo("style-b.css", date, Encoding.UTF8.GetBytes(".style-b { color: #00FF00; }")),
                MockFileInfo.CreateDirectory("sub", date,
                [
                    new MockFileInfo("inner.css", date, Encoding.UTF8.GetBytes(".inner { color: green; }")),
                ]),
            ]);
            var fileProvider = MockFileProvider.Create(root);

            var logger = new Mock<ILogger<Asset>>();
            var asset = new Asset("/all.css", "text/css", ["zeta.css", "sub/*.css", "style-*.css", "alpha.css"], logger.Object);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider)
                .Returns(fileProvider);

            var cache = new MemoryCache(new MemoryCacheOptions());

            List<string> files = [.. Asset.ExpandGlobs(asset, env.Object, cache)];
            // zeta.css, sub/inner.css, (style-a.css, style-b.css - any order), alpha.css

            Assert.Equal(5, files.Count);

            int indexZeta = files.IndexOf("zeta.css");
            int indexInner = files.IndexOf("sub/inner.css");
            int indexStyleA = files.IndexOf("style-a.css");
            int indexStyleB = files.IndexOf("style-b.css");
            int indexAlpha = files.IndexOf("alpha.css");

            Assert.True(indexZeta < indexInner, "zeta.css group precedes sub/*.css group");
            Assert.True(indexInner < indexStyleA && indexInner < indexStyleB,
                "sub/*.css group precedes style-*.css group");
            Assert.True(indexStyleA < indexAlpha && indexStyleB < indexAlpha,
                "style-*.css group precedes alpha.css group");
        }
    }
}