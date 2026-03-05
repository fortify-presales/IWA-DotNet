using Microsoft.VisualStudio.TestTools.UnitTesting;
using MicroFocus.InsecureWebApp.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace InsecureWebAppTests
{
    [TestClass]
    public class DivTagHelperTests
    {
        [TestMethod]
        public async Task ProcessAsync_SetsTagAndContent_WhenIsAdded()
        {
            var tagHelper = new DivTagHelper { IsAdded = true };

            var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
            var output = new TagHelperOutput("div", new TagHelperAttributeList(), (useCached, encoder) =>
            {
                var content = new DefaultTagHelperContent();
                return Task.FromResult<TagHelperContent>(content);
            });

            await tagHelper.ProcessAsync(context, output);

            Assert.AreEqual("Div", output.TagName);
            Assert.AreEqual("Great work", output.Content.GetContent());
        }
    }
}
