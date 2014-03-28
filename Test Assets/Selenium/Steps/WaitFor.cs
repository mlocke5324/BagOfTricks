using System;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace NesTrak.AcceptanceTests.Steps
{
    public static class WaitFor
    {
        private const int WaitSeconds = 5;
        public static IWebElement ElementPresent(IWebDriver browser, By locator, int seconds = WaitSeconds)
        {
            return Wait(browser, locator, TimeSpan.FromSeconds(seconds));
        }

        public static ReadOnlyCollection<IWebElement> ElementsPresent(IWebDriver browser, By locator, int seconds = WaitSeconds)
        {
            return Waits(browser, locator, TimeSpan.FromSeconds(seconds));
        }

        public static IWebElement ElementPresentWithException(IWebDriver browser, By locator, int seconds = WaitSeconds)
        {
            return WaitWithException(browser, locator, TimeSpan.FromSeconds(seconds));
        }

        private static IWebElement Wait(IWebDriver WebDriver, By byObj, TimeSpan timeSpan)
        {
            IWebElement element;
            IWait<IWebDriver> wait = new WebDriverWait(WebDriver, timeSpan);
            try
            {
                element = wait.Until(driver => driver.FindElement(byObj));
            }
            catch (Exception ex)
            {
                element = null;
            }
            return element;
        }

        private static ReadOnlyCollection<IWebElement> Waits(IWebDriver WebDriver, By byObj, TimeSpan timeSpan)
        {
            IWait<IWebDriver> wait = new WebDriverWait(WebDriver, timeSpan);
            return wait.Until(driver => driver.FindElements(byObj));
        }


        private static IWebElement WaitWithException(IWebDriver WebDriver, By byObj, TimeSpan timeSpan)
        {
            try
            {
                IWait<IWebDriver> wait = new WebDriverWait(WebDriver, timeSpan);
                return wait.Until(driver => driver.FindElement(byObj));
            }
            catch (NoSuchElementException)
            {
                throw new AssertFailedException(String.Format("Element [{0}] not found on page", byObj));
            }
        }
    }
}
