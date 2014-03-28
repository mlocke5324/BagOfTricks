@BackgroundFirstOrFailure
@DismissAlerts
Feature: Test Request Form Required Entries R&D Pilot Plant
	To submit an R&D Pilot Plant test request form for approval
	As an R&D Pilot Plant Project Manager
	I need to complete all required entries in the form

#Note:
#
#Pulldowns and File selectors that cannot be cleared are not tested:
#	Approver
#	Project Number/Name
#	Type of Test/Trial
#	Recipe Attached
#
#Optional fields are not tested:
#	Technologist Estimated Hours
#	Meat Thaw
#	Save Sauce
#	Ingredient Document Attached
#	Ingredients In-House and Available
#
Background:
	Given I run these steps on first scenario run or a scenario failure
         | steps                                                   |
         | I am assigned the Project Manager role                  |
         | I open the Test Request Page                            |
         | the Test Request Page is displayed                      |
         | I set the value of the Team pulldown to R&D Pilot Plant |
         | I set the value of the Project pulldown to Next Pizza   |
         | I submit the form                                       |
         | the Test Request Form is displayed                      |

	And I enter the following data into the Test Request Form:
		| Field                               | Value                             |
		| Project Number/Name                 | Pizza                             |
		| Title of Request                    | Test title                        |
		| NesTMS Trial Number                 | Test trial number                 |
		| Type of Test/Trial                  | Focus Group                       |
		| Production Line                     | Test production line              |
		| Recipe Ready to be Weighed/Executed | Checked                           |
		| Recipe Attached                     | Beef Wellington.docx              |
		| Ingredient Document Attached        | Ingredients.docx                  |
		| Finish Date                         | 12/31/2014                        |
		| Equipment needed                    | First line;Second line;Third Line |
		| Number of Variables                 | 10                                |
		| Number of Samples                   | 20                                |
		| Ready for approval                  | Checked                           |

Scenario Outline: Submit Test Request missing required fields - R&D Pilot Plant
	Given the Test Request Form is displayed
	When I save the value of the <Field> field
	And I clear the field
	And I submit the form
	Then the Test Request Form is displayed
	And the message "<Message>" is displayed on the form
	And I can restore the field's value

	Examples:
		| Field               | Message                         |
		| Title of Request    | Title of Request is required    |
		| Production Line     | Production Line is required     |
		| Finish Date         | Finish Date is required         |
		| Number of Variables | Number of Variables is required |
		| Number of Samples   | Number of Samples is required   |

