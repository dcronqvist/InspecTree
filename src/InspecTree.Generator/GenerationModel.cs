using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace InspecTree.Generator
{
  public sealed class GeneratedParameter
  {
    public string ParemeterType { get; }
    public string ParameterName { get; }
  }

  public sealed class GeneratedOverload
  {
    public string NamespaceName { get; }
    public string ClassName { get; }
    public string ClassAccessModifier { get; }

    public string OverloadAccessModifier { get; }
    public bool IsStatic { get; }
    public string ReturnType { get; }
    public string MethodName { get; }

    public List<GeneratedParameter> Parameters { get; }
  }

  public sealed class SourceFile
  {
    public string FileName { get; }
    public SourceText SourceText { get; }
  }
}
