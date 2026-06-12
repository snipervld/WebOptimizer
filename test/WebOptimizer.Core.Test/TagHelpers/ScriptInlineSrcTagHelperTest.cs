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
    public class ScriptInlineSrcTagHelperTest
    {
        /// <summary>
        /// Tests that <see cref="ScriptInlineSrcTagHelper"/> inlines the file content read through
        /// <see cref="IFileInfo.CreateReadStream"/>, even when the file provider exposes no
        /// <see cref="IFileInfo.PhysicalPath"/> (e.g. embedded or composite providers).
        /// </summary>
        [Fact2]
        public async Task InlineSrc_FileProviderWithoutPhysicalPath_InlinesContent()
        {
            var date = new DateTime(2017, 1, 1);
            var content = "console.log('hello');";
            var root =
                MockFileInfo.CreateDirectory("", date,
                [
                    new MockFileInfo("test.js", date, Encoding.UTF8.GetBytes(content)),
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
            var optionsMonitor = new Mock<IOptionsMonitor<WebOptimizerOptions>>();
            optionsMonitor.Setup(x => x.CurrentValue).Returns(options);

            var route = "/test.js";
            IAsset asset;
            var assetPipeline = new Mock<IAssetPipeline>();
            assetPipeline.Setup(ap => ap.TryGetAssetFromRoute(It.IsAny<string>(), out asset)).Returns(false);

            var scriptTagHelper = new ScriptInlineSrcTagHelper(env.Object, cache.Object, assetPipeline.Object, optionsMonitor.Object, new Mock<IAssetBuilder>().Object);
            var viewContext = new ViewContext
            {
                HttpContext = new Mock<HttpContext>().SetupAllProperties().Object
            };
            scriptTagHelper.ViewContext = viewContext;
            scriptTagHelper.Src = route;

            var tagHelperContext = new Mock<TagHelperContext>(
                "script",
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                "unique");
            var attributes = new TagHelperAttributeList { new TagHelperAttribute("src", route) };

            var tagHelperOutput = new TagHelperOutput("script", attributes, (_, _) => Task.Factory.StartNew<TagHelperContent>(
                () => new DefaultTagHelperContent()));
            await scriptTagHelper.ProcessAsync(tagHelperContext.Object, tagHelperOutput);

            Assert.Equal(TagMode.StartTagAndEndTag, tagHelperOutput.TagMode);
            Assert.Equal(content, tagHelperOutput.Content.GetContent());

            Mock.VerifyAll(env, cache, assetPipeline);
        }

        /// <summary>
        /// Same scenario as <see cref="InlineSrc_FileProviderWithoutPhysicalPath_InlinesContent"/> but
        /// backed by a real <see cref="PhysicalFileProvider"/> over a temporary directory tree
        /// instead of a mocked file provider.
        /// </summary>
        [Fact2]
        public async Task InlineSrc_PhysicalFileProvider_InlinesContent()
        {
            var content = "console.log('hello');";
            string rootPath = Path.Combine(Path.GetTempPath(), "WebOptimizerTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(Path.Combine(rootPath, "test.js"), content);

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
                var optionsMonitor = new Mock<IOptionsMonitor<WebOptimizerOptions>>();
                optionsMonitor.Setup(x => x.CurrentValue).Returns(options);

                var route = "/test.js";
                IAsset asset;
                var assetPipeline = new Mock<IAssetPipeline>();
                assetPipeline.Setup(ap => ap.TryGetAssetFromRoute(It.IsAny<string>(), out asset)).Returns(false);

                var scriptTagHelper = new ScriptInlineSrcTagHelper(env.Object, cache.Object, assetPipeline.Object, optionsMonitor.Object, new Mock<IAssetBuilder>().Object);
                var viewContext = new ViewContext
                {
                    HttpContext = new Mock<HttpContext>().SetupAllProperties().Object
                };
                scriptTagHelper.ViewContext = viewContext;
                scriptTagHelper.Src = route;

                var tagHelperContext = new Mock<TagHelperContext>(
                    "script",
                    new TagHelperAttributeList(),
                    new Dictionary<object, object>(),
                    "unique");
                var attributes = new TagHelperAttributeList { new TagHelperAttribute("src", route) };

                var tagHelperOutput = new TagHelperOutput("script", attributes, (_, _) => Task.Factory.StartNew<TagHelperContent>(
                    () => new DefaultTagHelperContent()));
                await scriptTagHelper.ProcessAsync(tagHelperContext.Object, tagHelperOutput);

                Assert.Equal(TagMode.StartTagAndEndTag, tagHelperOutput.TagMode);
                Assert.Equal(content, tagHelperOutput.Content.GetContent());

                Mock.VerifyAll(cache, assetPipeline, env);
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
        /// Tests that <see cref="ScriptInlineSrcTagHelper"/> throws a <see cref="FileNotFoundException"/>
        /// when the referenced file does not exist in the file provider.
        /// </summary>
        [Fact2]
        public async Task InlineSrc_FileDoesNotExist_ThrowsFileNotFound()
        {
            var date = new DateTime(2017, 1, 1);
            var root =
                MockFileInfo.CreateDirectory("", date,
                [
                    new MockFileInfo("test.js", date, Encoding.UTF8.GetBytes("var x = 1;")),
                ]);
            var fileProvider = MockFileProvider.Create(root);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.WebRootFileProvider).Returns(fileProvider);

            var options = new WebOptimizerOptions();
            var optionsMonitor = new Mock<IOptionsMonitor<WebOptimizerOptions>>();
            optionsMonitor.Setup(x => x.CurrentValue).Returns(options);

            var route = "/missing.js";
            IAsset asset;
            var assetPipeline = new Mock<IAssetPipeline>();
            assetPipeline.Setup(ap => ap.TryGetAssetFromRoute(It.IsAny<string>(), out asset)).Returns(false);

            var scriptTagHelper = new ScriptInlineSrcTagHelper(env.Object, new Mock<IMemoryCache>().Object, assetPipeline.Object, optionsMonitor.Object, new Mock<IAssetBuilder>().Object);
            var viewContext = new ViewContext
            {
                HttpContext = new Mock<HttpContext>().SetupAllProperties().Object
            };
            scriptTagHelper.ViewContext = viewContext;
            scriptTagHelper.Src = route;

            var tagHelperContext = new Mock<TagHelperContext>(
                "script",
                new TagHelperAttributeList(),
                new Dictionary<object, object>(),
                "unique");
            var attributes = new TagHelperAttributeList { new TagHelperAttribute("src", route) };

            var tagHelperOutput = new TagHelperOutput("script", attributes, (_, _) => Task.Factory.StartNew<TagHelperContent>(
                () => new DefaultTagHelperContent()));

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => scriptTagHelper.ProcessAsync(tagHelperContext.Object, tagHelperOutput));

            Mock.VerifyAll(env, assetPipeline);
        }
    }
}
