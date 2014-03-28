@BackgroundFirstOrFailure
@DismissAlerts
Feature: Test Request Form Required Entries R&D Benchtop
	To submit an R&D Benchtop test request form for approval
	As an R&D Benchtop Project Manager
	I need to complete all required entries in the form

#Note:
#
#Pulldowns and File selectors that cannot be cleared are not tested:
#	Approver
#	Project Number/Name
#	Type of Test/Trial
#	WBS (Factory Use)
#	Recipe Attached
#
#Optional fields are not tested:
#	NesTMS Trial Number
#	Technologist Estimated Hours
#	Meat Thaw
#	Save Sauce
#	Ingredient Document Attached
#	Ingredients In-House and Available
#
Background:
	Given I run these steps on first scenario run or a scenario failure
         | steps                                                 |
         | I am assigned the Project Manager role                |
         | I open the Test Request Page                          |
         | the Test Request Page is displayed                    |
         | I set the value of the Team pulldown to R&D Benchtop  |
         | I set the value of the Project pulldown to Next Pizza |
         | I submit the form                                     |
         | the Test Request Form is displayed                    |

	And I enter the following data into the Test Request Form:
		| Field                                  | Value                             |
		| Project Number/Name                    | Pizza                             |
		| Title of Request                       | Test title                        |
		| NesTMS TRial Number                    | NES-101                           |
		| Dough/Mix Formula Code (handheld ONLY) | Test dough mix formula code       |
		| Type of Test/Trial                     | Recon Development                 |
		| Recipe Ready to be Weighed/Executed    | Checked                           |
		| Finish Date                            | 12/31/2014                        |
		| Equipment needed                       | First line;Second line;Third Line |
		| Number of Variables                    | 10                                |
		| Number of Samples                      | 20                                |
		| Recipe Attached                        | Beef Wellington.docx              |
		| Ingredient Document Attached           | Ingredients.docx                  |
		| WBS (Factory Use)                      | MKR                               |
		| Ready for approval                     | Checked                           |

Scenario Outline: Submit Test Request missing required fields - R&D Benchtop
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
		| Finish Date         | Finish Date is required         |
		| Number of Variables | Number of Variables is required |
		| Number of Samples   | Number of Samples is required   |
