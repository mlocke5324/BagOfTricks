@StartAtHomePage
Feature: Timesheet
	To report time expended on Test Requests
	As an end user
	I need to report my time on a timesheet

Background:
	Given I am assigned the Technician role
	And I open the Home Page
	And I click on the My Timesheets block
	Then the My Timesheets page is displayed

Scenario: Timesheet - Open Timesheet for Edit
	When I click on the Edit Icon of the first line item of the Timesheet list
	Then the Timesheet Form is displayed

Scenario: Timesheet - Open Timesheet for Edit and Cancel
	Given I click on the Edit Icon of the first line item of the Timesheet list
	And the Timesheet Form is displayed
	When I cancel the form
	And I accept the Cancel Confirmation alert with text "This will discard any changes you have made to your timesheet."
	Then the My Timesheets page is displayed

Scenario: Timesheet - Add row without project shows alert
	Given I click on the Edit Icon of the first line item of the Timesheet list
	And the Timesheet Form is displayed
	And I click the Add Test Request Icon
	And I accept the No Project Selected alert with text "Please select an item to add first!"
	Then the Timesheet Form is displayed
