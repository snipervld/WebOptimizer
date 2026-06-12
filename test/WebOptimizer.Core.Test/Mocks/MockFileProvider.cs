using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace WebOptimizer.Core.Test.Mocks
{
    // grabbed the logic from https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.TagHelpers/test/GlobbingUrlBuilderTest.cs
    // and made it recursive
    internal class MockFileProvider : IFileProvider
    {
        private readonly MockChangeToken _changeToken = new();
        private readonly Dictionary<string, MockFileInfo> _files;
        private readonly Dictionary<string, MockDirectoryContents> _directories;

        private MockFileProvider(Dictionary<string, MockFileInfo> files, Dictionary<string, MockDirectoryContents> directories)
        {
            _files = files;
            _directories = directories;
        }

        public static IFileProvider Create(MockFileInfo rootNode)
        {
            if (rootNode.Files == null || !rootNode.Files.Any())
            {
                throw new ArgumentException($"{nameof(rootNode)} must have children.", nameof(rootNode));
            }

            Dictionary<string, MockFileInfo> files = [];
            Dictionary<string, MockDirectoryContents> directories = [];

            var stack = new Stack<(MockFileInfo fileInfo, string directoryPath)>();
            stack.Push((rootNode, string.Empty));

            while (stack.Count > 0)
            {
                var (fileInfo, directoryPath) = stack.Pop();

                if (fileInfo.IsDirectory)
                {
                    var children = fileInfo.Files;

                    var fullPath = string.IsNullOrEmpty(directoryPath) ? fileInfo.Name : directoryPath + "/" + fileInfo.Name;

                    directories[fullPath] = new MockDirectoryContents(children);

                    foreach (var child in children)
                    {
                        stack.Push((child, fullPath));
                    }
                }
                else
                {
                    var fullPath = string.IsNullOrEmpty(directoryPath) ? fileInfo.Name : directoryPath + "/" + fileInfo.Name;
                    files[fullPath] = fileInfo;
                    files["/" + fullPath] = fileInfo;
                }
            }

            return new MockFileProvider(files, directories);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            return _files.TryGetValue(subpath, out var fileInfo) ? fileInfo : new NotFoundFileInfo(subpath);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
           return _directories.TryGetValue(subpath, out var directoryContents) ? directoryContents : new NotFoundDirectoryContents();
        }

        public IChangeToken Watch(string filter)
        {
            return _changeToken;
        }
    }
}
