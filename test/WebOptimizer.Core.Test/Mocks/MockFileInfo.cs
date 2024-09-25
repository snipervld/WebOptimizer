using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;

namespace WebOptimizer.Core.Test.Mocks
{
    internal class MockFileInfo : IFileInfo
    {
        private readonly byte[] _data;

        public MockFileInfo(string fileName, DateTimeOffset lastModified, byte[] data)
        {
            _data = data;
            Name = fileName;
            LastModified = lastModified;
        }

        private MockFileInfo(string directoryName, DateTimeOffset lastModified, IList<MockFileInfo> files = null)
        {
            _data = null;
            Name = directoryName;
            LastModified = lastModified;
            IsDirectory = true;
            Files = files;
        }

        /// <summary>
        /// Creates a new instance of <see cref="MockFileInfo"/> representing a directory.
        /// </summary>
        public static MockFileInfo CreateDirectory(string directoryName, DateTimeOffset lastModified, IList<MockFileInfo> files)
        {
            return new MockFileInfo(directoryName, lastModified, files);
        }

        public Stream CreateReadStream()
        {
            return new MemoryStream(_data, false);
        }

        public bool Exists => true;
        public bool IsDirectory { get; }
        public DateTimeOffset LastModified { get; }
        public long Length => _data.Length;
        public string Name { get; }
        public string PhysicalPath => null;
        public IList<MockFileInfo> Files {  get; }
    }

}
