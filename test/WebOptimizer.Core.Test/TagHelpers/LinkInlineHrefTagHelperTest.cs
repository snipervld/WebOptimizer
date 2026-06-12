using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Moq;
using WebOptimizer.Core.Test.Mocks;
using WebOptimizer.Taghelpers;
using Xunit;

namespace WebOptimizer.Core.Test.TagHelpers
{
    public class LinkInlineHrefTagHelperTest
    {
        /// <summary>
        /// Tests that <see cref="LinkInlineHrefTagHelper"/> inlines the file content read through
        /// <see cref="IFileInfo.CreateReadStream"/>, even when the file provider exposes no
        /// <see cref="IFileInfo.PhysicalPath"/> (e.g. embedded or composite providers).
        /// </summary>
        [Fact2]
        public async Task InlineHref_FileProviderWithoutPhysicalPath_InlinesContent()
        {
            var date = new DateTime(2017, 1, 1);
            var content = "body { background-color: red; }";
            var root =
                MockFileInfo.CreateDirectory("", date,
                [
                    new MockFileInfo("test.css", date, Encoding.UTF8.GetBytes(content)),
                ]);
            var fileProvider = MockFileProvider.Create(root);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider).Returns(fileProvider);
            var cache = new Mock<IMemoryCache>();
            cache.Setup(c => c.CreateEntry(It.IsAny<object>()))
                .Returns((object key) =>
                {
                    var cacheEntry = new Mock<ICacheEntry>();
                    cacheEntry.Setup(ce => ce.ExpirationTokens).Returns([]);

                    return cacheEntry.Object;
                });

            var options = new WebOptimizerOptions();
            var optionsFactory = new Mock<IOptionsFactory<WebOptimizerOptions>>();
            optionsFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(options);

            var sources = new List<IOptionsChangeTokenSource<WebOptimizerOptions>>();
            var optionsMonitorCache = new Mock<IOptionsMonitorCache<WebOptimizerOptions>>();

            var optionsMonitor = new Mock<OptionsMonitor<WebOptimizerOptions>>(optionsFactory.Object, sources, optionsMonitorCache.Object);
            optionsMonitor.Setup(x => x.Get(It.IsAny<string>())).Returns(options);

            var route = "/test.css";
            IAsset asset;
            var assetPipeline = new Mock<IAssetPipeline>();
            assetPipeline.Setup(ap => ap.TryGetAssetFromRoute(It.IsAny<string>(), out asset)).Returns(false);

            var linkTagHelper = new LinkInlineHrefTagHelper(env.Object, cache.Object, assetPipeline.Object, optionsMonitor.Object, new Mock<IAssetBuilder>().Object);
            var viewContext = new ViewContext
            {
                HttpContext = new Mock<HttpContext>().SetupAllProperties().Object
            };
            linkTagHelper.ViewContext = viewContext;
            linkTagHelper.Href = route;

            var tagHelperContext = new Mock<TagHelperContext>(
                "link",
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                "unique");
            var attributes = new TagHelperAttributeList { new TagHelperAttribute("href", route) };

            var tagHelperOutput = new TagHelperOutput("link", attributes, (_, _) => Task.Factory.StartNew<TagHelperContent>(
                () => new DefaultTagHelperContent()));
            await linkTagHelper.ProcessAsync(tagHelperContext.Object, tagHelperOutput);

            Assert.Equal("style", tagHelperOutput.TagName);
            Assert.Equal(TagMode.StartTagAndEndTag, tagHelperOutput.TagMode);
            Assert.Equal(content, tagHelperOutput.Content.GetContent());
        }

        /// <summary>
        /// Same scenario as <see cref="InlineHref_FileProviderWithoutPhysicalPath_InlinesContent"/> but
        /// backed by a real <see cref="PhysicalFileProvider"/> over a temporary directory tree
        /// instead of a mocked file provider.
        /// </summary>
        [Fact2]
        public async Task InlineHref_PhysicalFileProvider_InlinesContent()
        {
            var content = "body { background-color: red; }";
            string rootPath = Path.Combine(Path.GetTempPath(), "WebOptimizerTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "test.css"), content);

                var fileProvider = new PhysicalFileProvider(rootPath);

                var env = new Mock<IWebHostEnvironment>();
                env.Setup(e => e.WebRootFileProvider).Returns(fileProvider);
                var cache = new Mock<IMemoryCache>();
                cache.Setup(c => c.CreateEntry(It.IsAny<object>()))
                    .Returns((object key) =>
                    {
                        var cacheEntry = new Mock<ICacheEntry>();
                        cacheEntry.Setup(ce => ce.ExpirationTokens).Returns([]);

                        return cacheEntry.Object;
                    });

                var options = new WebOptimizerOptions();
                var optionsFactory = new Mock<IOptionsFactory<WebOptimizerOptions>>();
                optionsFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(options);

                var sources = new List<IOptionsChangeTokenSource<WebOptimizerOptions>>();
                var optionsMonitorCache = new Mock<IOptionsMonitorCache<WebOptimizerOptions>>();

                var optionsMonitor = new Mock<OptionsMonitor<WebOptimizerOptions>>(optionsFactory.Object, sources, optionsMonitorCache.Object);
                optionsMonitor.Setup(x => x.Get(It.IsAny<string>())).Returns(options);

                var route = "/test.css";
                IAsset asset;
                var assetPipeline = new Mock<IAssetPipeline>();
                assetPipeline.Setup(ap => ap.TryGetAssetFromRoute(It.IsAny<string>(), out asset)).Returns(false);

                var linkTagHelper = new LinkInlineHrefTagHelper(env.Object, cache.Object, assetPipeline.Object, optionsMonitor.Object, new Mock<IAssetBuilder>().Object);
                var viewContext = new ViewContext
                {
                    HttpContext = new Mock<HttpContext>().SetupAllProperties().Object
                };
                linkTagHelper.ViewContext = viewContext;
                linkTagHelper.Href = route;

                var tagHelperContext = new Mock<TagHelperContext>(
                    "link",
                    new TagHelperAttributeList(),
                    new Dictionary<object, object>(),
                    "unique");
                var attributes = new TagHelperAttributeList { new TagHelperAttribute("href", route) };

                var tagHelperOutput = new TagHelperOutput("link", attributes, (_, _) => Task.Factory.StartNew<TagHelperContent>(
                    () => new DefaultTagHelperContent()));
                await linkTagHelper.ProcessAsync(tagHelperContext.Object, tagHelperOutput);

                Assert.Equal("style", tagHelperOutput.TagName);
                Assert.Equal(TagMode.StartTagAndEndTag, tagHelperOutput.TagMode);
                Assert.Equal(content, tagHelperOutput.Content.GetContent());
            }
            finally
            {
                if (Directory.Exists(rootPath))
                {
                    Directory.Delete(rootPath, recursive: true);
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="LinkInlineHrefTagHelper"/> throws a <see cref="FileNotFoundException"/>
        /// when the referenced file does not exist in the file provider.
        /// </summary>
        [Fact2]
        public async Task InlineHref_FileDoesNotExist_ThrowsFileNotFound()
        {
            var date = new DateTime(2017, 1, 1);
            var root =
                MockFileInfo.CreateDirectory("", date,
                [
                    new MockFileInfo("test.css", date, Encoding.UTF8.GetBytes("body { }")),
                ]);
            var fileProvider = MockFileProvider.Create(root);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider).Returns(fileProvider);

            var options = new WebOptimizerOptions();
            var optionsFactory = new Mock<IOptionsFactory<WebOptimizerOptions>>();
            optionsFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(options);

            var sources = new List<IOptionsChangeTokenSource<WebOptimizerOptions>>();
            var optionsMonitorCache = new Mock<IOptionsMonitorCache<WebOptimizerOptions>>();

            var optionsMonitor = new Mock<OptionsMonitor<WebOptimizerOptions>>(optionsFactory.Object, sources, optionsMonitorCache.Object);
            optionsMonitor.Setup(x => x.Get(It.IsAny<string>())).Returns(options);

            var route = "/missing.css";
            IAsset asset;
            var assetPipeline = new Mock<IAssetPipeline>();
            assetPipeline.Setup(ap => ap.TryGetAssetFromRoute(It.IsAny<string>(), out asset)).Returns(false);

            var linkTagHelper = new LinkInlineHrefTagHelper(env.Object, new Mock<IMemoryCache>().Object, assetPipeline.Object, optionsMonitor.Object, new Mock<IAssetBuilder>().Object);
            var viewContext = new ViewContext
            {
                HttpContext = new Mock<HttpContext>().SetupAllProperties().Object
            };
            linkTagHelper.ViewContext = viewContext;
            linkTagHelper.Href = route;

            var tagHelperContext = new Mock<TagHelperContext>(
                "link",
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                "unique");
            var attributes = new TagHelperAttributeList { new TagHelperAttribute("href", route) };

            var tagHelperOutput = new TagHelperOutput("link", attributes, (_, _) => Task.Factory.StartNew<TagHelperContent>(
                () => new DefaultTagHelperContent()));

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => linkTagHelper.ProcessAsync(tagHelperContext.Object, tagHelperOutput));
        }
    }
}
