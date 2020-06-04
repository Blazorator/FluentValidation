using System;

    namespace Blazorator.FluentValidation.Exceptions
{
    public class AsyncValidationOnSyncPathException : Exception
    {
        public AsyncValidationOnSyncPathException(string message) : base(message)
        {
        }
    }
}