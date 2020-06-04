using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Blazorator.FluentValidation
{
    public class TemplatedValidationSummaryBase : ComponentBase
    {
        [CascadingParameter] 
        EditContext CurrentEditContext { get; set; }
        
        [Parameter] 
        public object Model { get; set; }
        
        [Parameter]
        public RenderFragment<IEnumerable<string>> ChildContent { get; set; }

        protected IEnumerable<string> ValidationMessages { get; set; } = new List<string>();
        
        protected override void OnInitialized()
        {
            ValidationMessages = Model is null ? CurrentEditContext.GetValidationMessages() :
                CurrentEditContext.GetValidationMessages(new FieldIdentifier(Model, string.Empty));
        }
    }
}