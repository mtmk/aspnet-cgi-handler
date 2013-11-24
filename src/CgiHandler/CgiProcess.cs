using System;
using System.IO;

namespace CgiHandler
{
    public class CgiProcess
    {
        public Stream Run(Stream inStream)
        {
            return new MemoryStream(new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9});
        }
    }
}