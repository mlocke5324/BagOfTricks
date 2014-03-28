using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace NesTrak.Web.Tests.Models
{
    public class TestController : Controller
    {
        public void TestValidateModel(object Model)
        {
            ValidationContext validationContext = new ValidationContext(Model, null, null);
            List<ValidationResult> validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(Model, validationContext, validationResults, true);
            foreach (ValidationResult validationResult in validationResults)
            {
                this.ModelState.AddModelError(String.Join(", ", validationResult.MemberNames), validationResult.ErrorMessage);
            }
        }
    }
}
