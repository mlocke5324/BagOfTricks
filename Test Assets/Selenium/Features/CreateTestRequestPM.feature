@StartAtHomePage
#@ResetData
Feature: Create Test Requests - Project Manager
	To manage work activities
	As a Project Manager
	I must manage test request creation, edit/update (prior to approval request), cancelation, and request for approval

Background:
	Given I am assigned the Project Manager role
	And I open the Home Page
	And I click on the Test Requests I Created block
	And the Test Requests I Created Page is displayed

Scenario: CTR-LT-01 - Open a Test Request Form and Cancel
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I enter the following data into the Test Request Page:
         | Field            | Value           |
         | Team Pulldown    | R&D Pilot Plant |
         | Project Pulldown | Next Pizza      |
	And I submit the form
	And the Test Request Form is displayed
	When I cancel the form
	And I accept the Message from webpage alert with text "This will discard any changes you have made to your test request."
	Then the Test Requests I Created Page is displayed

Scenario Outline: CTR-LT-02 - Save Test Request without all required fields entered
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I set the value of the Team pulldown to <team>
	And I set the value of the Project pulldown to Next Pizza
	And I submit the form
	And the Test Request Form is displayed
	When I enter the following data into the Test Request Form:
	| Field                        | Value                 |
	| Title of Request             | Test Title of Request |
	| Technologist Estimated Hours | 40                    |
	 
	And I submit the form
	Then the Test Request Details Page is displayed

	Examples:
	| team            |
	| R&D Pilot Plant |
	| TAG Pilot Plant |
	| R&D Benchtop    |
	| TAG Benchtop    |

Scenario Outline: CTR-LT-03 - Change status of Test Request to "Approved" not allowed with only Project Number/Name
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I set the value of the Team pulldown to <team>
	And I set the value of the Project pulldown to Next Pizza
	And I submit the form
	And the Test Request Form is displayed
	When I enter the following data into the Test Request Form:
	| Field               | Value   |
	| Project Number/Name | Pizza   |
	| Ready for approval  | Checked |
	
	And I submit the form
	Then the Test Request Form is displayed

	Examples:
	| team            |
	| R&D Pilot Plant |
	| TAG Pilot Plant |
	| R&D Benchtop    |
	| TAG Benchtop    |

Scenario: CTR-LT-04 - Enter all data for R&D Pilot Plant and save Test Request Form
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I set the value of the Team pulldown to R&D Pilot Plant
	And I set the value of the Project pulldown to Next Pizza
	And I submit the form
	And the Test Request Form is displayed
	When I enter the following data into the Test Request Form:
| Field                               | Value                 |
| Project Number/Name                 | Pizza                 |
| Title of Request                    | Test Title of Request |
| Finish Date                         | 12/31/2014            |
| Technologist Estimated Hours        | 1234                  |
| Number of Variables                 | 10                    |
| Number of Samples                   | 20                    |
| Save Sauce                          | Checked               |
| Meat Thaw                           | Checked               |
| Ingredients In-House and Available  | Checked               |
| Recipe Ready to be Weighed/Executed | Checked               |
| Recipe Attached                     | Beef Wellington.docx  |
| Ingredient Document Attached        | Ingredients.docx      |
| NesTMS Trial Number                 | Test trial number     |
| Type of Test/Trial                  | Focus Group           |
| Production Line                     | Test production line  |
 
	And I submit the form
	Then the Test Request Details Page is displayed

Scenario: CTR-LT-05 - Enter all data for TAG Pilot Plant and save Test Request Form
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I set the value of the Team pulldown to TAG Pilot Plant
	And I set the value of the Project pulldown to Next Pizza
	And I submit the form
	And the Test Request Form is displayed
	When I enter the following data into the Test Request Form:
| Field                               | Value                 |
| Project Number/Name                 | botticelli            |
| Brand/Group/Account                 | Buitoni               |
| Recipe name                         | Macaroni and Cheese   |
| Recipe Code/T-Code                  | Test Recipe Code      |
| Title of Request                    | Test Title of Request |
| Finish Date                         | 12/31/2014            |
| Technologist Estimated Hours        | 1234                  |
| Number of Variables                 | 10                    |
| Number of Samples                   | 20                    |
| Save Sauce                          | Checked               |
| Meat Thaw                           | Checked               |
| Ingredients In-House and Available  | Checked               |
| Recipe Ready to be Weighed/Executed | Checked               |
| Recipe Attached                     | Beef Wellington.docx  |
| Ingredient Document Attached        | Ingredients.docx      |
| PDS Test Number                     | Test PDS number       |
| Type of Test/Trial                  | Focus Group           |
| Production Line                     | Test production line  |
| TKS Approval Date                   | 12/31/2015            |
 
	And I submit the form
	Then the Test Request Details Page is displayed

Scenario: CTR-LT-06 - Enter all data for R&D Benchtop and save Test Request Form
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I set the value of the Team pulldown to R&D Benchtop
	And I set the value of the Project pulldown to Next Pizza
	And I submit the form
	And the Test Request Form is displayed
	When I enter the following data into the Test Request Form:
| Field                               | Value                 |
| Project Number/Name                 | Pizza                 |
| Title of Request                    | Test Title of Request |
| Finish Date                         | 12/31/2014            |
| Technologist Estimated Hours        | 1234                  |
| Number of Variables                 | 10                    |
| Number of Samples                   | 20                    |
| Save Sauce                          | Checked               |
| Meat Thaw                           | Checked               |
| Ingredients In-House and Available  | Checked               |
| Recipe Ready to be Weighed/Executed | Checked               |
| Recipe Attached                     | Beef Wellington.docx  |
| Ingredient Document Attached        | Ingredients.docx      |
| NesTMS Trial Number                 | Test trial number     |
| Type of Test/Trial                  | Concept               |
 
	And I submit the form
	Then the Test Request Details Page is displayed

Scenario: CTR-LT-07 - Enter all data for TAG Benchtop and save Test Request Form
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I set the value of the Team pulldown to TAG Benchtop
	And I set the value of the Project pulldown to Next Pizza
	And I submit the form
	And the Test Request Form is displayed
	When I enter the following data into the Test Request Form:
| Field                               | Value                 |
| Project Number/Name                 | botticelli            |
| Brand/Group/Account                 | Buitoni               |
| Recipe name                         | Macaroni and Cheese   |
| Recipe Code/T-Code                  | Test Recipe Code      |
| Title of Request                    | Test Title of Request |
| Finish Date                         | 12/31/2014            |
| Technologist Estimated Hours        | 1234                  |
| Number of Variables                 | 10                    |
| Number of Samples                   | 20                    |
| Save Sauce                          | Checked               |
| Meat Thaw                           | Checked               |
| Ingredients In-House and Available  | Checked               |
| Recipe Ready to be Weighed/Executed | Checked               |
| Recipe Attached                     | Beef Wellington.docx  |
| Ingredient Document Attached        | Ingredients.docx      |
| PDS Test Number                     | Test PDS number       |
| Type of Test/Trial                  | Recon                 |
 
	And I submit the form
	Then the Test Request Details Page is displayed

Scenario: CTR-LT-08 - Edit an existing Test Request
	When I click on the edit icon of the first line item of the Test Request table
	Then the Edit Test Request Form is displayed

Scenario: CTR-LT-09 - Show Details of an existing Test Request
	When I click on the details icon of the second line item of the Test Request table
	Then the Test Request Details page is displayed

Scenario: CTR-LT-10 - Delete an existing Test Request
	When I click on the delete icon of the third line item of the Test Request table
	And the Test Request Delete Confirmation Dialog is displayed
	And I click on the Yes button
	Then the Test Requests I Created page is displayed
