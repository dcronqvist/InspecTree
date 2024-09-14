using System.Collections.Generic;

namespace InspecTree.Generator
{
  public interface IModelToSourceConverter
  {
    IEnumerable<SourceFile> GetSourceFiles();
  }
}
