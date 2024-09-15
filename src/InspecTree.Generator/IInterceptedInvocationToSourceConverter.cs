using System.Collections.Generic;

namespace InspecTree.Generator
{
  public interface IInterceptedInvocationToSourceConverter
  {
    IEnumerable<SourceFile> ConvertToSource(IReadOnlyCollection<InterceptedInvocation> methodsToIntercept);
  }
}
