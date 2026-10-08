using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Tests
{
    public class CrmBusinessRulesTests
    {
        [Fact]
        public void Opportunity_WeightedAmount_CalculationIsAccurate()
        {
            // Arrange
            var opp = new Opportunity
            {
                Amount = 100000m,
                Probability = 60
            };

            // Act & Assert
            // 100000 * 60 / 100 = 60000
            Assert.Equal(60000m, opp.WeightedAmount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500)]
        public void Opportunity_AmountZeroOrNegative_FailsValidation(decimal invalidAmount)
        {
            // Arrange
            var model = new CreateOpportunityViewModel
            {
                OpportunityName = "Test Invalid Amount Deal",
                CustomerId = 1,
                Amount = invalidAmount,
                Stage = "Qualification",
                Probability = 50,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10)
            };

            // Act
            var results = ValidateModel(model);

            // Assert
            Assert.Contains(results, r => r.MemberNames.Contains("Amount") && r.ErrorMessage!.Contains("greater than 0"));
        }

        [Theory]
        [InlineData(101)]
        [InlineData(-5)]
        public void Opportunity_ProbabilityOutOfRange_FailsValidation(int invalidProb)
        {
            // Arrange
            var model = new CreateOpportunityViewModel
            {
                OpportunityName = "Test Invalid Probability Deal",
                CustomerId = 1,
                Amount = 50000m,
                Stage = "Proposal",
                Probability = invalidProb,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(10)
            };

            // Act
            var results = ValidateModel(model);

            // Assert
            Assert.Contains(results, r => r.MemberNames.Contains("Probability") && r.ErrorMessage!.Contains("between 0 and 100"));
        }

        [Fact]
        public void FollowUp_InvalidPhoneFormat_FailsValidation()
        {
            // Arrange
            var model = new CreateCustomerViewModel
            {
                CustomerName = "Test Corp",
                Email = "test@corp.com",
                Phone = "123" // Invalid, must be 10 digits starting 6-9
            };

            // Act
            var results = ValidateModel(model);

            // Assert
            Assert.Contains(results, r => r.MemberNames.Contains("Phone") && r.ErrorMessage!.Contains("10-digit"));
        }

        [Fact]
        public void Lead_InvalidEmail_FailsValidation()
        {
            // Arrange
            var model = new CreateLeadViewModel
            {
                LeadName = "John Bad Email",
                Email = "invalid-email-format",
                Phone = "9876543210",
                Source = "Website",
                Status = "New",
                ExpectedValue = 50000m
            };

            // Act
            var results = ValidateModel(model);

            // Assert
            Assert.Contains(results, r => r.MemberNames.Contains("Email") && r.ErrorMessage!.Contains("valid email"));
        }

        [Fact]
        public void Register_PasswordComplexity_EnforcesRules()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                FullName = "Security Test",
                Email = "sec@test.com",
                PhoneNumber = "9876543210",
                Password = "simple", // Missing upper, digit, special, and min length 8
                ConfirmPassword = "simple",
                Role = AppRoles.SalesExecutive
            };

            // Act
            var results = ValidateModel(model);

            // Assert
            Assert.Contains(results, r => r.MemberNames.Contains("Password"));
        }

        private List<ValidationResult> ValidateModel(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, true);
            return results;
        }
    }
}
