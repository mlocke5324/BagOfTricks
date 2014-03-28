using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using LinqToExcel;
using NesTrak.AcceptanceTests.Steps;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.PageObjects;
using OpenQA.Selenium.Support.UI;
using Remotion.Data.Linq.Utilities;

namespace NesTrak.AcceptanceTests.Pages
{
    public enum ControlType
    {
        Integer, Float, Currency, Text, MultiLineText, CheckBox,
        DateTime, DatePicker, Select, Select2, Select2NoSearch,
        FileBrowser, Link, Button, Table, Row, Cell
    }

    public enum FindBy
    {
        ID, CSS, TEXT, TITLE
    }

    public class TestPageField
    {
        public string PageName { get; set; }
        public string Name { get; set; }
        public string FindBy { get; set; }
        public string Using { get; set; }
        public string Type { get; set; }
        public bool Signature { get; set; }
        public bool Expected { get; set; }
    }

    public class FormField
    {
        public IWebElement Field { get; set; }
        public ControlType ControlType { get; set; }
        public string CssLocator { get; set; }

        public FormField(IWebElement field, ControlType type, string cssLocator = null)
        {
            Field = field;
            ControlType = type;
            CssLocator = cssLocator;
        }

        public FormField()
        {
            Field = null;
            ControlType = 0;
            CssLocator = null;
        }
    }

    public class TestPage
    {
        #region Class Variables & Properties

        public IWebDriver WebDriver { get; set; }
        public string PageName { get; set; }
        public string ServerUrl { get; set; }
        public string PageUrl { get; set; }
        public List<TestPageField> TestPageFields { get; set; }

        public static Hashtable fieldTypeHash = new Hashtable()
                                                {
                                                    {"BUTTON",ControlType.Button},
                                                    {"CELL",ControlType.Cell},
                                                    {"CHECKBOX",ControlType.CheckBox},
                                                    {"CURRENCY",ControlType.Currency},
                                                    {"DATEPICKER",ControlType.DatePicker},
                                                    {"DATETIME",ControlType.DateTime},
                                                    {"FILEBROWSER",ControlType.FileBrowser},
                                                    {"FLOAT",ControlType.Float},
                                                    {"INTEGER",ControlType.Integer},
                                                    {"LINK",ControlType.Link},
                                                    {"MULTILINETEXT",ControlType.MultiLineText},
                                                    {"ROW",ControlType.Row},
                                                    {"SELECT2", ControlType.Select2},
                                                    {"SELECT", ControlType.Select},
                                                    {"SELECT2NOSEARCH", ControlType.Select2NoSearch},
                                                    {"TABLE",ControlType.Table},
                                                    {"TEXT",ControlType.Text}
                                                };

        public int PageLoadWaitSeconds { get; set; }
        public string FullUrl
        {
            get { return TestConstants.ServerUrl + PageUrl; }
        }


        public string CurrentPageTitle()
        {
            return WebDriver.Title;
        }

        #endregion

        #region Initializers & Supporting Methods

        public TestPage(string pageName)
        {
            InitPage();
            PageName = pageName;
            InitFormFieldList();
        }

        public TestPage()
        {
            InitPage();
        }

        public void InitPage()
        {
            WebDriver = TestConstants.WebDriver;
            ServerUrl = TestConstants.ServerUrl;
            PageLoadWaitSeconds = TestConstants.PageLoadWaitSeconds;
        }

        public void InitFormFieldList()
        {
            ExcelQueryFactory excel =
                new ExcelQueryFactory(TestConstants.TestDataSourcePath + TestConstants.TestDataSource);

            excel.AddMapping<TestPageField>(x => x.PageName, "Page Name");
            excel.AddMapping<TestPageField>(x => x.Name, "Field Name");
            excel.AddMapping<TestPageField>(x => x.FindBy, "Find By");
            excel.AddMapping<TestPageField>(x => x.Type, "Type");
            excel.AddMapping<TestPageField>(x => x.Using, "Using");
            excel.AddMapping<TestPageField>(x => x.Signature, "Signature");
            excel.AddMapping<TestPageField>(x => x.Expected, "Expected");

            excel.AddTransformation<TestPageField>(x => x.Signature, cellValue => cellValue == "Y");
            excel.AddTransformation<TestPageField>(x => x.Expected, cellValue => cellValue == "Y");


            IQueryable<TestPageField> tpfs = from tpf in excel.Worksheet<TestPageField>("Pages")
                                             where tpf.PageName == PageName
                                             select tpf;

            TestPageFields = tpfs.ToList();

            TestPageField urlField = TestPageFields.FirstOrDefault(x => x.Name == "%PAGE-URL");
            if (urlField != null)
                PageUrl = urlField.Using == "%ROOT" ? "" : urlField.Using;
        }

        #endregion

        #region Navigation & Supporting Methods

        public bool NavigateToPage(string credentials = null)
        {
            if (PageUrl != null) // If null, its not something to which we can navigate, like the navigation bar
                WebDriver.Navigate().GoToUrl(FullUrl);

            if (!IsLoaded())
                return false;

            //InitPageElements();
            return true;
        }

        public bool IsLoaded()
        {
            if ((TestPageFields == null) || (TestPageFields.Count == 0))
                return false;                   // If no fields defined, can't tell if the page is loaded.

            List<TestPageField> signtureFields = TestPageFields
                .Where(x => x.Signature == true)
                .ToList();

            if (signtureFields.Count == 0)
                return false;                   // If no signature fields, can't tell if the page is loaded.
            string errMsg = "";
            string exMsg = "";
            bool funcStatus = true;
            //System.Threading.Thread.Sleep(2000);
            WebDriver.WaitForPage(TimeSpan.FromSeconds(10));

            foreach (TestPageField signtureField in signtureFields)
            {
                bool status = true;
                try
                {

                    switch (signtureField.FindBy.ToUpper().Trim())
                    {
                        case "ID":
                            if (WaitFor.ElementPresent(WebDriver, By.Id(signtureField.Using), PageLoadWaitSeconds) == null)
                            {
                                status = false;
                            }
                            break;

                        case "CSS":
                            if (WaitFor.ElementPresent(WebDriver, By.CssSelector(signtureField.Using), PageLoadWaitSeconds) == null)
                            {
                                status = false;
                            }
                            break;

                        case "TEXT":
                            if (!TextIsDisplayedOnPage(signtureField.Using))
                            {
                                status = false;
                            }
                            break;

                        case "TITLE":
                            if (WebDriver.Title != signtureField.Using)
                            {
                                status = false;
                            }
                            break;
                    }

                }
                catch (Exception ex)
                {
                    //throw new Exception("Page Signature Failure - Field: " + signtureField.Name);
                    status = false;
                    exMsg = ex.Message;
                }
                if (status == false)
                {
                    funcStatus = false;
                    errMsg += errMsg == "" ? "" : "; ";
                    errMsg += "Field [" + signtureField.Name + "] Value [" + signtureField.Using + "]";
                    errMsg += String.IsNullOrEmpty(exMsg) ? "" : " Message: " + exMsg;
                    exMsg = "";
                }

            }
            if (!funcStatus)
                throw new Exception("Page elements not found: " + errMsg);
            return funcStatus;
        }

        #endregion

        #region Get Page Fields & Elements

        public bool GetFormField(string fieldName, out FormField formField)
        {
            formField = null;

            if ((String.IsNullOrEmpty(fieldName)) || (TestPageFields == null) || (TestPageFields.Count == 0))
                return false;

            TestPageField tpf = TestPageFields.FirstOrDefault(x => x.Name == fieldName.ToUpper().Trim());
            if (tpf == null)
                return false;

            FormField ff = new FormField();
            ff.CssLocator = tpf.Using.Trim();
            ff.ControlType = (ControlType)fieldTypeHash[tpf.Type];
            if (tpf.Expected)
            {
                switch (tpf.FindBy)
                {
                    case "ID":
                        ff.Field = GetElement(By.Id(ff.CssLocator));
                        break;

                    case "CSS":
                        ff.Field = GetElement(By.CssSelector(ff.CssLocator));
                        break;

                    case "LINKTEXT":
                        ff.Field = GetElement(By.LinkText(ff.CssLocator));
                        break;
                }
            }
            formField = ff;
            return true;

        }

        public IWebElement GetElement(By byObj, string text = null)
        {
            ReadOnlyCollection<IWebElement> elements = WebDriver.FindElements(byObj);
            if (elements == null)
                return null;

            foreach (IWebElement element in elements)
            {
                // Return first element found if text is null or empty
                // or return first element containing text

                if ((element.GetCssValue("display").ToUpper() != "NONE") && (String.IsNullOrEmpty(text)) || (element.Text.Contains(text)))
                    return element;
            }
            return null;
        }

        public bool TextIsDisplayedOnPage(string text)
        {
            if (String.IsNullOrEmpty(text))
                throw new ArgumentEmptyException("TextIsDisplayedOnPage: Argument for text to find is null or empty");

            WebDriver.FindTextOnPage(text);

            //IWebElement bodyElement = WaitFor.ElementPresent(WebDriver, By.TagName("body"));
            //if ((bodyElement == null) || (bodyElement.Text == null))
            //    throw new Exception("TextIsDisplayedOnPage: \"body\" tag not found or text of body is null");

            //bool status = bodyElement.Text.Contains(text);
            //if (!status)
            //    throw new Exception("TextIsDisplayedOnPage: Text [" + text + "] not found in [" + bodyElement.Text.Substring(0, 50) + "...]");

            return true;
        }

        #endregion

        #region Select2 Handlers

        public bool Select2NoSearch(string elementId, string selectValue)
        {
            try
            {
                // find Select2's choice box and click it to reveal the true select control
                string cssSelector = "#s2id_" + elementId;
                IWebElement select2ChoiceElement = WebDriver.FindElement(By.CssSelector(cssSelector + " .select2-choice"));
                if (select2ChoiceElement == null)
                    return false;

                select2ChoiceElement.Click();

                // Find the true select element - its Id will be the control Id as stated in .cshtml file
                IWebElement webElement = WebDriver.FindElement(By.CssSelector("#" + elementId));
                if (webElement == null)
                    return false;

                //searchBox.SendKeys(selectValue);
                SelectElement select = new SelectElement(webElement);

                select.SelectByText(selectValue);
                var sel = select.SelectedOption;

                return sel.Text.Trim().ToUpper() == selectValue.Trim().ToUpper();
            }
            catch (Exception)
            {
                Console.WriteLine("Select2 select acton failed");
                throw;
            }
        }

        public void Select2SearchAndSelectItem(string elementId, string searchString)
        {
            //string select2SearchboxDivId = "div#s2id_" + elementId.Trim() + ">div.select2-with-searchbox";

            IWebElement select2ChoiceElement =
                WebDriver.FindElement(By.CssSelector("#s2id_" + elementId.Trim() + " .select2-choice"));
            select2ChoiceElement.Click();

            //IWebElement select2SearchBox = WaitFor.ElementPresent(WebDriver, By.CssSelector(select2SearchboxDivId),
            //                                                          1);
            //if(select2SearchBox == null)
            //{
            //    Select2NoSearch(elementId, searchString);
            //    return;
            //}

            const string subContainerClass = "#select2-drop:not([style*='display: none'])";
            var searchBox = WebDriver.FindElement(By.CssSelector(subContainerClass + " .select2-input"));
            searchBox.SendKeys(searchString);

            var selectedItem = WebDriver
                .FindElements(By.CssSelector(subContainerClass + " .select2-results li.select2-result-selectable"))
                .FirstOrDefault();

            if (selectedItem != null)
                selectedItem.Click();
            else
                throw new NotFoundException(String.Format("Value not found in Select2 search [{0}]", searchString));
        }
        #endregion

        #region Table Methods

        public int RowNumber(string rowReference)
        {
            Dictionary<string, int> numberDictionary = new Dictionary<string, int>()
                                                       {
                                                           {"FIRST", 0},
                                                           {"1ST", 0},
                                                           {"SECOND", 1},
                                                           {"2ND", 1},
                                                           {"THIRD", 2},
                                                           {"3RD", 2}
                                                       };
            int row;
            if (!numberDictionary.TryGetValue(rowReference.ToUpper().Trim(), out row))
                row = -1;

            return row;
        }

        public bool SetFieldValue(string tableName, string rowReference, string fieldName, string value = null)
        {
            FormField tableFormField;
            FormField fieldFormField;
            Dictionary<string, int> tableColumnIds = new Dictionary<string, int>();

            if (!GetFormField(tableName, out tableFormField))
                return false;

            if (tableFormField.ControlType != ControlType.Table)
                return false;

            if (!tableFormField.Field.Enabled)
                return false; // Failure - Field not enabled

            if (!GetFormField(fieldName, out fieldFormField))
                return false;

            ReadOnlyCollection<IWebElement> headerRows = tableFormField.Field.FindElements(By.CssSelector(" thead tr"));
            if (headerRows.Count > 0)
            {
                ReadOnlyCollection<IWebElement> headerThs = headerRows[0].FindElements(By.CssSelector(" th"));
                for (int i = 0; i < headerThs.Count; i++)
                {
                    string key = headerThs[i].Text.Trim().ToUpper();
                    if (!String.IsNullOrEmpty(key))
                    {
                        if (!tableColumnIds.ContainsKey(key))
                            tableColumnIds.Add(key, i);
                    }
                }
            }
            //ReadOnlyCollection<IWebElement> rowElements = tableFormField.Field.FindElements(By.CssSelector(" tbody tr"));
            //ReadOnlyCollection<IWebElement> rowElements1 = tableFormField.Field.FindElements(By.CssSelector("table tbody tr"));
            ReadOnlyCollection<IWebElement> rowElements = TestConstants.WebDriver.FindElements(By.CssSelector(tableFormField.CssLocator + " tbody tr"));
            if (rowElements.Count <= 0)
                return false;

            int row = RowNumber(rowReference);
            if (row == -1)
                return false;

            IWebElement rowElement = rowElements[row];

            //string cssSelector;
            //if (!RowFieldCSS.TryGetValue(fieldName.Trim().ToUpper(), out cssSelector))
            //    return false;

            IWebElement iconElement = rowElement.FindElement(By.CssSelector(fieldFormField.CssLocator));
            iconElement.Click();

            return true;
        }
        #endregion

        #region Scalar Methods
        public bool SetFieldValue(string fieldName, string value = null)
        {
            FormField formField;
            if (!GetFormField(fieldName, out formField))
                throw new ArgumentException("Field [" + fieldName + "] not found");
            //return false;

            if (!formField.Field.Enabled)
                throw new ArgumentException("Field [" + fieldName + "] not enabled");
            //return false; // Failure - Field not enabled

            string fieldType = formField.Field.GetAttribute("type");
            if (!String.IsNullOrEmpty(fieldType))
                fieldType = fieldType.ToUpper().Trim();

            string fieldClass = formField.Field.GetAttribute("class");
            if (!String.IsNullOrEmpty(fieldClass))
                fieldClass = fieldClass.ToUpper().Trim();

            switch (formField.ControlType)
            {
                case ControlType.Button:
                case ControlType.Link:
                    formField.Field.Click();
                    break;

                // FileBrowser is a special case for Selenium Webdriver.
                // Click on the true web browser form field opens the modal file browser dialog.
                // SendKeys to the browser form field actually sends keys to the modal dialog
                // and types the value text into the file name field of the file browser dialog.
                // WebDriver appears to send a modal form submit (or closing "Enter" key - not
                // sure which) on its own.
                case ControlType.FileBrowser:
                    formField.Field.Click();
                    formField.Field.SendKeys(value);
                    break;

                case ControlType.MultiLineText:
                    value = value.Replace(';', '\n');
                    formField.Field.Click();
                    formField.Field.Clear();
                    formField.Field.SendKeys(value);
                    break;

                case ControlType.Text:
                    formField.Field.Click();
                    formField.Field.Clear();
                    formField.Field.SendKeys(value);
                    break;

                case ControlType.Select2NoSearch:
                    Select2NoSearch(formField.Field.GetAttribute("Id"), value);
                    break;

                case ControlType.Select2:
                case ControlType.Select:
                    if (fieldClass.Contains("SELECT2"))
                    {
                        Select2SearchAndSelectItem(formField.Field.GetAttribute("Id"), value);
                    }
                    else
                    {
                        formField.Field.Click();
                        SelectElement selectElement = new SelectElement(formField.Field);
                        selectElement.SelectByText(value);
                        formField.Field.SendKeys(Keys.Tab);
                    }
                    break;

                case ControlType.DatePicker:
                case ControlType.DateTime:
                    formField.Field.Click();
                    formField.Field.Clear();
                    formField.Field.SendKeys(value);
                    formField.Field.SendKeys(Keys.Tab);
                    break;

                case ControlType.CheckBox:
                    if (fieldType == "CHECKBOX")
                    {
                        string fieldCheck = formField.Field.GetAttribute("checked");
                        if (value.ToUpper() == "CHECKED")
                        {
                            if ((fieldCheck == null) || (fieldCheck.ToUpper() != "CHECKED"))
                            {
                                formField.Field.Click();
                            }
                        }
                        else
                        {
                            if ((fieldCheck != null) || (fieldCheck.ToUpper() == "CHECKED"))
                            {
                                formField.Field.Click();
                            }
                        }
                    }
                    break;
            }
            return true;
        }

        public bool GetFieldValue(string fieldName, out string value)
        {
            FormField formField;
            value = null;
            if (!GetFormField(fieldName, out formField))
                return false;

            string fieldType = formField.Field.GetAttribute("type").ToUpper();
            switch (formField.ControlType)
            {
                case ControlType.MultiLineText:
                    value = formField.Field.GetAttribute("value");
                    break;

                case ControlType.Text:
                    value = formField.Field.GetAttribute("value");
                    break;

                case ControlType.Select2:
                    string elementId = formField.Field.GetAttribute("Id");
                    //Select2SearchAndSelectItem(elementId, value);
                    break;

                case ControlType.DatePicker:
                case ControlType.DateTime:
                    value = formField.Field.GetAttribute("value");
                    break;

                case ControlType.CheckBox:
                    if (fieldType == "CHECKBOX")
                    {
                        value = formField.Field.GetAttribute("checked").ToUpper();
                    }
                    break;
            }
            return true;
        }

        public bool ClearFieldValue(string fieldName)
        {
            FormField formField;
            if (!GetFormField(fieldName, out formField))
                return false;

            if (formField == null)
                return false;

            switch (formField.ControlType)
            {
                case ControlType.MultiLineText:
                    formField.Field.SendKeys("\u0001");  //Control-A
                    formField.Field.SendKeys(Keys.Delete);
                    break;

                case ControlType.Text:
                    formField.Field.SendKeys("\u0001");  //Control-A
                    formField.Field.SendKeys(Keys.Delete);
                    break;

                case ControlType.Select2:
                    formField.Field.Clear();
                    break;

                case ControlType.DatePicker:
                    formField.Field.SendKeys("\u0001");  //Control-A
                    formField.Field.SendKeys(Keys.Delete);
                    formField.Field.SendKeys(Keys.Escape);
                    break;

                case ControlType.DateTime:
                    formField.Field.SendKeys("\u0001");  //Control-A
                    formField.Field.SendKeys(Keys.Delete);
                    break;

                case ControlType.CheckBox:
                    return SetFieldValue(fieldName, "UNCHECKED");
            }
            return true;
        }
        #endregion
    }
}

#region Selenium Examples
#region CSS Locator Examples
/*
 * 
CSS locator Examples

1. Selenium CSS locator using Tag and any Attribute
css=input[type=search] \\\\ This syntax will find "input" tag node which contains "type=search" attribute.

css=input[id=searchInput] \\\\ This syntax will find "input" tag node which contains "id=searchInput" attribute.

css=form input[id=searchInput]  \\\\  This syntax will find form containing "input" tag node which contains
"id=searchInput" attribute.

(All three CSS path examples given above will locate Search text box.)

2. Selenium CSS locator using Tag and ID attribute
css=input#searchInput \\\\ Here, '#' sign is specially used for "id" attribute only. It will find "input" tag
node which contains "id=searchInput" attribute. This syntax will locate Search text box.

3. Selenium CSS locator using Tag and class attribute
css=input.formBtn  \\\\  Here, '.' is specially used for "class" attribute only. It will find "input" tag node
which contains "class=formBtn" attribute. This syntax will locate Search button (go).

4.  Selenium CSS locator using tag, class, and any attribute
css=input.formBtn[name=go]  \\\\ It will find "input" tag node which contains "class=formBtn" class and "name=go"
attribute. This syntax will locate Search button (go).

5. Tag and multiple Attribute CSS locator
css=input[type=search][name=search] \\\\  It will find "input" tag node which contains "type=search" attribute
and "name=search" attribute. This syntax will locate Search text box.

6. CSS Locator using Sub-string matches(Start, end and containing text) in selenium
css=input[id^='search']  \\\\  It will find input node which contains 'id' attribute starting with 'search' text.
(Here, ^ describes the starting text).

css=input[id$='chInput']  \\\\  It will find input node which contains 'id' attribute starting with 'chInput' text.
(Here, $ describes the ending text).

css=input[id*='archIn']  \\\\  It will find input node which contains 'id' attribute containing 'archIn' text.
(Here, * describes the containing text).

(All three CSS path examples given above will locate Search text box.)

7. CSS Element locator syntax using child Selectors
css=div.search-container>form>fieldset>input[id=searchInput]  \\\\  First it will find div tag with "class = search-container"
and then it will follow remaining path to locate child node. This syntax will locate Search text box.

8. CSS Element locator syntax using adjacent selectors
css=input + input  \\\\  It will locate "input" node where another "input" node is present before it on page.(for search tect box).

css=input + select or css=input + input + select \\\\  It will locate "select" node, where "input" node is present
before it on page(for language drop down).

9. CSS Element locator using contains keyword
css=strong:contains("English")  \\\\ It will looks for the element containing text "English" as a value on the page.

 */
#endregion
#endregion
