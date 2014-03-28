using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace NesTrak.AcceptanceTests.Steps
{
    public static class Extensions
    {
        public static void SetTextForControl(this IWebDriver webDriver, By byObj, string text)
        {
            IWebElement element = WaitFor.ElementPresent(webDriver, byObj);
            element.Clear();
            element.SendKeys(text);
        }

        public static void ClearTextForControl(this IWebDriver webDriver, By byObj, string text)
        {
            IWebElement element = WaitFor.ElementPresent(webDriver, byObj);
            element.Clear();
        }

        public static void SelectYesNoRadioButton(this IWebDriver webDriver, By byObj, bool isSelected)
        {
            WaitFor.ElementPresent(webDriver, byObj);
            ReadOnlyCollection<IWebElement> elements = webDriver.FindElements(byObj);

            if (isSelected)
            {
                elements[0].Click();
            }
            else
            {
                elements[1].Click();
            }
        }

        public static void SelectRadioButton(this IWebDriver webDriver, By byObj, int index)
        {
            WaitFor.ElementPresent(webDriver, byObj);
            ReadOnlyCollection<IWebElement> radioButtons = webDriver.FindElements(byObj);
            radioButtons[index].Click();
        }

        public static void SelectDropDownByText(this IWebDriver webDriver, By byObj, string text)
        {
            SelectElement selectElement = new SelectElement(WaitFor.ElementPresent(webDriver, byObj));
            selectElement.SelectByText(text);
        }

        public static void SelectCheckBox(this IWebDriver webDriver, By byObj, string label)
        {
            IWebElement checkBox = WaitFor.ElementPresent(webDriver, byObj);
            checkBox.Click();
        }

        public static void ClickLink(this IWebDriver webDriver, By byObj)
        {
            IWebElement element = WaitFor.ElementPresent(webDriver, byObj);
            element.Click();
        }

        public static void ClickNextButton(this ISearchContext webDriver)
        {
            var element = webDriver.FindElement(By.ClassName("next"));
            element.Click();
        }

        public static void ClickSubmitButton(this ISearchContext webDriver)
        {
            var element = webDriver.FindElement(By.XPath("//input[@type='submit']"));
            element.Click();
        }

        public static void FindTextOnPage(this ISearchContext webDriver, string textToFind)
        {
            var validationMessageLocator = By.XPath(string.Format("//*[contains(.,'{0}')]", textToFind));
            WaitFor.ElementPresent((IWebDriver)webDriver, validationMessageLocator);
        }

        public static ReadOnlyCollection<IWebElement> FindElementsByText(this IWebDriver webDriver, By byObj)
        {
            return WaitFor.ElementsPresent(webDriver, byObj);
        }

        public static T ExecuteJavaScript<T>(this IWebDriver driver, string script)
        {
            return (T)((IJavaScriptExecutor)driver).ExecuteScript(script);
        }

        // usage var title = driver.Execute<string>("return document.title");
        public static void WaitForPage(this IWebDriver WebDriver, TimeSpan timeSpan)
        {
            if (WebDriver == null)
                throw new Exception("Web driver is null");

            IWait<IWebDriver> wait = new WebDriverWait(WebDriver, timeSpan);

            wait.Until<bool>((d) =>
                                    {
                                        return WebDriver.ExecuteJavaScript<string>("return document.readyState")
                                                         .Equals("complete");
                                    }
                                    );
        }
    }
}
