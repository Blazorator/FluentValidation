using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorator.FluentValidation.Exceptions;
using Blazorator.FluentValidation.Markers;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazorator.FluentValidation
{
    public class FluentValidatedEditForm : ComponentBase, IDisposable
    {
        [Inject] private IServiceProvider ServiceProvider { get; set; }

        private readonly Func<Task> _handleSubmitDelegate; // Cache to avoid per-render allocations

        private EditContext _fixedEditContext;

        /// <summary>
        /// Constructs an instance of <see cref="EditForm"/>.
        /// </summary>
        public FluentValidatedEditForm()
        {
            _handleSubmitDelegate = HandleSubmitAsync;
        }

        /// <summary>
        /// Gets or sets a collection of additional attributes that will be applied to the created <c>form</c> element.
        /// </summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        /// <summary>
        /// Supplies the edit context explicitly. If using this parameter, do not
        /// also supply <see cref="Model"/>, since the model value will be taken
        /// from the <see cref="EditContext.Model"/> property.
        /// </summary>
        [Parameter]
        public EditContext EditContext { get; set; }

        /// <summary>
        /// Specifies the top-level model object for the form. An edit context will
        /// be constructed for this model. If using this parameter, do not also supply
        /// a value for <see cref="EditContext"/>.
        /// </summary>
        [Parameter]
        public object Model { get; set; }

        /// <summary>
        /// Specifies the content to be rendered inside this <see cref="EditForm"/>.
        /// </summary>
        [Parameter]
        public RenderFragment<EditContext> ChildContent { get; set; }

        /// <summary>
        /// A callback that will be invoked when the form is submitted and the
        /// <see cref="EditContext"/> is determined to be valid.
        /// </summary>
        [Parameter]
        public EventCallback<EditContext> OnValidSubmit { get; set; }

        /// <summary>
        /// A callback that will be invoked when the form is submitted and the
        /// <see cref="EditContext"/> is determined to be invalid.
        /// </summary>
        [Parameter]
        public EventCallback<EditContext> OnInvalidSubmit { get; set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            if ((EditContext == null) == (Model == null))
            {
                throw new InvalidOperationException($"{nameof(EditForm)} requires a {nameof(Model)} " +
                                                    $"parameter, or an {nameof(EditContext)} parameter, but not both.");
            }

            if (_fixedEditContext == null || EditContext != null || Model != _fixedEditContext.Model)
            {
                _fixedEditContext = EditContext ?? new EditContext(Model);
                _fixedEditContext.OnFieldChanged += FixedEditContextOnOnFieldChanged;
                if (_validationMessageStore == null)
                {
                    _validationMessageStore = new ValidationMessageStore(_fixedEditContext);
                }
            }
        }

        private void FixedEditContextOnOnFieldChanged(object sender, FieldChangedEventArgs e)
        {
            var properties = new[] {e.FieldIdentifier.FieldName};
            var context = new ValidationContext(e.FieldIdentifier.Model, new PropertyChain(),
                new MemberNameValidatorSelector(properties));
            var validatorType = typeof(IValidator<>).MakeGenericType(_fixedEditContext.Model.GetType());

            var validators = ServiceProvider.GetServices(validatorType).Where(x => !(x is IValidationAsyncMarker))
                .OfType<IValidator>();
            _validationMessageStore.Clear(e.FieldIdentifier);

            foreach (var validatorRuleSet in validators)
            {
                var validationRules = validatorRuleSet as IEnumerable<IValidationRule> ??
                                      Enumerable.Empty<IValidationRule>();
                foreach (var validationRule in validationRules)
                {
                    foreach (var ruleValidator in validationRule.Validators)
                    {
                        if (ruleValidator is AsyncPredicateValidator || ruleValidator is AsyncValidatorBase)
                        {
                            throw new AsyncValidationOnSyncPathException(
                                $"The following rules is async - {ruleValidator.GetType()}");
                        }
                    }

                    var validationResults = validatorRuleSet.Validate(context);
                    _validationMessageStore.Add(e.FieldIdentifier,
                        validationResults.Errors.Select(error => error.ErrorMessage));
                }
            }

            _fixedEditContext.NotifyValidationStateChanged();
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenRegion(_fixedEditContext.GetHashCode());
            builder.OpenElement(0, "form");
            builder.AddMultipleAttributes(1, AdditionalAttributes);
            builder.AddAttribute(2, "onsubmit", _handleSubmitDelegate);
            builder.OpenComponent<CascadingValue<EditContext>>(3);
            builder.AddAttribute(4, "IsFixed", true);
            builder.AddAttribute(5, "Value", _fixedEditContext);
            builder.AddAttribute(6, "ChildContent", ChildContent?.Invoke(_fixedEditContext));
            builder.CloseComponent();
            builder.CloseElement();
            builder.CloseRegion();
        }

        private async Task HandleSubmitAsync()
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(_fixedEditContext.Model.GetType());
            var validators = ServiceProvider.GetServices(validatorType).Where(x => x is IValidationAsyncMarker)
                .OfType<IValidator>();

            _validationMessageStore.Clear();

            foreach (var validatorRuleSet in validators)
            {
                var validationResults = await validatorRuleSet.ValidateAsync(_fixedEditContext.Model);
                foreach (var validationResult in validationResults.Errors)
                {
                    var fieldIdentifier = ToFieldIdentifier(_fixedEditContext, validationResult.PropertyName);
                    _validationMessageStore.Add(fieldIdentifier, validationResult.ErrorMessage);
                }
            }
        }

        private static readonly char[] Separators = {'.', '['};

        private ValidationMessageStore _validationMessageStore;

        private static FieldIdentifier ToFieldIdentifier(EditContext editContext, string propertyPath)
        {
            // https://blog.stevensanderson.com/2019/09/04/blazor-fluentvalidation/
            var obj = editContext.Model;

            while (true)
            {
                var nextTokenEnd = propertyPath.IndexOfAny(Separators);
                if (nextTokenEnd < 0)
                {
                    return new FieldIdentifier(obj, propertyPath);
                }

                var nextToken = propertyPath.Substring(0, nextTokenEnd);
                propertyPath = propertyPath.Substring(nextTokenEnd + 1);

                object newObj;
                if (nextToken.EndsWith("]"))
                {
                    // It's an indexer
                    // This code assumes C# conventions (one indexer named Item with one param)
                    nextToken = nextToken.Substring(0, nextToken.Length - 1);
                    var prop = obj.GetType().GetProperty("Item");
                    var indexerType = prop.GetIndexParameters()[0].ParameterType;
                    var indexerValue = Convert.ChangeType(nextToken, indexerType);
                    newObj = prop.GetValue(obj, new object[] {indexerValue});
                }
                else
                {
                    // It's a regular property
                    var prop = obj.GetType().GetProperty(nextToken);
                    if (prop == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not find property named {nextToken} on object of type {obj.GetType().FullName}.");
                    }

                    newObj = prop.GetValue(obj);
                }

                if (newObj == null)
                {
                    // This is as far as we can go
                    return new FieldIdentifier(obj, nextToken);
                }

                obj = newObj;
            }
        }

        public void Dispose()
        {
            _fixedEditContext.OnFieldChanged -= FixedEditContextOnOnFieldChanged;
        }
    }
}