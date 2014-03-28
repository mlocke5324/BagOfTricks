using System;
using System.Collections.Generic;
using System.Linq;
using NesTrak.AcceptanceTests.Pages;
using NesTrak.Data;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using TechTalk.SpecFlow;

namespace NesTrak.AcceptanceTests.Steps
{
    [Binding]                   // Must be included, else BeforeFeature / AfterFeature will not be executed by SpecFlow & MSTest test runner
    public class TestActions : TechTalk.SpecFlow.Steps
    {
        private const string ScenarioFailedId = "ScenarioFailed";
        public static bool NoOp = false;
        public static TestPage _HomePage;

        public void TakeScreenshot(string fileName)
        {
            try
            {
                Screenshot ss = ((ITakesScreenshot)TestConstants.WebDriver).GetScreenshot();
                if (ss == null)
                    throw new NullReferenceException("GetScreenshot returned null value");

                ss.SaveAsFile(TestConstants.TestScreenshotPath + fileName + ".jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        public static void ResetApplicationData()
        {
            using (NesTrakContainer db = new NesTrakContainer())
            {
                List<TestRequest> testRequests = db.EntryCategories.OfType<TestRequest>().ToList();
                if (testRequests.Count > 0)
                {
                    foreach (TestRequest tr in testRequests)
                        db.DeleteObject(tr);
                    db.SaveChanges();
                }
            }
        }

        public static bool SetUserRole(string roleDescription)
        {
            using (NesTrakContainer db = new NesTrakContainer())
            {
                List<Role> roles = db.Roles.ToList();
                if (String.IsNullOrEmpty(roleDescription))
                    return false;

                Role role = db.Roles.FirstOrDefault(r => r.Name == roleDescription);
                if (role == null)
                    return false;

                User user = db.Users
                    .Include("Roles")
                    .FirstOrDefault(x => x.UserName == TestConstants.TestUserName);
                if (user == null)
                    return false;
                /*
                 * If the user already has the role, then do nothing
                 * 
                 * If we are resetting the role, then we have to close and reopen the
                 * browser for the new role to take effect.
                 */
                if (!user.Roles.Contains(role))
                {
                    user.Roles.Clear();
                    user.Roles.Add(role);
                    db.SaveChanges();
                    TestConstants.Quit();
                    FeatureSetup();
                    //_HomePage.NavigateToPage();
                }
                return true;
            }
        }
        #region Alert Detection and Management
        public bool AlertIsPresent()
        {
            try
            {
                
                TestConstants.WebDriver.SwitchTo().Alert();
                return true;
            }
            catch (NoAlertPresentException Ex)
            {
                return false;
            }
        }

        public IAlert GetAlert()
        {
            return TestConstants.WebDriver.SwitchTo().Alert();
        }
        #endregion

        //[BeforeTestRun]
        //public static void TestSetup()
        //{
        //    ResetApplicationData();
        //}

        [BeforeFeature]
        public static void FeatureSetup()
        {
            TestConstants.InitializeDriver();        // Must be called first - ITestPage objects get values from TestConstants
            FeatureContext.Current[ScenarioFailedId] = true;      // Always execute scenarios with FirstOrFailed tag first time through
            NoOp = false;
            _HomePage = new TestPage("HOME PAGE");
        }

        [BeforeFeature("ResetData")]
        public static void FeatureSetupResetData()
        {
            ResetApplicationData();
        }

        [BeforeScenario("CloseBrowserAfterScenario")]
        public void ScenarioOpenBrowser()
        {
            FeatureSetup();
            //TestConstants.InitializeDriver();
        }
        
        [BeforeScenario("BackgroundFirstOrFailure")]
        public void ScenarioSetUpTestRequestRequiredFields()
        {
            bool ScenarioFailed = (bool?) FeatureContext.Current["ScenarioFailed"] ?? false;
            NoOp = !ScenarioFailed; // Did last scenario fail?
            if (ScenarioFailed)
            {
                _HomePage.NavigateToPage(); // First scenario starts at home page
            }
        }

        [BeforeScenario("StartAtHomePage")]
        public void ScenarioSetupHomePage()
        {
            _HomePage.NavigateToPage(); // Most scenarios start at home page
        }

        [BeforeStep("DismissAlerts")]
        public void StepSetup()
        {
            if (AlertIsPresent())
            {
                IAlert alert = GetAlert();
                string alertText = alert.Text;
                alert.Accept();
                if (AlertIsPresent())
                    alert.Dismiss();

                throw new UnhandledAlertException("Unhandled alert [" + alertText + "]");
            }
        }

        [AfterScenario]
        public void ScenarioTearDown()
        {
            // Take a screnshot if the scenario failed
            var error = ScenarioContext.Current.TestError;
            if (error == null)
            {
                FeatureContext.Current[ScenarioFailedId] = false;
            }
            else
            {
                //System.Threading.Thread.Sleep(2000);
                FeatureContext.Current[ScenarioFailedId] = true;
                string scenarioTitle = ScenarioContext.Current.ScenarioInfo.Title;
                string screenshotFileName = scenarioTitle.Replace(" ", "") + String.Format("{0}",
                    DateTime.Now.Year * 10000000000 +
                    DateTime.Now.Month * 100000000 +
                    DateTime.Now.Day * 1000000 +
                    DateTime.Now.Hour * 10000 +
                    DateTime.Now.Minute * 100 +
                    DateTime.Now.Second);
                Console.WriteLine("Error - Scenario: " + scenarioTitle);
                Console.WriteLine("      Error Type: " + error.GetType().Name);
                Console.WriteLine("         Message: " + error.Message);
                TakeScreenshot(screenshotFileName);
            }
        }

        [AfterScenario("CloseBrowserAfterScenario")]
        public void ScenarioCloseBrowser()
        {
            TestConstants.Quit();
        }

        [AfterFeature]
        public static void FeatureTearDown()
        {
            TestConstants.Quit();           // Terminates Selenium web driver and closes browser
        }
    }
}
//[Before]
//[BeforeFeature]
//[BeforeScenario]
//[BeforeScenarioBlock]
//[BeforeStep]
//[BeforeTestRun]
//[After]
//[AfterFeature]
//[AfterScenario]
//[AfterScenarioBlock]
//[AfterStep]
//[AfterTestRun]