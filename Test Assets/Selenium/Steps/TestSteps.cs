using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NesTrak.AcceptanceTests.Pages;
using OpenQA.Selenium;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace NesTrak.AcceptanceTests.Steps
{
    class TestFieldParameter
    {
        public string Field { get; set; }
        public string Value { get; set; }
    }

    [Binding]
    public class TestSteps : TestActions
    {
        private const string CurrentPage = "CurrentPage";
        private const string SavedFieldValue = "SavedFieldValue";
        private const string SavedFieldName = "SavedFeildName";

        #region Test Flow Methods

        [Given(@"I run these steps on first scenario run or a scenario failure")]
        public void TestFormFirstRunOrFailure(Table table)
        {
            if (NoOp)
                return;

            foreach (string step in table.Rows.Select(t => t[0]))
            {
                Given(step);
            }
        }

        #endregion

        #region Test Setup Methods

        [Given(@"I am assigned the (.*) role")]
        public void TestFormSetUserRole(string roleDescription)
        {
            Assert.IsTrue(SetUserRole(roleDescription), String.Format("Setting Role [{0}] for test user failed", roleDescription));
        }

        #endregion

        #region Alert Handling

        [Given(@"I accept the (.*) alert with text ""(.*)""")]
        [When(@"I accept the (.*) alert with text ""(.*)""")]
        public void AlertConfirmationAccept(string alertType, string expectedText)
        {
            if (!AlertIsPresent())
                throw new NoAlertPresentException("Expected alert for [" + alertType + "] not found");

            IAlert alert = GetAlert();
            if (!alert.Text.Contains(expectedText))
            {
                alert.Dismiss();
                throw new NotFoundException("Alert with expected text [" + expectedText + "] not found");
            }

            alert.Accept();
        }

        #endregion

        #region Page Navigation

        [Given(@"I start the application")]
        public void TestFormStartApplication()
        {
            TestFormOpenPage("HOME PAGE");
        }

        [Given(@"I open the (.+)")]
        public void TestFormOpenPage(string pageDescription)
        {
            TestPage pageObj = null;
            if (ScenarioContext.Current.ContainsKey(CurrentPage))
                pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            if ((pageObj == null) || (pageObj.PageName != pageDescription.Trim().ToUpper()))
                pageObj = new TestPage(pageDescription);
            Assert.IsTrue(pageObj.TestPageFields.Count > 0, String.Format("Could not load page fields for [{0}]", pageDescription));

            Assert.IsTrue(pageObj.NavigateToPage());
            Assert.IsTrue(pageObj.IsLoaded());
            //pageObj.InitPageElements();
            ScenarioContext.Current[CurrentPage] = pageObj;
        }

        [Given(@"the (.*) is displayed")]
        [Then(@"the (.*) is displayed")]
        [When(@"the (.*) is displayed")]
        public void TestFormConfirmPageIsDisplayed(string pageDescription)
        {
            TestPage pageObj = null;
            if (ScenarioContext.Current.ContainsKey(CurrentPage))
                pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            if ((pageObj == null) || (pageObj.PageName != pageDescription.Trim().ToUpper()))
                pageObj = new TestPage(pageDescription);
            Assert.IsTrue(pageObj.TestPageFields.Count > 0, String.Format("Could not load page fields for [{0}]", pageDescription));

            Assert.IsTrue(pageObj.IsLoaded());
            //pageObj.InitPageElements();
            ScenarioContext.Current[CurrentPage] = pageObj;
        }

        #endregion

        [Then(@"I should see navigation blocks with Text")]
        public void ThenIShouldSeeNavigationBlocksWithText(Table table)
        {
            foreach (TableRow t in table.Rows)
            {
                string blockText = t[0];
                Assert.IsNotNull(_HomePage.GetElement(By.CssSelector("div.homeBlock"), blockText),
                    String.Format("Block [{0}] not found, not visible, or disabled", blockText));
            }
        }

        #region Submit, Cancel, Button & Field Clicks

        [Given(@"I submit the form")]
        [When(@"I submit the form")]
        public void TestFormSubmit()
        {
            TestFormClickField("SUBMIT");
        }

        [When(@"I cancel the form")]
        public void TestFormCancel()
        {
            TestFormClickField("CANCEL");
        }

        [Given(@"I click on the (.*) button")]
        [When(@"I click on the (.*) button")]
        [Given(@"I click on the (.*) link")]
        [When(@"I click on the (.*) link")]
        [Given(@"I click on the (.*) block")]
        [When(@"I click on the (.*) block")]
        [Given(@"I click the (.*)")]
        [When(@"I click the (.*)")]
        public void TestFormClickField(string fieldName)
        {
            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            Assert.IsTrue(pageObj.SetFieldValue(fieldName), String.Format("Click action failed for [{0}]", fieldName));
        }

        [Given(@"I click on the (.*) of the (first|1st|second|2nd|third|3rd|.th|last) line item of the (.* list|.* table)")]
        [When(@"I click on the (.*) of the (first|1st|second|2nd|third|3rd|.th|last) line item of the (.* list|.* table)")]
        public void TestFormEditListLineItem(string clickItem, string itemLine, string listItem)
        {
            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;

            Assert.IsTrue(pageObj.SetFieldValue(listItem, itemLine, clickItem), "Unable to locate [" + listItem + "] on page");
        }



        #endregion

        #region Data Entry & Retrieval Methods

        // To-do: Change this to pageObj.Select(string fieldDescriptor, string valueToSelect);
        [Given(@"I set the value of the (.*) to (.*)")]
        [When(@"I set the value of the (.*) to (.*)")]
        [Given(@"I set the value of the (.*) field to (.*)")]
        [When(@"I set the value of the (.*) field to (.*)")]
        public void EnterFormDataForOneField(string fieldName, string value)
        {
            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            Assert.IsNotNull(pageObj, "Could not get test page object from scenario context");
            pageObj.SetFieldValue(fieldName, value);
        }

        [Given(@"I enter the following data into the (.*):")]
        [When(@"I enter the following data into the (.*):")]
        public void EnterDataIntoAForm(string pageDescription, Table table)
        {
            if (NoOp)
                return;

            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            if ((pageObj == null) || (pageObj.PageName != pageDescription.Trim().ToUpper()))
                pageObj = new TestPage(pageDescription);
            Assert.IsTrue(pageObj.TestPageFields.Count > 0, String.Format("Could not load page fields for [{0}]", pageDescription));

            IEnumerable<TestFieldParameter> testFieldParameters = table.CreateSet<TestFieldParameter>();
            foreach (TestFieldParameter testFieldParameter in testFieldParameters)
            {
                if (!pageObj.SetFieldValue(testFieldParameter.Field, testFieldParameter.Value))
                    throw new AssertFailedException(String.Format("Update failed for field [{0}] using value [{1}]",
                                                                  testFieldParameter.Field, testFieldParameter.Value));
            }
        }

        #endregion

        #region Single Field Save, Clear, Restore Methods

        [When(@"I save the value of the (.*) field")]
        public void TestFormSaveFieldValue(string fieldName)
        {
            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            string value;
            Assert.IsTrue(pageObj.GetFieldValue(fieldName, out value), "Unable to retireve value for field [" + fieldName + "]");
            ScenarioContext.Current[SavedFieldValue] = value;
            ScenarioContext.Current[SavedFieldName] = fieldName;
        }

        [When(@"I clear the field")]
        public void TestFormClearField()
        {
            string fieldName = ScenarioContext.Current[SavedFieldName] as string;
            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            Assert.IsTrue(pageObj.ClearFieldValue(fieldName), "Unable to clear value for field [" + fieldName + "]");
        }

        [Then(@"I can restore the field's value")]
        public void TestFormRestoreFieldValue()
        {
            string fieldName = ScenarioContext.Current[SavedFieldName] as string;
            string value = ScenarioContext.Current[SavedFieldValue] as string;
            EnterFormDataForOneField(fieldName, value);
        }

        #endregion

        #region Text Inspection Methods

        [Then(@"I should see Text")]
        public void ThenIShouldSeeText(Table table)
        {
            foreach (TableRow t in table.Rows)
            {
                string text = t[0];
                ThenTheMessageIsDisplayedOnTheForm(text);
            }
        }

        [Then(@"the message ""(.*)"" is displayed on the form")]
        public void ThenTheMessageIsDisplayedOnTheForm(string message)
        {
            TestPage pageObj = ScenarioContext.Current[CurrentPage] as TestPage;
            Assert.IsTrue(pageObj.TextIsDisplayedOnPage(message), "Unable to find text [" + message + "] on current page");
        }

        #endregion
    }
}
