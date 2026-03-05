using Microsoft.VisualStudio.TestTools.UnitTesting;
using MicroFocus.InsecureWebApp.Models;
using Microsoft.AspNetCore.Html;
using System;

namespace InsecureWebAppTests
{
    [TestClass]
    public class MyHtmlHelpersTests
    {
        [TestMethod]
        public void DivInjectionHTMLString_IncludesProductDescAndLink()
        {
            string desc = "ABCDEFGHIJKLMNOPQRSTUVWXYZ012345";

            IHtmlContent html = MyHTMLHelpers.DivInjectionHTMLString(null, desc);
            string s = html.ToString();

            Assert.IsTrue(s.Contains("xsinject"));
            Assert.IsTrue(s.Contains(desc.Substring(0, 20)));
            Assert.IsTrue(s.Contains("javascript:swal"));
            Assert.IsTrue(s.Contains(desc));
        }
    }
}
