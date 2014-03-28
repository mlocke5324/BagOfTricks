@StartAtHomePage
@DismissAlerts
Feature: Test Request
	To manage work activities
	As a project manager
	I must manage test request creation, edit/update (prior to approval request), cancelation, and request for approval

Background:
	Given I am assigned the Project Manager role
	And I open the Home Page
	And I click on the Test Requests I Created block
	And the Test Requests I Created Page is displayed

##All - Cancel test request after save
##All - Cancel test request before save
##All - Create test request
##All - Open test request for edit after save
#TAG Benchtop - Create test request
#TAG Benchtop - Create test request - Required fields present
#TAG Benchtop - Create test request - Absence of inappropriate fields
#TAG Benchtop - Create test request - Data entry
#TAG Benchtop - Edit test request after save - Required fields present
#TAG Benchtop - Edit test request after save - Absence of inappropriate fields
#TAG Benchtop - Edit test request after save - Data entry
#TAG Benchtop - Mark test request as ready for approval
#TAG Benchtop - Mark test request as ready for approval not permitted
#TAG Pilot Plant - Create test request
#TAG Pilot Plant - Create test request - Required fields present
#TAG Pilot Plant - Create test request - Absence of inappropriate fields
#TAG Pilot Plant - Create test request - Data entry
#TAG Pilot Plant - Edit test request after save - Required fields present
#TAG Pilot Plant - Edit test request after save - Absence of inappropriate fields
#TAG Pilot Plant - Edit test request after save - Data entry
#TAG Pilot Plant - Mark test request as ready for approval
#TAG Pilot Plant - Mark test request as ready for approval not permitted
#R&D Benchtop - Create test request
#R&D Benchtop - Create test request - Required fields present
#R&D Benchtop - Create test request - Absence of inappropriate fields
#R&D Benchtop - Create test request - Data entry
#R&D Benchtop - Edit test request after save - Required fields present
#R&D Benchtop - Edit test request after save - Absence of inappropriate fields
#R&D Benchtop - Edit test request after save - Data entry
#R&D Benchtop - Mark test request as ready for approval
#R&D Benchtop - Mark test request as ready for approval not permitted
#R&D Pilot Plant - Create test request
#R&D Pilot Plant - Create test request - Required fields present
#R&D Pilot Plant - Create test request - Absence of inappropriate fields
#R&D Pilot Plant - Create test request - Data entry
#R&D Pilot Plant - Edit test request after save - Required fields present
#R&D Pilot Plant - Edit test request after save - Absence of inappropriate fields
#R&D Pilot Plant - Edit test request after save - Data entry
#R&D Pilot Plant - Mark test request as ready for approval
#R&D Pilot Plant - Mark test request as ready for approval not permitted


@WorkOrder @All

Scenario: Create a Test Request
	Given I click the Add Test Request Button
	And the Test Request Page is displayed
	And I enter the following data into the Test Request Page:
         | Field            | Value           |
         | Team Pulldown    | R&D Pilot Plant |
         | Project Pulldown | Pizza           |
	And I submit the form
	Then the Test Request Form is displayed

#
#Scenario: Cancel test request after save
#	Given I open the test requests list
#	And I open an existing test request
#	And the test request has not been marked ready for approval
#	When I press "Cancel Work Order"
#	Then the test request is marked cancelled
#	And I am returned to the test requests List
#
#Scenario: Open test request for edit after save
#	Given I open the test requests list
#	And I open an existing test request
#	Then the test request is displayed
#
