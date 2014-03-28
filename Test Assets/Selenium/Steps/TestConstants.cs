using NesTrak.AcceptanceTests.Pages;
using OpenQA.Selenium;
using TechTalk.SpecFlow;
using System.Configuration;

namespace NesTrak.AcceptanceTests.Steps
{
    public static class TestConstants
    {
        private static string TargetBrowser;
        public static string TestUserName { get; set; }
        public static string ServerUrl { get; set; }
        public static string TestDataSource { get; set; }
        public static string TestDataSourcePath { get; set; }
        public static string TestScreenshotPath { get; set; }

        static TestConstants()
        {
            TargetBrowser = ConfigurationManager.AppSettings["TargetBrowser"];
            ServerUrl = ConfigurationManager.AppSettings["ApplicationRootURL"];
            TestUserName = ConfigurationManager.AppSettings["TestUser"];
            TestDataSource = ConfigurationManager.AppSettings["TestDataSource"];
            TestDataSourcePath = ConfigurationManager.AppSettings["TestDataSourcePath"];
            TestScreenshotPath = ConfigurationManager.AppSettings["TestScreenshotPath"];
        }

        private const string WebDriverContextKey = "WEB_DRIVER";
        public static IWebDriver WebDriver
        {
            get
            {
                IWebDriver webDriver = null;
                bool tryGetSuccess = FeatureContext.Current.TryGetValue(WebDriverContextKey, out webDriver);
                if (tryGetSuccess)
                    return webDriver;

                return null;
            }
            set
            {
                if (FeatureContext.Current.ContainsKey(WebDriverContextKey))
                {
                    if (value == null)
                        FeatureContext.Current.Remove(WebDriverContextKey);
                    else
                        FeatureContext.Current.Set(value, WebDriverContextKey);
                }
                else
                {
                    if (value != null)
                        FeatureContext.Current.Add(WebDriverContextKey, value);
                }
            }
        }


        public static void InitializeDriver(IWebDriver webDriver = null)
        {
            if (FeatureContext.Current.ContainsKey(WebDriverContextKey))
                return;

            FeatureContext.Current.Add(WebDriverContextKey, webDriver ?? WebDriverFactory.GetWebDriver(TargetBrowser));
        }

        private static int _pageLoadWaitSeconds = 8;
        public static int PageLoadWaitSeconds
        {
            get { return _pageLoadWaitSeconds; }
            set { _pageLoadWaitSeconds = value; }
        }

        //private const string SERVER_URL_CONTEXT_KEY = "SERVER_URL";

        //public static string ServerUrlForTests
        //{
        //    get
        //    {
        //        string url;
        //        if (FeatureContext.Current.TryGetValue(SERVER_URL_CONTEXT_KEY, out url))
        //            return url;

        //        return null;
        //    }
        //    set
        //    {
        //        if (FeatureContext.Current.ContainsKey(SERVER_URL_CONTEXT_KEY))
        //            FeatureContext.Current.Set<string>(value, SERVER_URL_CONTEXT_KEY);
        //        else
        //            FeatureContext.Current.Add(SERVER_URL_CONTEXT_KEY, value);
        //    }
        //}

        public static void Quit()
        {
            if (WebDriver != null)
            {
                WebDriver.Quit();
                WebDriver = null;
            }
        }

        //public static void StartTest(string applicationUrl)
        public static void StartTest()
        {
            //ServerUrlForTests = applicationUrl;
            InitializeDriver();
        }
    }
}

