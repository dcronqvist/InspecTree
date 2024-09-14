using System.Collections.Generic;

namespace InspecTree.Generator
{
  public interface IOverloadToSourceConverter
  {
    IEnumerable<SourceFile> ConvertToSource(IReadOnlyCollection<GeneratedOverload> overloads);
  }
}
