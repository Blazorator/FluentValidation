# Blazorator.FluentValidation

## Why another FluentValidation xxx

Other libraries available (that I've found) don't currently deal well with async validation rules (MustAsync). The
current implementation for ```EditForm``` only caters for async if you take full control of the validation (I imagine this will change in the future).

This library builds on the default behaviour while staying close to the ```EditForm``` implementation to feel familiar and splits the validation to allow 
async rules to execute correctly on full form submission. It's currently implemented using a marker interface.

## Installation

You can install from [NuGet](https://www.nuget.org/packages/Blazorator.FluentValidation/) using the following command:

`Install-Package Blazorator.FluentValidation`

Alternatively you can install via the inbuilt package manager in your IDE.

## Quickstart

### Add to _imports.razor

```c#
@using Blazorator.FluentValidation
```

### Create a new form

```c#

<FluentValidatedEditForm Model="@Person" OnValidSubmit="@ValidForm" OnInvalidSubmit="@InvalidForm">
    <TemplatedValidationSummary Context="ValidationItems">
        @foreach (var validationItem in ValidationItems)
        {
            <div>@validationItem</div>
        }
    </TemplatedValidationSummary>
    <p>
        <label>Name: </label>
        <InputText @bind-Value="@Person.Name"/>
        <ValidationMessage For="@(() => Person.Name)"/>
    </p>

    <p>
        <label>Age: </label>
        <InputNumber @bind-Value="@Person.Age"/>
        <ValidationMessage For="@(() => Person.Age)"/>
    </p>

    <p>
        <label>Email Address: </label>
        <InputText @bind-Value="@Person.EmailAddress"/>
        <ValidationMessage For="@(() => Person.EmailAddress)"/>
    </p>
    <button type="submit">Save</button>
</FluentValidatedEditForm>

```

### Add functions for valid and invalid submission

```c#
void ValidForm()
{
    Console.WriteLine("Form Submitted Successfully!");
}

void InvalidForm()
{
    Console.WriteLine("boo");
}
```

### Split Async and Sync validator

Upon OnFieldChanged isn't task aware inside the framework so it's important to split the validators into async and 
sync validators. It's important to add the ```IValidationAsyncMarker``` interface to the async validators as this 
allows Blazorator to select the appropriate set of validators to run.

**Important - It's possible for a form to look valid based purely on the output of field level validation, it's only 
when a form is submitted that async validators are run and this can result in additional form validation triggering e.g on the result of an async call** 


```c#
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
```

**If an async rule is found while trying to validate OnFieldChanged the following exception is raised ```AsyncValidationOnSyncPathException```. Check  that you've correctly split async and sync validators and have also applied the ```IValidationAsyncMarker```**

## Validation Summary

Although all the standard  edit form controls work, it's sometimes useful to have a little more control over the validation summary.

```c#
<TemplatedValidationSummary Context="ValidationItems">
    @foreach (var validationItem in ValidationItems)
    {
        <div>@validationItem</div>
    }
</TemplatedValidationSummary>
```

The context is an ```IEnumerable<string>```.

### Example(s)

Available in the repository.
