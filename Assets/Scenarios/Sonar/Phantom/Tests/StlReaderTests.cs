using System.IO;
using NUnit.Framework;
using Sonar.Phantom;
using UnityEngine;

namespace Sonar.Phantom.Tests
{
    public class StlReaderTests
    {
        const string AsciiTet = @"solid tet
facet normal 0 0 0
  outer loop
    vertex 0 0 0
    vertex 1 0 0
    vertex 0 1 0
  endloop
endfacet
facet normal 0 0 0
  outer loop
    vertex 0 0 0
    vertex 0 0 1
    vertex 1 0 0
  endloop
endfacet
endsolid";

        [Test]
        public void Ascii_ParsesVerticesAndTriangles()
        {
            var path = Path.Combine(Path.GetTempPath(), "test_tet.stl");
            File.WriteAllText(path, AsciiTet);
            try
            {
                var mesh = StlReader.Read(path);
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.EqualTo(6));
                Assert.That(mesh.triangles.Length, Is.EqualTo(6));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void MissingFile_ReturnsNull()
        {
            var mesh = StlReader.Read("/tmp/__definitely_missing_sonar.stl");
            Assert.That(mesh, Is.Null);
        }
    }
}
