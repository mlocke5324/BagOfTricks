using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;

namespace NesTrak.AcceptanceTests.Pages
{
    public class WebDriverFactory
    {
        public static IWebDriver GetWebDriver(string key = null)
        {
            IWebDriver webDriver = null;
            if (String.IsNullOrEmpty(key))
                key = "IE";
            switch (key)
            {
                case "Firefox":
                    FirefoxProfile profile = new FirefoxProfile();
                    profile.SetPreference("network.http.phishy-userpass-length", 255);
                    profile.SetPreference("browser.safebrowsing.malware.enabled", false);
                    profile.SetPreference("network.automatic-ntlm-auth.trusted-uris", "halcyonit.com");
                    webDriver = new FirefoxDriver(profile);
                    break;

                case "IE":
                    webDriver = new InternetExplorerDriver();
                    break;

                case "Chrome":
                    webDriver = new ChromeDriver();
                    break;

                default:
                    throw new ApplicationException("No mapping is defined for: " + key);
            }
            webDriver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(5));

            return webDriver;
        }
    }
}
