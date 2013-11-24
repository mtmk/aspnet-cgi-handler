using System.IO;
using NUnit.Framework;

namespace CgiHandler.Tests
{
    [TestFixture]
    public class CgiProcessFixture
    {
        [Test]
        public void Returns_response()
        {
            var cgiProcess = new CgiProcess();

            Stream inStream = new MemoryStream();
            
            Stream outStream = cgiProcess.Run(inStream);

            Assert.That(outStream.Length, Is.EqualTo(10));
        }
    }
}
