﻿using System.Threading.Tasks;
using FluentValidation;
using Blazorator.FluentValidation.Markers;

namespace Example.Shared
{
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string EmailAddress { get; set; }
    }

    public class PersonValidatorAsync : AbstractValidator<Person>, IValidationAsyncMarker
    {
        public PersonValidatorAsync()
        {
            RuleFor(x => x.Name).MustAsync(async (name, cancellationToken) => await IsUniqueAsync(name))
                 .WithMessage("Name must be unique")
                 .When(person => !string.IsNullOrEmpty(person.Name));
        }
        
        private async Task<bool> IsUniqueAsync(string name)
        {
            await Task.Delay(1000);
            return name.ToLower() != "test";
        }
    }
    
    public class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(p => p.Name).NotEmpty().WithMessage("You must enter a name");
            RuleFor(p => p.Name).MaximumLength(50).WithMessage("Name cannot be longer than 50 characters");
            RuleFor(p => p.Age).NotEmpty().WithMessage("Age must be greater than 0");
            RuleFor(p => p.Age).LessThan(150).WithMessage("Age cannot be greater than 150");
            RuleFor(p => p.EmailAddress).NotEmpty().WithMessage("You must enter a email address");
            RuleFor(p => p.EmailAddress).EmailAddress().WithMessage("You must provide a valid email address");
        }
    }
}