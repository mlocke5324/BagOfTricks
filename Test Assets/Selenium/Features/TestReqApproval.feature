@StartAtHomePage
@DismissAlerts

Feature: Test Request Approval
	To make Test Requests Available for action
	As a Manager or Lead Technician
	I must be able to review and approve or reject Test Requests

Scenario: Approve a Test Request
	Given I am assigned the Manager role
	And I open the Home Page
	And I click on the Test Requests To Approve block
	Then the Test Requests To Approve page is displayed

	