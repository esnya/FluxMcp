using System.Text.Json;
using Moq;
using FluxMcp.Tools;
using FrooxEngine;
using Elements.Core;

namespace FluxMcp.Tests;

[TestClass]
    public class NodeSerializationTests
    {
        [TestMethod]
        public void IWorldElementSerializationWritesRefIdNameType()
        {
            var mockElement = new Mock<IWorldElement>();
            mockElement.SetupGet(x => x.ReferenceID).Returns(new RefID(0x12345));
            mockElement.SetupGet(x => x.Name).Returns("Dummy");
            mockElement.SetupGet(x => x.Parent).Returns(default(IWorldElement)!);

            var options = new JsonSerializerOptions();
            NodeSerialization.RegisterConverters(options);

            string json = JsonSerializer.Serialize(mockElement.Object, options);

            Assert.IsTrue(json.Contains($"\"refId\":\"ID12345\""));
            Assert.IsTrue(json.Contains("\"name\":\"Dummy\""));
            Assert.IsTrue(json.Contains("\"type\""));
        }

        [TestMethod]
        public void Float3SerializationWritesXYZ()
        {
            var value = new float3(1.1f, 2.2f, 3.3f);
            var options = new JsonSerializerOptions();
            NodeSerialization.RegisterConverters(options);
            string json = JsonSerializer.Serialize(value, options);
            Assert.IsTrue(json.Contains("\"x\":1."));
            Assert.IsTrue(json.Contains("\"y\":2."));
            Assert.IsTrue(json.Contains("\"z\":3."));
        }
    }
