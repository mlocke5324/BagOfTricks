@StartAtHomePage
@DismissAlerts
Feature: PageNavigation
	To use the application
	As an end user
	I need to navigate to the different pages of the application

@PageNavigation
Scenario Outline: Navigate to pages of the application - Administrator
	Given I am assigned the Administrator role
	And I open the Home Page
	When I click on the <block> block
	Then the <page> is displayed

	Examples:
	| block               | page                     |
	| Find a Test Request | Find a Test Request Page |
	| Find Timesheets     | Find Timesheets Page     |
	| Admin Tools         | Admin Tools Page         |
	| Reports & Exports   | Reports Page             |

Scenario Outline: Navigate to pages of the application - Manager
	Given I am assigned the Manager role
	And I open the Home Page
	When I click on the <block> block
	Then the <page> is displayed

	Examples:
	| block                     | page                           |
	| Find a Test Request       | Find a Test Request Page       |
	| All Timesheets To Approve | All Timesheets To Approve Page |
	| Test Requests To Approve  | Test Requests To Approve Page  |

Scenario Outline: Navigate to pages of the application - Project Manager
	Given I am assigned the Project Manager role
	And I open the Home Page
	When I click on the <block> block
	Then the <page> is displayed

	Examples:
	| block                   | page                         |
	| Find a Test Request     | Find a Test Request Page     |
	| Test Requests I Created | Test Requests I Created Page |
	| Reports & Exports       | Reports Page                 |

Scenario Outline: Navigate to pages of the application - Lead Technician
	Given I am assigned the Lead Technician role
	And I open the Home Page
	When I click on the <block> block
	Then the <page> is displayed

	Examples:
	| block                       | page                             |
	| Find a Test Request         | Find a Test Request Page         |
	| My Working Test Requests    | My Working Test Requests Page    |
	| Test Requests I Created     | Test Requests I Created Page     |
	| Test Requests To Approve    | Test Requests To Approve Page    |
	| Staff Timesheets To Approve | Staff Timesheets To Approve Page |
	| My Timesheets               | My Timesheets Page               |
	| Reports & Exports           | Reports Page                     |


Scenario Outline: Navigate to pages of the application - Technician
	Given I am assigned the Technician role
	And I open the Home Page
	When I click on the <block> block
	Then the <page> is displayed

	Examples:
	| block                       | page                             |
	| Find a Test Request         | Find a Test Request Page         |
	| My Working Test Requests    | My Working Test Requests Page    |
	| My Timesheets               | My Timesheets Page               |
