using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Moq;

namespace WebOptimizer.Core.Test.Mocks
{
    // grabbed the logic from https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.TagHelpers/test/GlobbingUrlBuilderTest.cs
    // and made it recursive
    internal static class MockFileProvider
    {
        public static IFileProvider Create(MockFileInfo rootNode)
        {
            if (rootNode.Files == null || !rootNode.Files.Any())
            {
                throw new ArgumentException($"{nameof(rootNode)} must have children.", nameof(rootNode));
            }

            var fileProvider = new Mock<IFileProvider>(MockBehavior.Strict);
            fileProvider.Setup(fp => fp.GetFileInfo(It.IsAny<string>()))
                .Returns((string p) => new NotFoundFileInfo(p));
            SetupDirectoriesFiles(fileProvider, rootNode);
            fileProvider.Setup(fp => fp.Watch(It.IsAny<string>()))
                .Returns(new Mock<IChangeToken>().Object);

            return fileProvider.Object;
        }

        private static void SetupDirectoriesFiles(Mock<IFileProvider> fileProviderMock, MockFileInfo directory)
        {
            var stack = new Stack<(MockFileInfo fileInfo, string directoryPath)>();
            stack.Push((directory, string.Empty));

            while (stack.Count > 0)
            {
                var (fileInfo, directoryPath) = stack.Pop();

                if (fileInfo.IsDirectory)
                {
                    var children = fileInfo.Files;

                    var directoryContents = new Mock<IDirectoryContents>();
                    directoryContents.Setup(dc => dc.Exists).Returns(true);
                    directoryContents.Setup(dc => dc.GetEnumerator())
                        .Returns(() => children.GetEnumerator());
                    directoryContents
                        .As<IEnumerable>()
                        .Setup(dc => dc.GetEnumerator())
                        .Returns(() => children.GetEnumerator());

                    var fullPath = string.IsNullOrEmpty(directoryPath) ? fileInfo.Name : directoryPath + "/" + fileInfo.Name;

                    fileProviderMock.Setup(fp => fp.GetDirectoryContents(fullPath))
                        .Returns(directoryContents.Object);

                    foreach (var child in children)
                    {
                        stack.Push((child, fullPath));
                    }
                }
                else
                {
                    var fullPath = string.IsNullOrEmpty(directoryPath) ? fileInfo.Name : directoryPath + "/" + fileInfo.Name;
                    fileProviderMock.Setup(fp => fp.GetFileInfo(fullPath))
                        .Returns(fileInfo);
                    fileProviderMock.Setup(fp => fp.GetFileInfo("/" + fullPath))
                        .Returns(fileInfo);
                }
            }
        }
    }
}
