@DismissAlerts
Feature: HomePage
	To use the application
	As an end user
	I need to access the home page of the application and available actions per my role

Scenario: Home Page - Administrator
	Given I am assigned the Administrator role
	And I open the Home Page
	Then I should see navigation blocks with Text
	| Text                |
	| Find a Test Request |
	| Find Timesheets     |
	| Admin Tools         |
	| Reports             |

Scenario: Home Page - Manager
	Given I am assigned the Manager role
	And I open the Home Page
	Then I should see navigation blocks with Text
	| Text                      |
	| Test Requests To Approve  |
	| Find a Test Request       |
	| All Timesheets To Approve |
	| Reports                   |

Scenario: Home Page - Project Manager
	Given I am assigned the Project Manager role
	And I open the Home Page
	Then I should see navigation blocks with Text
	| Text                     |
	| Test Requests I Created |
	| Find a Test Request      |
	| Reports                  |

Scenario: Home Page - Lead Technician
	Given I am assigned the Lead Technician role
	And I open the Home Page
	Then I should see navigation blocks with Text
	| Text                        |
	| My Working Test Requests    |
	| Test Requests I Created     |
	| Test Requests To Approve    |
	| Find a Test Request         |
	| Staff Timesheets To Approve |
	| My Timesheets               |
	| Reports                     |

Scenario: Home Page - Technician
	Given I am assigned the Technician role
	And I open the Home Page
	Then I should see navigation blocks with Text
	| Text                     |
	| My Working Test Requests |
	| Find a Test Request      |
	| My Timesheets            |
	| Reports                  |


