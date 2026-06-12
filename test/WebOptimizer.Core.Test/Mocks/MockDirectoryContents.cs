using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;

namespace WebOptimizer.Core.Test.Mocks
{
    public class MockDirectoryContents : IDirectoryContents
    {
        private readonly IEnumerable<IFileInfo> _files;

        public MockDirectoryContents(IEnumerable<IFileInfo> files)
        {
            _files = files;
        }

        public bool Exists => true;

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            return _files.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _files.GetEnumerator();
        }
    }
}
