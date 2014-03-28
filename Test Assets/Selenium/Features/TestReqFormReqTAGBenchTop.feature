@BackgroundFirstOrFailure
@DismissAlerts
Feature: Test Request Form Required Entries for TAG Benchtop
	To submit an TAG Benchtop test request form for approval
	As an TAG Benchtop Project Manager
	I need to complete all required entries in the form

#Note:
#
#Pulldowns and File selectors that cannot be cleared are not tested:
#	Approver
#	Project Number/Name
#	Brand/Group/Account
#	Recipe Name
#	Type of Test/Trial
#	WBS (Factory Use)
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
         | steps                                                 |
         | I am assigned the Project Manager role                |
         | I open the Test Request Page                          |
         | the Test Request Page is displayed                    |
         | I set the value of the Team pulldown to TAG Benchtop  |
         | I set the value of the Project pulldown to Next Pizza |
         | I submit the form                                     |
         | the Test Request Form is displayed                    |

	And I enter the following data into the Test Request Form:
		| Field                               | Value                |
		| Project Number/Name                 | Pizza                |
		| Brand/Group/Account                 | Stouffers            |
		| Title of Request                    | Test title           |
		| Recipe name                         | Macroni              |
		| Recipe Code/T-Code                  | Test recipe code     |
		| PDS Test Number                     | Test PDS test number |
		| Type of Test/Trial                  | TAG Sensory Panel    |
		| Recipe Ready to be Weighed/Executed | Checked              |
		| Recipe Attached                     | Beef Wellington.docx |
		| Ingredient Document Attached        | Ingredients.docx     |
		| Finish Date                         | 12/31/2014           |
		| Equipment needed                    | Mixer;Extruder;Oven  |
		| Number of Variables                 | 10                   |
		| Number of Samples                   | 20                   |
		| Ready for approval                  | Checked              |

Scenario Outline: Submit Test Request missing required fields - TAG Benchtop
	Given the Test Request Form is displayed
	When I save the value of the <Field> field
	And I clear the field
	And I submit the form
	Then the Test Request Form is displayed
	And the message "<Message>" is displayed on the form
	And I can restore the field's value

	Examples:
		| Field               | Message                         |
		| Title of Request    | Title of Request is required       |
		| Recipe Code/T-Code  | Recipe Code/T-Code is required  |
		| Recipe Name         | Recipe Name is required         |
		| PDS Test Number     | PDS Test Number is required     |
		| Finish Date         | Finish Date is required         |
		| Number of Variables | Number of Variables is required |
		| Number of Samples   | Number of Samples is required   |
