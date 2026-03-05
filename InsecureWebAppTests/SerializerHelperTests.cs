using Microsoft.VisualStudio.TestTools.UnitTesting;
using MicroFocus.InsecureWebApp.Utils;

namespace InsecureWebAppTests
{
    [TestClass]
    public class SerializerHelperTests
    {
        [TestMethod]
        public void Serialize_ReturnsExpectedJson()
        {
            var obj = new { Name = "Alice", Age = 30 };
            string json = SerializerHelper.Serialize(obj);

            Assert.IsTrue(json.Contains("\"Name\":\"Alice\""));
            Assert.IsTrue(json.Contains("\"Age\":30"));
        }

        [TestMethod]
        public void Deserialize_ReturnsObject_ForValidJson()
        {
            string json = "{\"x\":1}";
            var o = SerializerHelper.Deserialize(json);

            Assert.IsNotNull(o);
        }
    }
}
