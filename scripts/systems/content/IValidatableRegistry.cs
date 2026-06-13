using System.Collections.Generic;

public interface IValidatableRegistry
{
    IReadOnlyList<string> ValidateTyped();
}
